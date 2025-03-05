using Trader.Core.Models;

namespace Trader.Core.Services;

/// <summary>
/// Service for identifying market movers and applying EMA filters
/// </summary>
public interface IMarketMoversService
{
    /// <summary>
    /// Gets the top market movers for forex pairs
    /// </summary>
    /// <param name="count">The number of market movers to return</param>
    /// <param name="timeframe">The timeframe to analyze</param>
    /// <param name="providerType">The data provider to use</param>
    /// <returns>A list of market movers</returns>
    Task<List<MarketMover>> GetTopForexMoversAsync(int count = 10, ChartTimeframe timeframe = ChartTimeframe.Hours1, DataProviderType providerType = DataProviderType.TwelveData);
    
    /// <summary>
    /// Gets the top market movers for crypto pairs
    /// </summary>
    /// <param name="count">The number of market movers to return</param>
    /// <param name="timeframe">The timeframe to analyze</param>
    /// <param name="providerType">The data provider to use</param>
    /// <returns>A list of market movers</returns>
    Task<List<MarketMover>> GetTopCryptoMoversAsync(int count = 10, ChartTimeframe timeframe = ChartTimeframe.Hours1, DataProviderType providerType = DataProviderType.TwelveData);
    
    /// <summary>
    /// Applies EMA filters to market movers
    /// </summary>
    /// <param name="marketMovers">The market movers to filter</param>
    /// <param name="shortTermTimeframe">The timeframe for short-term analysis</param>
    /// <param name="longTermTimeframe">The timeframe for long-term analysis</param>
    /// <param name="providerType">The data provider to use</param>
    /// <returns>The filtered market movers with EMA status</returns>
    Task<List<MarketMover>> ApplyEmaFiltersAsync(
        List<MarketMover> marketMovers, 
        ChartTimeframe shortTermTimeframe = ChartTimeframe.Hours1, 
        ChartTimeframe longTermTimeframe = ChartTimeframe.Day1,
        DataProviderType providerType = DataProviderType.TwelveData);
    
    /// <summary>
    /// Generates trade recommendations for market movers based on EMA analysis
    /// </summary>
    /// <param name="marketMovers">The market movers to generate recommendations for</param>
    /// <returns>The market movers with trade recommendations</returns>
    Task<List<MarketMover>> GenerateTradeRecommendationsAsync(List<MarketMover> marketMovers);
} 