using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Data;

/// <summary>
/// Data provider that fetches market data from Polygon.io API for forex and crypto assets.
/// </summary>
public class PolygonDataProvider : IForexDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolygonDataProvider> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.polygon.io";

    public PolygonDataProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PolygonDataProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get API key from configuration
        _apiKey = configuration["Polygon:ApiKey"] 
            ?? throw new ArgumentNullException(nameof(configuration), "Polygon API key is required");
        
        // Set up the HttpClient
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TraderApp/1.0");
    }

    /// <summary>
    /// Gets candle data for the specified symbol from Polygon.io.
    /// </summary>
    /// <param name="symbol">The symbol to get data for (e.g., "EURUSD", "BTCUSD")</param>
    /// <param name="timeframe">The timeframe for the candles</param>
    /// <param name="candleCount">The number of candles to retrieve</param>
    /// <returns>A list of candle data</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails</exception>
    /// <exception cref="InvalidOperationException">Thrown when the API returns invalid data</exception>
    public async Task<List<CandleData>> GetCandleDataAsync(string symbol, ChartTimeframe timeframe, int candleCount = 100)
    {
        // Determine if this is forex or crypto
        bool isForex = IsForexPair(symbol);
        bool isCrypto = IsCryptoPair(symbol);
        
        if (!isForex && !isCrypto)
        {
            throw new ArgumentException($"Symbol {symbol} is not a recognized forex or crypto pair");
        }
        
        // Format the symbol appropriately for the API
        string formattedSymbol = FormatSymbolForPolygon(symbol, isForex, isCrypto);
        
        _logger.LogInformation("Fetching Polygon.io data for {Symbol} ({FormattedSymbol}) at {Timeframe} timeframe", 
            symbol, formattedSymbol, timeframe);
        
        // Get current date for the grouped daily endpoint
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        
        // Build the endpoint URL based on whether it's forex or crypto
        string endpoint;
        
        if (isForex)
        {
            // Use the forex endpoint for current day
            endpoint = $"/v2/aggs/grouped/locale/global/market/fx/{today}?adjusted=true&apiKey={_apiKey}";
        }
        else // isCrypto
        {
            // Use the crypto endpoint for current day
            endpoint = $"/v2/aggs/grouped/locale/global/market/crypto/{today}?adjusted=true&apiKey={_apiKey}";
        }
        
        // Make the API request
        var response = await _httpClient.GetAsync(endpoint);
        
        // Check if the request was successful
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Polygon.io API request failed with status {StatusCode}: {ErrorMessage}", 
                response.StatusCode, errorContent);
            
            throw new HttpRequestException($"Polygon.io API request failed: {response.StatusCode} - {errorContent}");
        }
        
        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync();
        var polygonResponse = JsonSerializer.Deserialize<PolygonResponse>(responseContent);
        
        if (polygonResponse?.results == null || polygonResponse.results.Length == 0)
        {
            _logger.LogWarning("No results returned from Polygon.io for {Symbol}", symbol);
            throw new InvalidOperationException($"No data returned from Polygon.io for symbol {symbol}");
        }
        
        // Filter results to get only the requested symbol
        var symbolResults = polygonResponse.results
            .Where(r => r.T == formattedSymbol || 
                       (isForex && r.T.Replace("C:", "") == formattedSymbol) ||
                       (isCrypto && r.T.Replace("X:", "") == formattedSymbol))
            .ToArray();
            
        if (symbolResults.Length == 0)
        {
            _logger.LogWarning("Symbol {Symbol} not found in Polygon.io results", formattedSymbol);
            throw new InvalidOperationException($"Symbol {symbol} not found in Polygon.io results");
        }
        
        // Convert to our CandleData format
        var candles = symbolResults
            .OrderBy(r => r.t)
            .Select(result => new CandleData
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(result.t).DateTime,
                Open = (decimal)result.o,
                High = (decimal)result.h,
                Low = (decimal)result.l,
                Close = (decimal)result.c,
                Volume = result.v
            })
            .ToList();
            
        _logger.LogInformation("Successfully retrieved {Count} candles for {Symbol}", candles.Count, symbol);
        
        // If we don't have enough candles, throw an exception
        if (candles.Count < candleCount)
        {
            _logger.LogWarning("Only {ActualCount} candles available for {Symbol}, but {RequestedCount} were requested", 
                candles.Count, symbol, candleCount);
            
            // Return what we have instead of throwing an exception
            // This ensures we return data even if less than requested
        }
        
        return candles.Take(Math.Min(candles.Count, candleCount)).ToList();
    }

    /// <summary>
    /// Formats a symbol for use with the Polygon.io API.
    /// </summary>
    private string FormatSymbolForPolygon(string symbol, bool isForex, bool isCrypto)
    {
        // Strip out any separators like "/" or "-"
        string cleanSymbol = symbol.Replace("/", "").Replace("-", "").ToUpper();
        
        if (isForex)
            return $"C:{cleanSymbol}"; // Forex pairs use C: prefix
        else if (isCrypto)
            return $"X:{cleanSymbol}"; // Crypto pairs use X: prefix
        else
            return cleanSymbol; // Stocks use no prefix
    }
    
    /// <summary>
    /// Determines if a symbol is a forex pair.
    /// </summary>
    private bool IsForexPair(string symbol)
    {
        // Common forex currencies
        var forexCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD" };
        
        // Check if any two forex currencies are in the symbol
        int matched = 0;
        foreach (var currency in forexCurrencies)
        {
            if (symbol.Contains(currency, StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
        }
        
        return matched >= 2;
    }
    
    /// <summary>
    /// Determines if a symbol is a cryptocurrency pair.
    /// </summary>
    private bool IsCryptoPair(string symbol)
    {
        // Common cryptocurrencies
        var cryptoCurrencies = new[] { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK", "XLM" };
        
        // Check if the symbol contains any cryptocurrency code
        foreach (var crypto in cryptoCurrencies)
        {
            if (symbol.Contains(crypto, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Response structure from the Polygon.io API.
    /// </summary>
    private class PolygonResponse
    {
        public string? status { get; set; }
        public string? request_id { get; set; }
        public int count { get; set; }
        public PolygonResult[]? results { get; set; }
    }

    /// <summary>
    /// Result structure from the Polygon.io API.
    /// </summary>
    private class PolygonResult
    {
        public string? T { get; set; }  // Ticker
        public int v { get; set; }      // Volume 
        public double o { get; set; }   // Open price
        public double c { get; set; }   // Close price
        public double h { get; set; }   // High price
        public double l { get; set; }   // Low price
        public long t { get; set; }     // Timestamp
        public int n { get; set; }      // Number of trades
    }
}