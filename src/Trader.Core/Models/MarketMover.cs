using System.Text.Json.Serialization;

namespace Trader.Core.Models;

/// <summary>
/// Represents a market mover with information about price movement and EMA status
/// </summary>
public class MarketMover
{
    /// <summary>
    /// The symbol/pair (e.g., "EURUSD", "BTCUSD")
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// The current price of the asset
    /// </summary>
    public decimal CurrentPrice { get; set; }
    
    /// <summary>
    /// The previous price used for comparison
    /// </summary>
    public decimal PreviousPrice { get; set; }
    
    /// <summary>
    /// The absolute price change
    /// </summary>
    public decimal PriceChange => Math.Abs(CurrentPrice - PreviousPrice);
    
    /// <summary>
    /// The percentage price change
    /// </summary>
    public decimal PercentageChange => PreviousPrice != 0 ? Math.Abs((CurrentPrice - PreviousPrice) / PreviousPrice * 100) : 0;
    
    /// <summary>
    /// The price movement in pips
    /// </summary>
    public decimal PipMovement { get; set; }
    
    /// <summary>
    /// The direction of the price movement
    /// </summary>
    public MovementDirection Direction => CurrentPrice > PreviousPrice ? MovementDirection.Up : MovementDirection.Down;
    
    /// <summary>
    /// The timeframe used for analysis
    /// </summary>
    public ChartTimeframe Timeframe { get; set; }
    
    /// <summary>
    /// The EMA values for different periods
    /// </summary>
    public Dictionary<int, decimal> EmaValues { get; set; } = new Dictionary<int, decimal>();
    
    /// <summary>
    /// The EMA filter status for trading signals
    /// </summary>
    public EmaFilterStatus EmaStatus { get; set; } = new EmaFilterStatus();
    
    /// <summary>
    /// The asset type (Forex or Crypto)
    /// </summary>
    public AssetType AssetType { get; set; }
    
    /// <summary>
    /// The timestamp of the data
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the status of EMA filters for trading signals
/// </summary>
public class EmaFilterStatus
{
    /// <summary>
    /// Whether the price is above the 10 EMA
    /// </summary>
    public bool IsAboveEma10 { get; set; }
    
    /// <summary>
    /// Whether the price is above the 20 EMA
    /// </summary>
    public bool IsAboveEma20 { get; set; }
    
    /// <summary>
    /// Whether the price is above the 50 EMA
    /// </summary>
    public bool IsAboveEma50 { get; set; }
    
    /// <summary>
    /// Whether the 10 EMA is crossing above the 20 EMA
    /// </summary>
    public bool IsEma10CrossingAboveEma20 { get; set; }
    
    /// <summary>
    /// Whether the 10 EMA is crossing below the 20 EMA
    /// </summary>
    public bool IsEma10CrossingBelowEma20 { get; set; }
    
    /// <summary>
    /// Whether the price is bouncing off the 10 EMA
    /// </summary>
    public bool IsBouncingOffEma10 { get; set; }
    
    /// <summary>
    /// Whether the price is bouncing off the 20 EMA
    /// </summary>
    public bool IsBouncingOffEma20 { get; set; }
    
    /// <summary>
    /// Whether the price is bouncing off the 50 EMA
    /// </summary>
    public bool IsBouncingOffEma50 { get; set; }
    
    /// <summary>
    /// Whether the price is breaking through the 10 EMA
    /// </summary>
    public bool IsBreakingThroughEma10 { get; set; }
    
    /// <summary>
    /// Whether the price is breaking through the 20 EMA
    /// </summary>
    public bool IsBreakingThroughEma20 { get; set; }
    
    /// <summary>
    /// Whether the price is breaking through the 50 EMA
    /// </summary>
    public bool IsBreakingThroughEma50 { get; set; }
    
    /// <summary>
    /// Gets a list of trading signals based on EMA status
    /// </summary>
    public List<string> GetSignals()
    {
        var signals = new List<string>();
        
        if (IsEma10CrossingAboveEma20) signals.Add("EMA 10 crossing above EMA 20 (Bullish)");
        if (IsEma10CrossingBelowEma20) signals.Add("EMA 10 crossing below EMA 20 (Bearish)");
        
        if (IsBouncingOffEma10) signals.Add("Price bouncing off EMA 10");
        if (IsBouncingOffEma20) signals.Add("Price bouncing off EMA 20");
        if (IsBouncingOffEma50) signals.Add("Price bouncing off EMA 50");
        
        if (IsBreakingThroughEma10) signals.Add("Price breaking through EMA 10");
        if (IsBreakingThroughEma20) signals.Add("Price breaking through EMA 20");
        if (IsBreakingThroughEma50) signals.Add("Price breaking through EMA 50");
        
        return signals;
    }
}

/// <summary>
/// Represents the direction of price movement
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovementDirection
{
    Up,
    Down
}

/// <summary>
/// Represents the type of asset
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetType
{
    Forex,
    Crypto
} 