using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Constructor for TradingViewAnalyzer
    /// </summary>
    public TradingViewAnalyzer(
        IForexDataProvider dataProvider,
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
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        
        // Get API key from configuration
        _apiKey = configuration["OpenRouter:ApiKey"] ?? configuration["TRADER_OPENROUTER_API_KEY"] ?? 
                 configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"] ??
                 throw new ArgumentException("No API key found for OpenRouter or Perplexity");
        
        // Determine which API we're using
        if (!string.IsNullOrEmpty(configuration["OpenRouter:ApiKey"]) || !string.IsNullOrEmpty(configuration["TRADER_OPENROUTER_API_KEY"]))
        {
            _apiType = "OpenRouter";
            _model = configuration["OpenRouter:Model"] ?? "openai/o3-mini-high";
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            // Add default headers for OpenRouter
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://trader.app");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Trader App");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
        else
        {
            _apiType = "Perplexity";
            _model = "mistral-7b-instruct";
            _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
        }
        
        _logger.LogInformation("Initialized TradingViewAnalyzer with {ProviderType} data provider", dataProvider.GetType().Name);
    }

    /// <summary>
    /// Constructor for TradingViewAnalyzer using a factory
    /// </summary>
    public TradingViewAnalyzer(
        IForexDataProviderFactory dataProviderFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TradingViewAnalyzer> logger,
        ForexMarketSessionService marketSessionService,
        IPositionSizingService positionSizingService,
        IServiceProvider serviceProvider,
        DataProviderType? requestedProviderType = null) : this(
            dataProviderFactory.GetProvider(requestedProviderType ?? configuration.GetValue<DataProviderType>("DataProvider:Type", DataProviderType.Mock)),
            httpClient,
            configuration,
            logger,
            marketSessionService,
            positionSizingService,
            serviceProvider)
    {
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
            
            // Check if we're using TraderMade or TwelveData provider to adjust candle counts
            bool isTraderMade = _dataProvider is TraderMadeDataProvider;
            bool isTwelveData = _dataProvider is TwelveDataProvider;

            // Fetch candle data for multiple timeframes
            var candleTasks = new[]
            {
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Minutes5, 60),  // Get last 60 5-minute candles (5 hours)
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours1, 24),
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Hours4, 12),
                _dataProvider.GetCandleDataAsync(symbol, ChartTimeframe.Day1, 30)  // Get 30 days of daily data
            };
            
            await Task.WhenAll(candleTasks);
            
            var candles5m = await candleTasks[0];
            var candles1h = await candleTasks[1];
            var candles4h = await candleTasks[2];
            var candles1d = await candleTasks[3];
            
            await SaveChartDataToFile(symbol, candles5m, candles1h, candles4h, candles1d);

            var prompt = GenerateChartAnalysisPrompt(symbol, candles5m, candles1h, candles4h, candles1d);
            
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = @"You are a financial analyst specializing in forex markets. Your responses must be structured as follows:

1. REASONING: A brief explanation of your analysis process and key findings
2. JSON: A valid JSON object containing your analysis results

The JSON must follow this exact structure:
{
    ""sentiment"": ""Bullish"" | ""Bearish"" | ""Neutral"",
    ""confidence"": number between 0.0-1.0,
    ""currentPrice"": number,
    ""direction"": ""Buy"" | ""Sell"" | ""None"",
    ""stopLossPrice"": number,
    ""takeProfitPrice"": number,
    ""bestEntryPrice"": number,
    ""orderType"": ""Market"" | ""Limit"" | ""Stop"",
    ""timeToBestEntry"": string,
    ""validityPeriod"": string,
    ""isSafeToEnterAtCurrentPrice"": boolean,
    ""currentEntryReason"": string,
    ""riskLevel"": ""Low"" | ""Medium"" | ""High"" | ""Very High"",
    ""factors"": string[],
    ""summary"": string,
    ""sources"": string[],
    ""inOutPlay"": {
        ""available"": boolean,
        ""direction"": ""Buy"" | ""Sell"",
        ""entryPrice"": number,
        ""stopLoss"": number,
        ""takeProfit"": number,
        ""timeframe"": string,
        ""reason"": string
    }
}

Always verify your information with reliable sources and include citations. Be accurate with price levels and market data. Never make up information or sources. If you're uncertain about specific data points, acknowledge the limitations of your information." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 12000,
                stream = false,
                response_format = new
                {
                    type = "json_schema",
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sentiment = new { type = "string", @enum = new[] { "Bullish", "Bearish", "Neutral" } },
                            confidence = new { type = "number", minimum = 0, maximum = 1 },
                            currentPrice = new { type = "number", minimum = 0 },
                            direction = new { type = "string", @enum = new[] { "Buy", "Sell", "None" } },
                            stopLossPrice = new { type = "number", minimum = 0 },
                            takeProfitPrice = new { type = "number", minimum = 0 },
                            bestEntryPrice = new { type = "number", minimum = 0 },
                            orderType = new { type = "string", @enum = new[] { "Market", "Limit", "Stop" } },
                            timeToBestEntry = new { type = "string" },
                            validityPeriod = new { type = "string" },
                            isSafeToEnterAtCurrentPrice = new { type = "boolean" },
                            currentEntryReason = new { type = "string" },
                            riskLevel = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Very High" } },
                            factors = new { type = "array", items = new { type = "string" } },
                            summary = new { type = "string" },
                            sources = new { type = "array", items = new { type = "string" } },
                            inOutPlay = new
                            {
                                type = "object",
                                properties = new
                                {
                                    available = new { type = "boolean" },
                                    direction = new { type = "string", @enum = new[] { "Buy", "Sell" } },
                                    entryPrice = new { type = "number", minimum = 0 },
                                    stopLoss = new { type = "number", minimum = 0 },
                                    takeProfit = new { type = "number", minimum = 0 },
                                    timeframe = new { type = "string" },
                                    reason = new { type = "string" }
                                },
                                required = new[] { "available", "direction", "entryPrice", "stopLoss", "takeProfit", "timeframe", "reason" }
                            }
                        },
                        required = new[] { "sentiment", "confidence", "currentPrice", "direction", "stopLossPrice", "takeProfitPrice", "bestEntryPrice", "orderType", "timeToBestEntry", "validityPeriod", "isSafeToEnterAtCurrentPrice", "currentEntryReason", "riskLevel", "factors", "summary", "sources", "inOutPlay" }
                    }
                }
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
                request.Headers.Add("HTTP-Referer", "https://trader.app");
                request.Headers.Add("X-Title", "Trader App");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            // Log the complete request details
            try
            {
                var requestContent = await request.Content.ReadAsStringAsync();
                
                // Use a more reliable path for logs
                var currentDirectory = Directory.GetCurrentDirectory();
                var directory = Path.Combine(currentDirectory, "ApiLogs");
                
                _logger.LogInformation("Attempting to save logs to directory: {Directory}", directory);
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(directory))
                {
                    _logger.LogInformation("Creating ApiLogs directory at {Directory}", directory);
                    Directory.CreateDirectory(directory);
                }
                
                var requestFilename = Path.Combine(directory, $"{symbol}_OpenRouter_Request_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                _logger.LogInformation("Saving request to file: {Filename}", requestFilename);
                
                var logContent = $"Request URL: {_httpClient.BaseAddress}{request.RequestUri}\n" +
                    $"Headers:\n{string.Join("\n", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))})\n\n" +
                    $"Content:\n{requestContent}";
                
                await File.WriteAllTextAsync(requestFilename, logContent);
                _logger.LogInformation("Successfully saved request details to {Filename}", requestFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving request details. Current directory: {Directory}, Exception: {Message}", 
                    Directory.GetCurrentDirectory(), ex.Message);
            }

            _logger.LogInformation("Sending request to {BaseAddress} for {ApiType} analysis", _httpClient.BaseAddress, _apiType);
            _logger.LogDebug("Request content: {Content}", await request.Content.ReadAsStringAsync());

            // Create a cancellation token with a timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4)); // 4 minute timeout
            
            string responseString;
            string modelUsed;
            string responseContent;
            string reasoning = string.Empty;
            string jsonContent = string.Empty;
            
            try
            {
                var response = await _httpClient.SendAsync(request, cts.Token);
                
                // Log response status and headers before reading content
                _logger.LogInformation("Response Status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Response Headers: {Headers}", 
                    string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                
                if (!response.IsSuccessStatusCode)
                {
                    responseString = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API request failed with status code {StatusCode}. Response: {Response}", 
                        response.StatusCode, responseString);
                    return CreateFallbackAnalysis(symbol, $"API request failed with status {response.StatusCode}", _model, string.Empty);
                }
                
                responseString = await response.Content.ReadAsStringAsync();
                
                // Clean up empty lines at the start
                responseString = responseString?.TrimStart('\n', '\r', ' ') ?? string.Empty;
                
                _logger.LogInformation("Raw response length: {Length} characters", responseString?.Length ?? 0);
                _logger.LogDebug("Raw API response: {Response}", responseString);

                // Log the response to file
                try
                {
                    var currentDirectory = Directory.GetCurrentDirectory();
                    var directory = Path.Combine(currentDirectory, "ApiLogs");
                    
                    // Create directory if it doesn't exist (should already exist from request logging)
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var responseFilename = Path.Combine(directory, $"{symbol}_OpenRouter_Response_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    _logger.LogInformation("Saving response to file: {Filename}", responseFilename);
                    
                    var logContent = $"Response Status: {response.StatusCode}\n" +
                        $"Response Headers:\n{string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))})\n\n" +
                        $"Response Content:\n{responseString}";
                    
                    await File.WriteAllTextAsync(responseFilename, logContent);
                    _logger.LogInformation("Successfully saved response details to {Filename}", responseFilename);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving response details. Current directory: {Directory}, Exception: {Message}", 
                        Directory.GetCurrentDirectory(), ex.Message);
                }

                if (string.IsNullOrEmpty(responseString))
                {
                    throw new InvalidOperationException("Empty response from OpenRouter API");
                }

                // First parse the OpenRouter wrapper response
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                _logger.LogDebug("Attempting to deserialize OpenRouter response");
                var openRouterResponse = JsonSerializer.Deserialize<OpenRouterResponse>(responseString, options);
                
                if (openRouterResponse?.choices == null || openRouterResponse.choices.Count == 0 || 
                    string.IsNullOrEmpty(openRouterResponse.choices[0].message.content))
                {
                    throw new InvalidOperationException("Invalid response format from OpenRouter API");
                }

                // Get the actual content from the message
                responseContent = openRouterResponse.choices[0].message.content;
                modelUsed = !string.IsNullOrEmpty(openRouterResponse.model) ? openRouterResponse.model : _model;
                
                _logger.LogDebug("Successfully extracted content from OpenRouter response");
                _logger.LogDebug("Content: {Content}", responseContent);

                // Now parse the content as SentimentData
                try 
                {
                    // Clean up the content first
                    responseContent = responseContent.Replace("\n", "").Replace("\r", "");
                    
                    // Try to fix any malformed JSON
                    responseContent = CleanupJsonContent(responseContent);
                    
                    _logger.LogDebug("Cleaned response content: {Content}", responseContent);

                    // Verify we have a complete JSON object
                    if (!responseContent.StartsWith("{") || !responseContent.EndsWith("}"))
                    {
                        throw new InvalidOperationException("Response content is not a valid JSON object");
                    }

                    // Try to parse as JObject first to validate JSON structure
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var rootElement = jsonDoc.RootElement;

                    // Verify we have all required properties
                    var requiredProperties = new[] { "sentiment", "confidence", "currentPrice", "direction" };
                    foreach (var prop in requiredProperties)
                    {
                        if (!rootElement.TryGetProperty(prop, out _))
                        {
                            _logger.LogWarning("Missing required property: {Property}", prop);
                            throw new InvalidOperationException($"Response is missing required property: {prop}");
                        }
                    }
                    
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        Converters = 
                        {
                            new FlexibleBooleanConverter()
                        }
                    };

                    // Try to clean up any potential JSON issues
                    responseContent = responseContent.Replace("\"isSafeToEnterAtCurrentPrice\": \"true\"", "\"isSafeToEnterAtCurrentPrice\": true")
                                                  .Replace("\"isSafeToEnterAtCurrentPrice\": \"false\"", "\"isSafeToEnterAtCurrentPrice\": false")
                                                  .Replace("\"available\": \"true\"", "\"available\": true")
                                                  .Replace("\"available\": \"false\"", "\"available\": false");

                    var sentimentData = JsonSerializer.Deserialize<SentimentData>(responseContent, jsonOptions);
                    if (sentimentData == null)
                    {
                        throw new InvalidOperationException("Failed to deserialize sentiment data from response content");
                    }
                    
                    // Ensure arrays are not null
                    sentimentData.factors ??= new List<string>();
                    sentimentData.sources ??= new List<string>();
                    
                    _logger.LogDebug("Successfully deserialized sentiment data: Sentiment={Sentiment}, Direction={Direction}", 
                        sentimentData.sentiment, sentimentData.direction);

                    var sessionInfo = _marketSessionService.GetCurrentSessionInfo(symbol);
                    
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
                        TradeRecommendation = string.IsNullOrEmpty(sentimentData.direction) ? "None" : 
                            sentimentData.direction.Trim().ToLower() switch
                            {
                                "buy" => "Buy",
                                "sell" => "Sell",
                                _ => "None"
                            },
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
                        ModelUsed = modelUsed,
                        ModelReasoning = reasoning,
                        InOutPlay = sentimentData.inOutPlay != null ? new InOutPlay
                        {
                            Available = sentimentData.inOutPlay.available,
                            Direction = sentimentData.inOutPlay.direction,
                            EntryPrice = sentimentData.inOutPlay.entryPrice,
                            StopLoss = sentimentData.inOutPlay.stopLoss,
                            TakeProfit = sentimentData.inOutPlay.takeProfit,
                            Timeframe = sentimentData.inOutPlay.timeframe,
                            Reason = sentimentData.inOutPlay.reason
                        } : null
                    };

                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing sentiment data from content: {Content}", responseContent);
                    throw;
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Request timed out. Using fallback analysis.");
                return CreateFallbackAnalysis(symbol, "Request timed out", _model, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting response from API for {Symbol}", symbol);
                return CreateFallbackAnalysis(symbol, "Error communicating with API", _model, string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {Symbol}", symbol);
            return CreateFallbackAnalysis(symbol, "Error occurred during analysis", _model, string.Empty);
        }
    }

    /// <summary>
    /// Creates a fallback analysis when the API response cannot be properly parsed
    /// </summary>
    private SentimentAnalysisResult CreateFallbackAnalysis(string symbol, string responseContent, string modelUsed, string reasoning)
    {
        var sessionInfo = _marketSessionService.GetCurrentSessionInfo(symbol);
        
        // Try to determine sentiment from the response content
        var sentiment = SentimentType.Neutral;
        if (responseContent.Contains("bullish", StringComparison.OrdinalIgnoreCase))
            sentiment = SentimentType.Bullish;
        else if (responseContent.Contains("bearish", StringComparison.OrdinalIgnoreCase))
            sentiment = SentimentType.Bearish;

        return new SentimentAnalysisResult
        {
            CurrencyPair = symbol,
            Sentiment = sentiment,
            Confidence = 0.5m,
            Factors = new List<string> { "Analysis based on limited data due to parsing error" },
            Summary = "Could not generate detailed analysis. Please try again.",
            Sources = new List<string>(),
            Timestamp = DateTime.UtcNow,
            TradeRecommendation = "None",
            CurrentPrice = 0,
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
            ModelUsed = modelUsed,
            ModelReasoning = reasoning
        };
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
                    
                    // Perform a full analysis
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
                    _logger.LogError(ex, "Error analyzing {Pair}", pair);
                    continue;
                }
                
                // If we have enough good recommendations, stop analyzing more pairs
                if (goodRecommendations.Count >= count)
                {
                    break;
                }
            }
            
            // Return the best recommendations we found, preferring those with good R:R ratios
            var finalRecommendations = goodRecommendations.Count >= count
                ? goodRecommendations.Take(count).ToList()
                : goodRecommendations.Concat(allRecommendations.Where(r => !goodRecommendations.Contains(r)))
                    .Take(count)
                    .ToList();
            
            _logger.LogInformation("Returning {Count} recommendations", finalRecommendations.Count);
            return finalRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trading recommendations");
            return new List<ForexRecommendation>();
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
        
        sb.AppendLine($"Analyze chart data for {symbol}. Focus on daily, 4-hour, and 1-hour timeframes. Your response must be a valid JSON object following the schema below.");
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

        // Add 5-minute candles (lowest priority)
        sb.AppendLine("## 5-Minute Timeframe (Reference Only - Consider as Market Noise)");
        if (candles5m.Count > 0)
        {
            sb.AppendLine($"Last {candles5m.Count} 5-minute candles:");
            AppendCandleData(sb, candles5m);
        }
        else
        {
            sb.AppendLine("No 5-minute data available.");
        }
        sb.AppendLine();

        sb.AppendLine("Based on this data, provide a comprehensive analysis in the following JSON format:");
        sb.AppendLine();
        
        sb.AppendLine("{");
        sb.AppendLine("  \"sentiment\": \"Bullish\", \"Bearish\", or \"Neutral\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"currentPrice\": current price as decimal,");
        sb.AppendLine("  \"direction\": \"Buy\", \"Sell\", or \"None\",");
        sb.AppendLine("  \"stopLossPrice\": stop loss price as decimal,");
        sb.AppendLine("  \"takeProfitPrice\": target price as decimal,");
        sb.AppendLine("  \"bestEntryPrice\": optimal entry price as decimal,");
        sb.AppendLine("  \"orderType\": \"Market\", \"Limit\", or \"Stop\" (IMPORTANT: This must be accurate based on bestEntryPrice vs currentPrice),");
        sb.AppendLine("  \"timeToBestEntry\": \"estimated time until best entry price is reached\",");
        sb.AppendLine("  \"validityPeriod\": \"how long this recommendation is valid for\",");
        sb.AppendLine("  \"isSafeToEnterAtCurrentPrice\": true or false,");
        sb.AppendLine("  \"currentEntryReason\": \"explanation of why it's safe or unsafe to enter at current price\",");
        sb.AppendLine("  \"riskLevel\": \"Low\", \"Medium\", \"High\", or \"Very High\",");
        sb.AppendLine("  \"factors\": [\"list\", \"of\", \"factors\"],");
        sb.AppendLine("  \"summary\": \"brief summary of analysis\",");
        sb.AppendLine("  \"sources\": [\"list\", \"of\", \"sources\"],");
        sb.AppendLine("  \"inOutPlay\": {");
        sb.AppendLine("    \"available\": true or false,");
        sb.AppendLine("    \"direction\": \"Buy\" or \"Sell\",");
        sb.AppendLine("    \"entryPrice\": decimal price for quick entry,");
        sb.AppendLine("    \"stopLoss\": decimal price for quick stop loss,");
        sb.AppendLine("    \"takeProfit\": decimal price for quick take profit,");
        sb.AppendLine("    \"timeframe\": \"expected time in market (e.g., '5-15 minutes')\",");
        sb.AppendLine("    \"reason\": \"brief explanation of the quick trade setup\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("IMPORTANT RULES FOR ORDER TYPE:");
        sb.AppendLine("1. For BUY orders:");
        sb.AppendLine("   - If bestEntryPrice < currentPrice: Use \"Limit\" (waiting for price to drop)");
        sb.AppendLine("   - If bestEntryPrice > currentPrice: Use \"Stop\" (waiting for breakout confirmation)");
        sb.AppendLine("   - If bestEntryPrice = currentPrice: Use \"Market\" (immediate execution)");
        sb.AppendLine("2. For SELL orders:");
        sb.AppendLine("   - If bestEntryPrice > currentPrice: Use \"Limit\" (waiting for price to rise)");
        sb.AppendLine("   - If bestEntryPrice < currentPrice: Use \"Stop\" (waiting for breakdown confirmation)");
        sb.AppendLine("   - If bestEntryPrice = currentPrice: Use \"Market\" (immediate execution)");
        sb.AppendLine();

        sb.AppendLine("Rules for the analysis:");
        sb.AppendLine("1. Only recommend a trade if there is a clear setup with a good risk-reward ratio (at least 1.5:1)");
        sb.AppendLine("2. If there's no clear trade opportunity, set direction to \"None\"");
        sb.AppendLine("3. Focus primarily on daily and 4-hour timeframes for trend direction");
        sb.AppendLine("4. Use 1-hour timeframe for entry timing");
        sb.AppendLine("5. Consider 5-minute data only as a reference for very short-term price action - DO NOT base main decisions on it");
        sb.AppendLine("6. Set isSafeToEnterAtCurrentPrice to true only if entering at current price is acceptable");
        sb.AppendLine("7. For riskLevel: \"Low\" for strong confirmation, \"Medium\" for standard trades, \"High\" for less confirmation, \"Very High\" for counter-trend trades");
        sb.AppendLine();

        sb.AppendLine("For inOutPlay: Only set available=true if ALL of these conditions are met:");
        sb.AppendLine("1. Clear price action setup");
        sb.AppendLine("2. Tight stop loss (typically 5-10 pips for major pairs)");
        sb.AppendLine("3. Reward:risk ratio at least 1.5:1");
        sb.AppendLine("4. Strong momentum in the intended direction");
        sb.AppendLine("5. No major economic news expected in the next 30 minutes");
        sb.AppendLine("6. Price is at a key level");
        
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
            ModelUsed = analysis.ModelUsed,
            ModelReasoning = analysis.ModelReasoning, // Add the reasoning to the recommendation
            InOutPlay = analysis.InOutPlay
        };
        
        return recommendation;
    }

    /// <summary>
    /// Cleans up potentially malformed JSON content
    /// </summary>
    private string CleanupJsonContent(string content)
    {
        try
        {
            // Remove any leading/trailing whitespace
            content = content.Trim();
            
            // If the content starts with "REASONING" or similar, try to find the actual JSON
            if (!content.StartsWith("{"))
            {
                var jsonStart = content.IndexOf('{');
                if (jsonStart >= 0)
                {
                    content = content.Substring(jsonStart);
                }
            }
            
            // Find the outermost complete JSON object
            var stack = new Stack<int>();
            var start = -1;
            var end = -1;
            
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    if (stack.Count == 0)
                    {
                        start = i;
                    }
                    stack.Push(i);
                }
                else if (content[i] == '}')
                {
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                        if (stack.Count == 0)
                        {
                            end = i;
                            break;
                        }
                    }
                }
            }
            
            if (start >= 0 && end > start)
            {
                content = content.Substring(start, end - start + 1);
            }
            
            // Remove any trailing commas before closing braces or brackets
            content = System.Text.RegularExpressions.Regex.Replace(content, @",(\s*[}\]])", "$1");
            
            // Ensure all property names are properly quoted
            content = System.Text.RegularExpressions.Regex.Replace(content, @"([{,]\s*)(\w+)(\s*:)", "$1\"$2\"$3");
            
            // Fix any unescaped quotes within string values
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @"(?<=:\s*""|,\s*"")[^""\[\]{}]*(?="")", 
                m => m.Value.Replace("\"", "\\\"")
            );
            
            // Fix any missing quotes around string values
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @":\s*([^""\d\[\]{},\s][^,\[\]{}}]*?)(?=,|\s*[}\]])", 
                ": \"$1\""
            );
            
            _logger.LogDebug("Cleaned JSON content: {Content}", content);
            
            // Validate the JSON structure
            using (JsonDocument.Parse(content))
            {
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up JSON content");
            throw new InvalidOperationException("Failed to clean up malformed JSON", ex);
        }
    }

    #region JSON Converters
    
    /// <summary>
    /// Custom JSON converter that can handle boolean values represented as strings
    /// </summary>
    private class FlexibleBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.String:
                    var stringValue = reader.GetString()?.ToLower().Trim();
                    return stringValue switch
                    {
                        "true" or "yes" or "1" or "on" => true,
                        "false" or "no" or "0" or "off" => false,
                        _ => false // Default to false for unrecognized values
                    };
                case JsonTokenType.Number:
                    return reader.GetInt32() != 0;
                default:
                    return false;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
    
    #endregion

    #region API Response Classes
    
    /// <summary>
    /// Response structure from the Perplexity/OpenRouter API.
    /// </summary>
    private class PerplexityResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
        public string model { get; set; } = string.Empty;
        public string reasoning { get; set; } = string.Empty; // Store extracted reasoning
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
        [JsonPropertyName("sentiment")]
        public string sentiment { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public decimal confidence { get; set; }

        [JsonPropertyName("currentPrice")]
        public decimal currentPrice { get; set; }

        [JsonPropertyName("direction")]
        public string direction { get; set; } = string.Empty;

        [JsonPropertyName("stopLossPrice")]
        public decimal stopLossPrice { get; set; }

        [JsonPropertyName("takeProfitPrice")]
        public decimal takeProfitPrice { get; set; }

        [JsonPropertyName("bestEntryPrice")]
        public decimal bestEntryPrice { get; set; }

        [JsonPropertyName("orderType")]
        public string orderType { get; set; } = "Market";

        [JsonPropertyName("timeToBestEntry")]
        public string timeToBestEntry { get; set; } = string.Empty;

        [JsonPropertyName("validityPeriod")]
        public string validityPeriod { get; set; } = "24 hours";

        [JsonPropertyName("isSafeToEnterAtCurrentPrice")]
        [JsonConverter(typeof(FlexibleBooleanConverter))]
        public bool isSafeToEnterAtCurrentPrice { get; set; }

        [JsonPropertyName("currentEntryReason")]
        public string currentEntryReason { get; set; } = string.Empty;

        [JsonPropertyName("riskLevel")]
        public string riskLevel { get; set; } = "Medium";

        [JsonPropertyName("factors")]
        public List<string>? factors { get; set; }

        [JsonPropertyName("summary")]
        public string summary { get; set; } = string.Empty;

        [JsonPropertyName("sources")]
        public List<string>? sources { get; set; }

        [JsonPropertyName("inOutPlay")]
        public InOutPlayData? inOutPlay { get; set; }
    }

    /// <summary>
    /// Structure for quick scalping trade opportunities
    /// </summary>
    private class InOutPlayData
    {
        [JsonPropertyName("available")]
        [JsonConverter(typeof(FlexibleBooleanConverter))]
        public bool available { get; set; }

        [JsonPropertyName("direction")]
        public string direction { get; set; } = string.Empty;

        [JsonPropertyName("entryPrice")]
        public decimal entryPrice { get; set; }

        [JsonPropertyName("stopLoss")]
        public decimal stopLoss { get; set; }

        [JsonPropertyName("takeProfit")]
        public decimal takeProfit { get; set; }

        [JsonPropertyName("timeframe")]
        public string timeframe { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string reason { get; set; } = string.Empty;
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

    /// <summary>
    /// Response structure from the OpenRouter API.
    /// </summary>
    private class OpenRouterResponse
    {
        [JsonPropertyName("id")]
        public string id { get; set; } = string.Empty;
        
        [JsonPropertyName("provider")]
        public string provider { get; set; } = string.Empty;
        
        [JsonPropertyName("model")]
        public string model { get; set; } = string.Empty;
        
        [JsonPropertyName("object")]
        public string @object { get; set; } = string.Empty;
        
        [JsonPropertyName("created")]
        public long created { get; set; }
        
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice> choices { get; set; } = new();
        
        [JsonPropertyName("system_fingerprint")]
        public string system_fingerprint { get; set; } = string.Empty;
        
        [JsonPropertyName("usage")]
        public OpenRouterUsage usage { get; set; } = new();
    }

    public class OpenRouterChoice
    {
        [JsonPropertyName("logprobs")]
        public object? logprobs { get; set; }
        
        [JsonPropertyName("finish_reason")]
        public string finish_reason { get; set; } = string.Empty;
        
        [JsonPropertyName("native_finish_reason")]
        public string native_finish_reason { get; set; } = string.Empty;
        
        [JsonPropertyName("index")]
        public int index { get; set; }
        
        [JsonPropertyName("message")]
        public OpenRouterMessage message { get; set; } = new();
    }

    public class OpenRouterMessage
    {
        [JsonPropertyName("role")]
        public string role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string content { get; set; } = string.Empty;
        
        [JsonPropertyName("refusal")]
        public object? refusal { get; set; }
    }

    public class OpenRouterUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int prompt_tokens { get; set; }
        
        [JsonPropertyName("completion_tokens")]
        public int completion_tokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int total_tokens { get; set; }
    }
    
    #endregion
}