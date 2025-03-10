namespace Trader.Core.Services;

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
    /// <returns>A list of trading recommendations for the most promising forex pairs.</returns>
    Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3);
}

/// <summary>
/// Represents the overall sentiment type for a financial instrument.
/// </summary>
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
/// Contains the result of a sentiment analysis for a currency pair.
/// </summary>
public class SentimentAnalysisResult
{
    /// <summary>
    /// The currency pair being analyzed (e.g., "EURUSD").
    /// </summary>
    public string CurrencyPair { get; set; } = string.Empty;
    
    /// <summary>
    /// The overall sentiment (Bullish, Bearish, or Neutral).
    /// </summary>
    public SentimentType Sentiment { get; set; }
    
    /// <summary>
    /// A confidence score between 0.0 and 1.0 indicating the strength of the sentiment.
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
    /// Potential risk-reward ratio for this trade.
    /// </summary>
    public decimal RiskRewardRatio 
    { 
        get
        {
            // Handle cases where values are 0 or denominator would be 0
            if (CurrentPrice == 0 || StopLossPrice == 0 || TakeProfitPrice == 0 || CurrentPrice == StopLossPrice)
                return 0;
                
            // Calculate reward (distance to take profit)
            decimal reward = Math.Abs(TakeProfitPrice - CurrentPrice);
            
            // Calculate risk (distance to stop loss)
            decimal risk = Math.Abs(CurrentPrice - StopLossPrice);
            
            // Avoid division by zero
            if (risk == 0)
                return 0;
                
            return reward / risk;
        }
    }
    
    /// <summary>
    /// Key factors supporting this recommendation.
    /// </summary>
    public List<string> Factors { get; set; } = new List<string>();
    
    /// <summary>
    /// Brief trading rationale.
    /// </summary>
    public string Rationale { get; set; } = string.Empty;
    
    /// <summary>
    /// List of source URLs for the data used in this recommendation.
    /// </summary>
    public List<string> Sources { get; set; } = new List<string>();
    
    /// <summary>
    /// Timestamp of when this recommendation was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Information about the current forex market session.
    /// </summary>
    public MarketSessionInfo? MarketSession { get; set; }
}