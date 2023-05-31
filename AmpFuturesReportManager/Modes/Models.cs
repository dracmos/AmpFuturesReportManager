
using AmpFuturesReportManager.Modes;

namespace AmpFuturesReportManager.Application.Modes;

public enum ReportType
{
    AMPFutures,
    CQG
}


public class Operation
{
    public DateTime Date { get; set; }
    public long TradeNumber { get; set; }
    public string Market { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public OperationType Type { get; set; }
    public string ContractDescription { get; set; }
    public Contract Contract { get; set; }
    public decimal TradePrice { get; set; }
    public string Currency { get; set; }
}

public enum OperationType
{
    Buy,
    Sell
}

public class RoundTripOperation
{
    public OperationType Type { get; set; }
    public int Quantity { get; set; }
    public Operation OpenOperation { get; set; }
    public Operation CloseOperation { get; set; }
    public Operation BuyOperation { get; set; }
    public Operation SellOperation { get; set; }
    public Contract Contract { get; set; }
    public decimal Ticks { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ProfitLossIncludingFees => ProfitLoss - Fees;
    public decimal Fees { get; set; }
}

public abstract class Contract
{
    public abstract string Name { get; set; }
    public abstract decimal TickMovement { get; set; }
    public abstract decimal TickMoneyValue { get; set; }
    public abstract decimal FeesForContract { get; set; }
    public abstract string Market { get; set; }
}

public class RusselMicro : Contract
{
    public override string Name { get; set; } = Tickers.RusselMicro;
    public override decimal TickMovement { get; set; } = 0.1M;
    public override decimal TickMoneyValue { get; set; } = 0.5M;
    public override decimal FeesForContract { get; set; } = 0.62M;
    public override string Market { get; set; } = "CME";
}

public class DowMicro : Contract
{
    public override string Name { get; set; } = Tickers.DowMicro;
    public override decimal TickMovement { get; set; } = 1M;
    public override decimal TickMoneyValue { get; set; } = 0.5M;
    public override decimal FeesForContract { get; set; } = 0.62M;
    public override string Market { get; set; } = "CBOT";
}


public class OrderFromCSV
{
    public string Account { get; set; }
    public string Status { get; set; }
    public string BS { get; set; }
    public int Qty { get; set; }
    public int UnFld { get; set; }
    public string Symbol { get; set; }
    public string OrdP { get; set; }
    public string AvgFillP { get; set; }
    public string Type { get; set; }
    public string LMTP { get; set; }
    public string DUR { get; set; }
    public int Fld { get; set; }
    public string PlaceT { get; set; }
    public string Hash { get; set; }
    public string User { get; set; }
    public string FillT { get; set; }
    public string CXLT { get; set; }
}