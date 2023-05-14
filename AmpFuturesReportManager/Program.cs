using AmpFuturesReportManager.Application;
using AmpFuturesReportManager.Application.Modes;

// Execute that command from the command prompt in the project folder to generate a self contained file:
//dotnet publish -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true

namespace AmpFuturesReportManager
{
    class Program
    {
        static void Main(string[] args)
        {
            ReportGenerator reportGenerator;

            ReportType _reportType = ReportType.CQG;

            string extension = "*.csv";
            if (_reportType == ReportType.AMPFutures)
                extension = "*.pdf";

            if (args.Length != 0)
            {
                reportGenerator = new ReportGenerator(new List<string>() { args[0] }, _reportType);
            }
            else
            {
                List<string> reportListNames = Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "Input"), extension)
                                                        .OrderBy(x => x)
                                                        .ToList();

                if (reportListNames.Any())
                {
                    reportGenerator = new ReportGenerator(reportListNames, _reportType);
                }
                else
                {
                    Console.WriteLine("No files found in the Input directory.");
                    return;
                }
            }

            reportGenerator.CreateOutputFile();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static FileInfo? GetTheLatestFilename()
        {
            // Get the Input directory
            var inputDirectory = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "Input"));

            // Get the newest file
            var newestFile = inputDirectory.GetFiles("*.pdf")
                                           .OrderByDescending(f => f.LastWriteTime)
                                           .FirstOrDefault();
            return newestFile;
        }

        //static void ParseDocument()
        //{
        //    List<string> lines = GetAllTextualLinesFromPurceaseSaleTable();
        //    List<Operation> operations = GetOperationsFromTextualLines(lines);

        //    // Order operations by TradeNumber Ascending: on top the oldest
        //    operations = operations.OrderBy(o => o.TradeNumber).ToList();

        //    List<RoundTripOperation> roundTripOperations = GenerateRoundTripOperations(operations);

        //    //// Print operations for testing
        //    //foreach (var operation in operations)
        //    //{
        //    //    Console.WriteLine($"{operation.Date} {operation.TradeNumber} {operation.Market} {operation.Quantity} {operation.Type} {operation.ContractDescription} {operation.TradePrice} {operation.Currency}");
        //    //}

        //    string reportForFile = string.Empty;

        //    foreach (RoundTripOperation roundTripOperation in roundTripOperations)
        //    {
        //        string textResult = ReportDetailForRoundTripOperation(roundTripOperation) + "----------------------------------------" + Environment.NewLine;
        //        reportForFile += textResult;
        //    }
        //    var summary = GenerateSummary(roundTripOperations);
        //    reportForFile += summary;

        //    Console.WriteLine(reportForFile);

        //    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputReportName);
        //    string outputFileName = $"Output_For_{fileNameWithoutExtension}.txt";

        //    File.WriteAllText(outputFileName, reportForFile);

        //}

        //public static List<Operation> GetOperationsFromTextualLines(List<string> lines)
        //{
        //    var operations = new List<Operation>();

        //    foreach (var line in lines)
        //    {
        //        var date = line[..9];
        //        var tradeNumber = line[11..20];
        //        var market = line[21..36].Trim();
        //        var contractDescription = line[49..85].Trim();
        //        var tradePrice = line[100..107];
        //        var currency = line[111..].Trim();
        //        var quantity = line[37..48];

        //        var operation = new Operation
        //        {
        //            Date = DateOnly.ParseExact(date, "dd-MMM-yy", CultureInfo.InvariantCulture),
        //            TradeNumber = long.Parse(tradeNumber), // Parsing as long
        //            Market = market,
        //            Contract = GetContract(contractDescription),
        //            ContractDescription = contractDescription,
        //            TradePrice = decimal.Parse(tradePrice, CultureInfo.InvariantCulture),
        //            Currency = currency
        //        };

        //        //The buys are when we have a value in the column 37
        //        if (quantity[0] != ' ')
        //        {
        //            operation.Type = OperationType.Buy;
        //            operation.Quantity = int.Parse(quantity[0].ToString());
        //        }
        //        // The sells are when we have a value in the column 48
        //        else
        //        {
        //            operation.Type = OperationType.Sell;
        //            operation.Quantity = int.Parse(quantity[^1].ToString());
        //        }

        //        operations.Add(operation);
        //    }

        //    return operations;
        //}

        //private static Contract GetContract(string contractDescription)
        //{
        //    string[] contractDescriptionWords = contractDescription.Split(' ');
        //    Contract contract;
        //    if (contractDescriptionWords.Length > 0 && contractDescriptionWords[0] == "M2K")
        //    {
        //        contract = new RusselMicro();
        //    }
        //    else
        //    {
        //        throw new Exception("Contract not yet mapped");
        //    }

        //    return contract;
        //}

        //public static List<RoundTripOperation> GenerateRoundTripOperations(List<Operation> operations)
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

        //public static string ReportDetailForRoundTripOperation(RoundTripOperation roundTripOperation)
        //{
        //    StringBuilder sb = new StringBuilder();

        //    // Append details of RoundTripOperation
        //    sb.AppendLine($"Entry Date: {roundTripOperation.OpenOperation.Date}   Exit Date:{roundTripOperation.CloseOperation.Date}");
        //    sb.AppendLine($"Profit/Loss Including Fees: {roundTripOperation.ProfitLossIncludingFees}");
        //    sb.AppendLine($"Profit/Loss: {roundTripOperation.ProfitLoss}");
        //    sb.AppendLine($"Fees: {roundTripOperation.Fees}");
        //    sb.AppendLine($"Operation Type: {roundTripOperation.Type}");
        //    sb.AppendLine($"Contract: {roundTripOperation.OpenOperation.ContractDescription}");
        //    sb.AppendLine($"Entry Price: {roundTripOperation.OpenOperation.TradePrice}     Trade Number: {roundTripOperation.OpenOperation.TradeNumber}");
        //    sb.AppendLine($"Exit Price: {roundTripOperation.CloseOperation.TradePrice}     Trade Number: {roundTripOperation.CloseOperation.TradeNumber}");
        //    sb.AppendLine($"Ticks: {roundTripOperation.Ticks}");
        //    sb.AppendLine($"Quantity: {roundTripOperation.Quantity}");
        //    sb.AppendLine($"Currency: {roundTripOperation.OpenOperation.Currency}");

        //    //// Append details of OpenOperation and CloseOperation
        //    //var operations = new { roundTripOperation.OpenOperation, roundTripOperation.CloseOperation };
        //    //foreach (var operation in new[] { operations.OpenOperation, operations.CloseOperation })
        //    //{
        //    //    if (operation == null)
        //    //        continue;

        //    //    sb.AppendLine($"Operation Date: {operation.Date}");
        //    //    sb.AppendLine($"Trade Number: {operation.TradeNumber}");
        //    //    sb.AppendLine($"Market: {operation.Market}");
        //    //    sb.AppendLine($"Quantity: {operation.Quantity}");
        //    //    sb.AppendLine($"Type: {operation.Type}");
        //    //    sb.AppendLine($"Contract Description: {operation.ContractDescription}");
        //    //    sb.AppendLine($"Trade Price: {operation.TradePrice}");
        //    //    sb.AppendLine($"Currency: {operation.Currency}");

        //    //    // Append details of Contract
        //    //    Contract contract = operation.Contract;
        //    //    if (contract != null)
        //    //    {
        //    //        sb.AppendLine($"Contract Name: {contract.Name}");
        //    //        sb.AppendLine($"Tick Movement: {contract.TickMovement}");
        //    //        sb.AppendLine($"Tick Money Value: {contract.TickMoneyValue}");
        //    //        sb.AppendLine($"Fees for Contract: {contract.FeesForContract}");
        //    //    }
        //    //}

        //    return sb.ToString();
        //}

        //public static string GenerateSummary(List<RoundTripOperation> roundTripOperations)
        //{
        //    StringBuilder sb = new StringBuilder();

        //    decimal totalProfitLoss = roundTripOperations.Sum(r => r.ProfitLoss);
        //    decimal totalProfitLossIncludingFees = roundTripOperations.Sum(r => r.ProfitLossIncludingFees);
        //    decimal totalTicks = roundTripOperations.Sum(r => r.Ticks);
        //    decimal totalFees = roundTripOperations.Sum(r => r.Fees);

        //    sb.AppendLine($"Total Profit Loss Including Fees: {totalProfitLossIncludingFees}");
        //    sb.AppendLine($"Total Profit Loss: {totalProfitLoss}");
        //    sb.AppendLine($"Total Ticks: {totalTicks}");
        //    sb.AppendLine($"Total Fees: {totalFees}");

        //    return sb.ToString();
        //}

        ////I need the information of the Purchase & Sale table, that is the second table available in the Pdf Report
        //static List<string> GetAllTextualLinesFromPurceaseSaleTable()
        //{
        //    List<string> resultLines = new List<string>();
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        string startWord = "DEBIT/CREDIT";
        //        string endWord = "TOTAL";

        //        int startWordCount = 0;
        //        int endWordCount = 0;

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

        //            foreach (var line in lines)
        //            {
        //                if (line.Contains(endWord))
        //                {
        //                    endWordCount++;
        //                    if (endWordCount == 2)
        //                    {
        //                        break;
        //                    }
        //                }
        //                if (startWordCount == 2 && endWordCount < 2)
        //                {
        //                    resultLines.Add(line);
        //                }
        //                if (line.Contains(startWord))
        //                {
        //                    startWordCount++;
        //                }
        //            }
        //            if (endWordCount == 2)
        //            {
        //                break;
        //            }
        //        }
        //    }

        //    return resultLines;
        //}


        //static void AllTheLinesFirstTable()
        //{
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        //        string startWord = "DEBIT/CREDIT";
        //        string endWord = "TOTAL";

        //        bool foundStart = false;
        //        bool shouldStop = false;

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

        //            foreach (var line in lines)
        //            {
        //                if (line.Contains(endWord) && foundStart)
        //                {
        //                    shouldStop = true;
        //                    break;
        //                }
        //                if (foundStart)
        //                {
        //                    Console.WriteLine(line);
        //                }
        //                if (line.Contains(startWord))
        //                {
        //                    foundStart = true;
        //                }
        //            }

        //            if (shouldStop)
        //            {
        //                break;
        //            }
        //        }
        //    }
        //}

        //static void AllTheLinesWrong()
        //{
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        //        string startWord = "DEBIT/CREDIT";
        //        string endWord = "TOTAL";

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

        //            bool foundStart = false;
        //            foreach (var line in lines)
        //            {
        //                if (line.Contains(endWord))
        //                {
        //                    foundStart = false;
        //                }
        //                if (foundStart)
        //                {
        //                    Console.WriteLine(line);
        //                }
        //                if (line.Contains(startWord))
        //                {
        //                    foundStart = true;
        //                }
        //            }
        //        }
        //    }
        //}

        //private static void ReadTheFile()
        //{
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var words = page.GetWords();
        //            Console.WriteLine($"Page {i} has {page.Letters.Count} letters and {words.Count()} words.");

        //            foreach (var word in words)
        //            {
        //                Console.WriteLine($"Word: {word.Text}");
        //            }
        //        }
        //    }
        //}

        //static void LineAfterWord()
        //{
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        //        string targetWord = "DEBIT/CREDIT";

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

        //            bool foundTarget = false;
        //            foreach (var line in lines)
        //            {
        //                if (foundTarget)
        //                {
        //                    Console.WriteLine(line);
        //                    return;
        //                }
        //                if (line.Contains(targetWord))
        //                {
        //                    foundTarget = true;
        //                }
        //            }
        //        }

        //        Console.WriteLine($"Word \"{targetWord}\" not found in the document.");
        //    }
        //}


        //static void CheckFirstWord()
        //{
        //    using (var document = PdfDocument.Open(inputReportName))
        //    {
        //        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        //        string targetWord = "Acireale";

        //        for (var i = 1; i <= document.NumberOfPages; i++)
        //        {
        //            Page page = document.GetPage(i);
        //            var lines = ContentOrderTextExtractor.GetText(page).Split('\n');

        //            foreach (var line in lines)
        //            {
        //                if (line.Contains(targetWord))
        //                {
        //                    Console.WriteLine(line);
        //                    return;
        //                }
        //            }
        //        }

        //        Console.WriteLine($"Word \"{targetWord}\" not found in the document.");
        //    }
        //}

        ////static void CheckFirstWord()
        ////{
        ////    using (var document = PdfDocument.Open(filename))
        ////    {
        ////        Console.WriteLine($"Document has {document.NumberOfPages} pages.");

        ////        string targetWord = "Casimiro";

        ////        for (var i = 1; i <= document.NumberOfPages; i++)
        ////        {
        ////            Page page = document.GetPage(i);
        ////            var words = page.GetWords().ToList();

        ////            for (int j = 0; j < words.Count; j++)
        ////            {
        ////                if (words[j].Text == targetWord)
        ////                {
        ////                    // Small threshold for Y-coordinate difference to account for slight inaccuracies
        ////                    const double lineThreshold = 1.0;

        ////                    // Get the start of the line
        ////                    int startOfLine = j;
        ////                    while (startOfLine > 0 && Math.Abs(words[startOfLine].BoundingBox.Bottom - words[j].BoundingBox.Bottom) < lineThreshold)
        ////                    {
        ////                        startOfLine--;
        ////                    }

        ////                    // Get the end of the line
        ////                    int endOfLine = j;
        ////                    while (endOfLine < words.Count - 1 && Math.Abs(words[endOfLine].BoundingBox.Bottom - words[j].BoundingBox.Bottom) < lineThreshold)
        ////                    {
        ////                        endOfLine++;
        ////                    }

        ////                    // Print the line
        ////                    for (int k = startOfLine; k <= endOfLine; k++)
        ////                    {
        ////                        Console.Write($"{words[k].Text} ");
        ////                    }
        ////                    Console.WriteLine();

        ////                    // Return after finding the first line
        ////                    return;
        ////                }
        ////            }
        ////        }

        ////        Console.WriteLine($"Word \"{targetWord}\" not found in the document.");
        ////    }
        ////}
    }

}