namespace AmpFuturesReportManager.Application.Modes;


public class Operation
{
    public DateOnly Date { get; set; }
    public long TradeNumber { get; set; } // Changed to long to handle large numbers
    public string Market { get; set; }
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
}

public class RusselMicro : Contract
{
    public override string Name { get; set; } = "M2K";
    public override decimal TickMovement { get; set; } = 0.1M;
    public override decimal TickMoneyValue { get; set; } = 0.5M;
    public override decimal FeesForContract { get; set; } = 0.62M;
}