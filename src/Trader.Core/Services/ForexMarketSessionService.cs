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
        { "EURSEK", MarketSession.London },
        
        // Crypto pairs - 24/7 but often follow US trading hours
        { "BTCUSD", MarketSession.NewYork },
        { "ETHUSD", MarketSession.NewYork }
    };

    /// <summary>
    /// Gets information about the current forex market session.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze.</param>
    /// <param name="currentTimeUtc">Optional parameter to specify a time (defaults to current UTC time).</param>
    /// <returns>Information about the current market session and recommendations.</returns>
    public SessionInfo GetCurrentSessionInfo(string currencyPair, DateTime? currentTimeUtc = null)
    {
        // Use provided time or current UTC time
        var now = currentTimeUtc ?? DateTime.UtcNow;
        var timeOfDay = now.TimeOfDay;
        
        // Determine current session
        var currentSession = DetermineCurrentSession(timeOfDay);
        
        // Get recommended session for this currency pair
        var recommendedSession = GetRecommendedSession(currencyPair);
        
        // Calculate time until next session
        var (nextSession, timeUntilNext) = CalculateNextSession(timeOfDay);
        
        return new SessionInfo
        {
            CurrentSession = currentSession,
            Description = GetSessionDescription(currentSession),
            LiquidityLevel = GetLiquidityLevel(currentSession, currencyPair),
            RecommendedSession = recommendedSession,
            RecommendationReason = GetRecommendationReason(recommendedSession, currencyPair),
            TimeUntilNextSession = timeUntilNext,
            NextSession = nextSession
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
    /// Calculates the next session that will become active and the time until it starts.
    /// </summary>
    private (MarketSession, TimeSpan) CalculateNextSession(TimeSpan currentTime)
    {
        // Define all session start times in sequence
        var sessionStarts = new List<(MarketSession, TimeSpan)>
        {
            (MarketSession.Asian, AsianStart),
            (MarketSession.London, LondonStart),
            (MarketSession.NewYork, NewYorkStart)
        };
        
        // Find the next session start time
        foreach (var (session, startTime) in sessionStarts)
        {
            if (currentTime < startTime)
            {
                return (session, startTime - currentTime);
            }
        }
        
        // If we're after all session starts today, the next is Asian tomorrow
        var timeUntilTomorrow = new TimeSpan(24, 0, 0) - currentTime + AsianStart;
        return (MarketSession.Asian, timeUntilTomorrow);
    }

    /// <summary>
    /// Gets a description of the current market session.
    /// </summary>
    private string GetSessionDescription(MarketSession session)
    {
        return session switch
        {
            MarketSession.Asian => "Asian Session (Tokyo, Singapore, Hong Kong) - Moderate liquidity, often range-bound trading",
            MarketSession.London => "London Session (European markets) - High liquidity, often trending movements",
            MarketSession.NewYork => "New York Session (North American markets) - High liquidity, often volatile movements",
            MarketSession.AsianLondonOverlap => "Asian-London Overlap - Increasing liquidity as European markets open",
            MarketSession.LondonNewYorkOverlap => "London-New York Overlap - Highest liquidity period, often largest price movements",
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
            MarketSession.Asian => $"The Asian session provides better liquidity for {currencyPair} due to regional market activity. Expect moderate volatility and potentially range-bound trading.",
            
            MarketSession.London => $"The London session is optimal for {currencyPair} with high liquidity and often establishes the daily trend direction. European economic news has significant impact during this time.",
            
            MarketSession.NewYork => $"The New York session offers strong liquidity for {currencyPair} with US economic data releases often creating trading opportunities. Volatility can be high during this period.",
            
            MarketSession.AsianLondonOverlap => $"The Asian-London overlap provides increasing liquidity for {currencyPair} as European traders enter the market. This transition period can offer good entry points as new trends develop.",
            
            MarketSession.LondonNewYorkOverlap => $"The London-New York overlap (13:00-16:00 UTC) provides the highest liquidity for {currencyPair}, with maximum market participation and often the largest price movements of the day. This is generally the optimal trading window.",
            
            MarketSession.Closed => $"Currently between major sessions. For {currencyPair}, it's recommended to wait for a major session to open for better liquidity and trading conditions.",
            
            _ => "Unknown session recommendation"
        };
    }
} 