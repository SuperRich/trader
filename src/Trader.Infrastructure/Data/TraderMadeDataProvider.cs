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
                Open = decimal.Parse(quote.open),
                High = decimal.Parse(quote.high),
                Low = decimal.Parse(quote.low),
                Close = decimal.Parse(quote.close),
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
    /// Response structure from the TraderMade API.
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
        public string open { get; set; } = string.Empty;
        public string high { get; set; } = string.Empty;
        public string low { get; set; } = string.Empty;
        public string close { get; set; } = string.Empty;
    }
} 