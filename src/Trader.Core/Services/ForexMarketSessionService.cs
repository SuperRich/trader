using System;
using System.Collections.Generic;

namespace Trader.Core.Services;

/// <summary>
/// Service for identifying forex market sessions and providing liquidity information.
/// </summary>
public class ForexMarketSessionService
{
    /// <summary>
    /// Enum representing the major forex market sessions.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum MarketSession
    {
        /// <summary>
        /// Asian session (Tokyo, Singapore, Hong Kong)
        /// </summary>
        Asian,
        
        /// <summary>
        /// London session (European markets)
        /// </summary>
        London,
        
        /// <summary>
        /// New York session (North American markets)
        /// </summary>
        NewYork,
        
        /// <summary>
        /// Overlap between Asian and London sessions
        /// </summary>
        AsianLondonOverlap,
        
        /// <summary>
        /// Overlap between London and New York sessions (highest liquidity)
        /// </summary>
        LondonNewYorkOverlap,
        
        /// <summary>
        /// No major session is active
        /// </summary>
        Closed
    }

    /// <summary>
    /// Information about a forex market session.
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// The current market session.
        /// </summary>
        public MarketSession CurrentSession { get; set; }
        
        /// <summary>
        /// Description of the current session.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Liquidity level from 1 (lowest) to 5 (highest).
        /// </summary>
        public int LiquidityLevel { get; set; }
        
        /// <summary>
        /// Recommended session for executing trades for the given currency pair.
        /// </summary>
        public MarketSession RecommendedSession { get; set; }
        
        /// <summary>
        /// Explanation of why the recommended session is optimal.
        /// </summary>
        public string RecommendationReason { get; set; } = string.Empty;
        
        /// <summary>
        /// Time remaining until the next session starts (in hours and minutes).
        /// </summary>
        public TimeSpan TimeUntilNextSession { get; set; }
        
        /// <summary>
        /// The next session that will become active.
        /// </summary>
        public MarketSession NextSession { get; set; }
        
        /// <summary>
        /// Current UTC time used for the calculation.
        /// </summary>
        public DateTime CurrentTimeUtc { get; set; }
        
        /// <summary>
        /// Next session start time in UTC.
        /// </summary>
        public DateTime NextSessionStartTimeUtc { get; set; }
    }

    // Session times in UTC
    private static readonly TimeSpan AsianStart = new TimeSpan(23, 0, 0); // 23:00 UTC (previous day)
    private static readonly TimeSpan AsianEnd = new TimeSpan(8, 0, 0);    // 08:00 UTC
    private static readonly TimeSpan LondonStart = new TimeSpan(7, 0, 0);  // 07:00 UTC
    private static readonly TimeSpan LondonEnd = new TimeSpan(16, 0, 0);   // 16:00 UTC
    private static readonly TimeSpan NewYorkStart = new TimeSpan(12, 0, 0); // 12:00 UTC
    private static readonly TimeSpan NewYorkEnd = new TimeSpan(21, 0, 0);   // 21:00 UTC

    // Currency pair to optimal session mapping
    private readonly Dictionary<string, MarketSession> _optimalSessionMap = new Dictionary<string, MarketSession>(StringComparer.OrdinalIgnoreCase)
    {
        // Major pairs - generally best during London/NY overlap
        { "EURUSD", MarketSession.LondonNewYorkOverlap },
        { "GBPUSD", MarketSession.LondonNewYorkOverlap },
        { "USDJPY", MarketSession.LondonNewYorkOverlap },
        { "USDCHF", MarketSession.LondonNewYorkOverlap },
        
        // Yen crosses - often more active during Asian session
        { "EURJPY", MarketSession.Asian },
        { "GBPJPY", MarketSession.Asian },
        { "AUDJPY", MarketSession.Asian },
        
        // Commodity currencies - often follow their regional sessions
        { "AUDUSD", MarketSession.Asian },
        { "NZDUSD", MarketSession.Asian },
        { "USDCAD", MarketSession.NewYork },
        
        // Exotic pairs - follow their regional markets
        { "USDHKD", MarketSession.Asian },
        { "USDSGD", MarketSession.Asian },
        { "EURNOK", MarketSession.London },
        { "EURSEK", MarketSession.London }
        
        // Removed crypto pairs from this mapping as they trade 24/7
    };

    /// <summary>
    /// Determines if a symbol is a cryptocurrency pair.
    /// </summary>
    /// <param name="symbol">The symbol to check.</param>
    /// <returns>True if the symbol is a cryptocurrency pair, false otherwise.</returns>
    private bool IsCryptoPair(string symbol)
    {
        // Common cryptocurrencies
        var cryptoCurrencies = new[] { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK", "XLM", "SOL", "DOGE" };
        
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
    /// Gets information about the current forex market session.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze.</param>
    /// <param name="currentTimeUtc">Optional parameter to specify a time (defaults to current UTC time).</param>
    /// <returns>Information about the current market session and recommendations.</returns>
    public SessionInfo GetCurrentSessionInfo(string currencyPair, DateTime? currentTimeUtc = null)
    {
        // Check if this is a cryptocurrency pair
        bool isCrypto = IsCryptoPair(currencyPair);
        
        // Use provided time or current UTC time
        var now = currentTimeUtc ?? DateTime.UtcNow;
        var timeOfDay = now.TimeOfDay;
        
        // Determine current session
        var currentSession = DetermineCurrentSession(timeOfDay);
        
        // For cryptocurrencies, always set current session as the recommended session
        // since crypto markets trade 24/7
        var recommendedSession = isCrypto ? currentSession : GetRecommendedSession(currencyPair);
        
        // Calculate time until next session and next session start time
        var (nextSession, timeUntilNext, nextSessionStartTime) = CalculateNextSession(now);
        
        return new SessionInfo
        {
            CurrentSession = currentSession,
            Description = isCrypto ? GetCryptoSessionDescription(currentSession) : GetSessionDescription(currentSession),
            LiquidityLevel = isCrypto ? 5 : GetLiquidityLevel(currentSession, currencyPair), // Crypto has high liquidity 24/7
            RecommendedSession = recommendedSession,
            RecommendationReason = isCrypto ? GetCryptoRecommendationReason(currencyPair) : GetRecommendationReason(recommendedSession, currencyPair),
            TimeUntilNextSession = timeUntilNext,
            NextSession = nextSession,
            CurrentTimeUtc = now,
            NextSessionStartTimeUtc = nextSessionStartTime
        };
    }

    /// <summary>
    /// Determines the current forex market session based on the time of day.
    /// </summary>
    private MarketSession DetermineCurrentSession(TimeSpan timeOfDay)
    {
        // Check for Asian session (crosses midnight UTC)
        bool isAsianSession = (timeOfDay >= AsianStart) || (timeOfDay < AsianEnd);
        
        // Check for London session
        bool isLondonSession = (timeOfDay >= LondonStart) && (timeOfDay < LondonEnd);
        
        // Check for New York session
        bool isNewYorkSession = (timeOfDay >= NewYorkStart) && (timeOfDay < NewYorkEnd);
        
        // Determine overlaps and current session
        if (isLondonSession && isNewYorkSession)
        {
            return MarketSession.LondonNewYorkOverlap;
        }
        else if (isAsianSession && isLondonSession)
        {
            return MarketSession.AsianLondonOverlap;
        }
        else if (isAsianSession)
        {
            return MarketSession.Asian;
        }
        else if (isLondonSession)
        {
            return MarketSession.London;
        }
        else if (isNewYorkSession)
        {
            return MarketSession.NewYork;
        }
        else
        {
            return MarketSession.Closed;
        }
    }

    /// <summary>
    /// Gets the recommended trading session for a specific currency pair.
    /// </summary>
    private MarketSession GetRecommendedSession(string currencyPair)
    {
        // Default to London-NY overlap if not specifically mapped
        return _optimalSessionMap.TryGetValue(currencyPair, out var session) 
            ? session 
            : MarketSession.LondonNewYorkOverlap;
    }

    /// <summary>
    /// Calculates the next session that will become active, the time until it starts, and its start time.
    /// </summary>
    private (MarketSession, TimeSpan, DateTime) CalculateNextSession(DateTime currentTimeUtc)
    {
        var timeOfDay = currentTimeUtc.TimeOfDay;
        var today = currentTimeUtc.Date;
        var tomorrow = today.AddDays(1);
        
        // Define all session start times in sequence with their actual dates
        var sessionStarts = new List<(MarketSession, DateTime)>();
        
        // Add today's sessions if they haven't started yet
        if (timeOfDay < LondonStart)
        {
            sessionStarts.Add((MarketSession.London, today.Add(LondonStart)));
        }
        
        if (timeOfDay < NewYorkStart)
        {
            sessionStarts.Add((MarketSession.NewYork, today.Add(NewYorkStart)));
        }
        
        // Add tomorrow's Asian session (which actually starts tonight)
        if (timeOfDay < AsianStart)
        {
            sessionStarts.Add((MarketSession.Asian, today.Add(AsianStart)));
        }
        else
        {
            // We're past Asian start time today, so the next Asian session is tomorrow night
            sessionStarts.Add((MarketSession.Asian, tomorrow.Add(AsianStart)));
        }
        
        // Add tomorrow's sessions
        sessionStarts.Add((MarketSession.London, tomorrow.Add(LondonStart)));
        sessionStarts.Add((MarketSession.NewYork, tomorrow.Add(NewYorkStart)));
        
        // Sort by start time
        sessionStarts.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        
        // Find the next session
        var nextSessionInfo = sessionStarts[0];
        var timeUntilNext = nextSessionInfo.Item2 - currentTimeUtc;
        
        return (nextSessionInfo.Item1, timeUntilNext, nextSessionInfo.Item2);
    }

    /// <summary>
    /// Gets a description of the current market session.
    /// </summary>
    private string GetSessionDescription(MarketSession session)
    {
        return session switch
        {
            MarketSession.Asian => "Asian Session (Tokyo, Singapore, Hong Kong) - 23:00-08:00 UTC - Moderate liquidity, often range-bound trading",
            MarketSession.London => "London Session (European markets) - 07:00-16:00 UTC - High liquidity, often trending movements",
            MarketSession.NewYork => "New York Session (North American markets) - 12:00-21:00 UTC - High liquidity, often volatile movements",
            MarketSession.AsianLondonOverlap => "Asian-London Overlap - 07:00-08:00 UTC - Increasing liquidity as European markets open",
            MarketSession.LondonNewYorkOverlap => "London-New York Overlap - 12:00-16:00 UTC - Highest liquidity period, often largest price movements",
            MarketSession.Closed => "Market is between major sessions - Lower liquidity, often consolidation",
            _ => "Unknown session"
        };
    }

    /// <summary>
    /// Gets the liquidity level for the current session and currency pair.
    /// </summary>
    private int GetLiquidityLevel(MarketSession session, string currencyPair)
    {
        // Base liquidity level on session
        int baseLevel = session switch
        {
            MarketSession.LondonNewYorkOverlap => 5, // Highest liquidity
            MarketSession.London => 4,
            MarketSession.NewYork => 4,
            MarketSession.AsianLondonOverlap => 3,
            MarketSession.Asian => 2,
            MarketSession.Closed => 1, // Lowest liquidity
            _ => 1
        };
        
        // Adjust for specific currency pairs
        if (currencyPair.Contains("JPY", StringComparison.OrdinalIgnoreCase) && session == MarketSession.Asian)
        {
            // Yen pairs more liquid during Asian session
            return Math.Min(baseLevel + 1, 5);
        }
        else if (currencyPair.Contains("AUD", StringComparison.OrdinalIgnoreCase) && session == MarketSession.Asian)
        {
            // AUD pairs more liquid during Asian session
            return Math.Min(baseLevel + 1, 5);
        }
        else if (currencyPair.Contains("CAD", StringComparison.OrdinalIgnoreCase) && session == MarketSession.NewYork)
        {
            // CAD pairs more liquid during NY session
            return Math.Min(baseLevel + 1, 5);
        }
        else if ((currencyPair == "EURUSD" || currencyPair == "GBPUSD") && 
                 (session == MarketSession.London || session == MarketSession.LondonNewYorkOverlap))
        {
            // Major pairs most liquid during London/NY
            return 5;
        }
        
        return baseLevel;
    }

    /// <summary>
    /// Gets the reason why a particular session is recommended for a currency pair.
    /// </summary>
    private string GetRecommendationReason(MarketSession session, string currencyPair)
    {
        return session switch
        {
            MarketSession.Asian => $"The Asian session (23:00-08:00 UTC) provides better liquidity for {currencyPair} due to regional market activity. Expect moderate volatility and potentially range-bound trading.",
            
            MarketSession.London => $"The London session (07:00-16:00 UTC) is optimal for {currencyPair} with high liquidity and often establishes the daily trend direction. European economic news has significant impact during this time.",
            
            MarketSession.NewYork => $"The New York session (12:00-21:00 UTC) offers strong liquidity for {currencyPair} with US economic data releases often creating trading opportunities. Volatility can be high during this period.",
            
            MarketSession.AsianLondonOverlap => $"The Asian-London overlap (07:00-08:00 UTC) provides increasing liquidity for {currencyPair} as European traders enter the market. This transition period can offer good entry points as new trends develop.",
            
            MarketSession.LondonNewYorkOverlap => $"The London-New York overlap (12:00-16:00 UTC) provides the highest liquidity for {currencyPair}, with maximum market participation and often the largest price movements of the day. This is generally the optimal trading window.",
            
            MarketSession.Closed => $"Currently between major sessions. For {currencyPair}, it's recommended to wait for a major session to open for better liquidity and trading conditions.",
            
            _ => "Unknown session recommendation"
        };
    }

    /// <summary>
    /// Gets a description of the current market session for cryptocurrencies.
    /// </summary>
    private string GetCryptoSessionDescription(MarketSession session)
    {
        return $"{session} - Cryptocurrency markets trade 24/7 with consistent liquidity across all sessions";
    }

    /// <summary>
    /// Gets the reason why all sessions are suitable for cryptocurrency trading.
    /// </summary>
    private string GetCryptoRecommendationReason(string currencyPair)
    {
        return $"{currencyPair} is a cryptocurrency pair that trades 24/7 with consistent liquidity. While traditional forex sessions still influence volume somewhat, cryptocurrencies can be traded at any time without significant liquidity concerns.";
    }
} 