using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;
using System.Text;

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
    
    // Cache for candle data to reduce API calls
    private readonly Dictionary<string, Dictionary<ChartTimeframe, List<CandleData>>> _candleCache = new();
    
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
        
        // Limit the number of pairs to analyze based on count to reduce API calls
        // We'll analyze at most 3x the requested count to ensure we have enough data
        // after filtering out pairs with insufficient data
        int pairsToAnalyze = Math.Min(_commonForexPairs.Length, count * 3);
        var pairsToCheck = _commonForexPairs.Take(pairsToAnalyze).ToArray();
        
        _logger.LogInformation("Analyzing {Count} forex pairs to find top {RequestedCount} movers", 
            pairsToAnalyze, count);
        
        // Get data for selected forex pairs
        foreach (var pair in pairsToCheck)
        {
            try
            {
                // Get candle data and cache it for later use
                var candles = await GetCandleDataAsync(pair, timeframe, 100, dataProvider);
                
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
        
        // Limit the number of pairs to analyze based on count to reduce API calls
        // We'll analyze at most 3x the requested count to ensure we have enough data
        // after filtering out pairs with insufficient data
        int pairsToAnalyze = Math.Min(_commonCryptoPairs.Length, count * 3);
        var pairsToCheck = _commonCryptoPairs.Take(pairsToAnalyze).ToArray();
        
        _logger.LogInformation("Analyzing {Count} crypto pairs to find top {RequestedCount} movers", 
            pairsToAnalyze, count);
        
        // Get data for selected crypto pairs
        foreach (var pair in pairsToCheck)
        {
            try
            {
                // Get candle data and cache it for later use
                var candles = await GetCandleDataAsync(pair, timeframe, 100, dataProvider);
                
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
                _logger.LogInformation("Calculating EMAs for {Symbol}", marketMover.Symbol);
                
                // Get short-term data for 10/20 EMA - reuse cached data if possible
                var shortTermCandles = await GetCandleDataAsync(
                    marketMover.Symbol, shortTermTimeframe, 100, dataProvider);
                
                _logger.LogInformation("Retrieved {Count} candles for {Symbol} at {Timeframe} timeframe", 
                    shortTermCandles.Count, marketMover.Symbol, shortTermTimeframe);
                
                if (shortTermCandles.Count < 21) // Need at least 21 candles for 20-period EMA
                {
                    _logger.LogWarning("Not enough short-term candles for {Symbol} (only {Count}), skipping EMA filters", 
                        marketMover.Symbol, shortTermCandles.Count);
                    continue;
                }
                
                // Get long-term data for 50 EMA - reuse cached data if possible
                var longTermCandles = await GetCandleDataAsync(
                    marketMover.Symbol, longTermTimeframe, 100, dataProvider);
                
                _logger.LogInformation("Retrieved {Count} candles for {Symbol} at {Timeframe} timeframe", 
                    longTermCandles.Count, marketMover.Symbol, longTermTimeframe);
                
                bool has50EmaData = longTermCandles.Count >= 51; // Need at least 51 candles for 50-period EMA
                
                if (!has50EmaData)
                {
                    _logger.LogWarning("Not enough long-term candles for {Symbol} (only {Count}), skipping 50 EMA filter", 
                        marketMover.Symbol, longTermCandles.Count);
                }
                
                try
                {
                    // Calculate EMAs
                    var closePrices = shortTermCandles.Select(c => c.Close).ToList();
                    
                    // Debug log the prices
                    _logger.LogDebug("Close prices for {Symbol}: {Prices}", 
                        marketMover.Symbol, string.Join(", ", closePrices.TakeLast(10)));
                    
                    // Calculate and store EMA values
                    decimal ema10 = CalculateEMA(closePrices, 10);
                    _logger.LogDebug("Calculated EMA10 for {Symbol}: {EMA10}", marketMover.Symbol, ema10);
                    marketMover.EmaValues[10] = ema10;
                    
                    decimal ema20 = CalculateEMA(closePrices, 20);
                    _logger.LogDebug("Calculated EMA20 for {Symbol}: {EMA20}", marketMover.Symbol, ema20);
                    marketMover.EmaValues[20] = ema20;
                    
                    // Calculate 50 EMA if we have enough long-term candles
                    if (has50EmaData)
                    {
                        var longTermClosePrices = longTermCandles.Select(c => c.Close).ToList();
                        decimal ema50 = CalculateEMA(longTermClosePrices, 50);
                        _logger.LogDebug("Calculated EMA50 for {Symbol}: {EMA50}", marketMover.Symbol, ema50);
                        marketMover.EmaValues[50] = ema50;
                    }
                    
                    // Get previous EMAs for crossover detection
                    var previousClosePrices = closePrices.Take(closePrices.Count - 1).ToList();
                    decimal previousEma10 = CalculateEMA(previousClosePrices, 10);
                    decimal previousEma20 = CalculateEMA(previousClosePrices, 20);
                    
                    // Current price
                    decimal currentPrice = marketMover.CurrentPrice;
                    
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
                    decimal bounceThreshold = 0.001m; // 0.1%
                    
                    emaStatus.IsBouncingOffEma10 = Math.Abs((currentPrice / ema10) - 1) < bounceThreshold;
                    emaStatus.IsBouncingOffEma20 = Math.Abs((currentPrice / ema20) - 1) < bounceThreshold;
                    
                    if (marketMover.EmaValues.ContainsKey(50))
                    {
                        emaStatus.IsBouncingOffEma50 = Math.Abs((currentPrice / marketMover.EmaValues[50]) - 1) < bounceThreshold;
                    }
                    
                    // Price breaking through EMAs (crossed within last 3 candles)
                    var recentCandles = shortTermCandles.TakeLast(3).ToList();
                    
                    emaStatus.IsBreakingThroughEma10 = recentCandles.Any(c => 
                        (c.Low < ema10 && c.Close > ema10) || (c.High > ema10 && c.Close < ema10));
                    
                    emaStatus.IsBreakingThroughEma20 = recentCandles.Any(c => 
                        (c.Low < ema20 && c.Close > ema20) || (c.High > ema20 && c.Close < ema20));
                    
                    if (marketMover.EmaValues.ContainsKey(50))
                    {
                        decimal ema50 = marketMover.EmaValues[50];
                        var longTermRecentCandles = longTermCandles.TakeLast(3).ToList();
                        
                        emaStatus.IsBreakingThroughEma50 = longTermRecentCandles.Any(c => 
                            (c.Low < ema50 && c.Close > ema50) || (c.High > ema50 && c.Close < ema50));
                    }
                    
                    marketMover.EmaStatus = emaStatus;
                    
                    _logger.LogInformation("Successfully applied EMA filters for {Symbol}", marketMover.Symbol);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating EMAs for {Symbol}: {Message}", 
                        marketMover.Symbol, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying EMA filters to {Symbol}: {Message}", 
                    marketMover.Symbol, ex.Message);
            }
        }
        
        return marketMovers;
    }
    
    /// <summary>
    /// Gets candle data for a symbol and timeframe, using cache when possible
    /// </summary>
    /// <param name="symbol">The symbol to get data for</param>
    /// <param name="timeframe">The timeframe to get data for</param>
    /// <param name="count">The number of candles to get</param>
    /// <param name="dataProvider">The data provider to use</param>
    /// <returns>The candle data</returns>
    private async Task<List<CandleData>> GetCandleDataAsync(
        string symbol, 
        ChartTimeframe timeframe, 
        int count,
        IForexDataProvider dataProvider)
    {
        // Check if we have cached data for this symbol and timeframe
        if (_candleCache.TryGetValue(symbol, out var timeframeCache) && 
            timeframeCache.TryGetValue(timeframe, out var candles) && 
            candles.Count >= count)
        {
            _logger.LogInformation("Using cached data for {Symbol} at {Timeframe} timeframe", symbol, timeframe);
            return candles;
        }
        
        // If not, get the data from the provider
        _logger.LogInformation("Fetching data for {Symbol} at {Timeframe} timeframe", symbol, timeframe);
        var newCandles = await dataProvider.GetCandleDataAsync(symbol, timeframe, count);
        
        // Cache the data
        if (!_candleCache.ContainsKey(symbol))
        {
            _candleCache[symbol] = new Dictionary<ChartTimeframe, List<CandleData>>();
        }
        
        _candleCache[symbol][timeframe] = newCandles;
        
        return newCandles;
    }
    
    /// <summary>
    /// Calculates the Exponential Moving Average (EMA) for a list of prices
    /// </summary>
    /// <param name="prices">The list of prices</param>
    /// <param name="period">The EMA period</param>
    /// <returns>The EMA value</returns>
    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        if (prices == null || prices.Count == 0)
        {
            throw new ArgumentException("Prices list cannot be null or empty", nameof(prices));
        }
        
        if (prices.Count < period)
        {
            throw new ArgumentException($"Not enough prices to calculate {period} EMA. Need at least {period} prices, but got {prices.Count}", nameof(prices));
        }
        
        // Calculate multiplier: 2 / (period + 1)
        decimal multiplier = 2m / (period + 1m);
        
        // Calculate SMA for the initial value
        decimal sma = 0;
        for (int i = 0; i < period; i++)
        {
            sma += prices[i];
        }
        sma /= period;
        
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
    
    /// <inheritdoc />
    public Task<List<MarketMover>> GenerateTradeRecommendationsAsync(List<MarketMover> marketMovers)
    {
        _logger.LogInformation("Generating trade recommendations for {Count} market movers", marketMovers.Count);
        
        foreach (var marketMover in marketMovers)
        {
            try
            {
                // Skip if EMA values are not available
                if (marketMover.EmaValues.Count == 0)
                {
                    _logger.LogWarning("No EMA values available for {Symbol}, skipping trade recommendation", 
                        marketMover.Symbol);
                    continue;
                }
                
                // Get EMA values
                bool hasEma10 = marketMover.EmaValues.TryGetValue(10, out decimal ema10);
                bool hasEma20 = marketMover.EmaValues.TryGetValue(20, out decimal ema20);
                bool hasEma50 = marketMover.EmaValues.TryGetValue(50, out decimal ema50);
                
                if (!hasEma10 || !hasEma20)
                {
                    _logger.LogWarning("Missing required EMA values for {Symbol}, skipping trade recommendation", 
                        marketMover.Symbol);
                    continue;
                }
                
                // Current price
                decimal currentPrice = marketMover.CurrentPrice;
                
                // Generate trade recommendation based on EMA analysis
                var recommendation = new TradeRecommendation
                {
                    EntryPrice = currentPrice,
                    Timestamp = DateTime.UtcNow,
                    Signals = marketMover.EmaStatus.GetSignals()
                };
                
                // Determine trade direction based on EMA signals
                string direction = DetermineTradeDirection(marketMover.EmaStatus);
                recommendation.Direction = direction;
                
                // Determine order type
                recommendation.OrderType = DetermineOrderType(direction, marketMover.EmaStatus);
                
                // Calculate stop loss and take profit levels
                CalculateStopLossAndTakeProfit(
                    marketMover.Symbol, 
                    direction, 
                    currentPrice, 
                    ema10, 
                    ema20, 
                    hasEma50 ? ema50 : 0, 
                    marketMover.AssetType,
                    out decimal stopLoss,
                    out decimal takeProfit);
                
                recommendation.StopLossPrice = stopLoss;
                recommendation.TakeProfitPrice = takeProfit;
                
                // Generate rationale
                recommendation.Rationale = GenerateTradeRationale(marketMover, recommendation);
                
                // Assign recommendation to market mover
                marketMover.RecommendedTrade = recommendation;
                
                _logger.LogInformation(
                    "Generated {Direction} recommendation for {Symbol} with entry at {Entry}, SL at {StopLoss}, TP at {TakeProfit}", 
                    recommendation.Direction, 
                    marketMover.Symbol, 
                    recommendation.EntryPrice, 
                    recommendation.StopLossPrice, 
                    recommendation.TakeProfitPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating trade recommendation for {Symbol}: {Message}", 
                    marketMover.Symbol, ex.Message);
            }
        }
        
        return Task.FromResult(marketMovers);
    }
    
    /// <summary>
    /// Determines the trade direction based on EMA status
    /// </summary>
    private string DetermineTradeDirection(EmaFilterStatus emaStatus)
    {
        // Bullish signals
        bool bullishSignals = emaStatus.IsAboveEma10 && 
                             emaStatus.IsAboveEma20 && 
                             (emaStatus.IsEma10CrossingAboveEma20 || 
                              emaStatus.IsBouncingOffEma10 || 
                              emaStatus.IsBouncingOffEma20);
        
        // Bearish signals
        bool bearishSignals = !emaStatus.IsAboveEma10 && 
                             !emaStatus.IsAboveEma20 && 
                             (emaStatus.IsEma10CrossingBelowEma20 || 
                              emaStatus.IsBouncingOffEma10 || 
                              emaStatus.IsBouncingOffEma20);
        
        // If we have clear bullish signals
        if (bullishSignals)
        {
            return "Buy";
        }
        // If we have clear bearish signals
        else if (bearishSignals)
        {
            return "Sell";
        }
        // If price is above both EMAs but no clear signal, default to bullish
        else if (emaStatus.IsAboveEma10 && emaStatus.IsAboveEma20)
        {
            return "Buy";
        }
        // If price is below both EMAs but no clear signal, default to bearish
        else if (!emaStatus.IsAboveEma10 && !emaStatus.IsAboveEma20)
        {
            return "Sell";
        }
        // If we're between EMAs, look for breakout signals
        else if (emaStatus.IsBreakingThroughEma10 || emaStatus.IsBreakingThroughEma20)
        {
            return emaStatus.IsAboveEma10 ? "Buy" : "Sell";
        }
        // Default to Buy if no clear direction
        else
        {
            return "Buy";
        }
    }
    
    /// <summary>
    /// Determines the order type based on direction and EMA status
    /// </summary>
    private Trader.Core.Models.OrderType DetermineOrderType(string direction, EmaFilterStatus emaStatus)
    {
        // For breakout trades, use stop orders
        if (emaStatus.IsBreakingThroughEma10 || emaStatus.IsBreakingThroughEma20 || emaStatus.IsBreakingThroughEma50)
        {
            return direction == "Buy" ? Trader.Core.Models.OrderType.StopBuy : Trader.Core.Models.OrderType.StopSell;
        }
        // For pullback trades, use limit orders
        else if (emaStatus.IsBouncingOffEma10 || emaStatus.IsBouncingOffEma20 || emaStatus.IsBouncingOffEma50)
        {
            return direction == "Buy" ? Trader.Core.Models.OrderType.LimitBuy : Trader.Core.Models.OrderType.LimitSell;
        }
        // Default to market orders
        else
        {
            return direction == "Buy" ? Trader.Core.Models.OrderType.MarketBuy : Trader.Core.Models.OrderType.MarketSell;
        }
    }
    
    /// <summary>
    /// Calculates stop loss and take profit levels based on EMA values
    /// </summary>
    private void CalculateStopLossAndTakeProfit(
        string symbol, 
        string direction, 
        decimal currentPrice, 
        decimal ema10, 
        decimal ema20, 
        decimal ema50,
        AssetType assetType,
        out decimal stopLoss,
        out decimal takeProfit)
    {
        // Default values
        stopLoss = 0;
        takeProfit = 0;
        
        try
        {
            // Use more reasonable ATR approximations for forex and crypto
            // For forex, typical daily ranges are much smaller percentages of price
            decimal atrMultiplier;
            
            if (assetType == AssetType.Forex)
            {
                // For JPY pairs, use different pip values
                if (symbol.Contains("JPY"))
                {
                    // For JPY pairs, 1 pip = 0.01, so use a smaller percentage
                    // Typical stop loss might be 30-50 pips for JPY pairs
                    atrMultiplier = 0.0030m; // 30 pips for JPY pairs
                }
                else
                {
                    // For non-JPY pairs, 1 pip = 0.0001, so use a smaller percentage
                    // Typical stop loss might be 30-50 pips
                    atrMultiplier = 0.0030m; // 30 pips for most pairs
                }
            }
            else // Crypto
            {
                // Crypto is more volatile, but still shouldn't have massive stops
                atrMultiplier = 0.02m; // 2% for crypto
            }
            
            decimal atr = currentPrice * atrMultiplier;
            
            if (direction == "Buy")
            {
                // For buy trades, use a reasonable stop below recent support
                // Use the lowest of the EMAs as a reference point, but don't go too far
                decimal supportLevel = Math.Min(ema10, ema20);
                
                // If we have EMA50 and it's valid, consider it
                if (ema50 > 0 && ema50 < supportLevel)
                {
                    supportLevel = ema50;
                }
                
                // Stop loss is below support with a buffer
                // But never more than a reasonable amount of pips
                decimal maxStopDistance = assetType == AssetType.Forex 
                    ? (symbol.Contains("JPY") ? 1.0m : 0.01m) // Max 100 pips for forex
                    : currentPrice * 0.05m; // Max 5% for crypto
                
                decimal calculatedStop = supportLevel - atr;
                decimal stopDistance = currentPrice - calculatedStop;
                
                // If stop distance is too large, cap it
                if (stopDistance > maxStopDistance)
                {
                    stopLoss = currentPrice - maxStopDistance;
                }
                else
                {
                    stopLoss = calculatedStop;
                }
                
                // Take profit is a multiple of the risk, but also capped
                decimal risk = currentPrice - stopLoss;
                decimal maxTpDistance = assetType == AssetType.Forex 
                    ? (symbol.Contains("JPY") ? 3.0m : 0.03m) // Max 300 pips for forex
                    : currentPrice * 0.15m; // Max 15% for crypto
                
                decimal calculatedTp = currentPrice + (risk * 2m); // 1:2 risk-reward ratio
                decimal tpDistance = calculatedTp - currentPrice;
                
                // If take profit distance is too large, cap it
                if (tpDistance > maxTpDistance)
                {
                    takeProfit = currentPrice + maxTpDistance;
                }
                else
                {
                    takeProfit = calculatedTp;
                }
            }
            else // Sell
            {
                // For sell trades, use a reasonable stop above recent resistance
                // Use the highest of the EMAs as a reference point, but don't go too far
                decimal resistanceLevel = Math.Max(ema10, ema20);
                
                // If we have EMA50 and it's valid, consider it
                if (ema50 > 0 && ema50 > resistanceLevel)
                {
                    resistanceLevel = ema50;
                }
                
                // Stop loss is above resistance with a buffer
                // But never more than a reasonable amount of pips
                decimal maxStopDistance = assetType == AssetType.Forex 
                    ? (symbol.Contains("JPY") ? 1.0m : 0.01m) // Max 100 pips for forex
                    : currentPrice * 0.05m; // Max 5% for crypto
                
                decimal calculatedStop = resistanceLevel + atr;
                decimal stopDistance = calculatedStop - currentPrice;
                
                // If stop distance is too large, cap it
                if (stopDistance > maxStopDistance)
                {
                    stopLoss = currentPrice + maxStopDistance;
                }
                else
                {
                    stopLoss = calculatedStop;
                }
                
                // Take profit is a multiple of the risk, but also capped
                decimal risk = stopLoss - currentPrice;
                decimal maxTpDistance = assetType == AssetType.Forex 
                    ? (symbol.Contains("JPY") ? 3.0m : 0.03m) // Max 300 pips for forex
                    : currentPrice * 0.15m; // Max 15% for crypto
                
                decimal calculatedTp = currentPrice - (risk * 2m); // 1:2 risk-reward ratio
                decimal tpDistance = currentPrice - calculatedTp;
                
                // If take profit distance is too large, cap it
                if (tpDistance > maxTpDistance)
                {
                    takeProfit = currentPrice - maxTpDistance;
                }
                else
                {
                    takeProfit = calculatedTp;
                }
            }
            
            // Ensure take profit and stop loss are valid
            if (direction == "Buy" && takeProfit <= currentPrice)
            {
                takeProfit = currentPrice * 1.01m; // Default 1% profit target
            }
            else if (direction == "Sell" && takeProfit >= currentPrice)
            {
                takeProfit = currentPrice * 0.99m; // Default 1% profit target
            }
            
            // Round to appropriate number of decimal places based on asset type
            int decimals = assetType == AssetType.Forex ? 
                (symbol.Contains("JPY") ? 3 : 5) : 2;
            
            stopLoss = Math.Round(stopLoss, decimals);
            takeProfit = Math.Round(takeProfit, decimals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating stop loss and take profit for {Symbol}: {Message}", 
                symbol, ex.Message);
                
            // Fallback values - much more reasonable defaults
            if (direction == "Buy")
            {
                // For forex, use reasonable pip values
                if (assetType == AssetType.Forex)
                {
                    if (symbol.Contains("JPY"))
                    {
                        stopLoss = currentPrice - 0.5m; // 50 pips for JPY pairs
                        takeProfit = currentPrice + 1.0m; // 100 pips for JPY pairs
                    }
                    else
                    {
                        stopLoss = currentPrice - 0.005m; // 50 pips for non-JPY pairs
                        takeProfit = currentPrice + 0.01m; // 100 pips for non-JPY pairs
                    }
                }
                else // Crypto
                {
                    stopLoss = currentPrice * 0.97m; // 3% stop for crypto
                    takeProfit = currentPrice * 1.06m; // 6% target for crypto
                }
            }
            else // Sell
            {
                // For forex, use reasonable pip values
                if (assetType == AssetType.Forex)
                {
                    if (symbol.Contains("JPY"))
                    {
                        stopLoss = currentPrice + 0.5m; // 50 pips for JPY pairs
                        takeProfit = currentPrice - 1.0m; // 100 pips for JPY pairs
                    }
                    else
                    {
                        stopLoss = currentPrice + 0.005m; // 50 pips for non-JPY pairs
                        takeProfit = currentPrice - 0.01m; // 100 pips for non-JPY pairs
                    }
                }
                else // Crypto
                {
                    stopLoss = currentPrice * 1.03m; // 3% stop for crypto
                    takeProfit = currentPrice * 0.94m; // 6% target for crypto
                }
            }
        }
    }
    
    /// <summary>
    /// Generates a rationale for the trade recommendation
    /// </summary>
    private string GenerateTradeRationale(MarketMover marketMover, TradeRecommendation recommendation)
    {
        var sb = new StringBuilder();
        
        // Add direction and symbol
        sb.AppendLine($"{recommendation.Direction} {marketMover.Symbol} based on EMA analysis:");
        
        // Add EMA positions
        if (marketMover.EmaValues.TryGetValue(10, out decimal ema10))
        {
            sb.AppendLine($"- EMA10: {ema10} ({(marketMover.CurrentPrice > ema10 ? "price above" : "price below")})");
        }
        
        if (marketMover.EmaValues.TryGetValue(20, out decimal ema20))
        {
            sb.AppendLine($"- EMA20: {ema20} ({(marketMover.CurrentPrice > ema20 ? "price above" : "price below")})");
        }
        
        if (marketMover.EmaValues.TryGetValue(50, out decimal ema50))
        {
            sb.AppendLine($"- EMA50: {ema50} ({(marketMover.CurrentPrice > ema50 ? "price above" : "price below")})");
        }
        
        // Add signals
        if (recommendation.Signals.Count > 0)
        {
            sb.AppendLine("Signals:");
            foreach (var signal in recommendation.Signals)
            {
                sb.AppendLine($"- {signal}");
            }
        }
        
        // Add trade details
        sb.AppendLine($"Entry: {recommendation.EntryPrice}");
        sb.AppendLine($"Stop Loss: {recommendation.StopLossPrice}");
        sb.AppendLine($"Take Profit: {recommendation.TakeProfitPrice}");
        sb.AppendLine($"Risk-Reward Ratio: {recommendation.RiskRewardRatio:F2}");
        
        return sb.ToString();
    }
} 