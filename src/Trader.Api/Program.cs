using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;
using Trader.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace Trader.Api;

// Request model for API key testing
public class KeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

/// <summary>
/// Request model for setting the NewsAPI key
/// </summary>
public class NewsAPIKeyRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

// Request model for Polygon API key
public class PolygonKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

// Request model for TraderMade API key
public class TraderMadeKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

// Request model for TwelveData API key
public class TwelveDataKeyRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

/// <summary>
/// Request model for setting the OpenRouter API key
/// </summary>
public class OpenRouterKeyRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

/// <summary>
/// Request model for setting the OpenRouter model
/// </summary>
public class OpenRouterModelRequest
{
    [Required]
    public string Model { get; set; } = string.Empty;
    public bool SaveToUserSecrets { get; set; } = false;
}

// Extension method to add in-memory configuration
public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddInMemory(this IConfigurationBuilder builder, Dictionary<string, string> dictionary)
    {
        // Create a new collection with the correct nullability
        var nullableCollection = dictionary.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value));
        return builder.AddInMemoryCollection(nullableCollection);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Enable environment variable configuration and ensure user secrets are loaded
        builder.Configuration.AddEnvironmentVariables(prefix: "TRADER_");
        
        // Debug configuration sources
        foreach (var provider in ((IConfigurationRoot)builder.Configuration).Providers)
        {
            Console.WriteLine($"Configuration provider: {provider.GetType().Name}");
        }

        // Add services to the container
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Configure CORS
        builder.Services.ConfigureCors();
        
        // Configure JSON serialization options
        builder.Services.ConfigureHttpJsonOptions(options => {
            options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.SerializerOptions.WriteIndented = true;
        });
        
        // Register our services
        builder.Services.AddSingleton<PredictionService>();
        
        // Register core services
        builder.Services.AddSingleton<ForexMarketSessionService>();
        builder.Services.AddSingleton<IPositionSizingService, PositionSizingService>();
        builder.Services.AddSingleton<IMarketMoversService, MarketMoversService>();
        builder.Services.AddSingleton<IForexDataProviderFactory, ForexDataProviderFactory>();
        
        // Register data providers
        if (!string.IsNullOrEmpty(builder.Configuration["Polygon:ApiKey"]))
        {
            builder.Services.AddHttpClient<PolygonDataProvider>();
            builder.Services.AddSingleton<PolygonDataProvider>();
            Console.WriteLine("Registered Polygon data provider");
        }
        
        // Register NewsAPI data provider if API key is available
        if (!string.IsNullOrEmpty(builder.Configuration["NewsAPI:ApiKey"]) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEWSAPI_KEY")))
        {
            builder.Services.AddHttpClient<NewsAPIDataProvider>();
            builder.Services.AddSingleton<INewsDataProvider, NewsAPIDataProvider>();
            Console.WriteLine("Registered NewsAPI data provider");
        }
        
        if (!string.IsNullOrEmpty(builder.Configuration["TwelveData:ApiKey"]))
        {
            builder.Services.AddHttpClient<TwelveDataProvider>();
            builder.Services.AddSingleton<TwelveDataProvider>();
            Console.WriteLine("Registered TwelveData provider");
        }
        
        if (!string.IsNullOrEmpty(builder.Configuration["TraderMade:ApiKey"]))
        {
            builder.Services.AddHttpClient<TraderMadeDataProvider>();
            builder.Services.AddSingleton<TraderMadeDataProvider>();
            Console.WriteLine("Registered TraderMade provider");
        }
        
        // Always register the mock provider last as a fallback
        builder.Services.AddSingleton<ForexDataProvider>();
        builder.Services.AddSingleton<IForexDataProvider>(sp => sp.GetRequiredService<ForexDataProvider>());
        Console.WriteLine("Registered mock forex data provider as fallback");
        
        // Register sentiment analyzers
        if (!string.IsNullOrEmpty(builder.Configuration["OpenRouter:ApiKey"]) || !string.IsNullOrEmpty(builder.Configuration["TRADER_OPENROUTER_API_KEY"]))
        {
            // Register OpenRouterAnalyzer if API key is available
            builder.Services.AddHttpClient<ISentimentAnalyzer, OpenRouterAnalyzer>();
            Console.WriteLine("Using OpenRouter analyzer");
        }
        else if (!string.IsNullOrEmpty(builder.Configuration["Perplexity:ApiKey"]) || !string.IsNullOrEmpty(builder.Configuration["TRADER_PERPLEXITY_API_KEY"]))
        {
            // Use Perplexity if OpenRouter is not available but Perplexity is
            builder.Services.AddHttpClient<ISentimentAnalyzer, PerplexitySentimentAnalyzer>();
            Console.WriteLine("Using Perplexity sentiment analyzer");
        }
        else if (!string.IsNullOrEmpty(builder.Configuration["Polygon:ApiKey"]) || !string.IsNullOrEmpty(builder.Configuration["TraderMade:ApiKey"]))
        {
            // Register TradingViewAnalyzer if any data provider is available but no sentiment API keys
            builder.Services.AddHttpClient<ISentimentAnalyzer, TradingViewAnalyzer>()
                .ConfigureHttpClient(client => {
                    client.Timeout = TimeSpan.FromMinutes(5); // Increase timeout to 5 minutes for DeepSeek model
                });
            Console.WriteLine("Using TradingView chart analyzer");
        }
        else
        {
            throw new InvalidOperationException("No sentiment analyzer could be configured. Please provide API keys.");
        }
        
        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            
            // In development, don't require HTTPS
            app.Use(async (context, next) =>
            {
                context.Request.Scheme = "http";
                await next();
            });
        }
        else
        {
            app.UseHttpsRedirection();
        }

        // Use CORS before other middleware
        app.UseCors(CorsConfiguration.DefaultPolicy);
        
        // Add health check endpoint for Docker
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
           .WithName("HealthCheck")
           .WithOpenApi();

        // Define API endpoints
        // Endpoint to get prediction for a specific currency pair and timeframe
        app.MapGet("/api/forex/prediction/{currencyPair}/{timeframe}", 
            (string currencyPair, string timeframe, PredictionService predictionService) =>
        {
            if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
            {
                return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
            }
            
            var prediction = predictionService.GeneratePrediction(currencyPair, timeframeEnum);
            return Results.Ok(prediction);
        })
        .WithName("GetForexPrediction")
        .WithOpenApi();

        // Endpoint to get predictions for a currency pair across all timeframes
        app.MapGet("/api/forex/multi-timeframe/{currencyPair}", 
            (string currencyPair, PredictionService predictionService) =>
        {
            if (string.IsNullOrWhiteSpace(currencyPair))
            {
                return Results.BadRequest("Currency pair is required");
            }
            
            var predictions = predictionService.AnalyzeMultipleTimeframes(currencyPair);
            return Results.Ok(predictions);
        })
        .WithName("GetMultiTimeframePredictions")
        .WithOpenApi();

        // Endpoint to get historical candle data
        app.MapGet("/api/forex/candles/{symbol}/{timeframe}/{count}", 
            async (string symbol, string timeframe, int count, IForexDataProvider dataProvider, ILogger<Program> logger) =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
                {
                    return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (count <= 0 || count > 1000)
                {
                    return Results.BadRequest("Count must be between 1 and 1000");
                }
                
                var candles = await dataProvider.GetCandleDataAsync(symbol, timeframeEnum, count);
                return Results.Ok(candles);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching candle data for {Symbol} on {Timeframe} timeframe", symbol, timeframe);
                return Results.Problem($"Error fetching data: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetCandles")
        .WithOpenApi();
        
        // Endpoint to get historical candle data with specific provider
        app.MapGet("/api/forex/candles/{symbol}/{timeframe}/{count}/{provider}", 
            async (string symbol, string timeframe, int count, string provider, IForexDataProviderFactory providerFactory, ILogger<Program> logger) =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
                {
                    return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (count <= 0 || count > 1000)
                {
                    return Results.BadRequest("Count must be between 1 and 1000");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                var dataProvider = providerFactory.GetProvider(providerType);
                var candles = await dataProvider.GetCandleDataAsync(symbol, timeframeEnum, count);
                return Results.Ok(candles);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching candle data for {Symbol} on {Timeframe} timeframe with provider {Provider}", 
                    symbol, timeframe, provider);
                return Results.Problem($"Error fetching data: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetCandlesWithProvider")
        .WithOpenApi();
        
        // Endpoint for TradingView chart analysis with AI recommendations
        app.MapGet("/api/trading/analyze/{symbol}", 
            async (string symbol, ISentimentAnalyzer analyzer, ILogger<Program> logger, 
                   decimal? accountBalance, decimal? leverage, string? targetProfits) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return Results.BadRequest("Symbol is required");
                }
                
                logger.LogInformation("Analyzing {Symbol} with TradingView", symbol);
                var analysis = await analyzer.AnalyzeSentimentAsync(symbol);
                
                // Add position sizing calculations with custom parameters if provided
                if (analysis != null && analysis.CurrentPrice > 0)
                {
                    try
                    {
                        var positionSizingService = app.Services.GetRequiredService<IPositionSizingService>();
                        
                        // Parse target profits if provided
                        decimal[]? profitTargets = null;
                        if (!string.IsNullOrEmpty(targetProfits))
                        {
                            profitTargets = targetProfits.Split(',')
                                .Select(p => decimal.TryParse(p, out decimal value) ? value : 0)
                                .Where(p => p > 0)
                                .ToArray();
                        }
                        
                        analysis.PositionSizing = await positionSizingService.CalculatePositionSizingAsync(
                            symbol,
                            analysis.CurrentPrice,
                            accountBalance ?? 201m,
                            leverage ?? 1000m,
                            profitTargets,
                            analysis.TradeRecommendation,
                            analysis.StopLossPrice > 0 ? analysis.StopLossPrice : null,
                            analysis.TakeProfitPrice > 0 ? analysis.TakeProfitPrice : null);
                        
                        logger.LogInformation("Added position sizing calculations for {Symbol} with balance {Balance} and leverage {Leverage}", 
                            symbol, accountBalance ?? 201m, leverage ?? 1000m);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error calculating position sizing for {Symbol}", symbol);
                    }
                }
                
                // Log trade recommendation if available
                if (analysis != null && analysis.IsTradeRecommended)
                {
                    logger.LogInformation(
                        "Trade recommendation for {Symbol}: {Direction} at {Price}, SL: {StopLoss}, TP: {TakeProfit}, R:R {RiskReward}",
                        symbol, 
                        analysis.TradeRecommendation, 
                        analysis.CurrentPrice,
                        analysis.StopLossPrice,
                        analysis.TakeProfitPrice,
                        analysis.RiskRewardRatio);
                }
                else
                {
                    logger.LogInformation("No trade recommended for {Symbol} at this time", symbol);
                }
                
                return Results.Ok(analysis);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error analyzing {Symbol}", symbol);
                return Results.Problem($"Error analyzing chart: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("AnalyzeChart")
        .WithOpenApi();
        
        // Endpoint for TradingView chart analysis with specific data provider
        app.MapGet("/api/trading/analyze/{symbol}/{provider}", 
            async (string symbol, string provider, IForexDataProviderFactory providerFactory, 
                   IConfiguration configuration, ILoggerFactory loggerFactory, ILogger<Program> logger,
                   decimal? accountBalance, decimal? leverage, string? targetProfits) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return Results.BadRequest("Symbol is required");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                logger.LogInformation("Analyzing {Symbol} with TradingView using {Provider} data provider", symbol, provider);
                
                // Create a new HttpClient for the analyzer
                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5); // Increase timeout to 5 minutes for DeepSeek model
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                // Check for OpenRouter API key first
                var openRouterApiKey = configuration["OpenRouter:ApiKey"] ?? configuration["TRADER_OPENROUTER_API_KEY"];
                var perplexityApiKey = configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"];
                
                logger.LogInformation("Provider-specific endpoint - Available API keys: OpenRouter: {HasOpenRouter}, Perplexity: {HasPerplexity}",
                    !string.IsNullOrEmpty(openRouterApiKey),
                    !string.IsNullOrEmpty(perplexityApiKey));
                
                string analyzerType = "Perplexity"; // Default
                string model = ""; // Will store the model name
                
                if (!string.IsNullOrEmpty(openRouterApiKey))
                {
                    // Use OpenRouter
                    httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openRouterApiKey);
                    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://trader.app"); // Required by OpenRouter
                    analyzerType = "OpenRouter";
                    
                    // Get the model from configuration or use a default
                    model = configuration["OpenRouter:Model"] ?? "openrouter/auto";
                    logger.LogInformation("Provider-specific endpoint using OpenRouter with model: {Model}", model);
                }
                else if (!string.IsNullOrEmpty(perplexityApiKey))
                {
                    // Use Perplexity
                    httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", perplexityApiKey);
                    model = "sonar-pro"; // Default Perplexity model
                    logger.LogInformation("Provider-specific endpoint using Perplexity with model: {Model}", model);
                }
                else
                {
                    return Results.BadRequest("Either OpenRouter or Perplexity API key is required for analysis");
                }
                
                logger.LogInformation("Provider-specific endpoint using {AnalyzerType} for sentiment analysis", analyzerType);
                
                // Create a configuration with the model explicitly set to ensure it's used
                var configDictionary = new Dictionary<string, string>();
                if (analyzerType == "OpenRouter")
                {
                    configDictionary["OpenRouter:ApiKey"] = openRouterApiKey;
                    configDictionary["OpenRouter:Model"] = model;
                    // Don't clear Perplexity key, just ensure OpenRouter is prioritized
                }
                else if (analyzerType == "Perplexity")
                {
                    if (!string.IsNullOrEmpty(perplexityApiKey))
                    {
                        configDictionary["Perplexity:ApiKey"] = perplexityApiKey;
                    }
                }
                
                // Create a configuration that includes our model setting
                var configBuilder = new ConfigurationBuilder()
                    .AddConfiguration(configuration)
                    .AddInMemory(configDictionary);
                var combinedConfig = configBuilder.Build();
                
                // Create a new analyzer with the specified provider and our combined configuration
                var analyzer = new TradingViewAnalyzer(
                    providerFactory,
                    httpClient,
                    combinedConfig,
                    loggerFactory.CreateLogger<TradingViewAnalyzer>(),
                    app.Services.GetRequiredService<ForexMarketSessionService>(),
                    app.Services.GetRequiredService<IPositionSizingService>(),
                    app.Services,
                    providerType);
                
                var analysis = await analyzer.AnalyzeSentimentAsync(symbol);
                
                // Add position sizing calculations with custom parameters if provided
                if (analysis != null && analysis.CurrentPrice > 0 && (accountBalance.HasValue || leverage.HasValue || !string.IsNullOrEmpty(targetProfits)))
                {
                    try
                    {
                        var positionSizingService = app.Services.GetRequiredService<IPositionSizingService>();
                        
                        // Parse target profits if provided
                        decimal[]? profitTargets = null;
                        if (!string.IsNullOrEmpty(targetProfits))
                        {
                            profitTargets = targetProfits.Split(',')
                                .Select(p => decimal.TryParse(p, out decimal value) ? value : 0)
                                .Where(p => p > 0)
                                .ToArray();
                        }
                        
                        analysis.PositionSizing = await positionSizingService.CalculatePositionSizingAsync(
                            symbol,
                            analysis.CurrentPrice,
                            accountBalance ?? 201m,
                            leverage ?? 1000m,
                            profitTargets,
                            analysis.TradeRecommendation,
                            analysis.StopLossPrice > 0 ? analysis.StopLossPrice : null,
                            analysis.TakeProfitPrice > 0 ? analysis.TakeProfitPrice : null);
                        
                        logger.LogInformation("Added position sizing calculations for {Symbol} with balance {Balance} and leverage {Leverage}", 
                            symbol, accountBalance ?? 201m, leverage ?? 1000m);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error calculating position sizing for {Symbol}", symbol);
                    }
                }
                
                // Log trade recommendation if available
                if (analysis != null && analysis.IsTradeRecommended)
                {
                    logger.LogInformation(
                        "Trade recommendation for {Symbol} using {Provider}: {Direction} at {Price}, SL: {StopLoss}, TP: {TakeProfit}, R:R {RiskReward}",
                        symbol,
                        provider,
                        analysis.TradeRecommendation, 
                        analysis.CurrentPrice,
                        analysis.StopLossPrice,
                        analysis.TakeProfitPrice,
                        analysis.RiskRewardRatio);
                }
                else
                {
                    logger.LogInformation("No trade recommended for {Symbol} using {Provider} at this time", symbol, provider);
                }
                
                return Results.Ok(analysis);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error analyzing {Symbol} with {Provider}", symbol, provider);
                return Results.Problem($"Error analyzing chart: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("AnalyzeSymbol")
        .WithOpenApi();
        
        // Endpoint to get market sentiment analysis for a currency pair
        app.MapGet("/api/forex/sentiment/{currencyPair}", 
            async (string currencyPair, ISentimentAnalyzer sentimentAnalyzer, ILogger<Program> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currencyPair))
                {
                    return Results.BadRequest("Currency pair is required");
                }
                
                var sentiment = await sentimentAnalyzer.AnalyzeSentimentAsync(currencyPair);
                return Results.Ok(sentiment);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error analyzing sentiment for {CurrencyPair}", currencyPair);
                return Results.Problem($"Error analyzing sentiment: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetForexSentiment")
        .WithOpenApi();
        
        // Endpoint to get recommended trading opportunities based on AI analysis
        app.MapGet("/api/trading/recommendations", 
            async (int? count, string? provider, ISentimentAnalyzer analyzer, ILogger<Program> logger,
                   decimal? accountBalance, decimal? leverage, string? targetProfits) =>
        {
            try
            {
                // Limit to reasonable values, default to 3
                var pairCount = count.HasValue && count.Value > 0 && count.Value <= 5 ? count.Value : 3;
                
                logger.LogInformation("Getting {Count} trading recommendations using provider {Provider}", pairCount, provider ?? "default");
                var recommendations = await analyzer.GetTradingRecommendationsAsync(pairCount, provider);
                
                // Add position sizing calculations with custom parameters if provided
                if (recommendations != null && recommendations.Any() && 
                    (accountBalance.HasValue || leverage.HasValue || !string.IsNullOrEmpty(targetProfits)))
                {
                    try
                    {
                        var positionSizingService = app.Services.GetRequiredService<IPositionSizingService>();
                        
                        // Parse target profits if provided
                        decimal[]? profitTargets = null;
                        if (!string.IsNullOrEmpty(targetProfits))
                        {
                            profitTargets = targetProfits.Split(',')
                                .Select(p => decimal.TryParse(p, out decimal value) ? value : 0)
                                .Where(p => p > 0)
                                .ToArray();
                        }
                        
                        foreach (var recommendation in recommendations)
                        {
                            if (recommendation.CurrentPrice > 0)
                            {
                                recommendation.PositionSizing = await positionSizingService.CalculatePositionSizingAsync(
                                    recommendation.CurrencyPair,
                                    recommendation.CurrentPrice,
                                    accountBalance ?? 201m,
                                    leverage ?? 1000m,
                                    profitTargets,
                                    recommendation.Direction,
                                    recommendation.StopLossPrice > 0 ? recommendation.StopLossPrice : null,
                                    recommendation.TakeProfitPrice > 0 ? recommendation.TakeProfitPrice : null);
                            }
                        }
                        
                        logger.LogInformation("Added position sizing calculations for recommendations with balance {Balance} and leverage {Leverage}", 
                            accountBalance ?? 201m, leverage ?? 1000m);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error calculating position sizing for recommendations");
                    }
                }
                
                // The analyzer now always returns at least one recommendation, even if it's a fallback
                // with low confidence, so we don't need to check for empty recommendations
                return Results.Ok(recommendations);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting trading recommendations");
                return Results.Problem($"Error getting recommendations: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetTradingRecommendations")
        .WithOpenApi();
        
        // Endpoint to set OpenRouter API key
        app.MapPost("/api/diagnostics/set-openrouter-key", 
            async (OpenRouterKeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Test if the key is valid by making a simple API call
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
                    httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://trader.app");
                    
                    var testUrl = "https://openrouter.ai/api/v1/auth/key";
                    
                    logger.LogInformation("Testing OpenRouter API with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("OpenRouter API response: Status: {Status}, Content: {Content}", 
                        response.StatusCode, responseContent);
                        
                    var isValid = response.IsSuccessStatusCode;
                    var statusMessage = isValid 
                        ? "Valid API key" 
                        : $"Invalid API key: {response.StatusCode}. Response: {responseContent}";
                    
                    // Only save the key if it's valid and user wants to save
                    if (isValid && request.SaveToUserSecrets)
                    {
                        try
                        {
                            // Save to user secrets
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            
                            // Read existing secrets if they exist
                            Dictionary<string, string> secrets;
                            if (File.Exists(secretsFilePath))
                            {
                                var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                                secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                                    ?? new Dictionary<string, string>();
                            }
                            else
                            {
                                secrets = new Dictionary<string, string>();
                            }
                            
                            // Add/update the OpenRouter API key
                            secrets["OpenRouter:ApiKey"] = request.ApiKey;
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid OpenRouter API key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file entry
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            string envContent = "";
                            
                            if (File.Exists(envFilePath))
                            {
                                envContent = await File.ReadAllTextAsync(envFilePath);
                            }
                            
                            // Update or add the OpenRouter API key
                            var envVarName = "TRADER_OPENROUTER_API_KEY";
                            if (envContent.Contains(envVarName))
                            {
                                // Replace existing line
                                var lines = envContent.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith(envVarName))
                                    {
                                        lines[i] = $"{envVarName}={request.ApiKey}";
                                        break;
                                    }
                                }
                                envContent = string.Join('\n', lines);
                            }
                            else
                            {
                                // Add new line
                                if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                                {
                                    envContent += '\n';
                                }
                                envContent += $"{envVarName}={request.ApiKey}\n";
                            }
                            
                            await File.WriteAllTextAsync(envFilePath, envContent);
                            logger.LogInformation("Also saved OpenRouter API key to .env file at {Path}", envFilePath);
                            
                            // Update appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Check if OpenRouter section exists
                                    if (json.Contains("\"OpenRouter\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                    }
                                    else
                                    {
                                        // Insert OpenRouter section before the closing brace
                                        var lastBrace = json.LastIndexOf('}');
                                        if (lastBrace > 0)
                                        {
                                            var updated = json.Insert(lastBrace, $",\n  \"OpenRouter\": {{\n    \"ApiKey\": \"{request.ApiKey}\"\n  }}\n");
                                            await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        }
                                    }
                                    
                                    logger.LogInformation("Updated OpenRouter API key in {Path}", appSettingsDevPath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save OpenRouter API key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing OpenRouter API key");
                    return Results.Problem($"Error testing API key: {ex.Message}");
                }
            });
        
        // Endpoint to set Polygon.io API key
        app.MapPost("/api/diagnostics/set-polygon-key", 
            async (PolygonKeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Test if the key is valid by making a simple API call with historical data (free tier limitation)
                    var httpClient = new HttpClient();
                    var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
                    var to = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
                    var testUrl = $"https://api.polygon.io/v2/aggs/ticker/C:EURUSD/range/1/day/{from}/{to}?adjusted=true&apiKey={request.ApiKey}";
                    
                    logger.LogInformation("Testing Polygon API with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("Polygon API response: Status: {Status}, Content: {Content}", 
                        response.StatusCode, responseContent);
                        
                    var isValid = response.IsSuccessStatusCode;
                    var statusMessage = isValid 
                        ? "Valid API key" 
                        : $"Invalid API key: {response.StatusCode}. Response: {responseContent}";
                    
                    // Only save the key if it's valid and user wants to save
                    if (isValid && request.SaveToUserSecrets)
                    {
                        try
                        {
                            // Save to user secrets
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            
                            // Read existing secrets if they exist
                            Dictionary<string, string> secrets;
                            if (File.Exists(secretsFilePath))
                            {
                                var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                                secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                                    ?? new Dictionary<string, string>();
                            }
                            else
                            {
                                secrets = new Dictionary<string, string>();
                            }
                            
                            // Add/update the Polygon API key
                            secrets["Polygon:ApiKey"] = request.ApiKey;
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid Polygon API key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file entry
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            string envContent = "";
                            
                            if (File.Exists(envFilePath))
                            {
                                envContent = await File.ReadAllTextAsync(envFilePath);
                            }
                            
                            // Update or add the Polygon API key
                            var envVarName = "TRADER_POLYGON_API_KEY";
                            if (envContent.Contains(envVarName))
                            {
                                // Replace existing line
                                var lines = envContent.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith(envVarName))
                                    {
                                        lines[i] = $"{envVarName}={request.ApiKey}";
                                        break;
                                    }
                                }
                                envContent = string.Join('\n', lines);
                            }
                            else
                            {
                                // Add new line
                                if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                                {
                                    envContent += '\n';
                                }
                                envContent += $"{envVarName}={request.ApiKey}\n";
                            }
                            
                            await File.WriteAllTextAsync(envFilePath, envContent);
                            logger.LogInformation("Also saved Polygon API key to .env file at {Path}", envFilePath);
                            
                            // Update appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Check if Polygon section exists
                                    if (json.Contains("\"Polygon\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                    }
                                    else
                                    {
                                        // Insert Polygon section before the closing brace
                                        var lastBrace = json.LastIndexOf('}');
                                        if (lastBrace > 0)
                                        {
                                            var updated = json.Insert(lastBrace, $",\n  \"Polygon\": {{\n    \"ApiKey\": \"{request.ApiKey}\"\n  }}\n");
                                            await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        }
                                    }
                                    
                                    logger.LogInformation("Updated Polygon API key in {Path}", appSettingsDevPath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save Polygon API key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing Polygon API key");
                    return Results.Problem($"Error testing API key: {ex.Message}");
                }
            });
        
        // Endpoint to set TraderMade API key
        app.MapPost("/api/diagnostics/set-tradermade-key", 
            async (TraderMadeKeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Test if the key is valid by making a simple API call
                    var httpClient = new HttpClient();
                    var testUrl = $"https://marketdata.tradermade.com/api/v1/timeseries?currency=EURUSD&api_key={request.ApiKey}&start_date={DateTime.UtcNow.AddDays(-7):yyyy-MM-dd}&end_date={DateTime.UtcNow:yyyy-MM-dd}&interval=daily";
                    
                    logger.LogInformation("Testing TraderMade API with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("TraderMade API response: Status: {Status}, Content: {Content}", 
                        response.StatusCode, responseContent);
                        
                    var isValid = response.IsSuccessStatusCode;
                    var statusMessage = isValid 
                        ? "Valid API key" 
                        : $"Invalid API key: {response.StatusCode}. Response: {responseContent}";
                    
                    // Only save the key if it's valid and user wants to save
                    if (isValid && request.SaveToUserSecrets)
                    {
                        try
                        {
                            // Save to user secrets
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            
                            // Read existing secrets if they exist
                            Dictionary<string, string> secrets;
                            if (File.Exists(secretsFilePath))
                            {
                                var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                                secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                                    ?? new Dictionary<string, string>();
                            }
                            else
                            {
                                secrets = new Dictionary<string, string>();
                            }
                            
                            // Add/update the TraderMade API key
                            secrets["TraderMade:ApiKey"] = request.ApiKey;
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid TraderMade API key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file entry
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            string envContent = "";
                            
                            if (File.Exists(envFilePath))
                            {
                                envContent = await File.ReadAllTextAsync(envFilePath);
                            }
                            
                            // Update or add the TraderMade API key
                            var envVarName = "TRADER_TRADERMADE_API_KEY";
                            if (envContent.Contains(envVarName))
                            {
                                // Replace existing line
                                var lines = envContent.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith(envVarName))
                                    {
                                        lines[i] = $"{envVarName}={request.ApiKey}";
                                        break;
                                    }
                                }
                                envContent = string.Join('\n', lines);
                            }
                            else
                            {
                                // Add new line
                                if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                                {
                                    envContent += '\n';
                                }
                                envContent += $"{envVarName}={request.ApiKey}\n";
                            }
                            
                            await File.WriteAllTextAsync(envFilePath, envContent);
                            logger.LogInformation("Also saved TraderMade API key to .env file at {Path}", envFilePath);
                            
                            // Update appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Check if TraderMade section exists
                                    if (json.Contains("\"TraderMade\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                    }
                                    else
                                    {
                                        // Insert TraderMade section before the closing brace
                                        var lastBrace = json.LastIndexOf('}');
                                        if (lastBrace > 0)
                                        {
                                            var updated = json.Insert(lastBrace, $",\n  \"TraderMade\": {{\n    \"ApiKey\": \"{request.ApiKey}\"\n  }}\n");
                                            await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        }
                                    }
                                    
                                    logger.LogInformation("Updated TraderMade API key in {Path}", appSettingsDevPath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save TraderMade API key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing TraderMade API key");
                    return Results.Problem($"Error testing API key: {ex.Message}");
                }
            });
        
        // Endpoint to set TwelveData API key
        app.MapPost("/api/diagnostics/set-twelvedata-key", 
            async (TwelveDataKeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Test if the key is valid by making a simple API call
                    var httpClient = new HttpClient();
                    var testUrl = $"https://api.twelvedata.com/time_series?symbol=EUR/USD&interval=1day&outputsize=5&apikey={request.ApiKey}";
                    
                    logger.LogInformation("Testing TwelveData API with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("TwelveData API response: Status: {Status}, Content: {Content}", 
                        response.StatusCode, responseContent);
                        
                    var isValid = response.IsSuccessStatusCode;
                    var statusMessage = isValid 
                        ? "Valid API key" 
                        : $"Invalid API key: {response.StatusCode}. Response: {responseContent}";
                    
                    // Only save the key if it's valid and user wants to save
                    if (isValid && request.SaveToUserSecrets)
                    {
                        try
                        {
                            // Save to user secrets
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            
                            // Read existing secrets if they exist
                            Dictionary<string, string> secrets;
                            if (File.Exists(secretsFilePath))
                            {
                                var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                                secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                                    ?? new Dictionary<string, string>();
                            }
                            else
                            {
                                secrets = new Dictionary<string, string>();
                            }
                            
                            // Add/update the TwelveData API key
                            secrets["TwelveData:ApiKey"] = request.ApiKey;
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid TwelveData API key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file entry
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            string envContent = "";
                            
                            if (File.Exists(envFilePath))
                            {
                                envContent = await File.ReadAllTextAsync(envFilePath);
                            }
                            
                            // Update or add the TwelveData API key
                            var envVarName = "TRADER_TWELVEDATA_API_KEY";
                            if (envContent.Contains(envVarName))
                            {
                                // Replace existing line
                                var lines = envContent.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith(envVarName))
                                    {
                                        lines[i] = $"{envVarName}={request.ApiKey}";
                                        break;
                                    }
                                }
                                envContent = string.Join('\n', lines);
                            }
                            else
                            {
                                // Add new line
                                if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                                {
                                    envContent += '\n';
                                }
                                envContent += $"{envVarName}={request.ApiKey}\n";
                            }
                            
                            await File.WriteAllTextAsync(envFilePath, envContent);
                            logger.LogInformation("Also saved TwelveData API key to .env file at {Path}", envFilePath);
                            
                            // Update appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Check if TwelveData section exists
                                    if (json.Contains("\"TwelveData\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                    }
                                    else
                                    {
                                        // Insert TwelveData section before the closing brace
                                        var lastBrace = json.LastIndexOf('}');
                                        if (lastBrace > 0)
                                        {
                                            var updated = json.Insert(lastBrace, $",\n  \"TwelveData\": {{\n    \"ApiKey\": \"{request.ApiKey}\"\n  }}\n");
                                            await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        }
                                    }
                                    
                                    logger.LogInformation("Updated TwelveData API key in {Path}", appSettingsDevPath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save TwelveData API key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing TwelveData API key");
                    return Results.Problem($"Error testing API key: {ex.Message}");
                }
            });
        
        // Endpoint to set NewsAPI key
        app.MapPost("/api/diagnostics/set-newsapi-key", 
            async (NewsAPIKeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Test if the key is valid by making a simple API call
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "TraderApp/1.0");
                    var testUrl = $"https://newsapi.org/v2/everything?q=forex&language=en&pageSize=1&apiKey={request.ApiKey}";
                    
                    logger.LogInformation("Testing NewsAPI with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    var response = await httpClient.GetAsync(testUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("NewsAPI response: Status: {Status}, Content: {Content}", 
                        response.StatusCode, responseContent);
                        
                    var isValid = response.IsSuccessStatusCode;
                    var statusMessage = isValid 
                        ? "Valid API key" 
                        : $"Invalid API key: {response.StatusCode}. Response: {responseContent}";
                    
                    // Set the environment variable regardless of validation result
                    Environment.SetEnvironmentVariable("NEWSAPI_KEY", request.ApiKey, EnvironmentVariableTarget.Process);
                    
                    // Only save the key if it's valid and user wants to save
                    if (isValid && request.SaveToUserSecrets)
                    {
                        try
                        {
                            // Save to user secrets
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            
                            // Read existing secrets if they exist
                            Dictionary<string, string> secrets;
                            if (File.Exists(secretsFilePath))
                            {
                                var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                                secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                                    ?? new Dictionary<string, string>();
                            }
                            else
                            {
                                secrets = new Dictionary<string, string>();
                            }
                            
                            // Add/update the NewsAPI key
                            secrets["NewsAPI:ApiKey"] = request.ApiKey;
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid NewsAPI key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file entry
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            string envContent = "";
                            
                            if (File.Exists(envFilePath))
                            {
                                envContent = await File.ReadAllTextAsync(envFilePath);
                            }
                            
                            // Update or add the NewsAPI key
                            var envVarName = "TRADER_NEWSAPI_KEY";
                            if (envContent.Contains(envVarName))
                            {
                                // Replace existing line
                                var lines = envContent.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith(envVarName))
                                    {
                                        lines[i] = $"{envVarName}={request.ApiKey}";
                                        break;
                                    }
                                }
                                envContent = string.Join('\n', lines);
                            }
                            else
                            {
                                // Add new line
                                if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                                {
                                    envContent += '\n';
                                }
                                envContent += $"{envVarName}={request.ApiKey}\n";
                            }
                            
                            await File.WriteAllTextAsync(envFilePath, envContent);
                            logger.LogInformation("Also saved NewsAPI key to .env file at {Path}", envFilePath);
                            
                            // Update appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Check if NewsAPI section exists
                                    if (json.Contains("\"NewsAPI\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                    }
                                    else
                                    {
                                        // Insert NewsAPI section before the closing brace
                                        var lastBrace = json.LastIndexOf('}');
                                        if (lastBrace > 0)
                                        {
                                            var updated = json.Insert(lastBrace, $",\n  \"NewsAPI\": {{\n    \"ApiKey\": \"{request.ApiKey}\"\n  }}\n");
                                            await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        }
                                    }
                                    
                                    logger.LogInformation("Updated NewsAPI key in {Path}", appSettingsDevPath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save NewsAPI key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing NewsAPI key");
                    return Results.Problem($"Error testing API key: {ex.Message}");
                }
            });
        
        // Endpoint to set OpenRouter model
        app.MapPost("/api/diagnostics/set-openrouter-model", 
            async (OpenRouterModelRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.Model))
                {
                    return Results.BadRequest("Model name cannot be empty");
                }
                
                try
                {
                    // Save to user secrets
                    var userSecretsId = "trader-app-secrets-id";
                    var userSecretsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Microsoft", "UserSecrets", userSecretsId);
                            
                    Directory.CreateDirectory(userSecretsPath);
                    
                    var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                    
                    // Read existing secrets if they exist
                    Dictionary<string, string> secrets;
                    if (File.Exists(secretsFilePath))
                    {
                        var existingJson = await File.ReadAllTextAsync(secretsFilePath);
                        secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson) 
                            ?? new Dictionary<string, string>();
                    }
                    else
                    {
                        secrets = new Dictionary<string, string>();
                    }
                    
                    // Add/update the OpenRouter model
                    secrets["OpenRouter:Model"] = request.Model;
                    
                    await File.WriteAllTextAsync(
                        secretsFilePath, 
                        System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                    logger.LogInformation("Saved OpenRouter model to user secrets at {Path}", secretsFilePath);
                    
                    // Also create a .env file entry
                    var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                    string envContent = "";
                    
                    if (File.Exists(envFilePath))
                    {
                        envContent = await File.ReadAllTextAsync(envFilePath);
                    }
                    
                    // Update or add the OpenRouter model
                    var envVarName = "TRADER_OPENROUTER_MODEL";
                    if (envContent.Contains(envVarName))
                    {
                        // Replace existing line
                        var lines = envContent.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith(envVarName))
                            {
                                lines[i] = $"{envVarName}={request.Model}";
                                break;
                            }
                        }
                        envContent = string.Join('\n', lines);
                    }
                    else
                    {
                        // Add new line
                        if (!string.IsNullOrEmpty(envContent) && !envContent.EndsWith('\n'))
                        {
                            envContent += '\n';
                        }
                        envContent += $"{envVarName}={request.Model}\n";
                    }
                    
                    await File.WriteAllTextAsync(envFilePath, envContent);
                    logger.LogInformation("Also saved OpenRouter model to .env file at {Path}", envFilePath);
                    
                    // Update appsettings.Development.json if it exists
                    var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                    if (File.Exists(appSettingsDevPath))
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(appSettingsDevPath);
                            
                            // Check if OpenRouter section exists
                            if (json.Contains("\"OpenRouter\": {"))
                            {
                                var updated = System.Text.RegularExpressions.Regex.Replace(
                                    json,
                                    "\"Model\":\\s*\"[^\"]*\"",
                                    $"\"Model\": \"{request.Model}\"");
                                    
                                await File.WriteAllTextAsync(appSettingsDevPath, updated);
                            }
                            else
                            {
                                // Insert OpenRouter section before the closing brace
                                var lastBrace = json.LastIndexOf('}');
                                if (lastBrace > 0)
                                {
                                    var updated = json.Insert(lastBrace, $",\n  \"OpenRouter\": {{\n    \"Model\": \"{request.Model}\"\n  }}\n");
                                    await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                }
                            }
                            
                            logger.LogInformation("Updated OpenRouter model in {Path}", appSettingsDevPath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        Message = $"OpenRouter model set to: {request.Model}",
                        SavedToUserSecrets = true
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error setting OpenRouter model");
                    return Results.Problem($"Error setting model: {ex.Message}");
                }
            });
        
        // Diagnostic endpoint to check configuration
        app.MapGet("/api/diagnostics/config", 
            (IConfiguration configuration, ILogger<Program> logger) =>
            {
                var perplexityApiKey = configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"];
                var polygonApiKey = configuration["Polygon:ApiKey"] ?? configuration["TRADER_POLYGON_API_KEY"];
                var tradermadeApiKey = configuration["TraderMade:ApiKey"] ?? configuration["TRADER_TRADERMADE_API_KEY"];
                var twelvedataApiKey = configuration["TwelveData:ApiKey"] ?? configuration["TRADER_TWELVEDATA_API_KEY"];
                var openrouterApiKey = configuration["OpenRouter:ApiKey"] ?? configuration["TRADER_OPENROUTER_API_KEY"];
                var newsapiKey = configuration["NewsAPI:ApiKey"] ?? Environment.GetEnvironmentVariable("NEWSAPI_KEY");
                var openrouterModel = configuration["OpenRouter:Model"] ?? "anthropic/claude-3-opus:beta"; // Default model
                
                var hasPerplexity = !string.IsNullOrEmpty(perplexityApiKey);
                var hasPolygon = !string.IsNullOrEmpty(polygonApiKey);
                var hasTraderMade = !string.IsNullOrEmpty(tradermadeApiKey);
                var hasTwelveData = !string.IsNullOrEmpty(twelvedataApiKey);
                var hasOpenRouter = !string.IsNullOrEmpty(openrouterApiKey);
                var hasNewsAPI = !string.IsNullOrEmpty(newsapiKey);
                
                logger.LogInformation("Configuration check: Perplexity API key: {HasKey}, Polygon API key: {HasPolygon}, TraderMade API key: {HasTraderMade}, TwelveData API key: {HasTwelveData}, OpenRouter API key: {HasOpenRouter}, NewsAPI key: {HasNewsAPI}",
                    hasPerplexity, hasPolygon, hasTraderMade, hasTwelveData, hasOpenRouter, hasNewsAPI);
                
                var defaultProvider = hasPolygon ? "Polygon" : hasTraderMade ? "TraderMade" : hasTwelveData ? "TwelveData" : "Mock";
                var defaultSentimentAnalyzer = hasOpenRouter ? "OpenRouter" : hasPerplexity ? "Perplexity" : "None";
                
                return Results.Ok(new
                {
                    HasPerplexityApiKey = hasPerplexity,
                    HasPolygonApiKey = hasPolygon,
                    HasTraderMadeApiKey = hasTraderMade,
                    HasTwelveDataApiKey = hasTwelveData,
                    HasOpenRouterApiKey = hasOpenRouter,
                    HasNewsAPIKey = hasNewsAPI,
                    DefaultDataProvider = defaultProvider,
                    DefaultSentimentAnalyzer = defaultSentimentAnalyzer,
                    OpenRouterModel = openrouterModel,
                    PerplexityKeyPrefix = hasPerplexity && !string.IsNullOrEmpty(perplexityApiKey) && perplexityApiKey.Length > 4 
                        ? perplexityApiKey.Substring(0, 4) + "..." 
                        : null,
                    PolygonKeyPrefix = hasPolygon && !string.IsNullOrEmpty(polygonApiKey) && polygonApiKey.Length > 4 
                        ? polygonApiKey.Substring(0, 4) + "..." 
                        : null,
                    TraderMadeKeyPrefix = hasTraderMade && !string.IsNullOrEmpty(tradermadeApiKey) && tradermadeApiKey.Length > 4 
                        ? tradermadeApiKey.Substring(0, 4) + "..." 
                        : null,
                    TwelveDataKeyPrefix = hasTwelveData && !string.IsNullOrEmpty(twelvedataApiKey) && twelvedataApiKey.Length > 4 
                        ? twelvedataApiKey.Substring(0, 4) + "..." 
                        : null,
                    OpenRouterKeyPrefix = hasOpenRouter && !string.IsNullOrEmpty(openrouterApiKey) && openrouterApiKey.Length > 4 
                        ? openrouterApiKey.Substring(0, 4) + "..." 
                        : null,
                    NewsAPIKeyPrefix = hasNewsAPI && !string.IsNullOrEmpty(newsapiKey) && newsapiKey.Length > 4 
                        ? newsapiKey.Substring(0, 4) + "..." 
                        : null
                });
            })
        .WithName("CheckConfiguration")
        .WithOpenApi();

        // Market Movers Endpoints
        
        // Get top forex market movers
        app.MapGet("/api/market-movers/forex", async (
            IMarketMoversService marketMoversService,
            ILogger<Program> logger,
            int count = 10,
            string timeframe = "Hours1",
            string provider = "TwelveData") =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
                {
                    return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                if (count <= 0 || count > 25)
                {
                    return Results.BadRequest("Count must be between 1 and 25");
                }
                
                var marketMovers = await marketMoversService.GetTopForexMoversAsync(count, timeframeEnum, providerType);
                return Results.Ok(marketMovers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting top forex movers");
                return Results.Problem($"Error getting top forex movers: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetTopForexMovers")
        .WithOpenApi(operation => {
            operation.Description = "Get top forex market movers. Uses smart pair selection and data caching to minimize API calls. First request analyzes ~10-15 pairs; subsequent requests use cached data when possible.";
            return operation;
        });
        
        // Get top crypto market movers
        app.MapGet("/api/market-movers/crypto", async (
            IMarketMoversService marketMoversService,
            ILogger<Program> logger,
            int count = 10,
            string timeframe = "Hours1",
            string provider = "TwelveData") =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
                {
                    return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                if (count <= 0 || count > 25)
                {
                    return Results.BadRequest("Count must be between 1 and 25");
                }
                
                var marketMovers = await marketMoversService.GetTopCryptoMoversAsync(count, timeframeEnum, providerType);
                return Results.Ok(marketMovers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting top crypto movers");
                return Results.Problem($"Error getting top crypto movers: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetTopCryptoMovers")
        .WithOpenApi(operation => {
            operation.Description = "Get top crypto market movers. Uses smart pair selection and data caching to minimize API calls. First request analyzes ~10-15 pairs; subsequent requests use cached data when possible.";
            return operation;
        });
        
        // Get top forex market movers with EMA filters
        app.MapGet("/api/market-movers/forex/ema-filtered", async (
            IMarketMoversService marketMoversService,
            ILogger<Program> logger,
            int count = 10,
            string shortTermTimeframe = "Hours1",
            string longTermTimeframe = "Day1",
            string provider = "TraderMade",
            bool includeNews = false) =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(shortTermTimeframe, true, out var shortTermTimeframeEnum))
                {
                    return Results.BadRequest($"Invalid short-term timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<ChartTimeframe>(longTermTimeframe, true, out var longTermTimeframeEnum))
                {
                    return Results.BadRequest($"Invalid long-term timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                if (count <= 0 || count > 25)
                {
                    return Results.BadRequest("Count must be between 1 and 25");
                }
                
                // Get top forex movers
                var marketMovers = await marketMoversService.GetTopForexMoversAsync(count, shortTermTimeframeEnum, providerType);
                
                // Apply EMA filters
                var filteredMarketMovers = await marketMoversService.ApplyEmaFiltersAsync(
                    marketMovers, shortTermTimeframeEnum, longTermTimeframeEnum, providerType);
                
                // Generate trade recommendations
                var marketMoversWithRecommendations = await marketMoversService.GenerateTradeRecommendationsAsync(filteredMarketMovers);
                
                // Enrich with news if requested
                if (includeNews)
                {
                    marketMoversWithRecommendations = await marketMoversService.EnrichWithNewsAsync(marketMoversWithRecommendations);
                }
                
                return Results.Ok(marketMoversWithRecommendations);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting top forex movers with EMA filters");
                return Results.Problem($"Error getting top forex movers with EMA filters: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetTopForexMoversWithEmaFilters")
        .WithOpenApi(operation => {
            operation.Description = "Get top forex market movers with EMA filters applied. Uses batch data fetching, smart pair selection, and time-based caching to minimize API calls. First request uses ~12-20 calls; subsequent requests use significantly fewer.";
            return operation;
        });
        
        // Endpoint to get news for a specific currency pair
        app.MapGet("/api/news/symbol/{symbol}", 
            async (string symbol, [FromServices] INewsDataProvider newsDataProvider, [FromServices] ILogger<Program> logger, int? count = null) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(symbol))
                    {
                        return Results.BadRequest("Symbol is required");
                    }
                    
                    var newsCount = count.HasValue && count.Value > 0 && count.Value <= 25 ? count.Value : 10;
                    
                    logger.LogInformation("Getting news for symbol {Symbol}", symbol);
                    var news = await newsDataProvider.GetSymbolNewsAsync(symbol, newsCount);
                    return Results.Ok(news);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error getting news for symbol {Symbol}", symbol);
                    return Results.Problem($"Error getting news: {ex.Message}", statusCode: 500);
                }
            })
            .WithName("GetSymbolNews")
            .WithOpenApi();
            
        // Endpoint to get economic news for a specific region
        app.MapGet("/api/news/region/{region}", 
            async (string region, [FromServices] INewsDataProvider newsDataProvider, [FromServices] ILogger<Program> logger, int? count = null) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(region))
                    {
                        return Results.BadRequest("Region is required");
                    }
                    
                    var newsCount = count.HasValue && count.Value > 0 && count.Value <= 25 ? count.Value : 10;
                    
                    logger.LogInformation("Getting economic news for region {Region}", region);
                    var news = await newsDataProvider.GetEconomicNewsAsync(region, newsCount);
                    return Results.Ok(news);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error getting economic news for region {Region}", region);
                    return Results.Problem($"Error getting news: {ex.Message}", statusCode: 500);
                }
            })
            .WithName("GetRegionNews")
            .WithOpenApi();
        
        // Get top crypto market movers with EMA filters
        app.MapGet("/api/market-movers/crypto/ema-filtered", async (
            IMarketMoversService marketMoversService,
            ILogger<Program> logger,
            int count = 10,
            string shortTermTimeframe = "Hours1",
            string longTermTimeframe = "Day1",
            string provider = "TraderMade",
            bool includeNews = false) =>
        {
            try
            {
                if (!Enum.TryParse<ChartTimeframe>(shortTermTimeframe, true, out var shortTermTimeframeEnum))
                {
                    return Results.BadRequest($"Invalid short-term timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<ChartTimeframe>(longTermTimeframe, true, out var longTermTimeframeEnum))
                {
                    return Results.BadRequest($"Invalid long-term timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
                }
                
                if (!Enum.TryParse<DataProviderType>(provider, true, out var providerType))
                {
                    return Results.BadRequest($"Invalid provider. Valid values: {string.Join(", ", Enum.GetNames<DataProviderType>())}");
                }
                
                if (count <= 0 || count > 25)
                {
                    return Results.BadRequest("Count must be between 1 and 25");
                }
                
                // Get top crypto movers
                var marketMovers = await marketMoversService.GetTopCryptoMoversAsync(count, shortTermTimeframeEnum, providerType);
                
                // Apply EMA filters
                var filteredMarketMovers = await marketMoversService.ApplyEmaFiltersAsync(
                    marketMovers, shortTermTimeframeEnum, longTermTimeframeEnum, providerType);
                
                // Generate trade recommendations
                var marketMoversWithRecommendations = await marketMoversService.GenerateTradeRecommendationsAsync(filteredMarketMovers);
                
                // Enrich with news if requested
                if (includeNews)
                {
                    marketMoversWithRecommendations = await marketMoversService.EnrichWithNewsAsync(marketMoversWithRecommendations);
                }
                
                return Results.Ok(marketMoversWithRecommendations);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting top crypto movers with EMA filters");
                return Results.Problem($"Error getting top crypto movers with EMA filters: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GetTopCryptoMoversWithEmaFilters")
        .WithOpenApi(operation => {
            operation.Description = "Get top crypto market movers with EMA filters applied. Uses batch data fetching, smart pair selection, and time-based caching to minimize API calls. First request uses ~12-20 calls; subsequent requests use significantly fewer.";
            return operation;
        });

        app.Run();
    }
}
