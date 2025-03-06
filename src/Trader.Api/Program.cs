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

// Extension method to add in-memory configuration
public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddInMemory(this IConfigurationBuilder builder, Dictionary<string, string> dictionary)
    {
        return builder.AddInMemoryCollection(dictionary);
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
        builder.Services.AddSingleton<IForexDataProvider, ForexDataProvider>();
        
        // Register Perplexity sentiment analyzer
        builder.Services.AddHttpClient<ISentimentAnalyzer, PerplexitySentimentAnalyzer>();
        
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
        app.MapGet("/api/forex/candles/{currencyPair}/{timeframe}/{count}", 
            async (string currencyPair, string timeframe, int count, IForexDataProvider dataProvider) =>
        {
            if (!Enum.TryParse<ChartTimeframe>(timeframe, true, out var timeframeEnum))
            {
                return Results.BadRequest($"Invalid timeframe. Valid values: {string.Join(", ", Enum.GetNames<ChartTimeframe>())}");
            }
            
            if (count <= 0 || count > 1000)
            {
                return Results.BadRequest("Count must be between 1 and 1000");
            }
            
            var candles = await dataProvider.GetCandleDataAsync(currencyPair, timeframeEnum, count);
            return Results.Ok(candles);
        })
        .WithName("GetForexCandles")
        .WithOpenApi();
        
        // Endpoint to get market sentiment analysis for a currency pair
        app.MapGet("/api/forex/sentiment/{currencyPair}", 
            async (string currencyPair, ISentimentAnalyzer sentimentAnalyzer) =>
        {
            if (string.IsNullOrWhiteSpace(currencyPair))
            {
                return Results.BadRequest("Currency pair is required");
            }
            
            var sentiment = await sentimentAnalyzer.AnalyzeSentimentAsync(currencyPair);
            return Results.Ok(sentiment);
        })
        .WithName("GetForexSentiment")
        .WithOpenApi();
        
        // Endpoint to get recommended forex trading opportunities
        app.MapGet("/api/forex/recommendations", 
            async (int? count, ISentimentAnalyzer sentimentAnalyzer) =>
        {
            // Limit to reasonable values, default to 3
            var pairCount = count.HasValue && count.Value > 0 && count.Value <= 5 ? count.Value : 3;
            
            var recommendations = await sentimentAnalyzer.GetTradingRecommendationsAsync(pairCount);
            return Results.Ok(recommendations);
        })
        .WithName("GetForexRecommendations")
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
        
        
        // Diagnostic endpoint to check Perplexity API configuration
        app.MapGet("/api/diagnostics/perplexity-config", 
            (IConfiguration configuration, IWebHostEnvironment env, ILogger<Program> logger) =>
        {
            var apiKeyFromSection = configuration["Perplexity:ApiKey"];
            var apiKeyFromEnvVar = configuration["TRADER_PERPLEXITY_API_KEY"];
            var apiKey = apiKeyFromSection ?? apiKeyFromEnvVar;
                
            var userSecretsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", "trader-app-secrets-id", "secrets.json");
                
            var configStatus = new
            {
                ApiKeyConfigured = !string.IsNullOrEmpty(apiKey),
                ApiKeyLength = apiKey?.Length ?? 0,
                ApiKeyPrefix = apiKey?.Length > 4 ? apiKey[..4] + "..." : "N/A",
                EnvironmentVariablePresent = !string.IsNullOrEmpty(apiKeyFromEnvVar),
                AppSettingsPresent = !string.IsNullOrEmpty(apiKeyFromSection),
                EnvironmentName = env.EnvironmentName,
                UserSecretsIdInProject = "trader-app-secrets-id",
                UserSecretsFileExists = File.Exists(userSecretsPath),
                UserSecretsPath = userSecretsPath,
                AvailableConfigKeys = configuration.AsEnumerable()
                    .Where(kvp => !kvp.Key.Contains("ConnectionString") && 
                                  !kvp.Key.Contains("Password") && 
                                  !kvp.Key.Contains("Secret"))
                    .Select(kvp => kvp.Key)
                    .ToList()
            };
            
            logger.LogInformation("Perplexity API configuration check: {@ConfigStatus}", 
                new { 
                    configStatus.ApiKeyConfigured,
                    configStatus.ApiKeyLength,
                    configStatus.ApiKeyPrefix,
                    configStatus.EnvironmentVariablePresent,
                    configStatus.AppSettingsPresent,
                    configStatus.EnvironmentName,
                    configStatus.UserSecretsFileExists
                });
            
            return Results.Ok(configStatus);
        })
        .WithName("CheckPerplexityConfig")
        .WithOpenApi();

        app.Run();
    }
}