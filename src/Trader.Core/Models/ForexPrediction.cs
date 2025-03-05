namespace Trader.Core.Models;

public class ForexPrediction
{
    public required string CurrencyPair { get; set; }
    public DateTime Timestamp { get; set; }
    public ChartTimeframe Timeframe { get; set; }
    public TradingDirection Direction { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal RiskRewardRatio => Math.Abs((TakeProfit - EntryPrice) / (EntryPrice - StopLoss));
    public List<string> AnalysisFactors { get; set; } = new List<string>();
}

public enum ChartTimeframe
{
    Minutes5 = 5,
    Minutes15 = 15,
    Hours1 = 60,
    Hours4 = 240,
    Day1 = 1440
}

public enum TradingDirection
{
    Buy,
    Sell
}