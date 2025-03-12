# Comprehensive User Guide: TradingViewAnalyzer API for Day Traders

## Table of Contents
1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [API Endpoints](#api-endpoints)
4. [Trading Strategies](#trading-strategies)
5. [Position Sizing and Risk Management](#position-sizing-and-risk-management)
6. [Market Sessions and Timing](#market-sessions-and-timing)
7. [Advanced Techniques](#advanced-techniques)
8. [Troubleshooting](#troubleshooting)
9. [Tips for Maximizing Profits](#tips-for-maximizing-profits)

## Introduction

The TradingViewAnalyzer API is a powerful tool that leverages AI to provide trading recommendations for forex and cryptocurrency pairs. It analyzes market data across multiple timeframes, identifies key support and resistance levels, and generates actionable trade setups with precise entry, stop-loss, and take-profit levels.

This guide is designed for experienced day traders who want to integrate this API into their trading workflow to enhance decision-making and potentially increase profitability.

## Getting Started

### API Requirements

To use the TradingViewAnalyzer API, you'll need:

1. **API Keys**: The system supports two AI providers:
   - OpenRouter API key (preferred)
   - Perplexity API key (fallback)

2. **Data Provider**: The system can use various data providers:
   - Polygon
   - TraderMade
   - TwelveData
   - Mock (for testing)

### Configuration

The API prioritizes OpenRouter if available, falling back to Perplexity if needed. You can set your API keys in the configuration or as environment variables:

```
OpenRouter:ApiKey or TRADER_OPENROUTER_API_KEY
Perplexity:ApiKey or TRADER_PERPLEXITY_API_KEY
```

## API Endpoints

### 1. Analyze a Specific Currency Pair

```
GET /api/trading/analyze/{symbol}
```

This endpoint provides a detailed analysis of a specific currency pair, including:
- Current market sentiment (Bullish, Bearish, Neutral)
- Trade recommendation (Buy, Sell, None)
- Entry, stop-loss, and take-profit levels
- Risk-reward ratio
- Market session information
- Position sizing recommendations

**Optional Parameters:**
- `accountBalance`: Your trading account balance
- `leverage`: Your account leverage
- `targetProfits`: Comma-separated list of profit targets

**Example:**
```
GET /api/trading/analyze/EURUSD?accountBalance=1000&leverage=100&targetProfits=10,20,50
```

### 2. Get Trading Recommendations

```
GET /api/trading/recommendations?count=3
```

This endpoint returns the top trading opportunities across multiple currency pairs, prioritizing setups with favorable risk-reward ratios (at least 1.5:1).

**Parameters:**
- `count`: Number of recommendations to return (default: 3, max: 5)
- `accountBalance`: Your trading account balance
- `leverage`: Your account leverage
- `targetProfits`: Comma-separated list of profit targets

### 3. Analyze with Specific Data Provider

```
GET /api/trading/analyze/{symbol}/{provider}
```

This endpoint allows you to specify which data provider to use for the analysis.

**Example:**
```
GET /api/trading/analyze/GBPUSD/Polygon
```

### 4. Get Candle Data

```
GET /api/forex/candles/{symbol}/{timeframe}/{count}/{provider}
```

This endpoint returns raw candle data for a specific symbol and timeframe.

**Example:**
```
GET /api/forex/candles/EURUSD/Hours4/12/TraderMade
```

## Trading Strategies

The TradingViewAnalyzer excels at identifying high-probability trade setups based on technical analysis. Here's how to leverage its capabilities:

### 1. Multi-Timeframe Analysis

The API analyzes both 1-hour and 4-hour timeframes to filter out market noise and identify reliable signals. This approach helps you:

- Identify the primary trend on higher timeframes
- Find optimal entry points on lower timeframes
- Avoid false breakouts and whipsaws

### 2. Order Types

The API recommends specific order types based on price action:

- **Market Orders**: For immediate execution at current price
- **Limit Orders**: 
  - Buy Limit: When best entry is below current price
  - Sell Limit: When best entry is above current price
- **Stop Orders**:
  - Buy Stop: When best entry is above current price (breakout confirmation)
  - Sell Stop: When best entry is below current price (breakdown confirmation)

### 3. Entry Timing

The API provides two key pieces of information for timing your entries:

- `BestEntryPrice`: The optimal price level for entry
- `TimeToBestEntry`: Estimated time until the best entry price might be reached
- `IsSafeToEnterAtCurrentPrice`: Whether entering at current price is acceptable

## Position Sizing and Risk Management

The API includes sophisticated position sizing calculations to help you manage risk effectively:

### 1. Position Sizing Information

For each trade recommendation, you'll receive:

- `MaxPositionSize`: Maximum position size based on your account balance and leverage
- `MaxLotSize`: Maximum position size expressed in lots
- `ProfitTargets`: Detailed calculations for different profit targets

### 2. Risk-Reward Ratio

The API prioritizes trade setups with favorable risk-reward ratios:

- Recommendations with R:R ≥ 1.5 are prioritized
- Each recommendation includes precise stop-loss and take-profit levels
- The API calculates the exact risk percentage of your account for each trade

### 3. Risk Level Classification

Each trade is classified by risk level:

- **Low**: Strong confirmation, clear support/resistance, favorable market conditions
- **Medium**: Standard trades with reasonable confirmation and acceptable risk-reward
- **High**: Less confirmation, higher volatility, or proximity to key levels
- **Very High**: Counter-trend trades, minimal confirmation, or during high-impact news events

## Market Sessions and Timing

The API provides valuable information about market sessions to help you trade during optimal hours:

### 1. Session Information

Each analysis includes:

- `CurrentSession`: The active market session (Asian, London, New York)
- `LiquidityLevel`: Rating from 1-5 indicating current market liquidity
- `RecommendedSession`: The optimal session for trading this pair
- `TimeUntilNextSession`: Time remaining until the next session begins

### 2. Session Warnings

The API will warn you when trading during suboptimal sessions:

```
Warning: Current market session (Asian) is not optimal for trading EURUSD. 
Consider waiting for the London session for better liquidity and trading conditions.
```

**Note**: Session warnings are not applied to cryptocurrency pairs since they trade 24/7 with relatively consistent liquidity.

## Advanced Techniques

### 1. Combining Multiple Timeframes

For the most reliable signals:

1. Use the `/api/trading/recommendations` endpoint to identify potential opportunities
2. For promising pairs, use `/api/trading/analyze/{symbol}` to get detailed analysis
3. Verify the setup using `/api/forex/candles/{symbol}/{timeframe}/{count}/{provider}` to view raw data

### 2. Custom Position Sizing

Experienced traders can provide custom parameters:

```
GET /api/trading/analyze/EURUSD?accountBalance=5000&leverage=50&targetProfits=25,50,100
```

This allows you to:
- Match the position sizing to your actual account
- Calculate multiple profit targets
- See exactly how much price movement is needed for each target

### 3. Validating Recommendations

The API provides several data points to help you validate recommendations:

- `Confidence`: A decimal score from 0.0 to 1.0 indicating confidence level
- `Factors`: List of technical factors supporting the recommendation
- `ModelUsed`: The AI model used for analysis (helps track performance)

## Troubleshooting

### Common Issues

1. **No Trade Recommendations**:
   - The API only recommends trades with favorable risk-reward ratios
   - During choppy or ranging markets, it may not find good opportunities
   - Try analyzing different currency pairs or waiting for clearer market conditions

2. **API Key Issues**:
   - Ensure your OpenRouter or Perplexity API key is correctly configured
   - Check the logs for API key validation messages

3. **Data Provider Limitations**:
   - Some providers have rate limits or restricted symbol access
   - If using Mock data provider, be aware that data is simulated

## Tips for Maximizing Profits

### 1. Focus on High-Quality Setups

The API ranks recommendations by risk-reward ratio and confidence. For best results:

- Prioritize trades with R:R ≥ 2.0 and confidence ≥ 0.7
- Be patient and wait for ideal setups rather than forcing trades
- Pay attention to the `RiskLevel` field and avoid "Very High" risk trades unless you're experienced

### 2. Optimal Trading Times

The API provides market session information for a reason:

- Trade major forex pairs during their most liquid sessions:
  - EUR/USD, GBP/USD: London and New York overlap (1300-1600 GMT)
  - USD/JPY: Asian and early London sessions
  - AUD/USD, NZD/USD: Asian session and early London
- Avoid trading during major news releases
- Use the `TimeUntilNextSession` field to plan your trading day

### 3. Advanced Risk Management

Experienced traders can use these techniques:

- **Scaled Entries**: If the API suggests a limit order, consider scaling in at multiple price levels
- **Trailing Stops**: Once a trade moves in your favor, adjust stops to lock in profits
- **Multiple Take-Profit Levels**: Use the position sizing calculations to set multiple profit targets

### 4. Edge Cases and Special Situations

1. **Divergent Timeframes**:
   - If 1-hour and 4-hour analyses conflict, prioritize the 4-hour timeframe
   - Look for confluence between timeframes for the strongest signals

2. **Volatility Spikes**:
   - During high volatility, the API may recommend wider stop-losses
   - Consider reducing position size to compensate for wider stops

3. **Range-Bound Markets**:
   - The API excels at identifying breakouts and trend continuations
   - In ranging markets, look for recommendations with "Limit" order types at range boundaries

4. **Cryptocurrency Trading**:
   - Crypto recommendations typically have wider stops due to higher volatility
   - The API adjusts position sizing calculations accordingly for crypto pairs

### 5. Maximizing API Value

1. **Regular Checks**:
   - The API caches results for 5 minutes to ensure consistency
   - Check for new recommendations every 15-30 minutes during active sessions

2. **Combining with Fundamental Analysis**:
   - The API focuses on technical analysis
   - For best results, combine with your own fundamental analysis and economic calendar awareness

3. **Performance Tracking**:
   - Note the `ModelUsed` field in responses to track which AI models perform best
   - Keep a trading journal to compare API recommendations with actual outcomes

## Conclusion

The TradingViewAnalyzer API provides sophisticated AI-powered trading recommendations that can significantly enhance your day trading strategy. By understanding its capabilities and following the best practices outlined in this guide, you can leverage this tool to identify high-probability trade setups with favorable risk-reward profiles.

Remember that no trading system is perfect, and the API should be used as one component of a comprehensive trading strategy that includes proper risk management, continuous learning, and disciplined execution.

Happy trading! 