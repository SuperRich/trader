using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Data;

/// <summary>
/// Data provider that fetches market data from TwelveData API for forex and crypto assets.
/// </summary>
public class TwelveDataProvider : IForexDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwelveDataProvider> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.twelvedata.com";

    public TwelveDataProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TwelveDataProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get API key from configuration
        _apiKey = configuration["TwelveData:ApiKey"] ?? configuration["TRADER_TWELVEDATA_API_KEY"] 
            ?? throw new ArgumentNullException(nameof(configuration), "TwelveData API key is required");
        
        // Set up the HttpClient
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TraderApp/1.0");
    }

    /// <summary>
    /// Gets candle data for the specified symbol from TwelveData.
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
        string formattedSymbol = FormatSymbolForTwelveData(symbol);
        
        _logger.LogInformation("Fetching TwelveData data for {Symbol} ({FormattedSymbol}) at {Timeframe} timeframe", 
            symbol, formattedSymbol, timeframe);
        
        // Map timeframe to TwelveData interval
        string interval = MapTimeframeToInterval(timeframe);
        
        // Build the API endpoint URL
        string endpoint = $"/time_series?symbol={formattedSymbol}&interval={interval}&outputsize={candleCount}&apikey={_apiKey}";
        
        _logger.LogInformation("Using TwelveData endpoint: {Endpoint}", endpoint);
        
        // Make the API request
        var response = await _httpClient.GetAsync(endpoint);
        
        // Check if the request was successful
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("TwelveData API request failed with status {StatusCode}: {ErrorMessage}", 
                response.StatusCode, errorContent);
            
            throw new HttpRequestException($"TwelveData API request failed: {response.StatusCode} - {errorContent}");
        }
        
        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var twelveDataResponse = JsonSerializer.Deserialize<TwelveDataResponse>(responseContent);
        
        if (twelveDataResponse?.values == null || !twelveDataResponse.values.Any())
        {
            _logger.LogWarning("No results returned from TwelveData for {Symbol}", symbol);
            throw new InvalidOperationException($"No data returned from TwelveData for symbol {symbol}");
        }
        
        // Convert to our CandleData format
        var candles = twelveDataResponse.values
            .Select(quote => new CandleData
            {
                Timestamp = DateTime.Parse(quote.datetime),
                Open = decimal.Parse(quote.open),
                High = decimal.Parse(quote.high),
                Low = decimal.Parse(quote.low),
                Close = decimal.Parse(quote.close),
                Volume = quote.volume != null ? double.Parse(quote.volume) : 0
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
        
        return candles;
    }

    /// <summary>
    /// Maps our chart timeframe to TwelveData interval format.
    /// </summary>
    private string MapTimeframeToInterval(ChartTimeframe timeframe)
    {
        return timeframe switch
        {
            ChartTimeframe.Minutes5 => "5min",
            ChartTimeframe.Minutes15 => "15min",
            ChartTimeframe.Hours1 => "1h",
            ChartTimeframe.Hours4 => "4h",
            ChartTimeframe.Day1 => "1day",
            _ => "1h" // Default to 1 hour
        };
    }

    /// <summary>
    /// Formats a symbol for use with the TwelveData API.
    /// </summary>
    private string FormatSymbolForTwelveData(string symbol)
    {
        // TwelveData uses formats like "EUR/USD" for forex pairs
        // First, clean any existing separators
        string cleanSymbol = symbol.Replace("/", "").Replace("-", "").ToUpper();
        
        // Check if this is likely a forex pair (6 chars like "EURUSD")
        if (IsProbablyForexPair(cleanSymbol))
        {
            // Insert a slash between the currency codes (e.g., "EUR/USD")
            return cleanSymbol.Insert(3, "/");
        }
        
        // For crypto and other symbols, just use the plain format
        return cleanSymbol;
    }
    
    /// <summary>
    /// Determines if a symbol is likely a forex pair based on its format.
    /// </summary>
    private bool IsProbablyForexPair(string symbol)
    {
        // Common forex symbols are 6 characters (two 3-letter currency codes)
        if (symbol.Length != 6)
            return false;
        
        // Check if it contains common forex currency codes
        var forexCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD" };
        int matched = 0;
        
        // Look for first currency (first 3 chars)
        string firstCurrency = symbol.Substring(0, 3);
        if (forexCurrencies.Contains(firstCurrency))
            matched++;
        
        // Look for second currency (last 3 chars)
        string secondCurrency = symbol.Substring(3, 3);
        if (forexCurrencies.Contains(secondCurrency))
            matched++;
        
        // If both parts match known currencies, it's likely a forex pair
        return matched == 2;
    }
    
    /// <summary>
    /// Response structure from the TwelveData API.
    /// </summary>
    private class TwelveDataResponse
    {
        public string? meta { get; set; }
        public TwelveDataValue[]? values { get; set; }
        public string? status { get; set; }
    }

    /// <summary>
    /// Value structure from the TwelveData API.
    /// </summary>
    private class TwelveDataValue
    {
        public string datetime { get; set; } = string.Empty;
        public string open { get; set; } = string.Empty;
        public string high { get; set; } = string.Empty;
        public string low { get; set; } = string.Empty;
        public string close { get; set; } = string.Empty;
        public string? volume { get; set; }
    }
}
