using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;

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
    private readonly ForexMarketSessionService _marketSessionService;

    /// <summary>
    /// Constructor for TradingViewAnalyzer
    /// </summary>
    public TradingViewAnalyzer(
        IForexDataProviderFactory dataProviderFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TradingViewAnalyzer> logger,
        DataProviderType? providerType = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _marketSessionService = new ForexMarketSessionService();
        
        // Try to get the API key from configuration (checking both regular config and environment variables)
        var perplexityApiKey = configuration["Perplexity:ApiKey"];
        var envApiKey = configuration["TRADER_PERPLEXITY_API_KEY"];
        
        _logger.LogInformation("Perplexity API key from config: {HasKey}", !string.IsNullOrEmpty(perplexityApiKey));
        _logger.LogInformation("Perplexity API key from env: {HasKey}", !string.IsNullOrEmpty(envApiKey));
        
        if (!string.IsNullOrEmpty(perplexityApiKey))
            _perplexityApiKey = perplexityApiKey;
        else if (!string.IsNullOrEmpty(envApiKey))
            _perplexityApiKey = envApiKey;
        else
            throw new ArgumentNullException(nameof(configuration), "Perplexity API key is required");
            
        // Log the first few characters of the API key for debugging
        if (!string.IsNullOrEmpty(_perplexityApiKey) && _perplexityApiKey.Length > 4)
        {
            _logger.LogInformation("Using Perplexity API key starting with: {KeyPrefix}...", _perplexityApiKey.Substring(0, 4));
        }
        else
        {
            _logger.LogWarning("Perplexity API key is too short or empty");
        }
        
        // Set up HttpClient
        _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _perplexityApiKey);
        
        // If a specific provider type is provided, use it
        if (providerType.HasValue)
        {
            _dataProvider = dataProviderFactory.GetProvider(providerType.Value);
            _logger.LogInformation("TradingViewAnalyzer using specified {ProviderType} data provider", providerType.Value);
        }
        else
        {
            // Determine which data provider to use based on configuration
            DataProviderType defaultProviderType = DataProviderType.Mock;
            
            if (!string.IsNullOrEmpty(configuration["Polygon:ApiKey"]))
            {
                defaultProviderType = DataProviderType.Polygon;
                _logger.LogInformation("TradingViewAnalyzer using Polygon data provider");
            }
            else if (!string.IsNullOrEmpty(configuration["TraderMade:ApiKey"]))
            {
                defaultProviderType = DataProviderType.TraderMade;
                _logger.LogInformation("TradingViewAnalyzer using TraderMade data provider");
            }
            else
            {
                _logger.LogInformation("TradingViewAnalyzer using Mock data provider");
            }
            
            // Get the appropriate data provider from the factory
            _dataProvider = dataProviderFactory.GetProvider(defaultProviderType);
        }
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
            
            // Check if we're using TraderMade provider to adjust candle counts
            bool isTraderMade = _dataProvider is TraderMadeDataProvider;
            
            // Fetch candle data for multiple timeframes - focusing on higher timeframes
            // We'll skip 5-minute data entirely for more stable recommendations
            var candleTasks = new[]
            {
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours1, 24),  // More 1H candles
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours4, 12),  // More 4H candles
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Day1, 10)     // More daily candles
            };
            
            // Wait for all data to be retrieved
            await Task.WhenAll(candleTasks);
            
            // Extract candle data for each timeframe
            var candles1h = await candleTasks[0];
            var candles4h = await candleTasks[1];
            var candles1d = await candleTasks[2];
            
            // Create an empty list for 5m data (we're not using it anymore)
            var candles5m = new List<CandleData>();
            
            // Save chart data to a text file for reference
            await SaveChartDataToFile(symbol, candles5m, candles1h, candles4h, candles1d);
            
            // Generate the prompt for the sentiment analysis with emphasis on higher timeframes
            var prompt = GenerateChartAnalysisPrompt(symbol, candles5m, candles1h, candles4h, candles1d);
            
            // Send the prompt to Perplexity AI
            var requestBody = new
            {
                model = "sonar-pro",
                messages = new[]
                {
                    new { role = "system", content = "You are an expert trading analyst specializing in technical analysis and market sentiment. Focus on higher timeframes (daily, 4-hour, and 1-hour) for more reliable signals and to filter out market noise. Provide concise, accurate trading advice based on chart data. Be precise with price levels and never make up information. If you're uncertain about specific data points, acknowledge the limitations of your information. Prioritize longer-term trends over short-term fluctuations." },
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
                    // Determine trade recommendation based on direction and sentiment
                    string tradeRecommendation = "None";
                    if (!string.IsNullOrEmpty(sentimentData.direction))
                    {
                        tradeRecommendation = sentimentData.direction.Trim().ToLower() switch
                        {
                            "buy" => "Buy",
                            "sell" => "Sell",
                            _ => "None"
                        };
                    }
                    
                    // Get market session information
                    var sessionInfo = _marketSessionService.GetCurrentSessionInfo(symbol);
                    
                    // Create the sentiment analysis result
                    var result = new SentimentAnalysisResult
                    {
                        CurrencyPair = symbol,
                        Sentiment = ParseSentiment(sentimentData.sentiment),
                        Confidence = sentimentData.confidence,
                        Factors = sentimentData.factors ?? new List<string>(),
                        Summary = sentimentData.summary,
                        Sources = sentimentData.sources ?? new List<string>(),
                        Timestamp = DateTime.UtcNow,
                        CurrentPrice = sentimentData.currentPrice,
                        TradeRecommendation = tradeRecommendation,
                        StopLossPrice = sentimentData.stopLossPrice,
                        TakeProfitPrice = sentimentData.takeProfitPrice,
                        MarketSession = new MarketSessionInfo
                        {
                            CurrentSession = sessionInfo.CurrentSession.ToString(),
                            Description = sessionInfo.Description,
                            LiquidityLevel = sessionInfo.LiquidityLevel,
                            RecommendedSession = sessionInfo.RecommendedSession.ToString(),
                            RecommendationReason = sessionInfo.RecommendationReason,
                            TimeUntilNextSession = FormatTimeSpan(sessionInfo.TimeUntilNextSession),
                            NextSession = sessionInfo.NextSession.ToString()
                        }
                    };
                    
                    _logger.LogInformation(
                        "Analysis for {Symbol}: {Sentiment} ({Confidence:P0}), Trade: {Trade}, Current Session: {Session} (Liquidity: {Liquidity}/5)",
                        symbol,
                        result.Sentiment,
                        result.Confidence,
                        result.TradeRecommendation,
                        result.MarketSession.CurrentSession,
                        result.MarketSession.LiquidityLevel);
                    
                    return result;
                }
            }
            
            throw new InvalidOperationException("Failed to parse sentiment data from response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {Symbol}", symbol);
            throw;
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
            _logger.LogInformation("Getting {Count} trading recommendations", count);
            
            // Check if Perplexity API key is available
            if (string.IsNullOrEmpty(_perplexityApiKey))
            {
                _logger.LogError("Perplexity API key is missing. Cannot generate recommendations.");
                throw new InvalidOperationException("Perplexity API key is required for generating recommendations");
            }
            
            // Define a list of common forex pairs to analyze
            var commonPairs = new List<string>
            {
                "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "NZDUSD", "USDCHF",
                "EURJPY", "GBPJPY", "EURGBP", "AUDJPY", "EURAUD", "EURCHF", "GBPCAD"
            };
            
            // Add some crypto pairs if we're using a real data provider
            if (!(_dataProvider is ForexDataProvider))
            {
                commonPairs.AddRange(new[] { "BTCUSD", "ETHUSD" });
            }
            
            // Shuffle the list to get different recommendations each time
            var random = new Random();
            var shuffledPairs = commonPairs.OrderBy(_ => random.Next()).ToList();
            
            // Take more pairs to analyze to increase chances of finding good opportunities
            var pairsToAnalyze = shuffledPairs.Take(Math.Min(count + 7, shuffledPairs.Count)).ToList();
            
            _logger.LogInformation("Will analyze the following pairs: {Pairs}", string.Join(", ", pairsToAnalyze));
            
            // Analyze each pair
            var recommendations = new List<ForexRecommendation>();
            
            foreach (var pair in pairsToAnalyze)
            {
                try
                {
                    _logger.LogInformation("Analyzing {Pair} for trading recommendation", pair);
                    
                    // Fetch candle data for multiple timeframes - focusing on higher timeframes
                    var candles1h = await _dataProvider.GetCandleDataAsync(pair, ChartTimeframe.Hours1, 24);
                    var candles4h = await _dataProvider.GetCandleDataAsync(pair, ChartTimeframe.Hours4, 12);
                    var candles1d = await _dataProvider.GetCandleDataAsync(pair, ChartTimeframe.Day1, 10);
                    
                    _logger.LogInformation("Retrieved {H1Count} 1h candles, {H4Count} 4h candles, {D1Count} daily candles for {Pair}", 
                        candles1h.Count, candles4h.Count, candles1d.Count, pair);
                    
                    // Generate the prompt for the recommendation with empty 5m data
                    var prompt = GenerateRecommendationPrompt(pair, new List<CandleData>(), candles1h, candles4h, candles1d);
                    
                    // Send the prompt to Perplexity AI
                    var requestBody = new
                    {
                        model = "sonar-pro",
                        messages = new[]
                        {
                            new { role = "system", content = "You are an expert trading analyst specializing in technical analysis and market sentiment. Focus on higher timeframes (daily, 4-hour, and 1-hour) for more reliable signals and to filter out market noise. Only recommend trades with clear setups and good risk-reward ratios. If you don't see a strong trading opportunity, clearly state that no trade is recommended at this time. Be precise with price levels and never make up information. Prioritize longer-term trends over short-term fluctuations." },
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
                    
                    _logger.LogInformation("Sending request to Perplexity API for {Pair}", pair);
                    
                    var response = await _httpClient.SendAsync(request);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Perplexity API returned error for {Pair}: {StatusCode} - {Error}", 
                            pair, response.StatusCode, errorContent);
                        continue;
                    }
                    
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Received response from Perplexity API for {Pair} ({Length} chars)", 
                        pair, responseString.Length);
                    
                    var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
                    
                    if (responseObject?.choices == null || responseObject.choices.Length == 0)
                    {
                        _logger.LogWarning("No choices in Perplexity API response for {Pair}", pair);
                        continue;
                    }
                    
                    // Extract the JSON from the response text
                    var responseContent = responseObject.choices[0].message.content;
                    var jsonStartIndex = responseContent.IndexOf('{');
                    var jsonEndIndex = responseContent.LastIndexOf('}');
                    
                    if (jsonStartIndex < 0 || jsonEndIndex <= jsonStartIndex)
                    {
                        _logger.LogWarning("Could not extract JSON from Perplexity API response for {Pair}. Content: {Content}", 
                            pair, responseContent.Length > 100 ? responseContent.Substring(0, 100) + "..." : responseContent);
                        continue;
                    }
                    
                    var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                    
                    try
                    {
                        var recommendationData = JsonSerializer.Deserialize<RecommendationData>(jsonContent);
                        
                        if (recommendationData == null)
                        {
                            _logger.LogWarning("Failed to deserialize recommendation data for {Pair}", pair);
                            continue;
                        }
                        
                        // Skip if no clear direction or if "None" is specified
                        if (string.IsNullOrEmpty(recommendationData.direction) || 
                            recommendationData.direction.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                            recommendationData.direction.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("No clear direction in recommendation for {Pair}", pair);
                            continue;
                        }
                        
                        // Get market session information
                        var sessionInfo = _marketSessionService.GetCurrentSessionInfo(pair);
                        
                        // Create the recommendation
                        var recommendation = new ForexRecommendation
                        {
                            CurrencyPair = pair,
                            Direction = recommendationData.direction,
                            Sentiment = ParseSentiment(recommendationData.sentiment),
                            Confidence = recommendationData.confidence,
                            CurrentPrice = recommendationData.currentPrice,
                            TakeProfitPrice = recommendationData.takeProfitPrice,
                            StopLossPrice = recommendationData.stopLossPrice,
                            Factors = recommendationData.factors ?? new List<string>(),
                            Rationale = recommendationData.rationale,
                            Timestamp = DateTime.UtcNow,
                            MarketSession = new MarketSessionInfo
                            {
                                CurrentSession = sessionInfo.CurrentSession.ToString(),
                                Description = sessionInfo.Description,
                                LiquidityLevel = sessionInfo.LiquidityLevel,
                                RecommendedSession = sessionInfo.RecommendedSession.ToString(),
                                RecommendationReason = sessionInfo.RecommendationReason,
                                TimeUntilNextSession = FormatTimeSpan(sessionInfo.TimeUntilNextSession),
                                NextSession = sessionInfo.NextSession.ToString()
                            }
                        };
                        
                        // Only add if the risk-reward ratio is reasonable (at least 1.5)
                        if (recommendation.RiskRewardRatio >= 1.5m)
                        {
                            recommendations.Add(recommendation);
                            
                            _logger.LogInformation(
                                "Added recommendation for {Symbol}: {Direction} at {Price}, R:R {RiskReward}, Session: {Session} (Liquidity: {Liquidity}/5)",
                                recommendation.CurrencyPair,
                                recommendation.Direction,
                                recommendation.CurrentPrice,
                                recommendation.RiskRewardRatio,
                                recommendation.MarketSession.CurrentSession,
                                recommendation.MarketSession.LiquidityLevel);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Skipping recommendation with low R:R for {Symbol}: {Direction} at {Price}, R:R {RiskReward}",
                                recommendation.CurrencyPair,
                                recommendation.Direction,
                                recommendation.CurrentPrice,
                                recommendation.RiskRewardRatio);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing recommendation data for {Pair}. JSON: {Json}", 
                            pair, jsonContent.Length > 100 ? jsonContent.Substring(0, 100) + "..." : jsonContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing {Pair} for recommendations", pair);
                    // Continue with other pairs
                }
                
                // If we have enough recommendations, stop
                if (recommendations.Count >= count)
                {
                    break;
                }
            }
            
            // If no recommendations were found, return an empty list
            if (recommendations.Count == 0)
            {
                _logger.LogInformation("No trading opportunities with good risk-reward ratios found at this time.");
                return new List<ForexRecommendation>();
            }
            
            // Sort by confidence and take the requested number
            return recommendations
                .OrderByDescending(r => r.Confidence)
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            throw;
        }
    }
    
    /// <summary>
    /// Generates a prompt for chart analysis.
    /// </summary>
    private string GenerateChartAnalysisPrompt(
        string symbol, 
        List<CandleData> candles5m, 
        List<CandleData> candles1h, 
        List<CandleData> candles4h,
        List<CandleData> candles1d)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Analyze the following chart data for {symbol} and provide a detailed technical analysis with trading recommendation.");
        sb.AppendLine();
        
        sb.AppendLine("Focus on the higher timeframes (daily, 4-hour, and 1-hour) for more reliable signals.");
        sb.AppendLine("Ignore short-term noise and prioritize longer-term trends.");
        sb.AppendLine();
        
        // Add daily candles (highest priority)
        sb.AppendLine("## Daily Timeframe (Most Important)");
        if (candles1d.Count > 0)
        {
            sb.AppendLine($"Last {candles1d.Count} daily candles:");
            AppendCandleData(sb, candles1d);
        }
        else
        {
            sb.AppendLine("No daily data available.");
        }
        sb.AppendLine();
        
        // Add 4-hour candles (second priority)
        sb.AppendLine("## 4-Hour Timeframe (Important)");
        if (candles4h.Count > 0)
        {
            sb.AppendLine($"Last {candles4h.Count} 4-hour candles:");
            AppendCandleData(sb, candles4h);
        }
        else
        {
            sb.AppendLine("No 4-hour data available.");
        }
        sb.AppendLine();
        
        // Add 1-hour candles (third priority)
        sb.AppendLine("## 1-Hour Timeframe (Supplementary)");
        if (candles1h.Count > 0)
        {
            sb.AppendLine($"Last {candles1h.Count} 1-hour candles:");
            AppendCandleData(sb, candles1h);
        }
        else
        {
            sb.AppendLine("No 1-hour data available.");
        }
        sb.AppendLine();
        
        // We're skipping 5-minute data entirely
        
        sb.AppendLine("Based on this data, provide a comprehensive analysis including:");
        sb.AppendLine("1. Current market structure and trend direction on each timeframe");
        sb.AppendLine("2. Key support and resistance levels");
        sb.AppendLine("3. Important technical indicators and patterns");
        sb.AppendLine("4. Trading recommendation with entry, stop loss, and take profit levels");
        sb.AppendLine();
        
        sb.AppendLine("Provide your analysis in the following JSON format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"sentiment\": \"Bullish\", \"Bearish\", or \"Neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": current price as decimal,");
        sb.AppendLine("  \"direction\": \"Buy\", \"Sell\", or \"None\",");
        sb.AppendLine("  \"stopLossPrice\": stop loss price as decimal,");
        sb.AppendLine("  \"takeProfitPrice\": target price as decimal,");
        sb.AppendLine("  \"factors\": [\"factor1\", \"factor2\", ...],");
        sb.AppendLine("  \"summary\": \"Brief summary of the analysis\",");
        sb.AppendLine("  \"sources\": []");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Only recommend a trade if there is a clear setup with a good risk-reward ratio (at least 1.5:1). If there's no clear trade opportunity, set direction to \"None\".");
        
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
        
        sb.AppendLine($"Analyze the following chart data for {symbol} and provide a trading recommendation.");
        sb.AppendLine();
        
        sb.AppendLine("Focus on the higher timeframes (daily, 4-hour, and 1-hour) for more reliable signals.");
        sb.AppendLine("Ignore short-term noise and prioritize longer-term trends.");
        sb.AppendLine();
        
        // Add daily candles (highest priority)
        sb.AppendLine("## Daily Timeframe (Most Important)");
        if (candles1d.Count > 0)
        {
            sb.AppendLine($"Last {candles1d.Count} daily candles:");
            AppendCandleData(sb, candles1d);
        }
        else
        {
            sb.AppendLine("No daily data available.");
        }
        sb.AppendLine();
        
        // Add 4-hour candles (second priority)
        sb.AppendLine("## 4-Hour Timeframe (Important)");
        if (candles4h.Count > 0)
        {
            sb.AppendLine($"Last {candles4h.Count} 4-hour candles:");
            AppendCandleData(sb, candles4h);
        }
        else
        {
            sb.AppendLine("No 4-hour data available.");
        }
        sb.AppendLine();
        
        // Add 1-hour candles (third priority)
        sb.AppendLine("## 1-Hour Timeframe (Supplementary)");
        if (candles1h.Count > 0)
        {
            sb.AppendLine($"Last {candles1h.Count} 1-hour candles:");
            AppendCandleData(sb, candles1h);
        }
        else
        {
            sb.AppendLine("No 1-hour data available.");
        }
        sb.AppendLine();
        
        // We're skipping 5-minute data entirely
        
        sb.AppendLine("Based on this data, provide a trading recommendation in the following JSON format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"direction\": \"Buy\", \"Sell\", or \"None\",");
        sb.AppendLine("  \"sentiment\": \"Bullish\", \"Bearish\", or \"Neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": current price as decimal,");
        sb.AppendLine("  \"takeProfitPrice\": target price as decimal,");
        sb.AppendLine("  \"stopLossPrice\": stop loss price as decimal,");
        sb.AppendLine("  \"factors\": [\"factor1\", \"factor2\", ...],");
        sb.AppendLine("  \"rationale\": \"Brief explanation of the recommendation\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Only recommend a trade if there is a clear setup with a good risk-reward ratio (at least 1.5:1). If there's no clear trade opportunity, set direction to \"None\".");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Appends candle data to a string builder in a readable format.
    /// </summary>
    private void AppendCandleData(StringBuilder sb, IEnumerable<CandleData> candles)
    {
        // Sort candles by timestamp (oldest to newest) for better trend analysis
        var orderedCandles = candles.OrderBy(c => c.Timestamp).ToList();
        
        // Create a header row
        sb.AppendLine("| Date | Open | High | Low | Close |");
        sb.AppendLine("|------|------|------|-----|-------|");
        
        // Add each candle as a row
        foreach (var candle in orderedCandles)
        {
            sb.AppendLine($"| {candle.Timestamp:yyyy-MM-dd HH:mm} | {candle.Open:F5} | {candle.High:F5} | {candle.Low:F5} | {candle.Close:F5} |");
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
    
    /// <summary>
    /// Saves the chart data to a text file for reference
    /// </summary>
    private async Task SaveChartDataToFile(
        string symbol, 
        List<CandleData> candles5m, 
        List<CandleData> candles1h, 
        List<CandleData> candles4h,
        List<CandleData> candles1d)
    {
        try
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"CHART DATA FOR {symbol} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("=======================================================");
            sb.AppendLine();
            
            // Add 5-minute timeframe data
            sb.AppendLine("5-MINUTE TIMEFRAME DATA (newest to oldest):");
            sb.AppendLine("Timestamp (UTC) | Open | High | Low | Close | Volume");
            sb.AppendLine("--------------------------------------------------------");
            foreach (var candle in candles5m.OrderByDescending(c => c.Timestamp))
            {
                sb.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm:ss} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume}");
            }
            sb.AppendLine();
            
            // Add 1-hour timeframe data
            sb.AppendLine("1-HOUR TIMEFRAME DATA (newest to oldest):");
            sb.AppendLine("Timestamp (UTC) | Open | High | Low | Close | Volume");
            sb.AppendLine("--------------------------------------------------------");
            foreach (var candle in candles1h.OrderByDescending(c => c.Timestamp))
            {
                sb.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm:ss} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume}");
            }
            sb.AppendLine();
            
            // Add 4-hour timeframe data
            sb.AppendLine("4-HOUR TIMEFRAME DATA (newest to oldest):");
            sb.AppendLine("Timestamp (UTC) | Open | High | Low | Close | Volume");
            sb.AppendLine("--------------------------------------------------------");
            foreach (var candle in candles4h.OrderByDescending(c => c.Timestamp))
            {
                sb.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm:ss} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume}");
            }
            sb.AppendLine();
            
            // Add daily timeframe data
            sb.AppendLine("DAILY TIMEFRAME DATA (newest to oldest):");
            sb.AppendLine("Timestamp (UTC) | Open | High | Low | Close | Volume");
            sb.AppendLine("--------------------------------------------------------");
            foreach (var candle in candles1d.OrderByDescending(c => c.Timestamp))
            {
                sb.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm:ss} | {candle.Open} | {candle.High} | {candle.Low} | {candle.Close} | {candle.Volume}");
            }
            
            // Create directory if it doesn't exist
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChartData");
            Directory.CreateDirectory(directory);
            
            // Save to file with timestamp
            var filename = Path.Combine(directory, $"{symbol}_ChartData_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(filename, sb.ToString());
            
            _logger.LogInformation("Chart data saved to {Filename}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chart data to file for {Symbol}", symbol);
        }
    }
    
    /// <summary>
    /// Formats a TimeSpan into a human-readable string (e.g., "2h 15m").
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 1)
        {
            return "less than a minute";
        }
        
        var hours = (int)timeSpan.TotalHours;
        var minutes = timeSpan.Minutes;
        
        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }
        
        return $"{minutes}m";
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