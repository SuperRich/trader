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
    /// The timestamp when the sentiment analysis was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
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
    public decimal RiskRewardRatio => Math.Abs((TakeProfitPrice - CurrentPrice) / (CurrentPrice - StopLossPrice));
    
    /// <summary>
    /// Key factors supporting this recommendation.
    /// </summary>
    public List<string> Factors { get; set; } = new List<string>();
    
    /// <summary>
    /// Brief trading rationale.
    /// </summary>
    public string Rationale { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp of when this recommendation was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}