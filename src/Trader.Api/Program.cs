using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;
using Trader.Infrastructure.Services;

namespace Trader.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Enable environment variable configuration
        builder.Configuration.AddEnvironmentVariables(prefix: "TRADER_");

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
        
        // Diagnostic endpoint to check Perplexity API configuration
        app.MapGet("/api/diagnostics/perplexity-config", 
            (IConfiguration configuration, ILogger<Program> logger) =>
        {
            var apiKey = configuration["Perplexity:ApiKey"] ?? 
                         configuration["TRADER_PERPLEXITY_API_KEY"];
                
            var configStatus = new
            {
                ApiKeyConfigured = !string.IsNullOrEmpty(apiKey),
                ApiKeyLength = apiKey?.Length ?? 0,
                ApiKeyPrefix = apiKey?.Length > 4 ? apiKey[..4] + "..." : "N/A",
                EnvironmentVariablePresent = !string.IsNullOrEmpty(configuration["TRADER_PERPLEXITY_API_KEY"]),
                AppSettingsPresent = !string.IsNullOrEmpty(configuration["Perplexity:ApiKey"])
            };
            
            logger.LogInformation("Perplexity API configuration check: {ConfigStatus}", configStatus);
            
            return Results.Ok(configStatus);
        })
        .WithName("CheckPerplexityConfig")
        .WithOpenApi();

        app.Run();
    }
}