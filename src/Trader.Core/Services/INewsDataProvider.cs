using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Trader.Core.Services
{
    /// <summary>
    /// Interface for services that provide news data for financial markets.
    /// </summary>
    public interface INewsDataProvider
    {
        /// <summary>
        /// Gets economic news for a specific region.
        /// </summary>
        /// <param name="region">The region to get news for (e.g., "US", "EU", "UK").</param>
        /// <param name="count">The number of news articles to return (default: 10).</param>
        /// <returns>A list of news articles related to the specified region.</returns>
        Task<List<NewsArticle>> GetEconomicNewsAsync(string region, int count = 10);
        
        /// <summary>
        /// Gets news related to a specific currency pair or symbol.
        /// </summary>
        /// <param name="symbol">The symbol to get news for (e.g., "EURUSD", "BTCUSD").</param>
        /// <param name="count">The number of news articles to return (default: 10).</param>
        /// <returns>A list of news articles related to the specified symbol.</returns>
        Task<List<NewsArticle>> GetSymbolNewsAsync(string symbol, int count = 10);
    }
    
    /// <summary>
    /// Represents a news article with relevant information for traders.
    /// </summary>
    public class NewsArticle
    {
        /// <summary>
        /// The title of the news article.
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// A brief description or summary of the article.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// The URL to the full article.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// The URL to the article's image, if available.
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// The source of the article (e.g., "Bloomberg", "Reuters").
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// The publication date and time of the article.
        /// </summary>
        public DateTime PublishedAt { get; set; }
        
        /// <summary>
        /// The sentiment of the article (Positive, Negative, Neutral).
        /// </summary>
        public string Sentiment { get; set; } = "Neutral";
        
        /// <summary>
        /// The impact level of the news (High, Medium, Low).
        /// </summary>
        public string ImpactLevel { get; set; } = "Low";
        
        /// <summary>
        /// Keywords or tags associated with the article.
        /// </summary>
        public List<string> Keywords { get; set; } = new List<string>();
        
        /// <summary>
        /// Currency pairs or symbols that may be affected by this news.
        /// </summary>
        public List<string> RelatedSymbols { get; set; } = new List<string>();
    }
}
