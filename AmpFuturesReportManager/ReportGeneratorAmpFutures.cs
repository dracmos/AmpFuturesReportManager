using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig;
using AmpFuturesReportManager.Application.Modes;
using CsvHelper.Configuration;
using CsvHelper;
using AmpFuturesReportManager.Modes;

namespace AmpFuturesReportManager.Application;

public class ReportGenerator
{
    private readonly List<string> _inputReportNames;
    private readonly ReportType _reportType;

    public ReportGenerator(List<string> inputReportNames, ReportType reportType)
    {
        _inputReportNames = inputReportNames;
        _reportType = reportType;
    }

    public void CreateOutputFile()
    {
        StringBuilder finalReport = new();
        decimal totalProfitLoss = 0;
        decimal totalProfitLossIncludingFees = 0;
        decimal totalTicks = 0;
        decimal totalFees = 0;

        foreach (var inputReportName in _inputReportNames)
        {
            List<Operation> operations = new();

            if (_reportType == ReportType.AMPFutures)
            {
                List<string> lines = GetAllTextualLinesFromAMPReportPurchaseSaleTable(inputReportName);
                operations = GetOperationsFromTextualLines(lines);
                // Order operations by TradeNumber Ascending: on top the oldest
                operations = operations.OrderBy(o => o.TradeNumber).ToList();
            }
            else if (_reportType == ReportType.CQG)
            {
                operations = ReadOperationsFromCQGCSVFile(inputReportName);
                operations = operations.OrderBy(o => o.Date).ToList();
            }
            else
            {
                throw new Exception("Report Type Not Supported");
            }

            List<RoundTripOperation> roundTripOperations = GenerateRoundTripOperations(operations);

            string reportForFile = GenerateReport(roundTripOperations);
            string filename = Path.GetFileName(inputReportName);
            finalReport.AppendLine($"########################## {filename} ##########################");
            finalReport.AppendLine(reportForFile);

            totalProfitLoss += roundTripOperations.Sum(r => r.ProfitLoss);
            totalProfitLossIncludingFees += roundTripOperations.Sum(r => r.ProfitLossIncludingFees);
            totalTicks += roundTripOperations.Sum(r => r.Ticks);
            totalFees += roundTripOperations.Sum(r => r.Fees);
        }

        finalReport.AppendLine("####################### Final Recap ###########################");
        finalReport.AppendLine($"Total Profit/Loss Including Fees: {totalProfitLossIncludingFees}");
        finalReport.AppendLine($"Total Profit/Loss: {totalProfitLoss}");
        finalReport.AppendLine($"Total Ticks: {totalTicks}");
        finalReport.AppendLine($"Total Fees: {totalFees}");

        Console.WriteLine(finalReport.ToString());
        GenerateOutputFile(finalReport.ToString());
    }

    public List<Operation> ReadOperationsFromCQGCSVFile(string inputReportName)
    {
        List<Operation> orderListFromCSV = new List<Operation>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            HasHeaderRecord = false,
            IgnoreBlankLines = true
        };

        using (var reader = new StreamReader(inputReportName))
        using (var csv = new CsvReader(reader, config))
        {
            // Skip first 3 lines
            for (int i = 0; i < 3; i++)
            {
                csv.Read();
            }

            var records = new List<OrderFromCSV>();

            while (csv.Read())
            {
                try
                {
                    var record = csv.GetRecord<OrderFromCSV>();
                    records.Add(record);
                }
                catch (CsvHelperException)
                {
                    // Couldn't parse the record to Order, so skip it
                    continue;
                }
            }
            foreach (var record in records)
            {
                Operation operation = new()
                {
                    TradeNumber = long.Parse(record.Hash),
                    Quantity = record.Qty,
                    Type = record.BS == "BUY" ? OperationType.Buy : OperationType.Sell,
                    ContractDescription = record.Symbol,
                    TradePrice = decimal.Parse(record.AvgFillP.Replace(",", "."), CultureInfo.InvariantCulture),
                    Currency = "USD"
                };

                bool isDateTime = DateTime.TryParseExact(record.FillT, "dd/MM/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fillDateTime);

                if (!isDateTime)
                {
                    bool isTime = DateTime.TryParseExact(record.FillT, "HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out fillDateTime);

                    if (isTime)
                        fillDateTime = DateTime.Today + fillDateTime.TimeOfDay;
                    else
                    {
                        throw new Exception("Unable to parse the Date Time of Fill");
                    }
                }
                operation.Date = fillDateTime;
                operation.Contract = GetContract(operation.ContractDescription);
                operation.Market = operation.Contract.Market;

                orderListFromCSV.Add(operation);

            }
        }

        return orderListFromCSV;
    }

    private string GenerateReport(List<RoundTripOperation> roundTripOperations)
    {
        StringBuilder reportForFile = new StringBuilder();

        foreach (RoundTripOperation roundTripOperation in roundTripOperations)
        {
            string textResult = ReportDetailForRoundTripOperation(roundTripOperation) + "----------------------------------------" + Environment.NewLine;
            reportForFile.AppendLine(textResult);
        }
        var summary = GenerateSummary(roundTripOperations);
        reportForFile.AppendLine(summary);
        return reportForFile.ToString();
    }

    private void GenerateOutputFile(string reportForFile)
    {
        string outputFileName = $"Output_For_{DateTime.Now:yyyyMMddHHmmss}.txt";

        // Create the directory if it doesn't exist
        string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Generated Reports");
        Directory.CreateDirectory(outputDirectory);

        // Combine the directory path and filename
        string fullOutputPath = Path.Combine(outputDirectory, outputFileName);

        File.WriteAllText(fullOutputPath, reportForFile);
    }

    private List<Operation> GetOperationsFromTextualLines(List<string> lines)
    {
        var operations = new List<Operation>();

        foreach (var line in lines)
        {
            var date = line[..9];
            var tradeNumber = line[11..20];
            //var market = line[21..36].Trim();
            var contractDescription = line[49..85].Trim();
            var tradePrice = line[100..107];
            var currency = line[111..].Trim();
            var quantity = line[37..48];

            var operation = new Operation
            {
                Date = DateTime.ParseExact(date, "dd-MMM-yy", CultureInfo.InvariantCulture),
                TradeNumber = long.Parse(tradeNumber), // Parsing as long
                Contract = GetContract(contractDescription),
                ContractDescription = contractDescription,
                TradePrice = decimal.Parse(tradePrice, CultureInfo.InvariantCulture),
                Currency = currency
            };

            operation.Market = operation.Contract.Market;

            //The buys are when we have a value in the column 37
            if (quantity[0] != ' ')
            {
                operation.Type = OperationType.Buy;
                operation.Quantity = int.Parse(quantity[0].ToString());
            }
            // The sells are when we have a value in the column 48
            else
            {
                operation.Type = OperationType.Sell;
                operation.Quantity = int.Parse(quantity[^1].ToString());
            }

            operations.Add(operation);
        }

        return operations;
    }

    private Contract GetContract(string contractDescription)
    {
        string[] contractDescriptionWords = contractDescription.Split(' ');
        Contract? contract = null;
        if (contractDescriptionWords.Length > 0)
        {
            if (contractDescriptionWords[0].Contains(Tickers.RusselMicro))
            {
                contract = new RusselMicro();
            }
            else if (contractDescriptionWords[0].Contains(Tickers.DowMicro))
            {
                contract = new DowMicro();
            }
        }

        if (contract == null)
            throw new Exception("Contract not yet mapped");

        return contract;
    }

    private List<RoundTripOperation> GenerateRoundTripOperations(List<Operation> operations)
    {
        List<Operation> sortedOperations;

        if (_reportType == ReportType.AMPFutures)
        {
            sortedOperations = operations.OrderBy(o => o.TradeNumber).ToList();
        }
        else
        {
            sortedOperations = operations.OrderBy(o => o.Date).ToList();
        }

        var pendingBuyOperations = new LinkedList<Operation>();
        var pendingSellOperations = new LinkedList<Operation>();

        var roundTripOperations = new List<RoundTripOperation>();

        foreach (var operation in sortedOperations)
        {
            LinkedList<Operation> pendingOperations;
            LinkedList<Operation> oppositePendingOperations;

            if (operation.Type == OperationType.Buy)
            {
                pendingOperations = pendingBuyOperations;
                oppositePendingOperations = pendingSellOperations;
            }
            else
            {
                pendingOperations = pendingSellOperations;
                oppositePendingOperations = pendingBuyOperations;
            }

            pendingOperations.AddLast(operation);

            while (pendingBuyOperations.Any() && pendingSellOperations.Any())
            {
                var buyOperation = pendingBuyOperations?.First?.Value;
                var sellOperation = pendingSellOperations?.First?.Value;

                var quantityToMatch = Math.Min(buyOperation.Quantity, sellOperation.Quantity);

                var roundTripOperation = GeneratePartialRoundTripOperation(buyOperation, sellOperation, quantityToMatch);
                roundTripOperations.Add(roundTripOperation);

                buyOperation.Quantity -= quantityToMatch;
                sellOperation.Quantity -= quantityToMatch;

                if (buyOperation.Quantity == 0)
                {
                    pendingBuyOperations.RemoveFirst();
                }

                if (sellOperation.Quantity == 0)
                {
                    pendingSellOperations.RemoveFirst();
                }
            }
        }

        if (pendingBuyOperations.Any() || pendingSellOperations.Any())
        {
            throw new Exception("Not all operations could be matched.");
        }

        return roundTripOperations;
    }
    private RoundTripOperation GeneratePartialRoundTripOperation(Operation buyOperation, Operation sellOperation, int quantity)
    {
        Operation openOperation, closeOperation;


        if (_reportType == ReportType.AMPFutures)
        {
            if (sellOperation.TradeNumber < buyOperation.TradeNumber)
            {
                openOperation = sellOperation;
                closeOperation = buyOperation;
            }
            else
            {
                openOperation = buyOperation;
                closeOperation = sellOperation;
            }
        }
        else
        {
            if (sellOperation.Date < buyOperation.Date)
            {
                openOperation = sellOperation;
                closeOperation = buyOperation;
            }
            else
            {
                openOperation = buyOperation;
                closeOperation = sellOperation;
            }
        }

        var roundTripOperation = new RoundTripOperation
        {
            Contract = openOperation.Contract,
            Type = openOperation.Type,
            Quantity = quantity,
            OpenOperation = openOperation,
            CloseOperation = closeOperation,
            BuyOperation = buyOperation,
            SellOperation = sellOperation,
        };

        decimal ticks;
        if (roundTripOperation.Type == OperationType.Buy)
            ticks = (roundTripOperation.CloseOperation.TradePrice * quantity) - (roundTripOperation.OpenOperation.TradePrice * quantity);
        else
            ticks = (roundTripOperation.OpenOperation.TradePrice * quantity) - (roundTripOperation.CloseOperation.TradePrice * quantity);

        ticks /= roundTripOperation.Contract.TickMovement;
        roundTripOperation.Ticks = ticks;
        roundTripOperation.ProfitLoss = ticks * roundTripOperation.Contract.TickMoneyValue;
        roundTripOperation.Fees = roundTripOperation.Contract.FeesForContract * quantity * 2;

        return roundTripOperation;
    }


    private string ReportDetailForRoundTripOperation(RoundTripOperation roundTripOperation)
    {
        StringBuilder sb = new StringBuilder();

        // Append details of RoundTripOperation
        sb.AppendLine($"Entry Date: {roundTripOperation.OpenOperation.Date}   Exit Date:{roundTripOperation.CloseOperation.Date}");
        sb.AppendLine($"Profit/Loss Including Fees: {roundTripOperation.ProfitLossIncludingFees}");
        sb.AppendLine($"Profit/Loss: {roundTripOperation.ProfitLoss}");
        sb.AppendLine($"Fees: {roundTripOperation.Fees}");
        sb.AppendLine($"Operation Type: {roundTripOperation.Type}");
        sb.AppendLine($"Contract: {roundTripOperation.OpenOperation.ContractDescription}");
        sb.AppendLine($"Entry Price: {roundTripOperation.OpenOperation.TradePrice}     Trade Number: {roundTripOperation.OpenOperation.TradeNumber}");
        sb.AppendLine($"Exit Price: {roundTripOperation.CloseOperation.TradePrice}     Trade Number: {roundTripOperation.CloseOperation.TradeNumber}");
        sb.AppendLine($"Ticks: {roundTripOperation.Ticks}");
        sb.AppendLine($"Quantity: {roundTripOperation.Quantity}");
        sb.AppendLine($"Currency: {roundTripOperation.OpenOperation.Currency}");

        //// Append details of OpenOperation and CloseOperation
        //var operations = new { roundTripOperation.OpenOperation, roundTripOperation.CloseOperation };
        //foreach (var operation in new[] { operations.OpenOperation, operations.CloseOperation })
        //{
        //    if (operation == null)
        //        continue;

        //    sb.AppendLine($"Operation Date: {operation.Date}");
        //    sb.AppendLine($"Trade Number: {operation.TradeNumber}");
        //    sb.AppendLine($"Market: {operation.Market}");
        //    sb.AppendLine($"Quantity: {operation.Quantity}");
        //    sb.AppendLine($"Type: {operation.Type}");
        //    sb.AppendLine($"Contract Description: {operation.ContractDescription}");
        //    sb.AppendLine($"Trade Price: {operation.TradePrice}");
        //    sb.AppendLine($"Currency: {operation.Currency}");

        //    // Append details of Contract
        //    Contract contract = operation.Contract;
        //    if (contract != null)
        //    {
        //        sb.AppendLine($"Contract Name: {contract.Name}");
        //        sb.AppendLine($"Tick Movement: {contract.TickMovement}");
        //        sb.AppendLine($"Tick Money Value: {contract.TickMoneyValue}");
        //        sb.AppendLine($"Fees for Contract: {contract.FeesForContract}");
        //    }
        //}

        return sb.ToString();
    }

    private string GenerateSummary(List<RoundTripOperation> roundTripOperations)
    {
        StringBuilder sb = new StringBuilder();

        decimal totalProfitLoss = roundTripOperations.Sum(r => r.ProfitLoss);
        decimal totalProfitLossIncludingFees = roundTripOperations.Sum(r => r.ProfitLossIncludingFees);
        decimal totalTicks = roundTripOperations.Sum(r => r.Ticks);
        decimal totalFees = roundTripOperations.Sum(r => r.Fees);

        sb.AppendLine($"Total Profit/Loss Including Fees: {totalProfitLossIncludingFees}");
        sb.AppendLine($"Total Profit/Loss: {totalProfitLoss}");
        sb.AppendLine($"Total Ticks: {totalTicks}");
        sb.AppendLine($"Total Fees: {totalFees}");

        return sb.ToString();
    }

    //I need the information of the Purchase & Sale table, that is the second table available in the Pdf Report
    private List<string> GetAllTextualLinesFromAMPReportPurchaseSaleTable(string inputReportName)
    {
        List<string> resultLines = new List<string>();
        string filenameComplete = Path.Combine(Directory.GetCurrentDirectory(), "Input", inputReportName);
        using (var document = PdfDocument.Open(filenameComplete))
        {
            string startWord = "DEBIT/CREDIT";
            string endWord = "TOTAL";

            int startWordCount = 0;
            int endWordCount = 0;

            for (var i = 1; i <= document.NumberOfPages; i++)
            {
                Page page = document.GetPage(i);
                var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

                foreach (var line in lines)
                {
                    if (line.Contains(endWord))
                    {
                        endWordCount++;
                        if (endWordCount == 2)
                        {
                            break;
                        }
                    }
                    if (startWordCount == 2 && endWordCount < 2)
                    {
                        resultLines.Add(line);
                    }
                    if (line.Contains(startWord))
                    {
                        startWordCount++;
                    }
                }
                if (endWordCount == 2)
                {
                    break;
                }
            }
        }

        return resultLines;
    }

}
