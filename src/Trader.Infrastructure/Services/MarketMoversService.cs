using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services;

/// <summary>
/// Service for identifying market movers and applying EMA filters
/// </summary>
public class MarketMoversService : IMarketMoversService
{
    private readonly IForexDataProviderFactory _dataProviderFactory;
    private readonly ILogger<MarketMoversService> _logger;
    
    // Common forex pairs to analyze
    private readonly string[] _commonForexPairs = new[]
    {
        "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", 
        "USDCHF", "NZDUSD", "EURGBP", "EURJPY", "GBPJPY",
        "AUDJPY", "CADJPY", "CHFJPY", "EURAUD", "EURCHF",
        "EURNZD", "GBPAUD", "GBPCAD", "GBPCHF", "AUDCAD",
        "AUDCHF", "AUDNZD", "CADCHF", "NZDCAD", "NZDCHF"
    };
    
    // Common crypto pairs to analyze
    private readonly string[] _commonCryptoPairs = new[]
    {
        "BTCUSD", "ETHUSD", "XRPUSD", "LTCUSD", "BCHUSD",
        "ADAUSD", "DOTUSD", "LINKUSD", "BNBUSD", "DOTUSD",
        "SOLUSD", "AVAXUSD", "MATICUSD", "DOGEUSD", "UNIUSD"
    };
    
    public MarketMoversService(
        IForexDataProviderFactory dataProviderFactory,
        ILogger<MarketMoversService> logger)
    {
        _dataProviderFactory = dataProviderFactory ?? throw new ArgumentNullException(nameof(dataProviderFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<List<MarketMover>> GetTopForexMoversAsync(
        int count = 10, 
        ChartTimeframe timeframe = ChartTimeframe.Hours1, 
        DataProviderType providerType = DataProviderType.TwelveData)
    {
        _logger.LogInformation("Getting top {Count} forex movers for {Timeframe} timeframe using {Provider}", 
            count, timeframe, providerType);
        
        var dataProvider = _dataProviderFactory.GetProvider(providerType);
        var marketMovers = new List<MarketMover>();
        
        // Get data for all common forex pairs
        foreach (var pair in _commonForexPairs)
        {
            try
            {
                var candles = await dataProvider.GetCandleDataAsync(pair, timeframe, 50);
                
                if (candles.Count < 2)
                {
                    _logger.LogWarning("Not enough candles for {Pair}, skipping", pair);
                    continue;
                }
                
                // Calculate pip movement
                var currentPrice = candles.Last().Close;
                var previousPrice = candles[candles.Count - 2].Close;
                
                // Calculate pip movement based on the pair
                decimal pipMovement = CalculatePipMovement(pair, currentPrice, previousPrice);
                
                var marketMover = new MarketMover
                {
                    Symbol = pair,
                    CurrentPrice = currentPrice,
                    PreviousPrice = previousPrice,
                    PipMovement = pipMovement,
                    Timeframe = timeframe,
                    AssetType = AssetType.Forex,
                    Timestamp = candles.Last().Timestamp
                };
                
                marketMovers.Add(marketMover);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data for {Pair}", pair);
            }
        }
        
        // Sort by absolute pip movement (descending)
        return marketMovers
            .OrderByDescending(m => Math.Abs(m.PipMovement))
            .Take(count)
            .ToList();
    }
    
    /// <inheritdoc />
    public async Task<List<MarketMover>> GetTopCryptoMoversAsync(
        int count = 10, 
        ChartTimeframe timeframe = ChartTimeframe.Hours1, 
        DataProviderType providerType = DataProviderType.TwelveData)
    {
        _logger.LogInformation("Getting top {Count} crypto movers for {Timeframe} timeframe using {Provider}", 
            count, timeframe, providerType);
        
        var dataProvider = _dataProviderFactory.GetProvider(providerType);
        var marketMovers = new List<MarketMover>();
        
        // Get data for all common crypto pairs
        foreach (var pair in _commonCryptoPairs)
        {
            try
            {
                var candles = await dataProvider.GetCandleDataAsync(pair, timeframe, 50);
                
                if (candles.Count < 2)
                {
                    _logger.LogWarning("Not enough candles for {Pair}, skipping", pair);
                    continue;
                }
                
                // Calculate pip movement (for crypto, we use points instead of pips)
                var currentPrice = candles.Last().Close;
                var previousPrice = candles[candles.Count - 2].Close;
                
                // For crypto, we use points instead of pips (1 point = 1 unit of price)
                decimal pipMovement = (currentPrice - previousPrice);
                
                var marketMover = new MarketMover
                {
                    Symbol = pair,
                    CurrentPrice = currentPrice,
                    PreviousPrice = previousPrice,
                    PipMovement = pipMovement,
                    Timeframe = timeframe,
                    AssetType = AssetType.Crypto,
                    Timestamp = candles.Last().Timestamp
                };
                
                marketMovers.Add(marketMover);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data for {Pair}", pair);
            }
        }
        
        // Sort by absolute pip movement (descending)
        return marketMovers
            .OrderByDescending(m => Math.Abs(m.PipMovement))
            .Take(count)
            .ToList();
    }
    
    /// <inheritdoc />
    public async Task<List<MarketMover>> ApplyEmaFiltersAsync(
        List<MarketMover> marketMovers, 
        ChartTimeframe shortTermTimeframe = ChartTimeframe.Hours1, 
        ChartTimeframe longTermTimeframe = ChartTimeframe.Day1,
        DataProviderType providerType = DataProviderType.TwelveData)
    {
        _logger.LogInformation("Applying EMA filters to {Count} market movers", marketMovers.Count);
        
        var dataProvider = _dataProviderFactory.GetProvider(providerType);
        
        foreach (var marketMover in marketMovers)
        {
            try
            {
                // Get short-term data for 10/20 EMA
                var shortTermCandles = await dataProvider.GetCandleDataAsync(
                    marketMover.Symbol, shortTermTimeframe, 100);
                
                if (shortTermCandles.Count < 50)
                {
                    _logger.LogWarning("Not enough short-term candles for {Symbol}, skipping EMA filters", 
                        marketMover.Symbol);
                    continue;
                }
                
                // Get long-term data for 50 EMA
                var longTermCandles = await dataProvider.GetCandleDataAsync(
                    marketMover.Symbol, longTermTimeframe, 100);
                
                if (longTermCandles.Count < 50)
                {
                    _logger.LogWarning("Not enough long-term candles for {Symbol}, skipping 50 EMA filter", 
                        marketMover.Symbol);
                }
                
                // Calculate EMAs
                var closePrices = shortTermCandles.Select(c => c.Close).ToList();
                var ema10 = CalculateEMA(closePrices, 10);
                var ema20 = CalculateEMA(closePrices, 20);
                
                // Store EMA values
                marketMover.EmaValues[10] = ema10;
                marketMover.EmaValues[20] = ema20;
                
                // Calculate 50 EMA if we have enough long-term candles
                if (longTermCandles.Count >= 50)
                {
                    var longTermClosePrices = longTermCandles.Select(c => c.Close).ToList();
                    var ema50 = CalculateEMA(longTermClosePrices, 50);
                    marketMover.EmaValues[50] = ema50;
                }
                
                // Get previous EMAs for crossover detection
                var previousClosePrices = shortTermCandles.Take(shortTermCandles.Count - 1).Select(c => c.Close).ToList();
                var previousEma10 = CalculateEMA(previousClosePrices, 10);
                var previousEma20 = CalculateEMA(previousClosePrices, 20);
                
                // Current price
                var currentPrice = marketMover.CurrentPrice;
                
                // Set EMA status
                var emaStatus = new EmaFilterStatus();
                
                // Price relative to EMAs
                emaStatus.IsAboveEma10 = currentPrice > ema10;
                emaStatus.IsAboveEma20 = currentPrice > ema20;
                
                if (marketMover.EmaValues.ContainsKey(50))
                {
                    emaStatus.IsAboveEma50 = currentPrice > marketMover.EmaValues[50];
                }
                
                // EMA crossovers
                emaStatus.IsEma10CrossingAboveEma20 = previousEma10 <= previousEma20 && ema10 > ema20;
                emaStatus.IsEma10CrossingBelowEma20 = previousEma10 >= previousEma20 && ema10 < ema20;
                
                // Price bouncing off EMAs (within 0.1% of EMA)
                var bounceThreshold = 0.001m; // 0.1%
                
                emaStatus.IsBouncingOffEma10 = Math.Abs((currentPrice / ema10) - 1) < bounceThreshold;
                emaStatus.IsBouncingOffEma20 = Math.Abs((currentPrice / ema20) - 1) < bounceThreshold;
                
                if (marketMover.EmaValues.ContainsKey(50))
                {
                    emaStatus.IsBouncingOffEma50 = Math.Abs((currentPrice / marketMover.EmaValues[50]) - 1) < bounceThreshold;
                }
                
                // Price breaking through EMAs (crossed within last 3 candles)
                var recentCandles = shortTermCandles.Skip(shortTermCandles.Count - 3).ToList();
                
                emaStatus.IsBreakingThroughEma10 = recentCandles.Any(c => 
                    (c.Low < ema10 && c.Close > ema10) || (c.High > ema10 && c.Close < ema10));
                
                emaStatus.IsBreakingThroughEma20 = recentCandles.Any(c => 
                    (c.Low < ema20 && c.Close > ema20) || (c.High > ema20 && c.Close < ema20));
                
                if (marketMover.EmaValues.ContainsKey(50))
                {
                    var ema50 = marketMover.EmaValues[50];
                    var longTermRecentCandles = longTermCandles.Skip(longTermCandles.Count - 3).ToList();
                    
                    emaStatus.IsBreakingThroughEma50 = longTermRecentCandles.Any(c => 
                        (c.Low < ema50 && c.Close > ema50) || (c.High > ema50 && c.Close < ema50));
                }
                
                marketMover.EmaStatus = emaStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying EMA filters to {Symbol}", marketMover.Symbol);
            }
        }
        
        return marketMovers;
    }
    
    /// <summary>
    /// Calculates the Exponential Moving Average (EMA) for a list of prices
    /// </summary>
    /// <param name="prices">The list of prices</param>
    /// <param name="period">The EMA period</param>
    /// <returns>The EMA value</returns>
    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        if (prices.Count < period)
        {
            throw new ArgumentException($"Not enough prices to calculate {period} EMA", nameof(prices));
        }
        
        // Calculate multiplier: 2 / (period + 1)
        decimal multiplier = 2m / (period + 1m);
        
        // Calculate SMA for the initial value
        decimal sma = prices.Take(period).Average();
        
        // Calculate EMA
        decimal ema = sma;
        
        for (int i = period; i < prices.Count; i++)
        {
            ema = (prices[i] - ema) * multiplier + ema;
        }
        
        return ema;
    }
    
    /// <summary>
    /// Calculates the pip movement for a forex pair
    /// </summary>
    /// <param name="pair">The forex pair</param>
    /// <param name="currentPrice">The current price</param>
    /// <param name="previousPrice">The previous price</param>
    /// <returns>The pip movement</returns>
    private decimal CalculatePipMovement(string pair, decimal currentPrice, decimal previousPrice)
    {
        // Calculate the absolute price difference
        decimal priceDifference = currentPrice - previousPrice;
        
        // Determine the pip value based on the currency pair
        decimal pipMultiplier;
        
        if (pair.Contains("JPY"))
        {
            // For JPY pairs, 1 pip = 0.01
            pipMultiplier = 100m;
        }
        else
        {
            // For most other pairs, 1 pip = 0.0001
            pipMultiplier = 10000m;
        }
        
        // Calculate the pip movement
        return priceDifference * pipMultiplier;
    }
} 