# Market Movers Analysis

This document provides detailed information about the Market Movers functionality in the Trading Analysis Platform. The Market Movers feature helps you identify the most volatile forex and crypto pairs based on pip movement and apply EMA filters to find trading opportunities.

## Overview

The Market Movers functionality:

1. Identifies the top market movers in forex and crypto markets based on absolute pip movement
2. Applies EMA filters (10, 20, 50) to detect potential trading opportunities
3. Provides signals based on EMA crossovers, bounces, and breakouts
4. Works with multiple data providers (Polygon.io, TraderMade, TwelveData)

## API Endpoints

### Get Top Forex Market Movers

```
GET /api/market-movers/forex
```

Retrieves the top forex market movers ranked by absolute pip movement.

#### Query Parameters

- `count`: Number of market movers to return (default: 10, max: 25)
- `timeframe`: Chart timeframe for analysis (default: Hours1)
- `provider`: Data provider to use (default: TwelveData)

#### Examples

Basic usage with default parameters:
```bash
curl "https://localhost:7001/api/market-movers/forex"
```

Specify the number of results:
```bash
curl "https://localhost:7001/api/market-movers/forex?count=5"
```

Change the timeframe:
```bash
curl "https://localhost:7001/api/market-movers/forex?timeframe=Minutes15"
```

Use a different data provider:
```bash
curl "https://localhost:7001/api/market-movers/forex?provider=Polygon"
```

Combine multiple parameters:
```bash
curl "https://localhost:7001/api/market-movers/forex?count=5&timeframe=Hours4&provider=TraderMade"
```

### Get Top Crypto Market Movers

```
GET /api/market-movers/crypto
```

Retrieves the top cryptocurrency market movers ranked by absolute price movement.

#### Query Parameters

- `count`: Number of market movers to return (default: 10, max: 25)
- `timeframe`: Chart timeframe for analysis (default: Hours1)
- `provider`: Data provider to use (default: TwelveData)

#### Examples

Basic usage with default parameters:
```bash
curl "https://localhost:7001/api/market-movers/crypto"
```

Specify the number of results:
```bash
curl "https://localhost:7001/api/market-movers/crypto?count=5"
```

Change the timeframe:
```bash
curl "https://localhost:7001/api/market-movers/crypto?timeframe=Minutes15"
```

Use a different data provider:
```bash
curl "https://localhost:7001/api/market-movers/crypto?provider=Polygon"
```

Combine multiple parameters:
```bash
curl "https://localhost:7001/api/market-movers/crypto?count=5&timeframe=Hours4&provider=TraderMade"
```

### Get Forex Market Movers with EMA Filters

```
GET /api/market-movers/forex/ema-filtered
```

Retrieves the top forex market movers with EMA filters applied to identify trading opportunities.

#### Query Parameters

- `count`: Number of market movers to return (default: 10, max: 25)
- `shortTermTimeframe`: Timeframe for short-term analysis (default: Hours1)
- `longTermTimeframe`: Timeframe for long-term analysis (default: Day1)
- `provider`: Data provider to use (default: TwelveData)

#### Examples

Basic usage with default parameters:
```bash
curl "https://localhost:7001/api/market-movers/forex/ema-filtered"
```

Specify the number of results:
```bash
curl "https://localhost:7001/api/market-movers/forex/ema-filtered?count=5"
```

Change the timeframes:
```bash
curl "https://localhost:7001/api/market-movers/forex/ema-filtered?shortTermTimeframe=Minutes15&longTermTimeframe=Hours4"
```

Use a different data provider:
```bash
curl "https://localhost:7001/api/market-movers/forex/ema-filtered?provider=Polygon"
```

Combine multiple parameters:
```bash
curl "https://localhost:7001/api/market-movers/forex/ema-filtered?count=5&shortTermTimeframe=Hours1&longTermTimeframe=Day1&provider=TraderMade"
```

### Get Crypto Market Movers with EMA Filters

```
GET /api/market-movers/crypto/ema-filtered
```

Retrieves the top cryptocurrency market movers with EMA filters applied to identify trading opportunities.

#### Query Parameters

- `count`: Number of market movers to return (default: 10, max: 25)
- `shortTermTimeframe`: Timeframe for short-term analysis (default: Hours1)
- `longTermTimeframe`: Timeframe for long-term analysis (default: Day1)
- `provider`: Data provider to use (default: TwelveData)

#### Examples

Basic usage with default parameters:
```bash
curl "https://localhost:7001/api/market-movers/crypto/ema-filtered"
```

Specify the number of results:
```bash
curl "https://localhost:7001/api/market-movers/crypto/ema-filtered?count=5"
```

Change the timeframes:
```bash
curl "https://localhost:7001/api/market-movers/crypto/ema-filtered?shortTermTimeframe=Minutes15&longTermTimeframe=Hours4"
```

Use a different data provider:
```bash
curl "https://localhost:7001/api/market-movers/crypto/ema-filtered?provider=Polygon"
```

Combine multiple parameters:
```bash
curl "https://localhost:7001/api/market-movers/crypto/ema-filtered?count=5&shortTermTimeframe=Hours1&longTermTimeframe=Day1&provider=TraderMade"
```

## Response Format

The market movers endpoints return a JSON array of market mover objects with the following structure:

```json
[
  {
    "symbol": "EURUSD",
    "currentPrice": 1.0923,
    "previousPrice": 1.0897,
    "priceChange": 0.0026,
    "percentageChange": 0.24,
    "pipMovement": 26.0,
    "direction": "Up",
    "timeframe": "Hours1",
    "emaValues": {
      "10": 1.0915,
      "20": 1.0905,
      "50": 1.0890
    },
    "emaStatus": {
      "isAboveEma10": true,
      "isAboveEma20": true,
      "isAboveEma50": true,
      "isEma10CrossingAboveEma20": false,
      "isEma10CrossingBelowEma20": false,
      "isBouncingOffEma10": false,
      "isBouncingOffEma20": false,
      "isBouncingOffEma50": false,
      "isBreakingThroughEma10": false,
      "isBreakingThroughEma20": false,
      "isBreakingThroughEma50": false
    },
    "recommendedTrade": {
      "direction": "Buy",
      "orderType": "MarketBuy",
      "entryPrice": 1.0923,
      "stopLossPrice": 1.0885,
      "takeProfitPrice": 1.0999,
      "riskRewardRatio": 2.0,
      "rationale": "Buy EURUSD based on EMA analysis:\n- EMA10: 1.0915 (price above)\n- EMA20: 1.0905 (price above)\n- EMA50: 1.0890 (price above)\nEntry: 1.0923\nStop Loss: 1.0885\nTake Profit: 1.0999\nRisk-Reward Ratio: 2.00",
      "signals": [
        "Price bouncing off EMA 10"
      ],
      "timestamp": "2023-06-15T14:30:00Z"
    },
    "assetType": "Forex",
    "timestamp": "2023-06-15T14:30:00Z"
  },
  // More market movers...
]
```

## Understanding the Response

### Basic Fields

- `symbol`: The trading pair (e.g., "EURUSD", "BTCUSD")
- `currentPrice`: The current price of the asset
- `previousPrice`: The previous price used for comparison
- `priceChange`: The absolute price change
- `percentageChange`: The percentage price change
- `pipMovement`: The price movement in pips (for forex) or points (for crypto)
- `direction`: The direction of the price movement (Up or Down)
- `timeframe`: The timeframe used for analysis
- `assetType`: The type of asset (Forex or Crypto)
- `timestamp`: The timestamp of the data

### EMA Values

The `emaValues` object contains the calculated EMA values for different periods:

- `10`: The 10-period EMA value
- `20`: The 20-period EMA value
- `50`: The 50-period EMA value (only available in EMA-filtered endpoints)

### EMA Status

The `emaStatus` object contains boolean flags indicating various EMA-related conditions:

- `isAboveEma10`: Whether the price is above the 10 EMA
- `isAboveEma20`: Whether the price is above the 20 EMA
- `isAboveEma50`: Whether the price is above the 50 EMA
- `isEma10CrossingAboveEma20`: Whether the 10 EMA is crossing above the 20 EMA (bullish)
- `isEma10CrossingBelowEma20`: Whether the 10 EMA is crossing below the 20 EMA (bearish)
- `isBouncingOffEma10`: Whether the price is bouncing off the 10 EMA
- `isBouncingOffEma20`: Whether the price is bouncing off the 20 EMA
- `isBouncingOffEma50`: Whether the price is bouncing off the 50 EMA
- `isBreakingThroughEma10`: Whether the price is breaking through the 10 EMA
- `isBreakingThroughEma20`: Whether the price is breaking through the 20 EMA
- `isBreakingThroughEma50`: Whether the price is breaking through the 50 EMA

### Trade Recommendations

The `recommendedTrade` object contains a trade recommendation based on EMA analysis:

- `direction`: The recommended trade direction (Buy or Sell)
- `orderType`: The type of order to place (MarketBuy, MarketSell, LimitBuy, LimitSell, StopBuy, StopSell)
- `entryPrice`: The recommended entry price for the trade
- `stopLossPrice`: The recommended stop loss price
- `takeProfitPrice`: The recommended take profit price
- `riskRewardRatio`: The calculated risk-reward ratio for the trade
- `rationale`: A detailed explanation of the trade recommendation
- `signals`: A list of signals that triggered the trade recommendation
- `timestamp`: The timestamp when the recommendation was generated

## API Usage Optimization

The Market Movers service is designed to minimize API calls to external data providers while still providing accurate results. Here's how it works:

### Data Caching

- The service implements an in-memory cache for candle data to avoid redundant API calls
- Cache entries include timestamps and expire based on the timeframe:
  - 5-minute data: expires after 5 minutes
  - 15-minute data: expires after 15 minutes
  - 1-hour data: expires after 60 minutes
  - 4-hour data: expires after 240 minutes
  - Daily data: expires after 24 hours
- When you request EMA-filtered market movers, the service reuses cached data instead of making new API calls
- This significantly reduces the number of API calls, especially when making multiple requests in a short period

### Smart Pair Selection

- Instead of analyzing all available pairs (25 forex pairs or 15 crypto pairs), the service uses several strategies to minimize API calls:
  1. **Volatility-Based Selection**: After the first request, the service tracks which pairs have the highest volatility and prioritizes those in future requests
  2. **Common Pairs First**: For the first request, the service starts with the most commonly traded pairs (e.g., EURUSD, GBPUSD, USDJPY for forex)
  3. **Reduced Sample Size**: The service analyzes only 2x the requested count (down from 3x in the previous version)
  4. **Random Sampling**: To ensure diversity, some random pairs are included in the analysis

### Batch Processing

- The service batches API requests whenever possible:
  1. **Shared Data Fetching**: When applying EMA filters, the service fetches all required data in two batch operations instead of making separate calls for each pair
  2. **Reuse Between Endpoints**: Data fetched for one endpoint can be reused by other endpoints if the timeframe matches
  3. **Prioritized Processing**: The service processes the most important pairs first, so if there are any API issues, you still get results for the major pairs

### Recommendations for Minimizing API Usage

1. **Use smaller count values**: Request only the number of market movers you actually need
2. **Reuse the same provider**: Switching providers requires new API calls to fetch data
3. **Use the EMA-filtered endpoints sparingly**: These endpoints require more data and calculations
4. **Make consecutive requests**: The second and subsequent requests will use much fewer API calls due to caching
5. **Use higher timeframes**: Higher timeframes (Hours1, Hours4, Day1) have longer cache expiration times

### API Call Estimates

| Endpoint | First Request | Subsequent Requests (within cache period) |
|----------|---------------|-------------------------------------------|
| `/api/market-movers/forex?count=1` | ~10 calls | 0-2 calls |
| `/api/market-movers/forex?count=5` | ~10-15 calls | 0-5 calls |
| `/api/market-movers/forex/ema-filtered?count=1` | ~12 calls | 0-2 calls |
| `/api/market-movers/forex/ema-filtered?count=5` | ~15-20 calls | 0-5 calls |

These estimates show a significant improvement over the previous implementation, which could make up to 54 API calls for a single request.

## Trading Strategies Using Market Movers

### Strategy 1: Trading High Volatility Pairs

1. Call the basic market movers endpoint to identify the most volatile pairs:
   ```bash
   curl "https://localhost:7001/api/market-movers/forex?count=5"
   ```

2. Focus on pairs with the highest pip movement for potential day trading opportunities.

### Strategy 2: EMA Crossover Strategy

1. Call the EMA-filtered endpoint:
   ```bash
   curl "https://localhost:7001/api/market-movers/forex/ema-filtered"
   ```

2. Look for pairs where `isEma10CrossingAboveEma20` is `true` for potential buy signals.
3. Look for pairs where `isEma10CrossingBelowEma20` is `true` for potential sell signals.

### Strategy 3: EMA Bounce Strategy

1. Call the EMA-filtered endpoint:
   ```bash
   curl "https://localhost:7001/api/market-movers/forex/ema-filtered"
   ```

2. Look for pairs where `isBouncingOffEma10`, `isBouncingOffEma20`, or `isBouncingOffEma50` is `true`.
3. Confirm the bounce with other technical indicators before entering a trade.

### Strategy 4: EMA Breakout Strategy

1. Call the EMA-filtered endpoint:
   ```bash
   curl "https://localhost:7001/api/market-movers/forex/ema-filtered"
   ```

2. Look for pairs where `isBreakingThroughEma10`, `isBreakingThroughEma20`, or `isBreakingThroughEma50` is `true`.
3. Confirm the breakout with volume or other technical indicators before entering a trade.

### Strategy 5: Trading with EMA-Based Recommendations

1. Call the EMA-filtered endpoint with TraderMade provider (recommended for best EMA calculations):
   ```bash
   curl "https://localhost:7001/api/market-movers/forex/ema-filtered?provider=TraderMade"
   ```

2. Look for pairs with a `recommendedTrade` object that has:
   - A clear direction (Buy or Sell)
   - A favorable risk-reward ratio (2.0 or higher)
   - Multiple supporting signals

3. Review the trade rationale to understand the technical basis for the recommendation.

4. Place the trade using the recommended entry, stop loss, and take profit levels.

## Data Provider Comparison

The market movers functionality supports multiple data providers, each with its own advantages:

### Polygon.io

- Comprehensive market data for forex and crypto
- High-quality historical data
- Requires a Polygon.io API key

Example:
```bash
curl "https://localhost:7001/api/market-movers/forex?provider=Polygon"
```

### TraderMade

- Real-time forex and crypto data
- Good for current market conditions
- **Best provider for EMA calculations and trade recommendations**
- Requires a TraderMade API key

Example:
```bash
curl "https://localhost:7001/api/market-movers/forex?provider=TraderMade"
```

### TwelveData

- Comprehensive market data for forex and crypto
- Good balance of historical and real-time data
- Requires a TwelveData API key

Example:
```bash
curl "https://localhost:7001/api/market-movers/forex?provider=TwelveData"
```

### Mock Provider

- Simulated data for testing
- No API key required
- Not suitable for real trading decisions

Example:
```bash
curl "https://localhost:7001/api/market-movers/forex?provider=Mock"
```

## Timeframe Selection

The market movers functionality supports various timeframes for analysis:

- `Minutes5`: 5-minute timeframe (short-term)
- `Minutes15`: 15-minute timeframe (short-term)
- `Hours1`: 1-hour timeframe (medium-term)
- `Hours4`: 4-hour timeframe (medium-term)
- `Day1`: 1-day timeframe (long-term)

For day trading, the 1-hour timeframe (`Hours1`) is often a good choice for the short-term analysis, while the 4-hour or 1-day timeframe is suitable for the long-term analysis.

## Implementation Details

The market movers functionality is implemented using real data providers and performs actual calculations:

1. **Data Collection**: The system fetches real-time market data from the specified provider for common forex and crypto pairs.

2. **Pip Movement Calculation**: For forex pairs, pip movement is calculated based on the standard pip value (0.0001 for most pairs, 0.01 for JPY pairs). For crypto pairs, the absolute price movement is used.

3. **EMA Calculation**: The system calculates EMAs using the standard formula:
   ```
   EMA = (Price - Previous EMA) * Multiplier + Previous EMA
   ```
   where Multiplier = 2 / (Period + 1)

4. **Signal Detection**: The system detects various signals based on the relationship between price and EMAs, including crossovers, bounces, and breakouts.

5. **Sorting**: The results are sorted by absolute pip movement (descending) to show the most volatile pairs first.

6. **Trade Recommendations**: For EMA-filtered endpoints, the system generates trade recommendations based on:
   - EMA positions (price above/below EMAs)
   - EMA crossovers (10 EMA crossing above/below 20 EMA)
   - Price bouncing off or breaking through EMAs
   - Optimal entry, stop loss, and take profit levels for a favorable risk-reward ratio

## Troubleshooting

### Common Issues

1. **No Data Returned**: Ensure you have configured the API key for the specified provider. If no API key is available, the system will fall back to the mock provider.

2. **Incorrect Provider**: Make sure you're using a valid provider name (Polygon, TraderMade, TwelveData, or Mock).

3. **Invalid Timeframe**: Ensure you're using a valid timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1).

4. **Rate Limiting**: If you're making too many requests, you might hit rate limits from the data providers. Consider using a higher timeframe or reducing the frequency of requests.

### Provider-Specific Issues

#### TraderMade

- For minute-based timeframes, TraderMade limits data to 2 working days per request.
- If you need more historical data, consider using Polygon.io or TwelveData.

#### TwelveData

- The free tier has limitations on the number of API calls and symbols.
- If you're experiencing issues, check your usage on the TwelveData dashboard.

#### Polygon.io

- Ensure your API key has access to forex and crypto data.
- The free tier has limitations on the number of API calls and historical data.

## Conclusion

The Market Movers functionality provides a powerful way to identify volatile pairs and potential trading opportunities based on EMA filters. By combining pip movement analysis with EMA-based signals, traders can focus on the most active pairs and make more informed trading decisions. 