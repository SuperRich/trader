using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly string _apiKey;
    private readonly string _apiType; // "OpenRouter" or "Perplexity"
    private readonly string _model;
    private readonly ILogger<TradingViewAnalyzer> _logger;
    private readonly ForexMarketSessionService _marketSessionService;
    private readonly IPositionSizingService _positionSizingService;
    private readonly IServiceProvider _serviceProvider;

    // Cache for recent analysis results to ensure consistency between endpoints
    private readonly Dictionary<string, (SentimentAnalysisResult Result, DateTime Timestamp)> _analysisCache = 
        new Dictionary<string, (SentimentAnalysisResult, DateTime)>(StringComparer.OrdinalIgnoreCase);

    // Cache expiration time (5 minutes)
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Constructor for TradingViewAnalyzer
    /// </summary>
    public TradingViewAnalyzer(
        IForexDataProviderFactory dataProviderFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TradingViewAnalyzer> logger,
        ForexMarketSessionService marketSessionService,
        IPositionSizingService positionSizingService,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _marketSessionService = marketSessionService ?? throw new ArgumentNullException(nameof(marketSessionService));
        _positionSizingService = positionSizingService ?? throw new ArgumentNullException(nameof(positionSizingService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        
        // Get API key from configuration
        _apiKey = configuration["OpenRouter:ApiKey"] ?? configuration["TRADER_OPENROUTER_API_KEY"] ?? 
                 configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"] ??
                 throw new ArgumentException("No API key found for OpenRouter or Perplexity");
        
        // Determine which API we're using
        if (!string.IsNullOrEmpty(configuration["OpenRouter:ApiKey"]) || !string.IsNullOrEmpty(configuration["TRADER_OPENROUTER_API_KEY"]))
        {
            _apiType = "OpenRouter";
            _model = "anthropic/claude-3-opus:beta";
        }
        else
        {
            _apiType = "Perplexity";
            _model = "mistral-7b-instruct";
        }

        // Get the provider type from configuration
        var providerType = configuration.GetValue<DataProviderType>("DataProvider:Type", DataProviderType.Mock);
        
        // Initialize the data provider
        _dataProvider = dataProviderFactory.GetProvider(providerType);
        
        _logger.LogInformation("Initialized TradingViewAnalyzer with {ProviderType} data provider", providerType);
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
            _logger.LogInformation("Analyzing sentiment for {Symbol} using {ApiType} with model {Model}", symbol, _apiType, _model);
            
            // Check if we have a recent cached result
            if (_analysisCache.TryGetValue(symbol, out var cachedResult))
            {
                if (DateTime.UtcNow - cachedResult.Timestamp < _cacheExpiration)
                {
                    _logger.LogInformation("Using cached analysis result for {Symbol} from {Timestamp}", 
                        symbol, cachedResult.Timestamp);
                    return cachedResult.Result;
                }
                else
                {
                    _logger.LogInformation("Cached analysis for {Symbol} expired, performing new analysis", symbol);
                }
            }
            
            // Check if we're using TraderMade or TwelveData provider to adjust candle counts
            bool isTraderMade = _dataProvider is TraderMadeDataProvider;
            bool isTwelveData = _dataProvider is TwelveDataProvider;

            // Fetch candle data for multiple timeframes - focusing on higher timeframes
            // We'll skip 5-minute data and daily data to reduce API calls
            var candleTasks = new[]
            {
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours1, 24),  // 1H candles
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours4, 12)   // 4H candles
            };
            
            // Wait for all data to be retrieved
            await Task.WhenAll(candleTasks);
            
            // Extract candle data for each timeframe
            var candles1h = await candleTasks[0];
            var candles4h = await candleTasks[1];
            
            // Create empty lists for timeframes we're not using
            var candles5m = new List<CandleData>();
            var candles1d = new List<CandleData>();
            
            // Save chart data to a text file for reference
            await SaveChartDataToFile(symbol, candles5m, candles1h, candles4h, candles1d);

            // Generate the prompt for the sentiment analysis with emphasis on higher timeframes
            var prompt = GenerateChartAnalysisPrompt(symbol, candles5m, candles1h, candles4h, candles1d);
            
            // Send the prompt to the selected API
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "You are an expert trading analyst specializing in technical analysis and market sentiment. Focus on 4-hour and 1-hour timeframes for reliable signals and to filter out market noise. Provide concise, accurate trading advice based on chart data. Be precise with price levels and never make up information. If you're uncertain about specific data points, acknowledge the limitations of your information. Prioritize medium-term trends over short-term fluctuations." },
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
            
            if (_apiType == "OpenRouter")
            {
                // OpenRouter requires HTTP-Referer header
                request.Headers.Add("HTTP-Referer", "https://trader.app");
                _logger.LogInformation("Added HTTP-Referer header for OpenRouter request");
            }

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            
            _logger.LogInformation("Sending request to {BaseAddress} for {ApiType} analysis", _httpClient.BaseAddress, _apiType);

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);

            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new InvalidOperationException("Invalid response from API");
            }

            // Extract the model used from the response
            string modelUsed = !string.IsNullOrEmpty(responseObject.model) ? responseObject.model : _model;
            _logger.LogInformation("{ApiType} selected model: {Model} for analysis of {Symbol}", _apiType, modelUsed, symbol);

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
                        BestEntryPrice = sentimentData.bestEntryPrice > 0 ? sentimentData.bestEntryPrice : sentimentData.currentPrice,
                        OrderType = ParseOrderType(sentimentData.orderType, sentimentData.direction, sentimentData.currentPrice, sentimentData.bestEntryPrice),
                        TimeToBestEntry = !string.IsNullOrEmpty(sentimentData.timeToBestEntry) ? sentimentData.timeToBestEntry : "Unknown",
                        ValidUntil = ParseValidityPeriod(sentimentData.validityPeriod),
                        IsSafeToEnterAtCurrentPrice = sentimentData.isSafeToEnterAtCurrentPrice,
                        CurrentEntryReason = sentimentData.currentEntryReason,
                        RiskLevel = !string.IsNullOrEmpty(sentimentData.riskLevel) ? sentimentData.riskLevel : "Medium",
                        MarketSession = new MarketSessionInfo
                        {
                            CurrentSession = sessionInfo.CurrentSession.ToString(),
                            Description = sessionInfo.Description,
                            LiquidityLevel = sessionInfo.LiquidityLevel,
                            RecommendedSession = sessionInfo.RecommendedSession.ToString(),
                            RecommendationReason = sessionInfo.RecommendationReason,
                            TimeUntilNextSession = FormatTimeSpan(sessionInfo.TimeUntilNextSession),
                            NextSession = sessionInfo.NextSession.ToString(),
                            CurrentTimeUtc = sessionInfo.CurrentTimeUtc,
                            NextSessionStartTimeUtc = sessionInfo.NextSessionStartTimeUtc
                        },
                        ModelUsed = modelUsed
                    };

                    // Add session warning if current session is not the recommended one
                    if (sessionInfo.CurrentSession != sessionInfo.RecommendedSession && result.IsTradeRecommended)
                    {
                        // Check if this is a cryptocurrency pair
                        bool isCrypto = IsCryptoPair(symbol);
                        
                        // Only add session warning for forex pairs, not for cryptocurrencies
                        if (!isCrypto)
                        {
                            result.SessionWarning = $"Warning: Current market session ({sessionInfo.CurrentSession}) is not optimal for trading {symbol}. Consider waiting for the {sessionInfo.RecommendedSession} session for better liquidity and trading conditions.";
                            _logger.LogInformation("Session warning added for {Symbol}: Current session {CurrentSession} is not the recommended {RecommendedSession}", 
                                symbol, sessionInfo.CurrentSession, sessionInfo.RecommendedSession);
                        }
                    }
                    
                    // Store the result in the cache
                    _analysisCache[symbol] = (result, DateTime.UtcNow);
                    
                    _logger.LogInformation(
                        "Analysis for {Symbol}: {Sentiment} ({Confidence:P0}), Trade: {Trade}, Current Session: {Session} (Liquidity: {Liquidity}/5)",
                        symbol,
                        result.Sentiment,
                        result.Confidence,
                        result.TradeRecommendation,
                        result.MarketSession.CurrentSession,
                        result.MarketSession.LiquidityLevel);
                    
                    // Add position sizing calculations
                    result.PositionSizing = await _positionSizingService.CalculatePositionSizingAsync(
                        symbol,
                        result.CurrentPrice);
                    
                    return result;
                }
            }
            
            throw new InvalidOperationException("Failed to parse sentiment data from response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {Symbol}", symbol);
            
            // Return a fallback result
            return new SentimentAnalysisResult
            {
                CurrencyPair = symbol,
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
    /// Provides trading recommendations for multiple assets.
    /// </summary>
    /// <param name="count">Number of recommendations to provide</param>
    /// <param name="provider">The data provider to use for price data</param>
    /// <returns>List of trading recommendations</returns>
    public async Task<List<ForexRecommendation>> GetTradingRecommendationsAsync(int count = 3, string? provider = null)
    {
        try
        {
            _logger.LogInformation("Getting {Count} trading recommendations using {ApiType} with model {Model} and provider {Provider}", 
                count, _apiType, _model, provider ?? "default");
            
            // Check if API key is available
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("API key is missing. Cannot generate recommendations.");
                throw new InvalidOperationException("API key is required for generating recommendations");
            }

            // If a specific provider is requested, try to use it
            IForexDataProvider dataProvider = _dataProvider;
            if (!string.IsNullOrEmpty(provider))
            {
                switch (provider.ToLower())
                {
                    case "twelvedata":
                        dataProvider = _serviceProvider.GetService<TwelveDataProvider>() ?? _dataProvider;
                        break;
                    case "tradermade":
                        dataProvider = _serviceProvider.GetService<TraderMadeDataProvider>() ?? _dataProvider;
                        break;
                    case "polygon":
                        dataProvider = _serviceProvider.GetService<PolygonDataProvider>() ?? _dataProvider;
                        break;
                    default:
                        _logger.LogWarning("Unknown provider {Provider} specified, using default provider", provider);
                        break;
                }
            }
            
            // Define a list of common forex pairs to analyze
            var commonPairs = new List<string>
            {
                "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "NZDUSD", "USDCHF",
                "EURJPY", "GBPJPY", "EURGBP", "AUDJPY", "EURAUD", "EURCHF", "GBPCAD"
            };

            // Add some crypto pairs if we're using a real data provider
            if (!(dataProvider is ForexDataProvider))
            {
                commonPairs.AddRange(new[] { "BTCUSD", "ETHUSD" });
            }
            
            // Shuffle the list to get different recommendations each time
            var random = new Random();
            var shuffledPairs = commonPairs.OrderBy(_ => random.Next()).ToList();
            
            // Take more pairs to analyze to increase chances of finding good opportunities
            // Increase the number of pairs to analyze to ensure we get enough recommendations
            var pairsToAnalyze = shuffledPairs.Take(Math.Min(count + 10, shuffledPairs.Count)).ToList();

            _logger.LogInformation("Will analyze the following pairs: {Pairs}", string.Join(", ", pairsToAnalyze));
            
            // Analyze each pair
            var goodRecommendations = new List<ForexRecommendation>(); // Recommendations with R:R >= 1.5
            var allRecommendations = new List<ForexRecommendation>(); // All valid recommendations regardless of R:R
            
            foreach (var pair in pairsToAnalyze)
            {
                try
                {
                    _logger.LogInformation("Analyzing {Pair} for trading recommendation", pair);
                    
                    // Check if we have a recent analysis for this pair and use it instead of doing a new analysis
                    if (_analysisCache.TryGetValue(pair, out var cachedResult) && 
                        DateTime.UtcNow - cachedResult.Timestamp < _cacheExpiration)
                    {
                        _logger.LogInformation("Using cached analysis for {Pair} from {Timestamp}", 
                            pair, cachedResult.Timestamp);
                        
                        // Convert the sentiment analysis to a recommendation
                        var recommendation = ConvertAnalysisToRecommendation(cachedResult.Result);
                        
                        // Add to all recommendations list regardless of R:R ratio
                        allRecommendations.Add(recommendation);
                        
                        // Only add to good recommendations if the risk-reward ratio is reasonable (at least 1.5)
                        if (recommendation.RiskRewardRatio >= 1.5m)
                        {
                            goodRecommendations.Add(recommendation);
                            
                            _logger.LogInformation(
                                "Added good recommendation from cache for {Symbol}: {Direction} at {Price}, R:R {RiskReward}, Sentiment: {Sentiment}",
                                recommendation.CurrencyPair,
                                recommendation.Direction,
                                recommendation.CurrentPrice,
                                recommendation.RiskRewardRatio,
                                recommendation.Sentiment);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Added recommendation with low R:R from cache for {Symbol}: {Direction} at {Price}, R:R {RiskReward}, Sentiment: {Sentiment}",
                                recommendation.CurrencyPair,
                                recommendation.Direction,
                                recommendation.CurrentPrice,
                                recommendation.RiskRewardRatio,
                                recommendation.Sentiment);
                        }
                        
                        continue; // Skip to the next pair
                    }
                    
                    // If no cached result, perform a full analysis
                    SentimentAnalysisResult analysisResult = await AnalyzeSentimentAsync(pair);
                    
                    // Convert the sentiment analysis to a recommendation
                    var newRecommendation = ConvertAnalysisToRecommendation(analysisResult);
                    
                    // Add to all recommendations list regardless of R:R ratio
                    allRecommendations.Add(newRecommendation);
                    
                    // Only add to good recommendations if the risk-reward ratio is reasonable (at least 1.5)
                    if (newRecommendation.RiskRewardRatio >= 1.5m)
                    {
                        goodRecommendations.Add(newRecommendation);
                        
                        _logger.LogInformation(
                            "Added good recommendation for {Symbol}: {Direction} at {Price}, R:R {RiskReward}, Sentiment: {Sentiment}",
                            newRecommendation.CurrencyPair,
                            newRecommendation.Direction,
                            newRecommendation.CurrentPrice,
                            newRecommendation.RiskRewardRatio,
                            newRecommendation.Sentiment);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Added recommendation with low R:R for {Symbol}: {Direction} at {Price}, R:R {RiskReward}, Sentiment: {Sentiment}",
                            newRecommendation.CurrencyPair,
                            newRecommendation.Direction,
                            newRecommendation.CurrentPrice,
                            newRecommendation.RiskRewardRatio,
                            newRecommendation.Sentiment);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing {Pair} for recommendations", pair);
                    // Continue with other pairs
                }
                
                // If we have enough good recommendations, stop
                if (goodRecommendations.Count >= count)
                {
                    break;
                }
            }

            List<ForexRecommendation> finalRecommendations;
            
            // Prioritize good recommendations, but fall back to all recommendations if needed
            if (goodRecommendations.Count >= count)
            {
                _logger.LogInformation("Found {Count} good recommendations with R:R >= 1.5", goodRecommendations.Count);
                finalRecommendations = goodRecommendations
                    .OrderByDescending(r => r.Confidence)
                    .Take(count)
                    .ToList();
            }
            else if (allRecommendations.Count > 0)
            {
                _logger.LogInformation("Using {GoodCount} good recommendations and {LowCount} lower-quality recommendations to meet requested count", 
                    goodRecommendations.Count, Math.Max(0, count - goodRecommendations.Count));
                    
                // Start with good recommendations
                finalRecommendations = new List<ForexRecommendation>(goodRecommendations);
                
                // Add lower quality recommendations if needed to reach the requested count
                if (finalRecommendations.Count < count)
                {
                    var remainingNeeded = count - finalRecommendations.Count;
                    var lowerQualityRecs = allRecommendations
                        .Where(r => !goodRecommendations.Any(g => g.CurrencyPair == r.CurrencyPair))
                        .OrderByDescending(r => r.RiskRewardRatio)
                        .ThenByDescending(r => r.Confidence)
                        .Take(remainingNeeded)
                        .ToList();
                        
                    finalRecommendations.AddRange(lowerQualityRecs);
                }
            }
            else
            {
                _logger.LogWarning("No trading opportunities found. Creating fallback recommendation.");
                
                // Create a fallback recommendation if we couldn't get any
                var fallbackRec = CreateFallbackRecommendation();
                finalRecommendations = new List<ForexRecommendation> { fallbackRec };
            }

            // Log the model being used
            _logger.LogInformation("{ApiType} using model: {Model} for trading recommendations", _apiType, _model);

            return finalRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            
            // Return a fallback recommendation
            var fallback = CreateFallbackRecommendation();
            fallback.ModelUsed = "Error - model information unavailable";
            return new List<ForexRecommendation> { fallback };
        }
    }

    /// <summary>
    /// Creates a fallback recommendation when no valid recommendations are found
    /// </summary>
    private ForexRecommendation CreateFallbackRecommendation()
    {
        // Choose a common pair
        string pair = "EURUSD";
        
        // Get market session information
        var sessionInfo = _marketSessionService.GetCurrentSessionInfo(pair);
        
        // Create a basic recommendation with neutral stance
        return new ForexRecommendation
        {
            CurrencyPair = pair,
            Direction = "Neutral",
            Sentiment = SentimentType.Neutral,
            Confidence = 0.3m,
            CurrentPrice = 1.1m,  // Reasonable default
            TakeProfitPrice = 1.11m,
            StopLossPrice = 1.09m,
            BestEntryPrice = 1.1m,
            OrderType = Trader.Core.Services.OrderType.MarketBuy,
            TimeToBestEntry = "Now",
            ValidUntil = DateTime.UtcNow.AddHours(24),
            IsSafeToEnterAtCurrentPrice = false,
            RiskLevel = "Medium",
            CurrentEntryReason = "This is a fallback recommendation due to lack of strong trading opportunities. Consider waiting for better market conditions.",
            Factors = new List<string> { 
                "No strong trading opportunities identified at this time",
                "This is a fallback recommendation with low confidence",
                "Consider waiting for clearer market conditions"
            },
            Rationale = "No clear trading opportunities were identified by the analysis. This is a fallback recommendation with low confidence. Consider waiting for more favorable market conditions or clearer signals.",
            Timestamp = DateTime.UtcNow,
            MarketSession = new MarketSessionInfo
            {
                CurrentSession = sessionInfo.CurrentSession.ToString(),
                Description = sessionInfo.Description,
                LiquidityLevel = sessionInfo.LiquidityLevel,
                RecommendedSession = sessionInfo.RecommendedSession.ToString(),
                RecommendationReason = sessionInfo.RecommendationReason,
                TimeUntilNextSession = FormatTimeSpan(sessionInfo.TimeUntilNextSession),
                NextSession = sessionInfo.NextSession.ToString(),
                CurrentTimeUtc = sessionInfo.CurrentTimeUtc,
                NextSessionStartTimeUtc = sessionInfo.NextSessionStartTimeUtc
            },
            ModelUsed = "Fallback - model information unavailable"
        };
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
        
        sb.AppendLine("Focus on the 4-hour and 1-hour timeframes for reliable signals.");
        sb.AppendLine("Ignore short-term noise and prioritize medium-term trends.");
        sb.AppendLine();
        
        // Add 4-hour candles (highest priority)
        sb.AppendLine("## 4-Hour Timeframe (Most Important)");
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
        
        // Add 1-hour candles (second priority)
        sb.AppendLine("## 1-Hour Timeframe (Important)");
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

        // We're skipping daily and 5-minute data entirely
        
        sb.AppendLine("Based on this data, provide a comprehensive analysis including:");
        sb.AppendLine("1. Current market structure and trend direction on each timeframe");
        sb.AppendLine("2. Key support and resistance levels");
        sb.AppendLine("3. Important technical indicators and patterns");
        sb.AppendLine("4. Trading recommendation with entry, stop loss, and take profit levels");
        sb.AppendLine("5. If the current price isn't ideal for entry, suggest a better entry price");
        sb.AppendLine();
        
        sb.AppendLine("Provide your analysis in the following JSON format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"sentiment\": \"Bullish\", \"Bearish\", or \"Neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": current price as decimal,");
        sb.AppendLine("  \"direction\": \"Buy\", \"Sell\", or \"None\",");
        sb.AppendLine("  \"stopLossPrice\": stop loss price as decimal,");
        sb.AppendLine("  \"takeProfitPrice\": target price as decimal,");
        sb.AppendLine("  \"bestEntryPrice\": optimal entry price as decimal (can be different from current price),");
        sb.AppendLine("  \"orderType\": \"Market\", \"Limit\", or \"Stop\",");
        sb.AppendLine("  \"timeToBestEntry\": \"estimated time until best entry price is reached (e.g., '2-3 hours', '1-2 days')\",");
        sb.AppendLine("  \"validityPeriod\": \"how long this recommendation is valid for (e.g., '24 hours', '3 days')\",");
        sb.AppendLine("  \"isSafeToEnterAtCurrentPrice\": true or false,");
        sb.AppendLine("  \"currentEntryReason\": \"explanation of why it's safe or unsafe to enter at current price\",");
        sb.AppendLine("  \"riskLevel\": \"Low\", \"Medium\", \"High\", or \"Very High\",");
        sb.AppendLine("  \"factors\": [\"list\", \"of\", \"factors\"],");
        sb.AppendLine("  \"summary\": \"brief summary of analysis\",");
        sb.AppendLine("  \"sources\": [\"list\", \"of\", \"sources\"]");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("Only recommend a trade if there is a clear setup with a good risk-reward ratio (at least 1.5:1). If there's no clear trade opportunity, set direction to \"None\".");
        sb.AppendLine();

        sb.AppendLine("If the current price isn't ideal for entry, suggest a better entry price in the bestEntryPrice field. This could be at a key support/resistance level, a retracement level, or a better risk-reward setup. However, don't be too conservative with the best entry price - if the current price is within 0.5% of your ideal entry for forex pairs (or 2% for crypto), consider the current price acceptable.");
        sb.AppendLine();

        sb.AppendLine("For the orderType field:");
        sb.AppendLine("- Use \"Market\" if the trade should be executed immediately at the current price");
        sb.AppendLine("- For Buy orders:");
        sb.AppendLine("  - Use \"Limit\" if the best entry price is BELOW the current price (waiting for price to drop)");
        sb.AppendLine("  - Use \"Stop\" if the best entry price is ABOVE the current price (waiting for breakout confirmation)");
        sb.AppendLine("- For Sell orders:");
        sb.AppendLine("  - Use \"Limit\" if the best entry price is ABOVE the current price (waiting for price to rise)");
        sb.AppendLine("  - Use \"Stop\" if the best entry price is BELOW the current price (waiting for breakdown confirmation)");
        sb.AppendLine();

        sb.AppendLine("Note: The system will automatically correct the order type based on the relationship between current price and best entry price if needed.");
        sb.AppendLine();

        sb.AppendLine("For the isSafeToEnterAtCurrentPrice field:");
        sb.AppendLine("- Set to true if entering at the current price is acceptable, even if not optimal");
        sb.AppendLine("- Set to false ONLY if entering at the current price would significantly reduce the risk-reward ratio or substantially increase risk");
        sb.AppendLine("- Be more lenient with this flag - if the trade still has a positive risk-reward ratio at current price, set this to true");
        sb.AppendLine("- Consider factors like volatility, proximity to key levels, and overall market conditions");
        sb.AppendLine();

        sb.AppendLine("For the riskLevel field:");
        sb.AppendLine("- Set to \"Low\" for trades with strong confirmation, clear support/resistance, and favorable market conditions");
        sb.AppendLine("- Set to \"Medium\" for standard trades with reasonable confirmation and acceptable risk-reward");
        sb.AppendLine("- Set to \"High\" for trades with less confirmation, higher volatility, or proximity to key levels");
        sb.AppendLine("- Set to \"Very High\" for counter-trend trades, trades with minimal confirmation, or during high-impact news events");
        sb.AppendLine("- Consider factors like trend strength, confirmation signals, volatility, and proximity to key levels");
        sb.AppendLine();

        sb.AppendLine("For the currentEntryReason field:");
        sb.AppendLine("- Provide a detailed explanation (1-3 sentences) of why it's safe or unsafe to enter at the current price");
        sb.AppendLine("- If safe, explain why the current price is still a good entry despite not being optimal");
        sb.AppendLine("- If unsafe, explain specifically what risks or disadvantages exist at the current price");
        sb.AppendLine("- Include specific price levels, risk-reward calculations, or technical factors in your explanation");
        sb.AppendLine("- This helps traders understand exactly why they should wait or can proceed immediately");
        sb.AppendLine();

        sb.AppendLine("For the timeToBestEntry field:");
        sb.AppendLine("- Provide an estimate of how long it might take for the price to reach the best entry level");
        sb.AppendLine("- Use formats like \"2-3 hours\", \"1-2 days\", or \"Unknown\" if it's not possible to estimate");
        sb.AppendLine("- Base this on recent price action, volatility, and market conditions");
        sb.AppendLine();

        sb.AppendLine("For the validityPeriod field:");
        sb.AppendLine("- Specify how long this recommendation should be considered valid");
        sb.AppendLine("- Use formats like \"24 hours\", \"3 days\", \"1 week\"");
        sb.AppendLine("- Consider market conditions, upcoming events, and the timeframe of the analysis");
        
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
    /// Parses a string order type indicator into the OrderType enum.
    /// </summary>
    private Trader.Core.Services.OrderType ParseOrderType(string orderType, string direction, decimal currentPrice, decimal bestEntryPrice)
    {
        // If the AI provided an order type, check if it's consistent with the price relationship
        // If not, override it with the correct order type
        
        // For buy orders:
        if (direction.ToLower() == "buy")
        {
            // If best entry is below current price, it should be a limit buy
            if (bestEntryPrice < currentPrice)
            {
                return Trader.Core.Services.OrderType.LimitBuy;
            }
            // If best entry is above current price, it should be a stop buy
            else if (bestEntryPrice > currentPrice)
            {
                return Trader.Core.Services.OrderType.StopBuy;
            }
            // If they're the same, use market buy
            else
            {
                return Trader.Core.Services.OrderType.MarketBuy;
            }
        }

        // For sell orders:
        else
        {
            // If best entry is above current price, it should be a limit sell
            if (bestEntryPrice > currentPrice)
            {
                return Trader.Core.Services.OrderType.LimitSell;
            }
            // If best entry is below current price, it should be a stop sell
            else if (bestEntryPrice < currentPrice)
            {
                return Trader.Core.Services.OrderType.StopSell;
            }
            // If they're the same, use market sell
            else
            {
                return Trader.Core.Services.OrderType.MarketSell;
            }
        }
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

    /// <summary>
    /// Parses a validity period string (e.g., "24 hours", "3 days") and returns a DateTime
    /// representing when the recommendation expires.
    /// </summary>
    private DateTime ParseValidityPeriod(string validityPeriod)
    {
        if (string.IsNullOrEmpty(validityPeriod))
        {
            // Default to 24 hours if not specified
            return DateTime.UtcNow.AddDays(1);
        }
        
        try
        {
            // Try to parse the string into a duration
            validityPeriod = validityPeriod.ToLower().Trim();
            
            if (validityPeriod.Contains("hour"))
            {
                int hours = ExtractNumber(validityPeriod);
                return DateTime.UtcNow.AddHours(hours);
            }
            else if (validityPeriod.Contains("day"))
            {
                int days = ExtractNumber(validityPeriod);
                return DateTime.UtcNow.AddDays(days);
            }
            else if (validityPeriod.Contains("week"))
            {
                int weeks = ExtractNumber(validityPeriod);
                return DateTime.UtcNow.AddDays(weeks * 7);
            }
            else if (validityPeriod.Contains("month"))
            {
                int months = ExtractNumber(validityPeriod);
                return DateTime.UtcNow.AddMonths(months);
            }
            else
            {
                // If we can't parse it, default to 24 hours
                _logger.LogWarning("Could not parse validity period: {ValidityPeriod}. Using default of 24 hours.", validityPeriod);
                return DateTime.UtcNow.AddDays(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing validity period: {ValidityPeriod}", validityPeriod);
            return DateTime.UtcNow.AddDays(1);
        }
    }

    /// <summary>
    /// Extracts the first number from a string.
    /// </summary>
    private int ExtractNumber(string input)
    {
        var numberString = new string(input.Where(c => char.IsDigit(c)).ToArray());
        
        if (string.IsNullOrEmpty(numberString))
        {
            return 1; // Default to 1 if no number found
        }
        
        return int.Parse(numberString);
    }

    /// <summary>
    /// Determines if a symbol is a cryptocurrency pair.
    /// </summary>
    private bool IsCryptoPair(string symbol)
    {
        // Common cryptocurrencies
        var cryptoCurrencies = new[] { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK", "XLM", "SOL", "DOGE" };
        
        // Check if the symbol contains any cryptocurrency code
        foreach (var crypto in cryptoCurrencies)
        {
            if (symbol.Contains(crypto, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Converts a SentimentAnalysisResult to a ForexRecommendation
    /// </summary>
    private ForexRecommendation ConvertAnalysisToRecommendation(SentimentAnalysisResult analysis)
    {
        // Ensure we're correctly mapping the sentiment and direction
        var direction = analysis.TradeRecommendation;
        
        // Log the conversion to help with debugging
        _logger.LogInformation(
            "Converting analysis to recommendation for {Symbol}: Sentiment={Sentiment}, TradeRecommendation={TradeRecommendation}",
            analysis.CurrencyPair,
            analysis.Sentiment,
            analysis.TradeRecommendation);
        
        // Make sure to copy the position sizing info
        var recommendation = new ForexRecommendation
        {
            CurrencyPair = analysis.CurrencyPair,
            Direction = direction,
            Sentiment = analysis.Sentiment,
            Confidence = analysis.Confidence,
            CurrentPrice = analysis.CurrentPrice,
            TakeProfitPrice = analysis.TakeProfitPrice,
            StopLossPrice = analysis.StopLossPrice,
            BestEntryPrice = analysis.BestEntryPrice,
            OrderType = analysis.OrderType,
            TimeToBestEntry = analysis.TimeToBestEntry,
            ValidUntil = analysis.ValidUntil,
            IsSafeToEnterAtCurrentPrice = analysis.IsSafeToEnterAtCurrentPrice,
            CurrentEntryReason = analysis.CurrentEntryReason,
            RiskLevel = analysis.RiskLevel,
            Factors = analysis.Factors,
            Rationale = analysis.Summary,
            Timestamp = DateTime.UtcNow,
            MarketSession = analysis.MarketSession,
            SessionWarning = analysis.SessionWarning,
            PositionSizing = analysis.PositionSizing,
            ModelUsed = analysis.ModelUsed
        };
        
        return recommendation;
    }

    #region API Response Classes
    
    /// <summary>
    /// Response structure from the Perplexity/OpenRouter API.
    /// </summary>
    private class PerplexityResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
        public string model { get; set; } = string.Empty;
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
        public decimal bestEntryPrice { get; set; }
        public string orderType { get; set; } = "Market";
        public string timeToBestEntry { get; set; } = string.Empty;
        public string validityPeriod { get; set; } = "24 hours";
        public bool isSafeToEnterAtCurrentPrice { get; set; } = false;
        public string currentEntryReason { get; set; } = string.Empty;
        public string riskLevel { get; set; } = "Medium";
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
        public decimal bestEntryPrice { get; set; }
        public string orderType { get; set; } = "Market";
        public string timeToBestEntry { get; set; } = string.Empty;
        public string validityPeriod { get; set; } = "24 hours";
        public bool isSafeToEnterAtCurrentPrice { get; set; } = false;
        public string currentEntryReason { get; set; } = string.Empty;
        public List<string>? factors { get; set; }
        public string rationale { get; set; } = string.Empty;
    }
    
    #endregion
}