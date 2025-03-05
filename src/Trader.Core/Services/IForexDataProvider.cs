using Trader.Core.Models;

namespace Trader.Core.Services;

public interface IForexDataProvider
{
    Task<List<CandleData>> GetCandleDataAsync(string currencyPair, ChartTimeframe timeframe, int candleCount = 100);
}

public class CandleData
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public double Volume { get; set; }
}