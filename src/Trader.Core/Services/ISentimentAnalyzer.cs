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