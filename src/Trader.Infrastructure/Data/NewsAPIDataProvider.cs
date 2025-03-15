using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Trader.Core.Services;
using System.Linq;

namespace Trader.Infrastructure.Data
{
    /// <summary>
    /// Implementation of INewsDataProvider using NewsAPI.org
    /// </summary>
    public class NewsAPIDataProvider : INewsDataProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsAPIDataProvider> _logger;
        private readonly string _apiKey;
        
        // Cache for news data to reduce API calls
        private class CacheEntry
        {
            public List<NewsArticle> Articles { get; set; } = new();
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
        
        private readonly Dictionary<string, CacheEntry> _newsCache = new();
        
        // Cache expiration time (in minutes)
        private const int CACHE_EXPIRATION_MINUTES = 15;
        
        public NewsAPIDataProvider(
            HttpClient httpClient,
            ILogger<NewsAPIDataProvider> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Get API key from configuration or environment variable
            _apiKey = configuration["NewsAPI:ApiKey"] 
                ?? Environment.GetEnvironmentVariable("NEWSAPI_KEY")
                ?? throw new InvalidOperationException("NewsAPI key not configured. Set the NEWSAPI_KEY environment variable or configure in appsettings.");
            
            // Set up the HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TraderApp/1.0");
        }
        
        /// <inheritdoc />
        public async Task<List<NewsArticle>> GetEconomicNewsAsync(string region, int count = 10)
        {
            string cacheKey = $"economic_{region}_{count}";
            
            // Check cache first
            if (IsCacheValid(cacheKey))
            {
                _logger.LogInformation("Using cached economic news data for region {Region}", region);
                return _newsCache[cacheKey].Articles;
            }
            
            try
            {
                _logger.LogInformation("Fetching economic news for region {Region}", region);
                
                // Construct query based on region
                string query = $"economy OR finance OR inflation OR \"central bank\" OR \"interest rates\" OR {region}";
                
                // For specific regions, add more targeted keywords
                if (region.Equals("US", StringComparison.OrdinalIgnoreCase))
                {
                    query += " OR \"Federal Reserve\" OR \"Fed\" OR \"US economy\" OR \"USD\" OR \"dollar\"";
                }
                else if (region.Equals("EU", StringComparison.OrdinalIgnoreCase))
                {
                    query += " OR \"European Central Bank\" OR \"ECB\" OR \"EU economy\" OR \"EUR\" OR \"euro\"";
                }
                else if (region.Equals("UK", StringComparison.OrdinalIgnoreCase))
                {
                    query += " OR \"Bank of England\" OR \"BoE\" OR \"UK economy\" OR \"GBP\" OR \"pound\"";
                }
                else if (region.Equals("JP", StringComparison.OrdinalIgnoreCase) || region.Equals("Japan", StringComparison.OrdinalIgnoreCase))
                {
                    query += " OR \"Bank of Japan\" OR \"BoJ\" OR \"Japan economy\" OR \"JPY\" OR \"yen\"";
                }
                
                // Construct the API URL
                string url = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}&language=en&sortBy=publishedAt&pageSize={count}&apiKey={_apiKey}";
                
                // Make the API request
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var newsResponse = JsonSerializer.Deserialize<NewsAPIResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (newsResponse == null || newsResponse.Articles == null)
                {
                    _logger.LogWarning("No news articles found for region {Region}", region);
                    return new List<NewsArticle>();
                }
                
                // Convert to our model
                var articles = ConvertToNewsArticles(newsResponse.Articles, region);
                
                // Cache the results
                _newsCache[cacheKey] = new CacheEntry
                {
                    Articles = articles,
                    Timestamp = DateTime.UtcNow
                };
                
                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching economic news for region {Region}: {Message}", region, ex.Message);
                return new List<NewsArticle>();
            }
        }
        
        /// <inheritdoc />
        public async Task<List<NewsArticle>> GetSymbolNewsAsync(string symbol, int count = 10)
        {
            string cacheKey = $"symbol_{symbol}_{count}";
            
            // Reduce cache time for more frequent updates
            const int CACHE_EXPIRATION_MINUTES = 5;
            
            // Check cache first
            if (IsCacheValid(cacheKey))
            {
                _logger.LogInformation("Using cached news data for symbol {Symbol}", symbol);
                return _newsCache[cacheKey].Articles;
            }
            
            try
            {
                _logger.LogInformation("Fetching news for symbol {Symbol}", symbol);
                
                // Construct query based on symbol
                string query = "";
                
                // For forex pairs, add more specific context
                if (symbol.Length == 6 && IsForexPair(symbol))
                {
                    string baseCurrency = symbol.Substring(0, 3);
                    string quoteCurrency = symbol.Substring(3, 3);
                    
                    // Build a more targeted query for forex
                    query = $"({baseCurrency}/{quoteCurrency} OR \"{baseCurrency} {quoteCurrency}\") AND (forex OR \"foreign exchange\" OR \"currency market\" OR \"exchange rate\")";
                    
                    // Add currency-specific terms
                    var currencyTerms = GetCurrencyTerms(baseCurrency, quoteCurrency);
                    if (!string.IsNullOrEmpty(currencyTerms))
                    {
                        query += $" AND ({currencyTerms})";
                    }
                    
                    // Add market-specific terms
                    query += " AND (trading OR market OR price OR rate OR movement OR trend OR analysis OR forecast)";
                }
                // For crypto, add more specific context
                else if (symbol.EndsWith("USD") && IsCryptoPair(symbol))
                {
                    string crypto = symbol.Substring(0, symbol.Length - 3);
                    string cryptoName = GetCryptoName(crypto);
                    
                    // Build a more targeted query for crypto
                    query = $"({crypto} OR {cryptoName}) AND (cryptocurrency OR crypto OR \"digital currency\" OR \"digital asset\")";
                    
                    // Add market-specific terms
                    query += " AND (trading OR market OR price OR blockchain OR analysis OR forecast OR trend)";
                    
                    // Add specific crypto context
                    if (crypto.Equals("BTC", StringComparison.OrdinalIgnoreCase))
                    {
                        query += " AND (Bitcoin OR BTC OR \"crypto market\" OR \"digital gold\")";
                    }
                    else if (crypto.Equals("ETH", StringComparison.OrdinalIgnoreCase))
                    {
                        query += " AND (Ethereum OR ETH OR \"smart contracts\" OR DeFi)";
                    }
                }
                
                // Add time relevance to prioritize recent news
                query += " AND (recent OR latest OR update OR today OR yesterday OR week OR analysis)";
                
                // Construct the API URL with sorting by relevance first
                string url = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}&language=en&sortBy=relevancy&pageSize={count * 2}&apiKey={_apiKey}";
                
                // Make the API request
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var newsResponse = JsonSerializer.Deserialize<NewsAPIResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (newsResponse == null || newsResponse.Articles == null)
                {
                    _logger.LogWarning("No news articles found for symbol {Symbol}", symbol);
                    return new List<NewsArticle>();
                }
                
                // Convert to our model and filter for relevance
                var articles = ConvertAndFilterNewsArticles(newsResponse.Articles, symbol);
                
                // Take only the most relevant articles up to the requested count
                articles = articles.Take(count).ToList();
                
                // Cache the results
                _newsCache[cacheKey] = new CacheEntry
                {
                    Articles = articles,
                    Timestamp = DateTime.UtcNow
                };
                
                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching news for symbol {Symbol}: {Message}", symbol, ex.Message);
                return new List<NewsArticle>();
            }
        }
        
        /// <summary>
        /// Checks if the cache for a given key is valid
        /// </summary>
        private bool IsCacheValid(string cacheKey)
        {
            if (_newsCache.TryGetValue(cacheKey, out var cacheEntry))
            {
                return cacheEntry.Timestamp.AddMinutes(CACHE_EXPIRATION_MINUTES) > DateTime.UtcNow;
            }
            
            return false;
        }
        
        /// <summary>
        /// Converts NewsAPI articles to our NewsArticle model
        /// </summary>
        private List<NewsArticle> ConvertToNewsArticles(List<NewsAPIArticle> apiArticles, string context)
        {
            var articles = new List<NewsArticle>();
            
            foreach (var apiArticle in apiArticles)
            {
                var article = new NewsArticle
                {
                    Title = apiArticle.Title ?? string.Empty,
                    Description = apiArticle.Description ?? string.Empty,
                    Url = apiArticle.Url ?? string.Empty,
                    ImageUrl = apiArticle.UrlToImage ?? string.Empty,
                    Source = apiArticle.Source?.Name ?? string.Empty,
                    PublishedAt = apiArticle.PublishedAt,
                    Keywords = ExtractKeywords(apiArticle.Title, apiArticle.Description),
                    RelatedSymbols = DetermineRelatedSymbols(apiArticle.Title, apiArticle.Description, context)
                };
                
                // Determine sentiment and impact level based on content
                DetermineSentimentAndImpact(article, apiArticle.Title, apiArticle.Description);
                
                articles.Add(article);
            }
            
            return articles;
        }
        
        /// <summary>
        /// Extracts keywords from the title and description
        /// </summary>
        private List<string> ExtractKeywords(string title, string description)
        {
            var keywords = new List<string>();
            
            // Combine title and description
            string content = $"{title} {description}";
            
            // List of common financial keywords to look for
            string[] financialKeywords = new[]
            {
                "inflation", "interest rate", "GDP", "economy", "recession", "growth", "central bank",
                "Federal Reserve", "ECB", "Bank of England", "Bank of Japan", "policy", "monetary",
                "fiscal", "stimulus", "debt", "deficit", "unemployment", "jobs", "labor market",
                "housing", "manufacturing", "services", "retail", "consumer", "business", "corporate",
                "earnings", "profit", "revenue", "forecast", "outlook", "guidance", "estimate",
                "bull", "bear", "bullish", "bearish", "rally", "correction", "crash", "volatility",
                "risk", "uncertainty", "confidence", "sentiment", "optimism", "pessimism"
            };
            
            // Check for each keyword
            foreach (var keyword in financialKeywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase) && 
                    !keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
                {
                    keywords.Add(keyword);
                }
            }
            
            return keywords;
        }
        
        /// <summary>
        /// Determines related symbols based on content and context
        /// </summary>
        private List<string> DetermineRelatedSymbols(string title, string description, string context)
        {
            var symbols = new List<string>();
            
            // Add the context symbol if it's a valid forex or crypto pair
            if (IsForexPair(context) || IsCryptoPair(context))
            {
                symbols.Add(context);
            }
            
            // Combine title and description
            string content = $"{title} {description}";
            
            // Check for currency codes
            string[] currencyCodes = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD" };
            
            // Check for each currency code
            foreach (var code in currencyCodes)
            {
                if (content.Contains(code, StringComparison.OrdinalIgnoreCase))
                {
                    // For each found currency, check if it forms a pair with other currencies
                    foreach (var otherCode in currencyCodes)
                    {
                        if (code != otherCode && content.Contains(otherCode, StringComparison.OrdinalIgnoreCase))
                        {
                            symbols.Add($"{code}{otherCode}");
                        }
                    }
                }
            }
            
            // Check for crypto codes
            string[] cryptoCodes = new[] { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK", "BNB" };
            
            // Check for each crypto code
            foreach (var code in cryptoCodes)
            {
                if (content.Contains(code, StringComparison.OrdinalIgnoreCase) || 
                    content.Contains(GetCryptoName(code), StringComparison.OrdinalIgnoreCase))
                {
                    symbols.Add($"{code}USD");
                }
            }
            
            return symbols;
        }
        
        /// <summary>
        /// Determines sentiment and impact level based on content
        /// </summary>
        private void DetermineSentimentAndImpact(NewsArticle article, string title, string description)
        {
            // Combine title and description
            string content = $"{title} {description}";
            
            // Positive keywords
            string[] positiveKeywords = new[]
            {
                "growth", "rally", "surge", "gain", "rise", "increase", "improve", "positive", "optimistic",
                "bullish", "upbeat", "recovery", "strong", "boost", "confidence", "opportunity"
            };
            
            // Negative keywords
            string[] negativeKeywords = new[]
            {
                "decline", "drop", "fall", "decrease", "reduce", "negative", "pessimistic", "bearish",
                "downbeat", "recession", "weak", "slowdown", "concern", "worry", "fear", "risk",
                "uncertainty", "volatility", "crash", "crisis", "downturn", "slump"
            };
            
            // High impact keywords
            string[] highImpactKeywords = new[]
            {
                "central bank", "interest rate", "inflation", "recession", "crisis", "crash", "policy",
                "decision", "announcement", "unexpected", "surprise", "shock", "dramatic", "significant",
                "substantial", "major", "critical", "crucial", "important", "key", "essential"
            };
            
            // Count positive and negative keywords
            int positiveCount = 0;
            int negativeCount = 0;
            
            foreach (var keyword in positiveKeywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    positiveCount++;
                }
            }
            
            foreach (var keyword in negativeKeywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    negativeCount++;
                }
            }
            
            // Determine sentiment
            if (positiveCount > negativeCount)
            {
                article.Sentiment = "Positive";
            }
            else if (negativeCount > positiveCount)
            {
                article.Sentiment = "Negative";
            }
            else
            {
                article.Sentiment = "Neutral";
            }
            
            // Determine impact level
            int highImpactCount = 0;
            
            foreach (var keyword in highImpactKeywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    highImpactCount++;
                }
            }
            
            if (highImpactCount >= 3)
            {
                article.ImpactLevel = "High";
            }
            else if (highImpactCount >= 1)
            {
                article.ImpactLevel = "Medium";
            }
            else
            {
                article.ImpactLevel = "Low";
            }
        }
        
        /// <summary>
        /// Checks if a symbol is a valid forex pair
        /// </summary>
        private bool IsForexPair(string symbol)
        {
            if (symbol.Length != 6)
            {
                return false;
            }
            
            string[] validCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "NZD" };
            
            string baseCurrency = symbol.Substring(0, 3);
            string quoteCurrency = symbol.Substring(3, 3);
            
            return Array.Exists(validCurrencies, c => c.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase)) &&
                   Array.Exists(validCurrencies, c => c.Equals(quoteCurrency, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Checks if a symbol is a valid crypto pair
        /// </summary>
        private bool IsCryptoPair(string symbol)
        {
            if (!symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            string[] validCryptos = new[] { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK", "BNB" };
            
            string crypto = symbol.Substring(0, symbol.Length - 3);
            
            return Array.Exists(validCryptos, c => c.Equals(crypto, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Gets currency names for a forex pair
        /// </summary>
        private string GetCurrencyNames(string baseCurrency, string quoteCurrency)
        {
            string query = string.Empty;
            
            // Add currency names based on the codes
            if (baseCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"US Dollar\" OR \"Dollar\"";
            }
            else if (baseCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Euro\"";
            }
            else if (baseCurrency.Equals("GBP", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"British Pound\" OR \"Pound Sterling\" OR \"Sterling\"";
            }
            else if (baseCurrency.Equals("JPY", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Japanese Yen\" OR \"Yen\"";
            }
            else if (baseCurrency.Equals("AUD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Australian Dollar\" OR \"Aussie\"";
            }
            else if (baseCurrency.Equals("CAD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Canadian Dollar\" OR \"Loonie\"";
            }
            else if (baseCurrency.Equals("CHF", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Swiss Franc\" OR \"Franc\"";
            }
            else if (baseCurrency.Equals("NZD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"New Zealand Dollar\" OR \"Kiwi\"";
            }
            
            // Add quote currency names
            if (quoteCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"US Dollar\" OR \"Dollar\"";
            }
            else if (quoteCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Euro\"";
            }
            else if (quoteCurrency.Equals("GBP", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"British Pound\" OR \"Pound Sterling\" OR \"Sterling\"";
            }
            else if (quoteCurrency.Equals("JPY", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Japanese Yen\" OR \"Yen\"";
            }
            else if (quoteCurrency.Equals("AUD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Australian Dollar\" OR \"Aussie\"";
            }
            else if (quoteCurrency.Equals("CAD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Canadian Dollar\" OR \"Loonie\"";
            }
            else if (quoteCurrency.Equals("CHF", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"Swiss Franc\" OR \"Franc\"";
            }
            else if (quoteCurrency.Equals("NZD", StringComparison.OrdinalIgnoreCase))
            {
                query += " OR \"New Zealand Dollar\" OR \"Kiwi\"";
            }
            
            return query;
        }
        
        /// <summary>
        /// Gets the full name of a cryptocurrency
        /// </summary>
        private string GetCryptoName(string code)
        {
            return code.ToUpperInvariant() switch
            {
                "BTC" => "Bitcoin",
                "ETH" => "Ethereum",
                "XRP" => "Ripple",
                "LTC" => "Litecoin",
                "BCH" => "Bitcoin Cash",
                "ADA" => "Cardano",
                "DOT" => "Polkadot",
                "LINK" => "Chainlink",
                "BNB" => "Binance Coin",
                _ => code
            };
        }
        
        /// <summary>
        /// Gets specific terms for currency pairs to improve relevance
        /// </summary>
        private string GetCurrencyTerms(string baseCurrency, string quoteCurrency)
        {
            var terms = new List<string>();
            
            // Add currency-specific terms
            if (baseCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
            {
                terms.Add("ECB OR \"European Central Bank\" OR Eurozone OR \"Euro currency\"");
            }
            else if (baseCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                terms.Add("Fed OR \"Federal Reserve\" OR \"US Dollar\" OR \"Dollar strength\"");
            }
            else if (baseCurrency.Equals("GBP", StringComparison.OrdinalIgnoreCase))
            {
                terms.Add("BoE OR \"Bank of England\" OR \"British Pound\" OR Sterling");
            }
            else if (baseCurrency.Equals("JPY", StringComparison.OrdinalIgnoreCase))
            {
                terms.Add("BoJ OR \"Bank of Japan\" OR \"Japanese Yen\" OR \"Yen strength\"");
            }
            
            // Add quote currency terms if different
            if (quoteCurrency.Equals("EUR", StringComparison.OrdinalIgnoreCase) && baseCurrency != "EUR")
            {
                terms.Add("ECB OR \"European Central Bank\" OR Eurozone");
            }
            else if (quoteCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase) && baseCurrency != "USD")
            {
                terms.Add("Fed OR \"Federal Reserve\" OR \"US economy\"");
            }
            
            return string.Join(" OR ", terms);
        }
        
        /// <summary>
        /// Converts and filters news articles for relevance
        /// </summary>
        private List<NewsArticle> ConvertAndFilterNewsArticles(List<NewsAPIArticle> apiArticles, string symbol)
        {
            var articles = new List<NewsArticle>();
            var isForex = IsForexPair(symbol);
            var isCrypto = IsCryptoPair(symbol);
            
            foreach (var apiArticle in apiArticles)
            {
                // Skip articles without sufficient content
                if (string.IsNullOrWhiteSpace(apiArticle.Title) || string.IsNullOrWhiteSpace(apiArticle.Description))
                {
                    continue;
                }
                
                var content = $"{apiArticle.Title} {apiArticle.Description}".ToLower();
                
                // Skip articles that don't mention the symbol or related terms
                if (isForex)
                {
                    string baseCurrency = symbol.Substring(0, 3);
                    string quoteCurrency = symbol.Substring(3, 3);
                    
                    // Check for currency mentions
                    if (!content.Contains(baseCurrency.ToLower()) && !content.Contains(quoteCurrency.ToLower()) &&
                        !content.Contains("forex") && !content.Contains("currency") && 
                        !content.Contains("exchange rate"))
                    {
                        continue;
                    }
                }
                else if (isCrypto)
                {
                    string crypto = symbol.Substring(0, symbol.Length - 3);
                    string cryptoName = GetCryptoName(crypto).ToLower();
                    
                    // Check for crypto mentions
                    if (!content.Contains(crypto.ToLower()) && !content.Contains(cryptoName) &&
                        !content.Contains("crypto") && !content.Contains("bitcoin"))
                    {
                        continue;
                    }
                }
                
                var article = new NewsArticle
                {
                    Title = apiArticle.Title ?? string.Empty,
                    Description = apiArticle.Description ?? string.Empty,
                    Url = apiArticle.Url ?? string.Empty,
                    ImageUrl = apiArticle.UrlToImage ?? string.Empty,
                    Source = apiArticle.Source?.Name ?? string.Empty,
                    PublishedAt = apiArticle.PublishedAt,
                    Keywords = ExtractKeywords(apiArticle.Title, apiArticle.Description),
                    RelatedSymbols = DetermineRelatedSymbols(apiArticle.Title, apiArticle.Description, symbol)
                };
                
                // Determine sentiment and impact level based on content
                DetermineSentimentAndImpact(article, apiArticle.Title, apiArticle.Description);
                
                // Only add articles with medium or high impact
                if (article.ImpactLevel != "Low")
                {
                    articles.Add(article);
                }
            }
            
            return articles;
        }
    }
    
    /// <summary>
    /// Response model for NewsAPI.org
    /// </summary>
    internal class NewsAPIResponse
    {
        public string Status { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<NewsAPIArticle> Articles { get; set; } = new();
    }
    
    /// <summary>
    /// Article model for NewsAPI.org
    /// </summary>
    internal class NewsAPIArticle
    {
        public NewsAPISource? Source { get; set; }
        public string? Author { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? UrlToImage { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Content { get; set; }
    }
    
    /// <summary>
    /// Source model for NewsAPI.org
    /// </summary>
    internal class NewsAPISource
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
