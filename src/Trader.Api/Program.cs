using Trader.Core.Models;
using Trader.Core.Services;
using Trader.Infrastructure.Data;

namespace Trader.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Register our services
        builder.Services.AddSingleton<PredictionService>();
        builder.Services.AddSingleton<IForexDataProvider, ForexDataProvider>();
        
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

        app.MapGet("/api/forex/multi-timeframe/{currencyPair}", 
            (string currencyPair, PredictionService predictionService) =>
        {
            var predictions = predictionService.AnalyzeMultipleTimeframes(currencyPair);
            return Results.Ok(predictions);
        })
        .WithName("GetMultiTimeframePredictions")
        .WithOpenApi();

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

        app.Run();
    }
}