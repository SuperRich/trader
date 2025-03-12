using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Data;

/// <summary>
/// Data provider that fetches market data from TraderMade API for forex and crypto assets.
/// </summary>
public class TraderMadeDataProvider : IForexDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TraderMadeDataProvider> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://marketdata.tradermade.com";
    
    // Cache for live rates to prevent excessive API calls
    private readonly Dictionary<string, (LiveRateData Data, DateTime Timestamp)> _liveRateCache = new();
    private readonly TimeSpan _liveCacheExpiration = TimeSpan.FromSeconds(5); // Cache live rates for 5 seconds

    public TraderMadeDataProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TraderMadeDataProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get API key from configuration
        _apiKey = configuration["TraderMade:ApiKey"] 
            ?? throw new ArgumentNullException(nameof(configuration), "TraderMade API key is required");
        
        // Set up the HttpClient
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TraderApp/1.0");
    }

    /// <summary>
    /// Gets live rate data for a specific symbol
    /// </summary>
    /// <param name="symbol">The symbol to get data for (e.g., "EURUSD", "BTCUSD")</param>
    /// <returns>The current price data</returns>
    public async Task<LiveRateData> GetLiveRateAsync(string symbol)
    {
        string formattedSymbol = FormatSymbolForTraderMade(symbol);
        
        // Check cache first
        if (_liveRateCache.TryGetValue(formattedSymbol, out var cachedData))
        {
            if (DateTime.UtcNow - cachedData.Timestamp < _liveCacheExpiration)
            {
                _logger.LogInformation("Using cached live rate for {Symbol} from {Timestamp}", 
                    symbol, cachedData.Timestamp);
                return cachedData.Data;
            }
            else
            {
                _logger.LogInformation("Cached live rate for {Symbol} expired, fetching new data", symbol);
            }
        }
        
        _logger.LogInformation("Fetching TraderMade live rate for {Symbol}", symbol);
        
        // Build the API endpoint URL for live rates
        string endpoint = $"/api/v1/live?currency={formattedSymbol}&api_key={_apiKey}";
        
        _logger.LogInformation("Using TraderMade live endpoint: {Endpoint}", endpoint);
        
        // Make the API request
        var response = await _httpClient.GetAsync(endpoint);
        
        // Check if the request was successful
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("TraderMade live API request failed with status {StatusCode}: {ErrorMessage}", 
                response.StatusCode, errorContent);
            
            throw new HttpRequestException($"TraderMade live API request failed: {response.StatusCode} - {errorContent}");
        }
        
        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var liveResponse = JsonSerializer.Deserialize<LiveRateResponse>(responseContent);
        
        if (liveResponse?.quotes == null || liveResponse.quotes.Length == 0)
        {
            _logger.LogWarning("No live rate results returned from TraderMade for {Symbol}", symbol);
            throw new InvalidOperationException($"No live rate data returned from TraderMade for symbol {symbol}");
        }
        
        // Extract the quote for the requested symbol
        var quote = liveResponse.quotes[0];
        
        var liveRateData = new LiveRateData
        {
            Symbol = symbol,
            Bid = quote.bid,
            Ask = quote.ask,
            Mid = quote.mid,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(liveResponse.timestamp).DateTime
        };
        
        // Update cache
        _liveRateCache[formattedSymbol] = (liveRateData, DateTime.UtcNow);
        
        return liveRateData;
    }

    /// <summary>
    /// Gets candle data for the specified symbol from TraderMade API.
    /// </summary>
    /// <param name="symbol">The symbol to get data for (e.g., "EURUSD", "BTCUSD")</param>
    /// <param name="timeframe">The timeframe for the candles</param>
    /// <param name="candleCount">The number of candles to retrieve</param>
    /// <returns>A list of candle data</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API returns invalid data</exception>
    public async Task<List<CandleData>> GetCandleDataAsync(string symbol, ChartTimeframe timeframe, int candleCount = 100)
    {
        // Format the symbol appropriately for the API
        string formattedSymbol = FormatSymbolForTraderMade(symbol);
        
        _logger.LogInformation("Fetching TraderMade data for {Symbol} ({FormattedSymbol}) at {Timeframe} timeframe", 
            symbol, formattedSymbol, timeframe);
        
        // Calculate date range based on timeframe and candle count
        DateTime endDate = DateTime.UtcNow;
        
        // For daily timeframe, we need to go back more days to get enough candles
        // For minute-based timeframes, TraderMade limits to 2 working days per request
        int daysToGoBack = timeframe switch
        {
            ChartTimeframe.Day1 => candleCount + 10, // Add buffer for weekends/holidays
            ChartTimeframe.Hours4 => (candleCount * 4) / 24 + 5,
            ChartTimeframe.Hours1 => (candleCount * 1) / 24 + 3,
            ChartTimeframe.Minutes15 => 2, // TraderMade limits minute data to 2 working days
            ChartTimeframe.Minutes5 => 2,  // TraderMade limits minute data to 2 working days
            _ => 2 // Default to 2 days for minute-based timeframes
        };
        
        DateTime startDate = endDate.AddDays(-daysToGoBack);
        
        // For minute-based timeframes, log a warning about the limitation
        if (timeframe == ChartTimeframe.Minutes5 || timeframe == ChartTimeframe.Minutes15)
        {
            _logger.LogWarning("TraderMade API limits minute data to 2 working days. Requested {Count} candles but date range is limited.", candleCount);
        }
        
        string from = startDate.ToString("yyyy-MM-dd");
        string to = endDate.ToString("yyyy-MM-dd");
        
        // Map timeframe to TraderMade interval
        // TraderMade formats: minute, hourly, daily
        string interval;
        
        switch (timeframe)
        {
            case ChartTimeframe.Minutes5:
                interval = "minute";
                break;
            case ChartTimeframe.Minutes15:
                interval = "minute";  // For 15-minute timeframe, we use 'minute' with period=15
                break;
            case ChartTimeframe.Hours1:
                interval = "hourly";
                break;
            case ChartTimeframe.Hours4:
                interval = "hourly";  // For 4-hour timeframe, we use 'hourly' with period=4
                break;
            case ChartTimeframe.Day1:
                interval = "daily";
                break;
            default:
                interval = "hourly";
                break;
        }
        
        // For minute-based timeframes, we need to add a period parameter
        string periodParam = "";
        if (timeframe == ChartTimeframe.Minutes5)
        {
            periodParam = "&period=5";
        }
        else if (timeframe == ChartTimeframe.Minutes15)
        {
            periodParam = "&period=15";
        }
        else if (timeframe == ChartTimeframe.Hours4)
        {
            periodParam = "&period=4";
        }
        
        // Build the API endpoint URL
        string endpoint = $"/api/v1/timeseries?currency={formattedSymbol}&api_key={_apiKey}&start_date={from}&end_date={to}&interval={interval}{periodParam}";
        
        _logger.LogInformation("Using TraderMade endpoint: {Endpoint}", endpoint);
        
        // Make the API request
        var response = await _httpClient.GetAsync(endpoint);
        
        // Check if the request was successful
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("TraderMade API request failed with status {StatusCode}: {ErrorMessage}", 
                response.StatusCode, errorContent);
            
            throw new HttpRequestException($"TraderMade API request failed: {response.StatusCode} - {errorContent}");
        }
        
        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var traderMadeResponse = JsonSerializer.Deserialize<TraderMadeResponse>(responseContent);
        
        if (traderMadeResponse?.quotes == null || traderMadeResponse.quotes.Length == 0)
        {
            _logger.LogWarning("No results returned from TraderMade for {Symbol}", symbol);
            throw new InvalidOperationException($"No data returned from TraderMade for symbol {symbol}");
        }
        
        // Convert to our CandleData format
        var candles = traderMadeResponse.quotes
            .Select(quote => new CandleData
            {
                Timestamp = DateTime.Parse(quote.date),
                Open = quote.GetOpenDecimal(),
                High = quote.GetHighDecimal(),
                Low = quote.GetLowDecimal(),
                Close = quote.GetCloseDecimal(),
                Volume = 0 // TraderMade doesn't provide volume data
            })
            .OrderBy(c => c.Timestamp)
            .ToList();
            
        _logger.LogInformation("Successfully retrieved {Count} candles for {Symbol}", candles.Count, symbol);
        
        // If we don't have enough candles, log a warning
        if (candles.Count < candleCount)
        {
            _logger.LogWarning("Only {ActualCount} candles available for {Symbol}, but {RequestedCount} were requested", 
                candles.Count, symbol, candleCount);
        }
        
        // Try to get the current live price to update the most recent candle
        try
        {
            var liveRate = await GetLiveRateAsync(symbol);
            
            // If we have candles and the live rate is more recent, update the last candle's close price
            if (candles.Count > 0)
            {
                _logger.LogInformation("Updating last candle with live rate: {LiveRate}", liveRate.Mid);
                
                // Get the most recent candle
                var lastCandle = candles.Last();
                
                // Only update if the live rate is more recent
                if (liveRate.Timestamp > lastCandle.Timestamp)
                {
                    // Create a new candle with the updated close price
                    var updatedCandle = new CandleData
                    {
                        Timestamp = lastCandle.Timestamp,
                        Open = lastCandle.Open,
                        High = Math.Max(lastCandle.High, liveRate.Mid), // Update high if live rate is higher
                        Low = Math.Min(lastCandle.Low, liveRate.Mid),   // Update low if live rate is lower
                        Close = liveRate.Mid,                           // Use live rate as close
                        Volume = lastCandle.Volume
                    };
                    
                    // Replace the last candle with the updated one
                    candles[candles.Count - 1] = updatedCandle;
                }
            }
        }
        catch (Exception ex)
        {
            // Log but continue with the historical data
            _logger.LogWarning(ex, "Failed to get live rate for {Symbol}. Using historical data only.", symbol);
        }
        
        // Return the requested number of candles (or less if not enough data)
        return candles.TakeLast(Math.Min(candles.Count, candleCount)).ToList();
    }

    /// <summary>
    /// Formats a symbol for use with the TraderMade API.
    /// </summary>
    private string FormatSymbolForTraderMade(string symbol)
    {
        // Strip out any separators like "/" or "-"
        return symbol.Replace("/", "").Replace("-", "").ToUpper();
    }
    
    /// <summary>
    /// Response structure from the TraderMade API for timeseries data.
    /// </summary>
    private class TraderMadeResponse
    {
        public string? endpoint { get; set; }
        public QuoteData[]? quotes { get; set; }
    }

    /// <summary>
    /// Quote data structure from the TraderMade API.
    /// </summary>
    private class QuoteData
    {
        public string date { get; set; } = string.Empty;
        
        // TraderMade API can return either string or numeric values for OHLC
        private JsonElement _open;
        private JsonElement _high;
        private JsonElement _low;
        private JsonElement _close;
        
        public JsonElement open 
        { 
            get => _open;
            set => _open = value;
        }
        
        public JsonElement high
        {
            get => _high;
            set => _high = value;
        }
        
        public JsonElement low
        {
            get => _low;
            set => _low = value;
        }
        
        public JsonElement close
        {
            get => _close;
            set => _close = value;
        }
        
        // Helper methods to convert JsonElement to decimal
        public decimal GetOpenDecimal() => GetDecimalValue(_open);
        public decimal GetHighDecimal() => GetDecimalValue(_high);
        public decimal GetLowDecimal() => GetDecimalValue(_low);
        public decimal GetCloseDecimal() => GetDecimalValue(_close);
        
        private decimal GetDecimalValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return decimal.Parse(element.GetString() ?? "0");
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDecimal();
            }
            return 0;
        }
    }
    
    /// <summary>
    /// Response structure from the TraderMade API for live rates.
    /// </summary>
    private class LiveRateResponse
    {
        public string? endpoint { get; set; }
        public long timestamp { get; set; }
        public LiveQuoteData[]? quotes { get; set; }
    }
    
    /// <summary>
    /// Live quote data structure from the TraderMade API.
    /// </summary>
    private class LiveQuoteData
    {
        public string? base_currency { get; set; }
        public string? quote_currency { get; set; }
        public decimal bid { get; set; }
        public decimal mid { get; set; }
        public decimal ask { get; set; }
    }
    
    /// <summary>
    /// Structure to hold live rate data.
    /// </summary>
    public class LiveRateData
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal Mid { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 