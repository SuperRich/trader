using Trader.Core.Models;

namespace Trader.Core.Services;

public class PredictionService
{
    private readonly IForexDataProvider _dataProvider;
    
    public PredictionService(IForexDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    // In a real implementation, this would use ML models or technical analysis algorithms
    public ForexPrediction GeneratePrediction(string currencyPair, ChartTimeframe timeframe)
    {
        // This is just a placeholder implementation
        // In a real app, you would analyze chart patterns, indicators, etc.
        
        var random = new Random();
        var direction = random.Next(2) == 0 ? TradingDirection.Buy : TradingDirection.Sell;
        
        var entryPrice = 1.0000m + (decimal)(random.NextDouble() * 0.1000);
        
        // Set stop loss and take profit based on direction
        var stopLoss = direction == TradingDirection.Buy 
            ? entryPrice - (decimal)(random.NextDouble() * 0.0050) 
            : entryPrice + (decimal)(random.NextDouble() * 0.0050);
            
        var takeProfit = direction == TradingDirection.Buy 
            ? entryPrice + (decimal)(random.NextDouble() * 0.0150) 
            : entryPrice - (decimal)(random.NextDouble() * 0.0150);
        
        return new ForexPrediction
        {
            CurrencyPair = currencyPair,
            Timestamp = DateTime.UtcNow,
            Timeframe = timeframe,
            Direction = direction,
            EntryPrice = Math.Round(entryPrice, 5),
            StopLoss = Math.Round(stopLoss, 5),
            TakeProfit = Math.Round(takeProfit, 5),
            AnalysisFactors = new List<string>
            {
                "Moving Average Crossover",
                "RSI Oversold/Overbought",
                "Support/Resistance Levels"
            }
        };
    }
    
    public List<ForexPrediction> AnalyzeMultipleTimeframes(string currencyPair)
    {
        var predictions = new List<ForexPrediction>();
        
        // Generate predictions for each requested timeframe
        predictions.Add(GeneratePrediction(currencyPair, ChartTimeframe.Minutes5));
        predictions.Add(GeneratePrediction(currencyPair, ChartTimeframe.Minutes15));
        predictions.Add(GeneratePrediction(currencyPair, ChartTimeframe.Hours1));
        predictions.Add(GeneratePrediction(currencyPair, ChartTimeframe.Hours4));
        predictions.Add(GeneratePrediction(currencyPair, ChartTimeframe.Day1));
        
        return predictions;
    }
    
    public async Task<ForexPrediction> GetPredictionAsync(string currencyPair)
    {
        // Get candle data for multiple timeframes
        var candleData = await _dataProvider.GetCandleDataAsync(currencyPair, ChartTimeframe.Hours1);
        
        // Create a prediction based on the data
        var prediction = new ForexPrediction
        {
            CurrencyPair = currencyPair,
            Timestamp = DateTime.UtcNow,
            Timeframe = ChartTimeframe.Hours1,
            Direction = candleData.Count > 0 && candleData.Last().Close > candleData.Last().Open 
                ? TradingDirection.Buy 
                : TradingDirection.Sell,
            EntryPrice = candleData.Count > 0 ? candleData.Last().Close : 1.0000m,
            StopLoss = candleData.Count > 0 ? Math.Min(candleData.Last().Low, candleData.Last().Close * 0.995m) : 0.9950m,
            TakeProfit = candleData.Count > 0 ? Math.Max(candleData.Last().High, candleData.Last().Close * 1.01m) : 1.0100m,
            AnalysisFactors = new List<string> { "Price Action", "Support/Resistance" }
        };
        
        return prediction;
    }
}