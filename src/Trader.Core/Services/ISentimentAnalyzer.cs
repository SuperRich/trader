namespace Trader.Core.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trader.Core.Models;

/// <summary>
/// Interface for services that provide sentiment analysis for forex currency pairs.
/// </summary>
public interface ISentimentAnalyzer
{
    /// <summary>
    /// Analyzes market sentiment for the specified currency pair.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze (e.g., "EURUSD", "GBPJPY").</param>
    /// <returns>A SentimentAnalysisResult containing sentiment data for the specified currency pair.</returns>
    Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string currencyPair);
    
    /// <summary>
    /// Gets recommended forex trading opportunities based on current market conditions.
    /// </summary>
    /// <param name="count">The number of recommendations to return (default: 3).</param>
    /// <param name="provider">The data provider to use for price data (optional).</param>
    /// <returns>A list of trading recommendations for the most promising forex pairs.</returns>
    Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3, string? provider = null);
}

/// <summary>
/// Represents the overall sentiment type for a financial instrument.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SentimentType
{
    /// <summary>
    /// Positive sentiment indicating expectation of price increases.
    /// </summary>
    Bullish,
    
    /// <summary>
    /// Negative sentiment indicating expectation of price decreases.
    /// </summary>
    Bearish,
    
    /// <summary>
    /// Neutral sentiment indicating no clear directional bias.
    /// </summary>
    Neutral
}

/// <summary>
/// Represents the type of order to be placed.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum OrderType
{
    /// <summary>
    /// Execute a buy trade immediately at the current market price.
    /// </summary>
    MarketBuy,
    
    /// <summary>
    /// Execute a sell trade immediately at the current market price.
    /// </summary>
    MarketSell,
    
    /// <summary>
    /// Buy when price falls to a specified level below current price.
    /// </summary>
    LimitBuy,
    
    /// <summary>
    /// Sell when price rises to a specified level above current price.
    /// </summary>
    LimitSell,
    
    /// <summary>
    /// Buy when price rises to a specified level above current price.
    /// </summary>
    StopBuy,
    
    /// <summary>
    /// Sell when price falls to a specified level below current price.
    /// </summary>
    StopSell
}

/// <summary>
/// Represents a quick scalping trade opportunity.
/// </summary>
public class InOutPlay
{
    /// <summary>
    /// Whether a quick scalping opportunity is available.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// The trade direction for the scalp (Buy or Sell).
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// The entry price for the scalp trade.
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// The stop loss price for the scalp trade.
    /// </summary>
    public decimal StopLoss { get; set; }

    /// <summary>
    /// The take profit price for the scalp trade.
    /// </summary>
    public decimal TakeProfit { get; set; }

    /// <summary>
    /// Expected time to be in the trade (e.g., "5-15 minutes").
    /// </summary>
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>
    /// Brief explanation of the scalping setup.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Contains the result of a sentiment analysis for a currency pair.
/// </summary>
public class SentimentAnalysisResult
{
    /// <summary>
    /// The currency pair that was analyzed (e.g., "EURUSD").
    /// </summary>
    public string CurrencyPair { get; set; } = string.Empty;
    
    /// <summary>
    /// The overall sentiment for the currency pair.
    /// </summary>
    public SentimentType Sentiment { get; set; }
    
    /// <summary>
    /// Confidence score for the sentiment analysis (0.0 to 1.0).
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// A list of factors contributing to the sentiment analysis.
    /// </summary>
    public List<string> Factors { get; set; } = new List<string>();
    
    /// <summary>
    /// A brief summary of the sentiment analysis.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// List of sources cited in the analysis.
    /// </summary>
    public List<string> Sources { get; set; } = new List<string>();
    
    /// <summary>
    /// The timestamp when the analysis was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The model used for the analysis (e.g., "anthropic/claude-3-opus:beta", "openai/gpt-4o").
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;
    
    /// <summary>
    /// The current price at the time of analysis.
    /// </summary>
    public decimal CurrentPrice { get; set; }
    
    /// <summary>
    /// The recommended trade direction (Buy, Sell, or None).
    /// </summary>
    public string TradeRecommendation { get; set; } = "None";
    
    /// <summary>
    /// The recommended stop loss price level.
    /// </summary>
    public decimal StopLossPrice { get; set; }
    
    /// <summary>
    /// The recommended take profit price level.
    /// </summary>
    public decimal TakeProfitPrice { get; set; }
    
    /// <summary>
    /// The optimal entry price for the trade, which may differ from the current price.
    /// </summary>
    public decimal BestEntryPrice { get; set; }
    
    /// <summary>
    /// The type of order to place (MarketBuy, MarketSell, LimitBuy, LimitSell, StopBuy, StopSell).
    /// </summary>
    public OrderType OrderType { get; set; } = OrderType.MarketBuy;
    
    /// <summary>
    /// The risk level of the trade (Low, Medium, High, Very High).
    /// </summary>
    public string RiskLevel { get; set; } = "Medium";
    
    /// <summary>
    /// The risk-to-reward ratio of the recommended trade.
    /// </summary>
    public decimal RiskRewardRatio => 
        (TradeRecommendation == "Buy" && TakeProfitPrice > CurrentPrice && StopLossPrice < CurrentPrice) 
            ? Math.Round((TakeProfitPrice - CurrentPrice) / (CurrentPrice - StopLossPrice), 2)
            : (TradeRecommendation == "Sell" && TakeProfitPrice < CurrentPrice && StopLossPrice > CurrentPrice)
                ? Math.Round((CurrentPrice - TakeProfitPrice) / (StopLossPrice - CurrentPrice), 2)
                : 0;
    
    /// <summary>
    /// Whether a trade is recommended based on the analysis.
    /// </summary>
    public bool IsTradeRecommended => TradeRecommendation != "None" && RiskRewardRatio > 0;

    /// <summary>
    /// Information about the current forex market session.
    /// </summary>
    public MarketSessionInfo? MarketSession { get; set; }
    
    /// <summary>
    /// Warning message if the current market session is not optimal for trading.
    /// </summary>
    public string? SessionWarning { get; set; }
    
    /// <summary>
    /// Estimated time until the best entry price might be reached (e.g., "2-3 hours", "1-2 days").
    /// </summary>
    public string TimeToBestEntry { get; set; } = string.Empty;
    
    /// <summary>
    /// The date and time until which this recommendation is considered valid.
    /// </summary>
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(1);
    
    /// <summary>
    /// Indicates whether it's still safe to enter the trade at the current price, even if the best entry price is different.
    /// </summary>
    public bool IsSafeToEnterAtCurrentPrice { get; set; } = false;
    
    /// <summary>
    /// Explains why it's safe or unsafe to enter at the current price, providing specific reasoning.
    /// </summary>
    public string CurrentEntryReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Position sizing information for this currency pair.
    /// </summary>
    public PositionSizingInfo? PositionSizing { get; set; }
    
    /// <summary>
    /// The model's step-by-step reasoning process that led to this analysis.
    /// </summary>
    public string ModelReasoning { get; set; } = string.Empty;

    /// <summary>
    /// Quick scalping trade opportunity with tight stop loss and quick profit target.
    /// </summary>
    public InOutPlay? InOutPlay { get; set; }
}

/// <summary>
/// Contains information about the current forex market session.
/// </summary>
public class MarketSessionInfo
{
    /// <summary>
    /// The name of the current market session (e.g., "Asian", "London", "New York").
    /// </summary>
    public string CurrentSession { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the current session characteristics.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Liquidity level from 1 (lowest) to 5 (highest).
    /// </summary>
    public int LiquidityLevel { get; set; }
    
    /// <summary>
    /// The recommended session for executing trades for this currency pair.
    /// </summary>
    public string RecommendedSession { get; set; } = string.Empty;
    
    /// <summary>
    /// Explanation of why the recommended session is optimal.
    /// </summary>
    public string RecommendationReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Time remaining until the next session starts (formatted as hours and minutes).
    /// </summary>
    public string TimeUntilNextSession { get; set; } = string.Empty;
    
    /// <summary>
    /// The next session that will become active.
    /// </summary>
    public string NextSession { get; set; } = string.Empty;
    
    /// <summary>
    /// Current UTC time when this information was calculated.
    /// </summary>
    public DateTime CurrentTimeUtc { get; set; }
    
    /// <summary>
    /// The exact UTC time when the next session will start.
    /// </summary>
    public DateTime NextSessionStartTimeUtc { get; set; }
}

/// <summary>
/// Contains a trading recommendation for a forex pair.
/// </summary>
public class ForexRecommendation
{
    /// <summary>
    /// The currency pair for this recommendation (e.g., "EURUSD").
    /// </summary>
    public string CurrencyPair { get; set; } = string.Empty;
    
    /// <summary>
    /// The recommended trade direction (Buy or Sell).
    /// </summary>
    public string Direction { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of order to place (Market, BuyLimit, SellLimit, BuyStop, SellStop).
    /// </summary>
    public OrderType OrderType { get; set; } = OrderType.MarketBuy;
    
    /// <summary>
    /// The sentiment type for this recommendation.
    /// </summary>
    public SentimentType Sentiment { get; set; }
    
    /// <summary>
    /// Confidence score for this recommendation (0.0 to 1.0).
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// Current price at time of recommendation.
    /// </summary>
    public decimal CurrentPrice { get; set; }
    
    /// <summary>
    /// Recommended take profit level.
    /// </summary>
    public decimal TakeProfitPrice { get; set; }
    
    /// <summary>
    /// Recommended stop loss level.
    /// </summary>
    public decimal StopLossPrice { get; set; }
    
    /// <summary>
    /// The optimal entry price for the trade, which may differ from the current price.
    /// </summary>
    public decimal BestEntryPrice { get; set; }
    
    /// <summary>
    /// The model used for the analysis (e.g., "anthropic/claude-3-opus:beta", "openai/gpt-4o").
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;
    
    /// <summary>
    /// Potential risk-reward ratio for this trade.
    /// </summary>
    public decimal RiskRewardRatio 
    {
        get
        {
            if (Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase) && 
                TakeProfitPrice > CurrentPrice && 
                StopLossPrice < CurrentPrice)
            {
                return Math.Round((TakeProfitPrice - CurrentPrice) / (CurrentPrice - StopLossPrice), 2);
            }
            else if (Direction.Equals("Sell", StringComparison.OrdinalIgnoreCase) && 
                     TakeProfitPrice < CurrentPrice && 
                     StopLossPrice > CurrentPrice)
            {
                return Math.Round((CurrentPrice - TakeProfitPrice) / (StopLossPrice - CurrentPrice), 2);
            }
            
            return 0;
        }
    }
    
    /// <summary>
    /// Factors supporting this recommendation.
    /// </summary>
    public List<string> Factors { get; set; } = new List<string>();
    
    /// <summary>
    /// Detailed rationale for this recommendation.
    /// </summary>
    public string Rationale { get; set; } = string.Empty;
    
    /// <summary>
    /// Sources cited in the analysis.
    /// </summary>
    public List<string> Sources { get; set; } = new List<string>();
    
    /// <summary>
    /// The timestamp when the recommendation was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Information about the current forex market session.
    /// </summary>
    public MarketSessionInfo? MarketSession { get; set; }
    
    /// <summary>
    /// Warning message if the current market session is not optimal for trading.
    /// </summary>
    public string? SessionWarning { get; set; }
    
    /// <summary>
    /// Estimated time until the best entry price might be reached (e.g., "2-3 hours", "1-2 days").
    /// </summary>
    public string TimeToBestEntry { get; set; } = string.Empty;
    
    /// <summary>
    /// The date and time until which this recommendation is considered valid.
    /// </summary>
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(1);
    
    /// <summary>
    /// Indicates whether it's still safe to enter the trade at the current price, even if the best entry price is different.
    /// </summary>
    public bool IsSafeToEnterAtCurrentPrice { get; set; } = false;
    
    /// <summary>
    /// Explains why it's safe or unsafe to enter at the current price, providing specific reasoning.
    /// </summary>
    public string CurrentEntryReason { get; set; } = string.Empty;
    
    /// <summary>
    /// The risk level of the trade (Low, Medium, High, Very High).
    /// </summary>
    public string RiskLevel { get; set; } = "Medium";
    
    /// <summary>
    /// Position sizing information for this currency pair.
    /// </summary>
    public PositionSizingInfo? PositionSizing { get; set; }
    
    /// <summary>
    /// The model's step-by-step reasoning process that led to this recommendation.
    /// </summary>
    public string ModelReasoning { get; set; } = string.Empty;

    /// <summary>
    /// Quick scalping trade opportunity with tight stop loss and quick profit target.
    /// </summary>
    public InOutPlay? InOutPlay { get; set; }
}