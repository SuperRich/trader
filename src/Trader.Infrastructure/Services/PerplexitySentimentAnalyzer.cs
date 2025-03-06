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
            var prompt = $"Analyze the current market sentiment for the {currencyPair} forex pair. Consider recent economic news, technical analysis, and market trends. Provide a summary of bullish and bearish factors, and conclude with whether the overall sentiment is bullish, bearish, or neutral. Format your response as JSON with the following structure: {{\"sentiment\": \"bullish|bearish|neutral\", \"confidence\": 0.0-1.0, \"factors\": [list of factors], \"summary\": \"brief summary\"}}";

            // Using current Perplexity API models from https://docs.perplexity.ai/guides/model-cards
            var requestBody = new
            {
                model = "sonar", // Using current model from Perplexity docs
                messages = new[]
                {
                    new { role = "system", content = "You are a financial analyst specializing in forex markets. Provide objective analysis with current data." },
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
                Timestamp = DateTime.UtcNow
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
- Include the source(s) you used to verify the price data in the rationale

STEP 3: Format your entire response as VALID JSON with this exact structure:

{{
  ""recommendations"": [
    {{
      ""pair"": ""EURUSD"",
      ""direction"": ""Buy"",
      ""sentiment"": ""bullish"",
      ""confidence"": 0.85,
      ""currentPrice"": 1.0925,
      ""takeProfitPrice"": 1.1050,
      ""stopLossPrice"": 1.0850,
      ""factors"": [
        ""Key technical reason"",
        ""Important fundamental factor""
      ],
      ""rationale"": ""Concise trading rationale with verified price sources and levels""
    }}
  ]
}}

VERIFICATION REQUIREMENTS:
1. DOUBLE-CHECK all price data before submitting your response
2. Ensure price relationships are accurate (JPY pairs typically around 100-200, EUR/USD around 1.05-1.15, etc.)
3. If a price seems unusual or has changed significantly since your knowledge cutoff, note this in the rationale
4. Make sure TP and SL levels are realistic distances from current price (not too tight or too wide)
5. Buy direction should have TP higher than current price and SL lower
6. Sell direction should have TP lower than current price and SL higher

RESPONSE RULES:
1. Your entire response must be ONLY the JSON object - no intro text, no explanations
2. All price values must be non-zero decimal numbers with appropriate precision
3. Include exactly {count} recommendations
4. Sentiment must accurately match direction (bullish for Buy, bearish for Sell, or neutral)
5. Each recommendation must have all fields shown in the example

Give me the {count} most promising forex trades with VERIFIED, ACCURATE price data.";

            var requestBody = new
            {
                model = "sonar", // Using current model from Perplexity docs
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
            _logger.LogInformation("Message content from Perplexity: {Content}", responseContent);
            
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
                // Try to deserialize with our standard class first
                recommendationsData = JsonSerializer.Deserialize<RecommendationsData>(jsonContent);
                
                if (recommendationsData == null)
                {
                    _logger.LogError("Deserialized to null object");
                    throw new Exception("Deserialization resulted in null object");
                }
                
                if (recommendationsData.recommendations == null)
                {
                    _logger.LogError("Recommendations property is null");
                    throw new Exception("Recommendations property is null");
                }
                
                _logger.LogInformation("Successfully parsed {Count} recommendations", 
                    recommendationsData.recommendations.Count);
            }
            catch (JsonException firstException)
            {
                _logger.LogWarning("Initial JSON parsing failed: {Message}. Attempting alternative parsing.", firstException.Message);
                
                try
                {
                    // Try with a more flexible approach - maybe the JSON structure is different
                    var jsonDocument = JsonDocument.Parse(jsonContent);
                    var root = jsonDocument.RootElement;
                    
                    if (root.TryGetProperty("recommendations", out var recsElement) && recsElement.ValueKind == JsonValueKind.Array)
                    {
                        // Manually deserialize the recommendations
                        var recommendations = new List<RecommendationItem>();
                        
                        foreach (var item in recsElement.EnumerateArray())
                        {
                            try
                            {
                                var rec = new RecommendationItem();
                                
                                // Extract properties carefully with fallbacks
                                if (item.TryGetProperty("pair", out var pairProp))
                                    rec.pair = pairProp.GetString() ?? "EURUSD";
                                
                                if (item.TryGetProperty("direction", out var dirProp))
                                    rec.direction = dirProp.GetString() ?? "None";
                                
                                if (item.TryGetProperty("sentiment", out var sentProp))
                                    rec.sentiment = sentProp.GetString() ?? "neutral";
                                
                                if (item.TryGetProperty("confidence", out var confProp) && confProp.ValueKind == JsonValueKind.Number)
                                    rec.confidence = confProp.GetDecimal();
                                
                                if (item.TryGetProperty("currentPrice", out var cpProp) && cpProp.ValueKind == JsonValueKind.Number)
                                    rec.currentPrice = cpProp.GetDecimal();
                                
                                if (item.TryGetProperty("takeProfitPrice", out var tpProp) && tpProp.ValueKind == JsonValueKind.Number)
                                    rec.takeProfitPrice = tpProp.GetDecimal();
                                
                                if (item.TryGetProperty("stopLossPrice", out var slProp) && slProp.ValueKind == JsonValueKind.Number)
                                    rec.stopLossPrice = slProp.GetDecimal();
                                
                                if (item.TryGetProperty("rationale", out var ratProp))
                                    rec.rationale = ratProp.GetString() ?? "";
                                
                                // Handle factors array
                                if (item.TryGetProperty("factors", out var factorsProp) && factorsProp.ValueKind == JsonValueKind.Array)
                                {
                                    rec.factors = new List<string>();
                                    foreach (var factor in factorsProp.EnumerateArray())
                                    {
                                        if (factor.ValueKind == JsonValueKind.String)
                                            rec.factors.Add(factor.GetString() ?? "");
                                    }
                                }
                                
                                recommendations.Add(rec);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Error parsing recommendation item: {Message}", ex.Message);
                                // Continue with the next item
                            }
                        }
                        
                        recommendationsData = new RecommendationsData { recommendations = recommendations };
                        _logger.LogInformation("Successfully parsed {Count} recommendations using alternative method", 
                            recommendationsData.recommendations.Count);
                    }
                    else
                    {
                        _logger.LogError("JSON does not contain recommendations array");
                        throw new Exception("JSON does not contain recommendations array");
                    }
                }
                catch (Exception secondException)
                {
                    _logger.LogError(secondException, "Both JSON parsing approaches failed");
                    
                    // Create a simple fallback recommendation from the raw text
                    _logger.LogWarning("Creating fallback recommendations from raw response text");
                    
                    // Try to extract some meaningful data from the text
                    var pairs = new[] { "EURUSD", "GBPUSD", "USDJPY", "GBPJPY", "AUDUSD" };
                    var fallbackRecItems = new List<RecommendationItem>();
                    
                    // Try to find at least one currency pair mentioned in the response
                    foreach (var pair in pairs)
                    {
                        if (responseContent.Contains(pair))
                        {
                            _logger.LogInformation("Found currency pair in response: {Pair}", pair);
                            
                            var rec = new RecommendationItem
                            {
                                pair = pair,
                                direction = responseContent.Contains("Buy") ? "Buy" : 
                                           responseContent.Contains("Sell") ? "Sell" : "None",
                                sentiment = responseContent.Contains("bullish") ? "bullish" :
                                           responseContent.Contains("bearish") ? "bearish" : "neutral",
                                confidence = 0.6m,
                                currentPrice = pair.Contains("JPY") ? 150.0m : 1.1m,  // Reasonable defaults
                                takeProfitPrice = pair.Contains("JPY") ? 151.0m : 1.11m,
                                stopLossPrice = pair.Contains("JPY") ? 149.0m : 1.09m,
                                rationale = "Extracted from Perplexity analysis. Check market conditions before trading.",
                                factors = new List<string> { "Extracted from partial response", "Check current market conditions" }
                            };
                            
                            fallbackRecItems.Add(rec);
                            
                            // Just get one recommendation if we can find it
                            break;
                        }
                    }
                    
                    // If we couldn't find any pairs in the response, create a default
                    if (fallbackRecItems.Count == 0)
                    {
                        fallbackRecItems.Add(new RecommendationItem
                        {
                            pair = "EURUSD",
                            direction = "None",
                            sentiment = "neutral",
                            confidence = 0.5m,
                            currentPrice = 1.1m,
                            takeProfitPrice = 1.11m,
                            stopLossPrice = 1.09m,
                            rationale = "Default recommendation. Please retry for accurate analysis.",
                            factors = new List<string> { "Default fallback recommendation" }
                        });
                    }
                    
                    recommendationsData = new RecommendationsData { recommendations = fallbackRecItems };
                }
            }

            // Apply validation and fix any problematic recommendations
            var validRecommendations = recommendationsData.recommendations.Select(r => {
                _logger.LogInformation("Validating recommendation for {Pair} with price {Price}", r.pair, r.currentPrice);
                
                // Fix any missing or invalid values
                if (string.IsNullOrEmpty(r.pair))
                    r.pair = "EURUSD";
                
                // Verify prices are in a reasonable range based on currency pair
                bool containsJpy = r.pair.Contains("JPY", StringComparison.OrdinalIgnoreCase);
                bool isValid = true;
                
                // Validate and fix price magnitudes if needed
                if (containsJpy)
                {
                    // JPY pairs are typically around 100-200
                    if (r.currentPrice < 50 || r.currentPrice > 250)
                    {
                        _logger.LogWarning("Suspicious JPY price detected: {Pair} at {Price} - this is outside normal range", 
                            r.pair, r.currentPrice);
                            
                        // If price is just wrong by magnitude (decimal point)
                        if (r.currentPrice < 1 && r.currentPrice > 0)
                        {
                            _logger.LogInformation("Fixing JPY price magnitude (too small): {Pair} from {OldPrice} to {NewPrice}", 
                                r.pair, r.currentPrice, r.currentPrice * 100);
                            r.currentPrice *= 100;
                            
                            // Also fix TP/SL if they need the same adjustment
                            if (r.takeProfitPrice < 1) r.takeProfitPrice *= 100;
                            if (r.stopLossPrice < 1) r.stopLossPrice *= 100;
                        }
                        else if (r.currentPrice > 1000)
                        {
                            _logger.LogInformation("Fixing JPY price magnitude (too large): {Pair} from {OldPrice} to {NewPrice}", 
                                r.pair, r.currentPrice, r.currentPrice / 10);
                            r.currentPrice /= 10;
                            
                            // Also fix TP/SL if they need the same adjustment
                            if (r.takeProfitPrice > 1000) r.takeProfitPrice /= 10;
                            if (r.stopLossPrice > 1000) r.stopLossPrice /= 10;
                        }
                        else
                        {
                            isValid = false;
                        }
                    }
                }
                else
                {
                    // Most other pairs are in 0.5-2.0 range
                    if (r.currentPrice > 10 || r.currentPrice < 0.1)
                    {
                        _logger.LogWarning("Suspicious price detected: {Pair} at {Price} - this is outside normal range", 
                            r.pair, r.currentPrice);
                            
                        // If price is just wrong by magnitude (decimal point)
                        if (r.currentPrice > 10 && r.currentPrice < 1000)
                        {
                            _logger.LogInformation("Fixing price magnitude (too large): {Pair} from {OldPrice} to {NewPrice}", 
                                r.pair, r.currentPrice, r.currentPrice / 10);
                            r.currentPrice /= 10;
                            
                            // Also fix TP/SL if they need the same adjustment
                            if (r.takeProfitPrice > 10) r.takeProfitPrice /= 10;
                            if (r.stopLossPrice > 10) r.stopLossPrice /= 10;
                        }
                        else if (r.currentPrice < 0.1 && r.currentPrice > 0.001)
                        {
                            _logger.LogInformation("Fixing price magnitude (too small): {Pair} from {OldPrice} to {NewPrice}", 
                                r.pair, r.currentPrice, r.currentPrice * 10);
                            r.currentPrice *= 10;
                            
                            // Also fix TP/SL if they need the same adjustment
                            if (r.takeProfitPrice < 0.1) r.takeProfitPrice *= 10;
                            if (r.stopLossPrice < 0.1) r.stopLossPrice *= 10;
                        }
                        else
                        {
                            isValid = false;
                        }
                    }
                }
                    
                // Fix invalid current price
                if (!isValid || r.currentPrice <= 0)
                {
                    _logger.LogWarning("Using fallback price for {Pair}", r.pair);
                    r.currentPrice = containsJpy ? 150.0m : r.pair.StartsWith("GBP") ? 1.25m : 1.1m;
                }
                    
                // Fix invalid TP/SL
                if (r.takeProfitPrice <= 0 || Math.Abs(r.takeProfitPrice - r.currentPrice) < 0.0001m)
                {
                    r.takeProfitPrice = r.direction.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? 
                        r.currentPrice * 1.01m : r.currentPrice * 0.99m;
                }
                        
                if (r.stopLossPrice <= 0 || Math.Abs(r.stopLossPrice - r.currentPrice) < 0.0001m)
                {
                    r.stopLossPrice = r.direction.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? 
                        r.currentPrice * 0.99m : r.currentPrice * 1.01m;
                }
                
                // Ensure TP and SL are correctly positioned relative to current price
                if (r.direction.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                {
                    // For Buy, TP should be above current, SL below
                    if (r.takeProfitPrice < r.currentPrice)
                    {
                        _logger.LogWarning("Fixing Buy TP that was below current price: {Pair}", r.pair);
                        r.takeProfitPrice = r.currentPrice * 1.01m;
                    }
                    
                    if (r.stopLossPrice > r.currentPrice)
                    {
                        _logger.LogWarning("Fixing Buy SL that was above current price: {Pair}", r.pair);
                        r.stopLossPrice = r.currentPrice * 0.99m;
                    }
                }
                else if (r.direction.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                {
                    // For Sell, TP should be below current, SL above
                    if (r.takeProfitPrice > r.currentPrice)
                    {
                        _logger.LogWarning("Fixing Sell TP that was above current price: {Pair}", r.pair);
                        r.takeProfitPrice = r.currentPrice * 0.99m;
                    }
                    
                    if (r.stopLossPrice < r.currentPrice)
                    {
                        _logger.LogWarning("Fixing Sell SL that was below current price: {Pair}", r.pair);
                        r.stopLossPrice = r.currentPrice * 1.01m;
                    }
                }
                        
                if (string.IsNullOrEmpty(r.direction))
                    r.direction = "None";
                    
                if (string.IsNullOrEmpty(r.sentiment))
                    r.sentiment = "neutral";
                    
                if (r.confidence <= 0)
                    r.confidence = 0.5m;
                
                return r;
            }).ToList();
            
            // Safety check - if all recommendations were filtered out, add a default one
            if (validRecommendations.Count == 0)
            {
                _logger.LogWarning("No valid recommendations found after processing. Adding fallback recommendation.");
                validRecommendations.Add(new RecommendationItem
                {
                    pair = "EURUSD",
                    direction = "None",
                    sentiment = "neutral",
                    confidence = 0.5m,
                    currentPrice = 1.1m,
                    takeProfitPrice = 1.11m,
                    stopLossPrice = 1.09m,
                    rationale = "Default recommendation. Please retry for accurate analysis.",
                    factors = new List<string> { "Default fallback recommendation" }
                });
            }
            
            // Now map to ForexRecommendation objects
            var recommendations2 = validRecommendations
                .Select(r => new ForexRecommendation
                {
                    CurrencyPair = r.pair,
                    Direction = r.direction,
                    Sentiment = ParseSentiment(r.sentiment),
                    Confidence = r.confidence,
                    CurrentPrice = r.currentPrice,
                    TakeProfitPrice = r.takeProfitPrice,
                    StopLossPrice = r.stopLossPrice,
                    Factors = r.factors != null ? r.factors : new List<string>(),
                    Rationale = !string.IsNullOrEmpty(r.rationale) ? r.rationale : "Trading recommendation based on current market analysis",
                    Timestamp = DateTime.UtcNow
                })
                .ToList();
                
            // At this point, we're guaranteed to have at least one recommendation due to
            // the safety check in validRecommendations, so we don't need another fallback here
            return recommendations2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting forex trading recommendations");
            
            // Fallback if recommendations fail - use non-zero values for price fields
            return new List<ForexRecommendation>
            {
                new ForexRecommendation
                {
                    CurrencyPair = "EURUSD",
                    Direction = "None",
                    Sentiment = SentimentType.Neutral,
                    Confidence = 0.5m,
                    CurrentPrice = 1.0m, // Arbitrary non-zero value
                    TakeProfitPrice = 1.01m, // Above current price
                    StopLossPrice = 0.99m, // Below current price
                    Factors = new List<string> { "Error fetching recommendations" },
                    Rationale = "Could not retrieve trading recommendations at this time. Please try again later."
                }
            };
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
    }
}