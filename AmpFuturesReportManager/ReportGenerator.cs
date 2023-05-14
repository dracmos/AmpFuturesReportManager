using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig;
using AmpFuturesReportManager.Application.Modes;

namespace AmpFuturesReportManager.Application;

public class ReportGenerator
{
    private readonly List<string> _inputReportNames;

    public ReportGenerator(List<string> inputReportNames)
    {
        _inputReportNames = inputReportNames;
    }

    public void CreateOutputFile()
    {
        StringBuilder finalReport = new StringBuilder();
        decimal totalProfitLoss = 0;
        decimal totalProfitLossIncludingFees = 0;
        decimal totalTicks = 0;
        decimal totalFees = 0;

        foreach (var inputReportName in _inputReportNames)
        {
            List<string> lines = GetAllTextualLinesFromPurceaseSaleTable(inputReportName);
            List<Operation> operations = GetOperationsFromTextualLines(lines);

            // Order operations by TradeNumber Ascending: on top the oldest
            operations = operations.OrderBy(o => o.TradeNumber).ToList();

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
            var market = line[21..36].Trim();
            var contractDescription = line[49..85].Trim();
            var tradePrice = line[100..107];
            var currency = line[111..].Trim();
            var quantity = line[37..48];

            var operation = new Operation
            {
                Date = DateOnly.ParseExact(date, "dd-MMM-yy", CultureInfo.InvariantCulture),
                TradeNumber = long.Parse(tradeNumber), // Parsing as long
                Market = market,
                Contract = GetContract(contractDescription),
                ContractDescription = contractDescription,
                TradePrice = decimal.Parse(tradePrice, CultureInfo.InvariantCulture),
                Currency = currency
            };

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
        Contract contract;
        if (contractDescriptionWords.Length > 0 && contractDescriptionWords[0] == "M2K")
        {
            contract = new RusselMicro();
        }
        else
        {
            throw new Exception("Contract not yet mapped");
        }

        return contract;
    }

    private List<RoundTripOperation> GenerateRoundTripOperations(List<Operation> operations)
    {
        var sortedOperations = operations.OrderBy(o => o.TradeNumber).ToList();

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
        var roundTripOperation = new RoundTripOperation
        {
            Contract = buyOperation.Contract,
            Type = buyOperation.Type,
            Quantity = quantity,
            OpenOperation = buyOperation,
            CloseOperation = sellOperation,
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


    //private List<RoundTripOperation> GenerateRoundTripOperations(List<Operation> operations)
    //{
    //    var roundTripOperations = new List<RoundTripOperation>();

    //    for (int i = 0; i < operations.Count; i += 2)
    //    {
    //        var openOperation = operations[i];
    //        var closeOperation = operations[i + 1];


    //        //What will happen if we have two orders with the same type?
    //        //This can happen if you open 2 positions long, because we are now ordering the data
    //        var roundTripOperation = new RoundTripOperation
    //        {
    //            Contract = openOperation.Contract,
    //            Type = openOperation.Type,
    //            Quantity = openOperation.Quantity,
    //            OpenOperation = openOperation,
    //            CloseOperation = closeOperation,
    //            BuyOperation = openOperation.Type == OperationType.Buy ? openOperation : closeOperation,
    //            SellOperation = closeOperation.Type == OperationType.Sell ? closeOperation : openOperation,
    //        };

    //        decimal ticks;
    //        if (roundTripOperation.Type == OperationType.Buy)
    //            ticks = (roundTripOperation.CloseOperation.TradePrice * roundTripOperation.CloseOperation.Quantity) - (roundTripOperation.OpenOperation.TradePrice * roundTripOperation.OpenOperation.Quantity);
    //        else
    //            ticks = (roundTripOperation.OpenOperation.TradePrice * roundTripOperation.OpenOperation.Quantity) - (roundTripOperation.CloseOperation.TradePrice * roundTripOperation.CloseOperation.Quantity);

    //        ticks /= roundTripOperation.Contract.TickMovement;
    //        roundTripOperation.Ticks = ticks;
    //        roundTripOperation.ProfitLoss = ticks * roundTripOperation.Contract.TickMoneyValue;
    //        roundTripOperation.Fees = roundTripOperation.Contract.FeesForContract * roundTripOperation.Quantity * 2;

    //        roundTripOperations.Add(roundTripOperation);
    //    }

    //    return roundTripOperations;
    //}

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
    private List<string> GetAllTextualLinesFromPurceaseSaleTable(string inputReportName)
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
