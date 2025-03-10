using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;
using Trader.Infrastructure.Services;

namespace Trader.Api;

// Request model for API key testing
public class KeyRequest
{
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
        
        // Register our services
        builder.Services.AddSingleton<PredictionService>();
        
        // Register all data providers
        builder.Services.AddSingleton<ForexDataProvider>();
        builder.Services.AddHttpClient<PolygonDataProvider>();
        builder.Services.AddHttpClient<TraderMadeDataProvider>();
        
        // Register the data provider factory
        builder.Services.AddSingleton<IForexDataProviderFactory, ForexDataProviderFactory>();
        
        // Register default data provider based on available API keys
        if (!string.IsNullOrEmpty(builder.Configuration["Polygon:ApiKey"]))
        {
            builder.Services.AddHttpClient<IForexDataProvider, PolygonDataProvider>();
            Console.WriteLine("Using Polygon.io as default data provider");
        }
        else if (!string.IsNullOrEmpty(builder.Configuration["TraderMade:ApiKey"]))
        {
            builder.Services.AddHttpClient<IForexDataProvider, TraderMadeDataProvider>();
            Console.WriteLine("Using TraderMade as default data provider");
        }
        else
        {
            builder.Services.AddSingleton<IForexDataProvider, ForexDataProvider>();
            Console.WriteLine("Using mock forex data provider as default");
        }
        
        // Register sentiment analyzers
        if (!string.IsNullOrEmpty(builder.Configuration["Polygon:ApiKey"]) || !string.IsNullOrEmpty(builder.Configuration["TraderMade:ApiKey"]))
        {
            // Register TradingViewAnalyzer if any data provider is available
            builder.Services.AddHttpClient<ISentimentAnalyzer, TradingViewAnalyzer>();
            Console.WriteLine("Using TradingView chart analyzer");
        }
        else
        {
            // Fallback to Perplexity analyzer
            builder.Services.AddHttpClient<ISentimentAnalyzer, PerplexitySentimentAnalyzer>();
            Console.WriteLine("Using Perplexity sentiment analyzer");
        }
        
        // Enable CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");

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
            async (string symbol, ISentimentAnalyzer analyzer, ILogger<Program> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return Results.BadRequest("Symbol is required");
                }
                
                logger.LogInformation("Analyzing {Symbol} with TradingView", symbol);
                var analysis = await analyzer.AnalyzeSentimentAsync(symbol);
                
                // Log trade recommendation if available
                if (analysis.IsTradeRecommended)
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
            async (string symbol, string provider, IForexDataProviderFactory providerFactory, IConfiguration configuration, ILoggerFactory loggerFactory, ILogger<Program> logger) =>
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
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.perplexity.ai/")
                };
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                // Get the Perplexity API key
                var perplexityApiKey = configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"];
                if (string.IsNullOrEmpty(perplexityApiKey))
                {
                    return Results.BadRequest("Perplexity API key is required for analysis");
                }
                
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", perplexityApiKey);
                
                // Create a new analyzer with the specified provider
                var analyzer = new TradingViewAnalyzer(
                    providerFactory,
                    providerType,
                    httpClient,
                    configuration,
                    loggerFactory.CreateLogger<TradingViewAnalyzer>());
                
                var analysis = await analyzer.AnalyzeSentimentAsync(symbol);
                
                // Log trade recommendation if available
                if (analysis.IsTradeRecommended)
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
        .WithName("AnalyzeChartWithProvider")
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
            async (int? count, ISentimentAnalyzer analyzer, ILogger<Program> logger) =>
        {
            try
            {
                // Limit to reasonable values, default to 3
                var pairCount = count.HasValue && count.Value > 0 && count.Value <= 5 ? count.Value : 3;
                
                logger.LogInformation("Getting {Count} trading recommendations", pairCount);
                var recommendations = await analyzer.GetTradingRecommendationsAsync(pairCount);
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
        
        // Diagnostic endpoint to set Perplexity API key directly
        app.MapPost("/api/diagnostics/set-perplexity-key", 
            async (KeyRequest request, IConfiguration configuration, ILogger<Program> logger) =>
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.BadRequest("API key cannot be empty");
                }
                
                try
                {
                    // Create a temporary in-memory dictionary for testing this key
                    var memoryDict = new Dictionary<string, string>
                    {
                        { "Perplexity:ApiKey", request.ApiKey }
                    };
                    
                    var memoryConfig = new ConfigurationBuilder()
                        .AddInMemory(memoryDict)
                        .Build();
                    
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
                        
                    // Make a test request to Perplexity API
                    var testRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
                    {
                        Content = new StringContent(
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                model = "sonar", // Current Perplexity model
                                messages = new[] 
                                { 
                                    new { role = "system", content = "You are a helpful assistant." },
                                    new { role = "user", content = "Hello, please respond with a single word." } 
                                },
                                temperature = 0.1,
                                max_tokens = 100
                            }),
                            System.Text.Encoding.UTF8,
                            "application/json")
                    };
                    
                    logger.LogInformation("Testing Perplexity API with key starting with {KeyPrefix}", 
                        request.ApiKey.Length > 4 ? request.ApiKey[..4] + "..." : "too short");
                    
                    testRequest.Headers.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
                    
                    var response = await httpClient.SendAsync(testRequest);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    logger.LogInformation("Perplexity API response: Status: {Status}, Content: {Content}", 
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
                            // Try to save to user secrets first
                            var userSecretsId = "trader-app-secrets-id";
                            var userSecretsPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Microsoft", "UserSecrets", userSecretsId);
                                
                            Directory.CreateDirectory(userSecretsPath);
                            
                            var secretsFilePath = Path.Combine(userSecretsPath, "secrets.json");
                            var secrets = new Dictionary<string, string>
                            {
                                { "Perplexity:ApiKey", request.ApiKey }
                            };
                            
                            await File.WriteAllTextAsync(
                                secretsFilePath, 
                                System.Text.Json.JsonSerializer.Serialize(secrets, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                
                            logger.LogInformation("Saved valid API key to user secrets at {Path}", secretsFilePath);
                            
                            // Also create a .env file in the current directory as a backup
                            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                            await File.WriteAllTextAsync(envFilePath, $"TRADER_PERPLEXITY_API_KEY={request.ApiKey}");
                            logger.LogInformation("Also saved API key to .env file at {Path}", envFilePath);
                            
                            // Direct update in appsettings.Development.json if it exists
                            var appSettingsDevPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json");
                            if (File.Exists(appSettingsDevPath))
                            {
                                try
                                {
                                    var json = await File.ReadAllTextAsync(appSettingsDevPath);
                                    
                                    // Simple JSON replacement - not ideal but works for this quick fix
                                    if (json.Contains("\"Perplexity\": {"))
                                    {
                                        var updated = System.Text.RegularExpressions.Regex.Replace(
                                            json,
                                            "\"ApiKey\":\\s*\"[^\"]*\"",
                                            $"\"ApiKey\": \"{request.ApiKey}\"");
                                            
                                        await File.WriteAllTextAsync(appSettingsDevPath, updated);
                                        logger.LogInformation("Updated API key in {Path}", appSettingsDevPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to update appsettings.Development.json");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to save API key to some locations");
                        }
                    }
                    
                    return Results.Ok(new 
                    { 
                        IsValid = isValid,
                        StatusMessage = statusMessage,
                        SavedToUserSecrets = isValid && request.SaveToUserSecrets,
                        RequestToPerplexity = new {
                            Model = "sonar",
                            Messages = new[] 
                            { 
                                new { Role = "system", Content = "You are a helpful assistant." },
                                new { Role = "user", Content = "Hello, please respond with a single word." } 
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing API key");
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
                    var testUrl = $"https://marketdata.tradermade.com/api/v1/timeseries?currency=EURUSD&api_key={request.ApiKey}&format=json&start_date={DateTime.UtcNow.AddDays(-7):yyyy-MM-dd}&end_date={DateTime.UtcNow:yyyy-MM-dd}&interval=daily";
                    
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
        
        // Diagnostic endpoint to check API configuration
        app.MapGet("/api/diagnostics/config", 
            (IConfiguration configuration, IWebHostEnvironment env, ILogger<Program> logger) =>
        {
            var perplexityApiKey = configuration["Perplexity:ApiKey"] ?? configuration["TRADER_PERPLEXITY_API_KEY"];
            var polygonApiKey = configuration["Polygon:ApiKey"] ?? configuration["TRADER_POLYGON_API_KEY"];
                
            var userSecretsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", "trader-app-secrets-id", "secrets.json");
                
            var configStatus = new
            {
                PerplexityApiKey = new
                {
                    IsConfigured = !string.IsNullOrEmpty(perplexityApiKey),
                    Length = perplexityApiKey?.Length ?? 0,
                    Prefix = perplexityApiKey?.Length > 4 ? perplexityApiKey[..4] + "..." : "N/A"
                },
                PolygonApiKey = new
                {
                    IsConfigured = !string.IsNullOrEmpty(polygonApiKey),
                    Length = polygonApiKey?.Length ?? 0,
                    Prefix = polygonApiKey?.Length > 4 ? polygonApiKey[..4] + "..." : "N/A"
                },
                Environment = new
                {
                    Name = env.EnvironmentName,
                    UserSecretsPath = userSecretsPath,
                    UserSecretsExist = File.Exists(userSecretsPath)
                }
            };
            
            logger.LogInformation("API configuration check: {@ConfigStatus}", configStatus);
            
            return Results.Ok(configStatus);
        })
        .WithName("CheckApiConfig")
        .WithOpenApi();

        app.Run();
    }
}