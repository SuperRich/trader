using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services;

/// <summary>
/// Implementation of ISentimentAnalyzer using Perplexity AI's API to analyze forex market sentiment.
/// </summary>
public class PerplexitySentimentAnalyzer : ISentimentAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<PerplexitySentimentAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the PerplexitySentimentAnalyzer class.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for API requests.</param>
    /// <param name="configuration">Configuration containing the Perplexity API key.</param>
    /// <param name="logger">Logger for capturing errors and information.</param>
    /// <exception cref="ArgumentNullException">Thrown when the API key is not found in configuration.</exception>
    public PerplexitySentimentAnalyzer(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<PerplexitySentimentAnalyzer> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        // Try to get the API key from configuration (checking both regular config and environment variables)
        var perplexityApiKey = configuration["Perplexity:ApiKey"];
        var envApiKey = configuration["TRADER_PERPLEXITY_API_KEY"];
        
        if (!string.IsNullOrEmpty(perplexityApiKey))
            _apiKey = perplexityApiKey;
        else if (!string.IsNullOrEmpty(envApiKey))
            _apiKey = envApiKey;
        else
            throw new ArgumentNullException(nameof(configuration), "Perplexity API key is required");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Set up HttpClient
        _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    /// <summary>
    /// Analyzes market sentiment for the specified currency pair using Perplexity AI.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze (e.g., "EURUSD", "GBPJPY").</param>
    /// <returns>A SentimentAnalysisResult containing sentiment data for the specified currency pair.</returns>
    public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string currencyPair)
    {
        try
        {
            var prompt = $"Analyze the current market sentiment for the {currencyPair} forex pair. Consider recent economic news, technical analysis, and market trends. Provide a summary of bullish and bearish factors, and conclude with whether the overall sentiment is bullish, bearish, or neutral. Include specific data points and cite reliable sources for your analysis. Format your response as JSON with the following structure: {{\"sentiment\": \"bullish|bearish|neutral\", \"confidence\": 0.0-1.0, \"factors\": [list of factors], \"summary\": \"brief summary\", \"sources\": [list of sources with URLs]}}";

            // Using current Perplexity API models from https://docs.perplexity.ai/guides/model-cards
            var requestBody = new
            {
                model = "sonar-pro", // Using current model from Perplexity docs
                messages = new[]
                {
                    new { role = "system", content = "You are a financial analyst specializing in forex markets. Provide objective analysis with current data. Always verify your information with reliable sources and include citations. Be accurate with price levels and market data. Never make up information or sources. If you're uncertain about specific data points, acknowledge the limitations of your information." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 1000
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
                
            // Ensure the Authorization header is set for each request
            // Some HttpClient implementations might reset headers between requests
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            _logger.LogInformation("Sending request to Perplexity API for {CurrencyPair} with auth header: {AuthHeader}", 
                currencyPair, request.Headers.Authorization?.ToString() ?? "None");
                
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Perplexity API request failed with status {StatusCode}: {ErrorMessage}", 
                    response.StatusCode, errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Authorization failed. API Key: {ApiKey} (first/last 4 chars)", 
                        _apiKey.Length > 8 ? $"{_apiKey[..4]}...{_apiKey[^4..]}" : "Too short");
                }
                
                response.EnsureSuccessStatusCode(); // This will throw and be caught by the catch block
            }

            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response: {Response}", responseString);
            var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new Exception("Invalid response from Perplexity API");
            }

            // Extract the JSON from the response text
            var responseContent = responseObject.choices[0].message.content;
            var jsonStartIndex = responseContent.IndexOf('{');
            var jsonEndIndex = responseContent.LastIndexOf('}');
            
            if (jsonStartIndex == -1 || jsonEndIndex == -1)
            {
                throw new Exception("Could not extract JSON from response");
            }
                
            var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            var sentimentData = JsonSerializer.Deserialize<SentimentData>(jsonContent);
            
            if (sentimentData == null)
            {
                throw new Exception("Could not parse sentiment data");
            }

            return new SentimentAnalysisResult
            {
                CurrencyPair = currencyPair,
                Sentiment = ParseSentiment(sentimentData.sentiment),
                Confidence = sentimentData.confidence,
                Factors = sentimentData.factors != null ? sentimentData.factors : new List<string>(),
                Summary = sentimentData.summary,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {CurrencyPair}", currencyPair);
            
            // Return a fallback result
            return new SentimentAnalysisResult
            {
                CurrencyPair = currencyPair,
                Sentiment = SentimentType.Neutral,
                Confidence = 0.5m,
                Factors = new List<string> { "Error fetching sentiment data" },
                Summary = "Could not retrieve sentiment data at this time",
                Sources = new List<string>(),
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// Gets recommended forex trading opportunities based on current market conditions.
    /// </summary>
    /// <param name="count">The number of recommendations to return.</param>
    /// <param name="provider">The data provider to use for price data (optional).</param>
    /// <returns>A list of trading recommendations for the most promising forex pairs.</returns>
    public async Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3, string? provider = null)
    {
        try
        {
            // First, we'll use Perplexity to get both current market prices and trading recommendations
            var prompt = $@"
You are a forex trading expert. I need you to provide exactly {count} forex trading recommendations with ACCURATE, VERIFIED price data.

STEP 1: Find the CURRENT, REAL-TIME prices for these major forex pairs:
EURUSD, GBPUSD, USDJPY, GBPJPY, AUDUSD, USDCAD, EURJPY, EURGBP, USDCHF, NZDUSD

CRUCIAL: Please use MULTIPLE SOURCES to verify these prices. The prices MUST be current within the last hour. Make sure your price data reflects actual market rates, not estimates.

STEP 2: Based on these verified prices and your analysis:
- Select {count} most promising pairs to trade right now
- For each pair, determine Buy or Sell direction
- Include the current market price you verified (must be accurate to 5 decimal places for non-JPY pairs, 3 places for JPY pairs)
- Calculate take profit (TP) price based on key technical resistance/support levels
- Calculate stop loss (SL) price based on key technical resistance/support levels
- Ensure TP and SL are at logical price levels with a reasonable risk-reward ratio (at least 1:1)
- IMPORTANT: Provide the exact URLs of the sources you used to verify the price data in a separate ""sources"" array
- Each source URL must be a complete, clickable link (e.g., ""https://www.example.com/forex"")

STEP 3: Format your entire response as VALID JSON with this exact structure:
{{
    ""recommendations"": [
        {{
            ""pair"": ""EURUSD"",
            ""direction"": ""Buy"" or ""Sell"",
            ""sentiment"": ""Bullish"" or ""Bearish"" or ""Neutral"",
            ""confidence"": 0.0-1.0,
            ""currentPrice"": decimal,
            ""takeProfitPrice"": decimal,
            ""stopLossPrice"": decimal,
            ""factors"": [""list"", ""of"", ""factors""],
            ""rationale"": ""detailed explanation"",
            ""sources"": [""list"", ""of"", ""source"", ""URLs""]
        }}
    ]
}}";

            var requestBody = new
            {
                model = "sonar-pro", // Using current model from Perplexity docs
                messages = new[]
                {
                    new { role = "system", content = @"You are an expert forex trader with access to real-time market data from multiple financial sources. 

KEY RESPONSIBILITIES:
1. Provide ACCURATE, CURRENT price data that reflects actual market rates
2. Double-verify all prices from multiple reliable financial sources
3. Ensure price relationships are logical (e.g., EURUSD ~1.05-1.15, JPY pairs ~100-200)
4. Calculate reasonable take profit and stop loss levels based on verified prices
5. Format response as valid JSON that follows the requested structure exactly

YOU MUST:
- Verify price data is correct and current before submission
- Format all non-JPY forex pairs to 5 decimal places (e.g., 1.10952)
- Format JPY pairs to 3 decimal places (e.g., 154.305)
- Ensure price values reflect their proper magnitude (e.g., USDJPY > 100)
- Generate only properly formatted JSON with no text outside the JSON structure" },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2,
                max_tokens = 1500
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
                
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            _logger.LogInformation("Sending recommendation request to Perplexity API for {Count} forex pairs with live price data", count);
                
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received recommendations response: {Response}", responseString);
            var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                _logger.LogError("Perplexity API returned empty or null choices array");
                throw new Exception("Invalid response from Perplexity API");
            }

            // Extract the JSON from the response text
            var responseContent = responseObject.choices[0].message.content;
            var jsonStartIndex = responseContent.IndexOf('{');
            var jsonEndIndex = responseContent.LastIndexOf('}');
            
            if (jsonStartIndex == -1 || jsonEndIndex == -1)
            {
                _logger.LogError("Could not find JSON in Perplexity response");
                throw new Exception("Could not extract JSON from response");
            }
                
            var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            var recommendationsData = JsonSerializer.Deserialize<RecommendationsData>(jsonContent);
            
            if (recommendationsData?.recommendations == null)
            {
                _logger.LogError("Could not parse recommendations data from Perplexity response");
                throw new Exception("Could not parse recommendations data");
            }

            // Convert the recommendations to ForexRecommendation objects
            var recommendations = recommendationsData.recommendations.Select(r => new ForexRecommendation
            {
                CurrencyPair = r.pair,
                Direction = r.direction,
                Sentiment = ParseSentiment(r.sentiment),
                Confidence = r.confidence,
                CurrentPrice = r.currentPrice,
                TakeProfitPrice = r.takeProfitPrice,
                StopLossPrice = r.stopLossPrice,
                Factors = r.factors ?? new List<string>(),
                Rationale = r.rationale,
                Sources = r.sources ?? new List<string>(),
                Timestamp = DateTime.UtcNow,
                ModelUsed = "sonar-pro"
            }).ToList();

            _logger.LogInformation("Successfully generated {Count} trading recommendations", recommendations.Count);
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            return new List<ForexRecommendation>();
        }
    }

    /// <summary>
    /// Parses a string sentiment indicator into the SentimentType enum.
    /// </summary>
    /// <param name="sentiment">String sentiment indicator (e.g., "bullish", "bearish").</param>
    /// <returns>The corresponding SentimentType enum value.</returns>
    private SentimentType ParseSentiment(string sentiment)
    {
        return sentiment?.ToLower() switch
        {
            "bullish" => SentimentType.Bullish,
            "bearish" => SentimentType.Bearish,
            _ => SentimentType.Neutral
        };
    }

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
    /// Structure for parsing the sentiment data from the Perplexity response.
    /// </summary>
    private class SentimentData
    {
        public string sentiment { get; set; } = string.Empty;
        public decimal confidence { get; set; }
        public List<string>? factors { get; set; }
        public string summary { get; set; } = string.Empty;
        public List<string>? sources { get; set; }
    }
    
    /// <summary>
    /// Structure for parsing the recommendations data from the Perplexity response.
    /// </summary>
    private class RecommendationsData
    {
        public List<RecommendationItem> recommendations { get; set; } = new List<RecommendationItem>();
    }
    
    /// <summary>
    /// Structure for a single recommendation item.
    /// </summary>
    private class RecommendationItem
    {
        public string pair { get; set; } = string.Empty;
        public string direction { get; set; } = string.Empty;
        public string sentiment { get; set; } = string.Empty;
        public decimal confidence { get; set; }
        public decimal currentPrice { get; set; }
        public decimal takeProfitPrice { get; set; }
        public decimal stopLossPrice { get; set; }
        public List<string>? factors { get; set; }
        public string rationale { get; set; } = string.Empty;
        public List<string>? sources { get; set; }
    }
}