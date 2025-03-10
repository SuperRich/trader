using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services;

/// <summary>
/// Trading chart analyzer that provides AI-based trading recommendations for various timeframes.
/// </summary>
public class TradingViewAnalyzer : ISentimentAnalyzer
{
    private readonly IForexDataProvider _dataProvider;
    private readonly HttpClient _httpClient;
    private readonly string _perplexityApiKey;
    private readonly ILogger<TradingViewAnalyzer> _logger;

    public TradingViewAnalyzer(
        IForexDataProvider dataProvider,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TradingViewAnalyzer> logger)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Try to get the API key from configuration (checking both regular config and environment variables)
        var perplexityApiKey = configuration["Perplexity:ApiKey"];
        var envApiKey = configuration["TRADER_PERPLEXITY_API_KEY"];
        
        if (!string.IsNullOrEmpty(perplexityApiKey))
            _perplexityApiKey = perplexityApiKey;
        else if (!string.IsNullOrEmpty(envApiKey))
            _perplexityApiKey = envApiKey;
        else
            throw new ArgumentNullException(nameof(configuration), "Perplexity API key is required");
            
        // Set up HttpClient
        _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _perplexityApiKey);
    }

    /// <summary>
    /// Analyzes a trading chart and provides sentiment analysis.
    /// </summary>
    /// <param name="symbol">The trading symbol to analyze (e.g., "EURUSD", "BTCUSD")</param>
    /// <returns>A sentiment analysis result with trading recommendation</returns>
    public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string symbol)
    {
        try
        {
            _logger.LogInformation("Analyzing sentiment for {Symbol}", symbol);
            
            // Fetch candle data for multiple timeframes
            var candleTasks = new[]
            {
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Minutes15, 20),
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours1, 12),
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours4, 8),
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Day1, 5)
            };
            
            // Wait for all data to be retrieved
            await Task.WhenAll(candleTasks);
            
            // Extract candle data for each timeframe
            var candles15m = await candleTasks[0];
            var candles1h = await candleTasks[1];
            var candles4h = await candleTasks[2];
            var candles1d = await candleTasks[3];
            
            // Generate the prompt for the sentiment analysis
            var prompt = GenerateChartAnalysisPrompt(symbol, candles15m, candles1h, candles4h, candles1d);
            
            // Send the prompt to Perplexity AI
            var requestBody = new
            {
                model = "sonar-pro",
                messages = new[]
                {
                    new { role = "system", content = "You are an expert trading analyst specializing in technical analysis and market sentiment. Provide concise, accurate trading advice based on chart data. Always verify your information with reliable sources and include citations when making claims about market conditions. Be precise with price levels and never make up information. If you're uncertain about specific data points, acknowledge the limitations of your information." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 1000
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
                
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _perplexityApiKey);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new InvalidOperationException("Invalid response from Perplexity API");
            }
            
            // Extract the JSON from the response text
            var responseContent = responseObject.choices[0].message.content;
            var jsonStartIndex = responseContent.IndexOf('{');
            var jsonEndIndex = responseContent.LastIndexOf('}');
            
            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
            {
                var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                var sentimentData = JsonSerializer.Deserialize<SentimentData>(jsonContent);
                
                if (sentimentData != null)
                {
                    return new SentimentAnalysisResult
                    {
                        CurrencyPair = symbol,
                        Sentiment = ParseSentiment(sentimentData.sentiment),
                        Confidence = sentimentData.confidence,
                        Factors = sentimentData.factors != null ? sentimentData.factors : new List<string>(),
                        Summary = sentimentData.summary,
                        Sources = sentimentData.sources != null ? sentimentData.sources : new List<string>(),
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            
            // Fallback if we couldn't parse the JSON
            return new SentimentAnalysisResult
            {
                CurrencyPair = symbol,
                Sentiment = SentimentType.Neutral,
                Confidence = 0.5m,
                Factors = new List<string> { "Error parsing sentiment data" },
                Summary = "Could not parse sentiment data from the response",
                Sources = new List<string>(),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {Symbol}", symbol);
            throw; // Don't swallow the exception - let the caller handle it
        }
    }
    
    /// <summary>
    /// Provides trading recommendations for multiple assets.
    /// </summary>
    /// <param name="count">Number of recommendations to provide</param>
    /// <returns>List of trading recommendations</returns>
    public async Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3)
    {
        try
        {
            _logger.LogInformation("Getting trading recommendations for {Count} assets", count);
            
            // Define symbols to analyze
            var symbols = new List<string>
            {
                "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD",
                "BTCUSD", "ETHUSD", "XRPUSD"
            };
            
            // Generate multi-timeframe analysis for all symbols
            var recommendations = new List<ForexRecommendation>();
            
            foreach (var symbol in symbols)
            {
                try
                {
                    // Get data for all timeframes
                    var m5Data = await _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Minutes5, 50);
                    var h1Data = await _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours1, 24);
                    var h4Data = await _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours4, 20);
                    var d1Data = await _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Day1, 10);
                    
                    // Generate the prompt for chart analysis
                    var prompt = GenerateRecommendationPrompt(symbol, m5Data, h1Data, h4Data, d1Data);
                    
                    // Send the prompt to Perplexity AI
                    var requestBody = new
                    {
                        model = "sonar-pro",
                        messages = new[]
                        {
                            new { role = "system", content = "You are a professional trader with extensive experience in technical analysis. Provide accurate, actionable trading advice based on chart patterns and price action." },
                            new { role = "user", content = prompt }
                        },
                        temperature = 0.1,
                        max_tokens = 1000
                    };
                    
                    var content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json");
                        
                    var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                    {
                        Content = content
                    };
                    
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _perplexityApiKey);
                    
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    var responseString = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
                    
                    if (responseObject?.choices == null || responseObject.choices.Length == 0)
                    {
                        _logger.LogWarning("Invalid response from Perplexity API for {Symbol}", symbol);
                        continue;
                    }
                    
                    // Extract the JSON from the response text
                    var responseContent = responseObject.choices[0].message.content;
                    var jsonStartIndex = responseContent.IndexOf('{');
                    var jsonEndIndex = responseContent.LastIndexOf('}');
                    
                    if (jsonStartIndex == -1 || jsonEndIndex == -1)
                    {
                        _logger.LogWarning("Could not extract JSON from response for {Symbol}", symbol);
                        continue;
                    }
                        
                    var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                    var recommendationData = JsonSerializer.Deserialize<RecommendationData>(jsonContent);
                    
                    if (recommendationData == null)
                    {
                        _logger.LogWarning("Could not parse recommendation data for {Symbol}", symbol);
                        continue;
                    }
                    
                    // Add to recommendations list
                    recommendations.Add(new ForexRecommendation
                    {
                        CurrencyPair = symbol,
                        Direction = recommendationData.direction,
                        Sentiment = ParseSentiment(recommendationData.sentiment),
                        Confidence = recommendationData.confidence,
                        CurrentPrice = recommendationData.currentPrice,
                        TakeProfitPrice = recommendationData.takeProfitPrice,
                        StopLossPrice = recommendationData.stopLossPrice,
                        Factors = recommendationData.factors ?? new List<string>(),
                        Rationale = recommendationData.rationale,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Log but continue with other symbols
                    _logger.LogError(ex, "Error getting recommendation for {Symbol}", symbol);
                }
                
                // Add a small delay to avoid API rate limits
                await Task.Delay(200);
            }
            
            // Sort recommendations by confidence and return the top N
            return recommendations
                .OrderByDescending(r => r.Confidence)
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            throw; // Don't swallow the exception - let the caller handle it
        }
    }
    
    /// <summary>
    /// Generates a prompt for chart analysis across multiple timeframes.
    /// </summary>
    private string GenerateChartAnalysisPrompt(
        string symbol, 
        List<CandleData> candles15m, 
        List<CandleData> candles1h, 
        List<CandleData> candles4h,
        List<CandleData> candles1d)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Analyze the following {symbol} chart data across multiple timeframes and provide a trading recommendation with stop loss and take profit levels.");
        sb.AppendLine();
        
        // Add 15-minute timeframe data
        sb.AppendLine("15-MINUTE TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles15m.OrderByDescending(c => c.Timestamp).Take(8));
        sb.AppendLine();
        
        // Add 1-hour timeframe data
        sb.AppendLine("1-HOUR TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles1h.OrderByDescending(c => c.Timestamp).Take(6));
        sb.AppendLine();
        
        // Add 4-hour timeframe data
        sb.AppendLine("4-HOUR TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles4h.OrderByDescending(c => c.Timestamp).Take(5));
        sb.AppendLine();
        
        // Add daily timeframe data
        sb.AppendLine("DAILY TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles1d.OrderByDescending(c => c.Timestamp).Take(3));
        sb.AppendLine();
        
        // Add instructions for analysis
        sb.AppendLine("ANALYSIS INSTRUCTIONS:");
        sb.AppendLine("1. Identify key support and resistance levels visible on these charts");
        sb.AppendLine("2. Analyze price action trends and market structure on each timeframe");
        sb.AppendLine("3. Determine if any significant chart patterns are present");
        sb.AppendLine("4. Check if the charts indicate bullish or bearish sentiment");
        sb.AppendLine("5. Be precise with price levels and never make up information");
        sb.AppendLine("6. If referencing external market conditions or news, cite reliable sources");
        sb.AppendLine("7. Provide reasonable stop loss and take profit levels based on support/resistance");
        sb.AppendLine();
        
        // Request JSON format
        sb.AppendLine("RESPONSE FORMAT:");
        sb.AppendLine("Provide your analysis as a JSON object with the following structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"sentiment\": \"bullish|bearish|neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": 0.0,");
        sb.AppendLine("  \"direction\": \"buy|sell\",");
        sb.AppendLine("  \"stopLossPrice\": 0.0,");
        sb.AppendLine("  \"takeProfitPrice\": 0.0,");
        sb.AppendLine("  \"factors\": [\"factor1\", \"factor2\", ...],");
        sb.AppendLine("  \"summary\": \"brief summary\",");
        sb.AppendLine("  \"sources\": [\"source1\", \"source2\", ...]");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a prompt for multi-timeframe trading recommendation.
    /// </summary>
    private string GenerateRecommendationPrompt(
        string symbol, 
        List<CandleData> candles5m, 
        List<CandleData> candles1h, 
        List<CandleData> candles4h,
        List<CandleData> candles1d)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Analyze the following {symbol} chart data across multiple timeframes and provide a trading recommendation with precise entry, stop loss, and take profit levels.");
        sb.AppendLine();
        
        // Current price (most recent close)
        decimal currentPrice = candles5m.OrderByDescending(c => c.Timestamp).First().Close;
        
        // Add 5-minute timeframe data
        sb.AppendLine("5-MINUTE TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles5m.OrderByDescending(c => c.Timestamp).Take(10));
        sb.AppendLine();
        
        // Add 1-hour timeframe data
        sb.AppendLine("1-HOUR TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles1h.OrderByDescending(c => c.Timestamp).Take(8));
        sb.AppendLine();
        
        // Add 4-hour timeframe data
        sb.AppendLine("4-HOUR TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles4h.OrderByDescending(c => c.Timestamp).Take(5));
        sb.AppendLine();
        
        // Add daily timeframe data
        sb.AppendLine("DAILY TIMEFRAME DATA (newest to oldest):");
        AppendCandleData(sb, candles1d.OrderByDescending(c => c.Timestamp).Take(3));
        sb.AppendLine();
        
        // Add instructions for analysis
        sb.AppendLine("TRADING RECOMMENDATION INSTRUCTIONS:");
        sb.AppendLine("1. Determine the current trend direction on each timeframe");
        sb.AppendLine("2. Identify key support and resistance levels");
        sb.AppendLine("3. Look for trade entry signals (e.g., reversal patterns, breakouts, continuation patterns)");
        sb.AppendLine("4. If you identify a valid trade setup, recommend either Buy or Sell with an entry price at the current price");
        sb.AppendLine("5. Based on the chart structure, provide appropriate stop loss and take profit levels");
        sb.AppendLine("6. Ensure the risk-reward ratio is at least 1:1.5");
        sb.AppendLine("7. If no clear trade setup exists, recommend 'None' for the direction");
        sb.AppendLine();
        
        // Request JSON format
        sb.AppendLine("RESPONSE FORMAT:");
        sb.AppendLine("Provide your analysis as a JSON object with the following structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"sentiment\": \"bullish|bearish|neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": 0.0,");
        sb.AppendLine("  \"direction\": \"buy|sell\",");
        sb.AppendLine("  \"stopLossPrice\": 0.0,");
        sb.AppendLine("  \"takeProfitPrice\": 0.0,");
        sb.AppendLine("  \"factors\": [\"factor1\", \"factor2\", ...],");
        sb.AppendLine("  \"rationale\": \"brief explanation of trade setup and key levels\",");
        sb.AppendLine("  \"sources\": [\"source1\", \"source2\", ...]");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Appends candle data to a string builder in a readable format.
    /// </summary>
    private void AppendCandleData(StringBuilder sb, IEnumerable<CandleData> candles)
    {
        sb.AppendLine("Timestamp | Open | High | Low | Close | Volume");
        sb.AppendLine("----------|------|------|-----|-------|-------");
        
        foreach (var candle in candles)
        {
            sb.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume}");
        }
    }
    
    /// <summary>
    /// Parses a string sentiment indicator into the SentimentType enum.
    /// </summary>
    private SentimentType ParseSentiment(string sentiment)
    {
        return sentiment?.ToLower() switch
        {
            "bullish" => SentimentType.Bullish,
            "bearish" => SentimentType.Bearish,
            _ => SentimentType.Neutral
        };
    }
    
    #region API Response Classes
    
    /// <summary>
    /// Response structure from the Perplexity API.
    /// </summary>
    private class PerplexityResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
    }

    /// <summary>
    /// Represents a choice in the Perplexity API response.
    /// </summary>
    private class Choice
    {
        public Message message { get; set; } = new Message();
    }

    /// <summary>
    /// Represents a message in the Perplexity API response.
    /// </summary>
    private class Message
    {
        public string content { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Structure for parsing the sentiment analysis data from the Perplexity response.
    /// </summary>
    private class SentimentData
    {
        public string sentiment { get; set; } = string.Empty;
        public decimal confidence { get; set; }
        public decimal currentPrice { get; set; }
        public string direction { get; set; } = string.Empty;
        public decimal stopLossPrice { get; set; }
        public decimal takeProfitPrice { get; set; }
        public List<string>? factors { get; set; }
        public string summary { get; set; } = string.Empty;
        public List<string>? sources { get; set; }
    }
    
    /// <summary>
    /// Structure for parsing trading recommendation data from the Perplexity response.
    /// </summary>
    private class RecommendationData
    {
        public string direction { get; set; } = string.Empty;
        public string sentiment { get; set; } = string.Empty;
        public decimal confidence { get; set; }
        public decimal currentPrice { get; set; }
        public decimal stopLossPrice { get; set; }
        public decimal takeProfitPrice { get; set; }
        public List<string>? factors { get; set; }
        public string rationale { get; set; } = string.Empty;
    }
    
    #endregion
}