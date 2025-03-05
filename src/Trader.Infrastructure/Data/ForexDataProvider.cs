using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Data;

public class ForexDataProvider : IForexDataProvider
{
    // In a real implementation, this would connect to a forex data API
    public async Task<List<CandleData>> GetCandleDataAsync(string currencyPair, ChartTimeframe timeframe, int candleCount = 100)
    {
        // Simulate async API call
        await Task.Delay(500);
        
        var candles = new List<CandleData>();
        var random = new Random();
        
        // Base price - simulating EUR/USD around 1.1000
        decimal basePrice = 1.1000m;
        decimal currentPrice = basePrice;
        
        // Generate historical candle data
        DateTime currentTime = DateTime.UtcNow;
        
        // Adjust time based on timeframe
        int minutesPerTimeframe = (int)timeframe;
        currentTime = currentTime.AddMinutes(-minutesPerTimeframe * candleCount);
        
        for (int i = 0; i < candleCount; i++)
        {
            // Simulate price movements
            decimal volatility = timeframe switch
            {
                ChartTimeframe.Minutes5 => 0.0005m,
                ChartTimeframe.Minutes15 => 0.0010m,
                ChartTimeframe.Hours1 => 0.0020m,
                ChartTimeframe.Hours4 => 0.0040m,
                ChartTimeframe.Day1 => 0.0080m,
                _ => 0.0010m
            };
            
            decimal change = (decimal)(random.NextDouble() * 2 - 1) * volatility;
            currentPrice += change;
            
            // Ensure price stays reasonable
            if (currentPrice < basePrice * 0.95m) currentPrice = basePrice * 0.95m;
            if (currentPrice > basePrice * 1.05m) currentPrice = basePrice * 1.05m;
            
            // Create random candle data
            decimal open = currentPrice;
            decimal close = open + (decimal)(random.NextDouble() * 2 - 1) * (volatility / 2);
            
            // Ensure high is higher than both open and close
            decimal high = Math.Max(open, close) + (decimal)(random.NextDouble()) * (volatility / 2);
            
            // Ensure low is lower than both open and close
            decimal low = Math.Min(open, close) - (decimal)(random.NextDouble()) * (volatility / 2);
            
            var candle = new CandleData
            {
                Timestamp = currentTime,
                Open = Math.Round(open, 5),
                High = Math.Round(high, 5),
                Low = Math.Round(low, 5),
                Close = Math.Round(close, 5),
                Volume = random.Next(100, 1000)
            };
            
            candles.Add(candle);
            
            // Move to next candle time
            currentTime = currentTime.AddMinutes(minutesPerTimeframe);
            currentPrice = close; // Next candle starts at previous close
        }
        
        return candles;
    }
}

