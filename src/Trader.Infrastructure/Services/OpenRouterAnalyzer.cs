using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services;

/// <summary>
/// Implementation of ISentimentAnalyzer using OpenRouter API to analyze forex market sentiment.
/// </summary>
public class OpenRouterAnalyzer : ISentimentAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenRouterAnalyzer> _logger;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the OpenRouterAnalyzer class.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for API requests.</param>
    /// <param name="configuration">Configuration containing the OpenRouter API key.</param>
    /// <param name="logger">Logger for capturing errors and information.</param>
    /// <exception cref="ArgumentNullException">Thrown when the API key is not found in configuration.</exception>
    public OpenRouterAnalyzer(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<OpenRouterAnalyzer> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        // Try to get the API key from configuration (checking both regular config and environment variables)
        var openRouterApiKey = configuration["OpenRouter:ApiKey"];
        var envApiKey = configuration["TRADER_OPENROUTER_API_KEY"];
        
        if (!string.IsNullOrEmpty(openRouterApiKey))
            _apiKey = openRouterApiKey;
        else if (!string.IsNullOrEmpty(envApiKey))
            _apiKey = envApiKey;
        else
            throw new ArgumentNullException(nameof(configuration), "OpenRouter API key is required");
        
        // Get the model from configuration or use a default
        var configModel = configuration["OpenRouter:Model"];
        _model = !string.IsNullOrEmpty(configModel) ? configModel : "anthropic/claude-3-opus:beta";
        
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Set up HttpClient
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://trader.app"); // Required by OpenRouter
    }

    /// <summary>
    /// Analyzes market sentiment for the specified currency pair using OpenRouter API.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze (e.g., "EURUSD", "GBPJPY").</param>
    /// <returns>A SentimentAnalysisResult containing sentiment data for the specified currency pair.</returns>
    public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string currencyPair)
    {
        try
        {
            var prompt = $"Analyze the current market sentiment for the {currencyPair} forex pair. Consider recent economic news, technical analysis, and market trends. Provide a summary of bullish and bearish factors, and conclude with whether the overall sentiment is bullish, bearish, or neutral. Include specific data points and cite reliable sources for your analysis. Format your response as JSON with the following structure: {{\"sentiment\": \"bullish|bearish|neutral\", \"confidence\": 0.0-1.0, \"factors\": [list of factors], \"summary\": \"brief summary\", \"sources\": [list of sources with URLs]}}";

            var requestBody = new
            {
                model = _model,
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

            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response: {Response}", responseString);
            var responseObject = JsonSerializer.Deserialize<OpenRouterResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new Exception("Invalid response from OpenRouter API");
            }

            // Extract the model used from the response
            string modelUsed = !string.IsNullOrEmpty(responseObject.model) ? responseObject.model : _model;
            _logger.LogInformation("OpenRouter selected model: {Model} for analysis of {CurrencyPair}", modelUsed, currencyPair);

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
                Sources = sentimentData.sources != null ? sentimentData.sources : new List<string>(),
                Timestamp = DateTime.UtcNow,
                ModelUsed = modelUsed
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
                Timestamp = DateTime.UtcNow,
                ModelUsed = "Error - model information unavailable"
            };
        }
    }
    
    /// <summary>
    /// Gets recommended forex trading opportunities based on current market conditions.
    /// </summary>
    /// <param name="count">The number of recommendations to return.</param>
    /// <returns>A list of trading recommendations for the most promising forex pairs.</returns>
    public async Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3)
    {
        try
        {
            // First, we'll use OpenRouter to get both current market prices and trading recommendations

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
      ""direction"": ""Buy"",
      ""sentiment"": ""bullish"",
      ""confidence"": 0.85,
      ""currentPrice"": 1.09123,
      ""takeProfitPrice"": 1.09500,
      ""stopLossPrice"": 1.08900,
      ""factors"": [
        ""ECB hawkish stance"",
        ""Price above 200 EMA"",
        ""Bullish engulfing pattern on 4H chart""
      ],
      ""rationale"": ""EURUSD shows strong bullish momentum with key support at 1.0890"",
      ""sources"": [
        ""https://www.example.com/forex/eurusd"",
        ""https://www.another-source.com/markets/eur-usd""
      ]
    }}
  ]
}}

CRITICAL: Your response MUST be valid JSON that can be parsed programmatically. Do not include any explanatory text outside the JSON structure. Ensure all decimal values use periods, not commas, as decimal separators.";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "You are a forex trading expert with access to real-time market data. Provide accurate, data-driven trading recommendations with precise price levels. Always verify your information with reliable sources and include citations. Never make up information or sources. Format your responses as valid JSON that can be parsed programmatically." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 2000
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
                
            var response = await _httpClient.PostAsync("chat/completions", content);
            response.EnsureSuccessStatusCode();
            
            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received response: {Response}", responseString);
            var responseObject = JsonSerializer.Deserialize<OpenRouterResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new Exception("Invalid response from OpenRouter API");
            }
            
            // Extract the model used from the response
            string modelUsed = !string.IsNullOrEmpty(responseObject.model) ? responseObject.model : _model;
            _logger.LogInformation("OpenRouter selected model: {Model} for trading recommendations", modelUsed);
            
            // Extract the JSON from the response text
            var responseContent = responseObject.choices[0].message.content;
            _logger.LogInformation("Message content from OpenRouter: {Content}", responseContent);
            
            // Pre-process the response content to help with malformed responses
            string preprocessedContent = responseContent;
            
            // Remove markdown code blocks if present
            if (preprocessedContent.Contains("```json"))
            {
                preprocessedContent = preprocessedContent.Replace("```json", "");
                preprocessedContent = preprocessedContent.Replace("```", "");
            }
            
            // Handle the case where model returns explanatory text despite instructions
            var jsonStartIndex = preprocessedContent.IndexOf('{');
            var jsonEndIndex = preprocessedContent.LastIndexOf('}');
            
            if (jsonStartIndex == -1 || jsonEndIndex == -1)
            {
                _logger.LogError("Could not find JSON markers in message content: {Content}", preprocessedContent);
                throw new Exception("Could not extract JSON from recommendations response");
            }
                
            var jsonContent = preprocessedContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            
            // Try to clean up common JSON issues
            jsonContent = jsonContent.Replace("\n", " ")
                                    .Replace("\r", "")
                                    .Replace("\t", "")
                                    .Replace("\\n", " ")
                                    .Replace("\\r", "")
                                    .Replace("\\t", "");
                                    
            // Attempt to fix trailing commas in arrays and objects
            jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, ",\\s*}", "}");
            jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, ",\\s*\\]", "]");
            _logger.LogInformation("Extracted JSON: {Json}", jsonContent);
            
            RecommendationsData? recommendationsData;
            try
            {
                recommendationsData = JsonSerializer.Deserialize<RecommendationsData>(jsonContent);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing recommendations JSON: {Json}", jsonContent);
                throw new Exception("Could not parse recommendations data", ex);
            }
            
            if (recommendationsData?.recommendations == null || !recommendationsData.recommendations.Any())
            {
                throw new Exception("No recommendations found in response");
            }
            
            // Convert to ForexRecommendation objects
            var forexRecommendations = new List<ForexRecommendation>();
            foreach (var rec in recommendationsData.recommendations)
            {
                var recommendation = new ForexRecommendation
                {
                    CurrencyPair = rec.pair,
                    Direction = rec.direction,
                    Sentiment = ParseSentiment(rec.sentiment),
                    Confidence = rec.confidence,
                    CurrentPrice = rec.currentPrice,
                    TakeProfitPrice = rec.takeProfitPrice,
                    StopLossPrice = rec.stopLossPrice,
                    BestEntryPrice = rec.currentPrice, // Using current price as best entry by default
                    Factors = rec.factors ?? new List<string>(),
                    Rationale = rec.rationale,
                    Sources = rec.sources ?? new List<string>(),
                    Timestamp = DateTime.UtcNow,
                    ModelUsed = modelUsed
                };
                
                // Determine order type based on direction
                recommendation.OrderType = rec.direction.Trim().ToLower() == "buy" 
                    ? OrderType.MarketBuy 
                    : OrderType.MarketSell;
                
                forexRecommendations.Add(recommendation);
            }
            
            return forexRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            
            // Return an empty list as fallback
            return new List<ForexRecommendation>();
        }
    }
    
    /// <summary>
    /// Parses a sentiment string into a SentimentType enum value.
    /// </summary>
    /// <param name="sentiment">The sentiment string to parse.</param>
    /// <returns>The corresponding SentimentType enum value.</returns>
    private static SentimentType ParseSentiment(string sentiment)
    {
        if (string.IsNullOrEmpty(sentiment))
            return SentimentType.Neutral;
            
        return sentiment.Trim().ToLower() switch
        {
            "bullish" => SentimentType.Bullish,
            "bearish" => SentimentType.Bearish,
            _ => SentimentType.Neutral
        };
    }
    
    #region API Response Classes
    
    /// <summary>
    /// Response structure from the OpenRouter API.
    /// </summary>
    private class OpenRouterResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
        public string model { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a choice in the OpenRouter API response.
    /// </summary>
    private class Choice
    {
        public Message message { get; set; } = new Message();
    }

    /// <summary>
    /// Represents a message in the OpenRouter API response.
    /// </summary>
    private class Message
    {
        public string content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Structure for parsing the sentiment data from the OpenRouter response.
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
    /// Structure for parsing the recommendations data from the OpenRouter response.
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
    
    #endregion
} 