# Trading Analysis Platform

A C# backend application for analyzing forex, crypto and stock charts at different timeframes (1m, 5m, 15m, 1h, 4h, 1d) and generating AI-powered trading predictions with stop loss and take profit levels.

## Project Structure

- `src/Trader.Api` - ASP.NET Core Web API application
- `src/Trader.Core` - Business logic and domain models
- `src/Trader.Infrastructure` - Data access and external services integration
- `tests/Trader.Tests` - Unit and integration tests

## Features

- View candlestick data from multiple sources:
  - Polygon.io market data API for forex and crypto (requires API key)
  - TraderMade API for real-time forex and crypto data (requires API key)
  - Simulated data provider (fallback when no API key provided)
- Analyze charts for multiple asset types:
  - Forex pairs (EURUSD, GBPJPY, etc.)
  - Cryptocurrencies (BTCUSD, ETHUSD, etc.)
- Generate trading predictions with:
  - Buy/Sell recommendation
  - Entry price
  - Stop loss level
  - Take profit level
  - Risk/reward ratio
- Support for multiple timeframes:
  - 1 minute
  - 5 minute
  - 15 minute
  - 1 hour
  - 4 hour
  - 1 day
- AI-powered market analysis:
  - TradingView-style chart analysis (with Polygon.io data)
  - Multi-timeframe technical analysis
  - Sentiment analysis for any tradable asset
  - Trading recommendations with precise TP/SL levels
  - Risk/reward calculation and trade rationale

## API Endpoints

### Chart Data Endpoints

- `GET /api/forex/candles/{symbol}/{timeframe}/{count}` - Get historical candle data for any symbol
  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
  - `count`: Number of candles to retrieve (1-1000)

- `GET /api/forex/candles/{symbol}/{timeframe}/{count}/{provider}` - Get historical candle data using a specific provider
  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
  - `count`: Number of candles to retrieve (1-1000)
  - `provider`: Data provider to use (Polygon, TraderMade, Mock)

### Analysis Endpoints

- `GET /api/trading/analyze/{symbol}` - Get AI analysis of a trading chart with buy/sell recommendation
  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")

- `GET /api/trading/analyze/{symbol}/{provider}` - Get AI analysis using a specific data provider
  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")
  - `provider`: Data provider to use (Polygon, TraderMade, Mock)

- `GET /api/forex/sentiment/{symbol}` - Get market sentiment analysis for a symbol
  - `symbol`: Symbol to analyze (e.g., "EURUSD", "BTCUSD")

- `GET /api/trading/recommendations?count=3` - Get top trading recommendations with entry, stop loss, and take profit
  - `count`: Number of recommendations to return (default: 3, max: 5)

### Legacy Endpoints

- `GET /api/forex/prediction/{currencyPair}/{timeframe}` - Get a prediction for a specific currency pair and timeframe
- `GET /api/forex/multi-timeframe/{currencyPair}` - Get predictions across all timeframes for a currency pair

## Setup Instructions

### Prerequisites

- .NET 8.0 SDK or later
- Perplexity API key (for AI analysis)
- Polygon.io API key (for real market data) or
- TraderMade API key (for real market data)

### Configuring API Keys

#### Option 1: Environment Variables

Set the environment variables before running the application:

```bash
# For Windows PowerShell
$env:TRADER_PERPLEXITY_API_KEY="your-perplexity-key-here"
$env:TRADER_POLYGON_API_KEY="your-polygon-key-here"
$env:TRADER_TRADERMADE_API_KEY="your-tradermade-key-here"

# For Windows Command Prompt
set TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
set TRADER_POLYGON_API_KEY=your-polygon-key-here
set TRADER_TRADERMADE_API_KEY=your-tradermade-key-here

# For Linux/macOS
export TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
export TRADER_POLYGON_API_KEY=your-polygon-key-here
export TRADER_TRADERMADE_API_KEY=your-tradermade-key-here
```

#### Option 2: User Secrets (Development)

Use .NET User Secrets for development environments:

```bash
cd src/Trader.Api
dotnet user-secrets init  # Only needed if you haven't set up user secrets yet
dotnet user-secrets set "Perplexity:ApiKey" "your-perplexity-key-here"
dotnet user-secrets set "Polygon:ApiKey" "your-polygon-key-here"
dotnet user-secrets set "TraderMade:ApiKey" "your-tradermade-key-here"
```

Verify your secrets are set correctly:
```bash
dotnet user-secrets list
```

#### Option 3: Using API Setup Endpoints

For easier setup, use our configuration endpoints:

1. Set up Perplexity API key:
```bash
curl -X POST https://localhost:7001/api/diagnostics/set-perplexity-key \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": true}'
```

2. Set up Polygon.io API key:
```bash
curl -X POST https://localhost:7001/api/diagnostics/set-polygon-key \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-polygon-key-here", "saveToUserSecrets": true}'
```

3. Set up TraderMade API key:
```bash
curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
```

### Obtaining API Keys

#### Perplexity API

1. Create an account at [Perplexity.ai](https://www.perplexity.ai/)
2. Go to the [API section](https://www.perplexity.ai/settings/api) in your account settings
3. Generate a new API key
4. The key will start with "pplx-"

#### Polygon.io API

1. Sign up for an account at [Polygon.io](https://polygon.io/)
2. Choose a plan (they offer a free tier with limited requests)
3. Find your API key in your dashboard
4. The key will be a long alphanumeric string

#### TraderMade API

1. Sign up for an account at [TraderMade](https://tradermade.com/)
2. Choose a plan (they offer a free tier with limited requests)
3. Find your API key in your dashboard
4. The key will be a long alphanumeric string

To test your TraderMade API key:

```bash
# Test your TraderMade API key
curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
```

Once configured, you can use TraderMade as your data provider:

```bash
# Get EURUSD data using TraderMade
curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade

# Analyze BTCUSD using TraderMade data
curl https://localhost:7001/api/trading/analyze/BTCUSD/TraderMade
```

### Running the Application

1. Clone the repository
2. Navigate to the project directory
3. Build the solution:
   ```
   dotnet build
   ```
4. Run the API:
   ```
   cd src/Trader.Api
   dotnet run
   ```
5. Access the Swagger UI at `https://localhost:7001/swagger`

## Using the TradingView Analysis Feature

With the TradingView analysis feature, you can analyze chart patterns and get AI-powered trading recommendations for any forex pair or cryptocurrency. Here's how to use it:

1. Ensure you have both API keys set up:
   - Polygon.io API key (for fetching real market data)
   - Perplexity API key (for AI analysis)

2. Test your setup with the diagnostic endpoint:
   ```bash
   curl https://localhost:7001/api/diagnostics/config
   ```
   Confirm both API keys are configured.

3. Get chart analysis for a symbol:
   ```bash
   curl https://localhost:7001/api/trading/analyze/BTCUSD
   ```
   This will return a detailed analysis with:
   - Current price
   - Buy/Sell recommendation
   - Stop loss level
   - Take profit level
   - Supporting factors for the recommendation
   - Market sentiment
   - Current market session information and liquidity

4. Get multiple trading recommendations:
   ```bash
   curl https://localhost:7001/api/trading/recommendations?count=3
   ```
   This will return the top 3 trading opportunities across both forex and crypto.

5. View raw chart data:
   ```bash
   # Get the last 50 candles on the 1-hour timeframe for BTCUSD
   curl https://localhost:7001/api/forex/candles/BTCUSD/Hours1/50
   ```

### Example Trading Recommendation Response

```json
{
  "currencyPair": "BTCUSD",
  "direction": "Buy",
  "sentiment": "Bullish",
  "confidence": 0.85,
  "currentPrice": 45120.50,
  "bestEntryPrice": 44850.00,
  "takeProfitPrice": 46500.00,
  "stopLossPrice": 44200.00,
  "orderType": "LimitBuy",
  "timeToBestEntry": "2-3 hours",
  "validUntil": "2025-03-11T12:34:56Z",
  "isSafeToEnterAtCurrentPrice": true,
  "currentEntryReason": "While the best entry is at 44850.00, entering at the current price of 45120.50 still provides a favorable risk-reward ratio of 1.4:1. Price is currently consolidating above key support at 45000 with strong momentum indicators.",
  "riskRewardRatio": 1.5,
  "factors": [
    "Price broke above key resistance at 45000",
    "Strong bullish momentum on 1h and 4h timeframes",
    "Support confluence at current levels"
  ],
  "rationale": "BTC is showing strong upside momentum after breaking the key psychological level of $45,000. Multiple timeframes align for a bullish continuation pattern.",
  "timestamp": "2025-03-10T12:34:56Z",
  "marketSession": {
    "currentSession": "NewYork",
    "description": "New York Session (North American markets) - 12:00-21:00 UTC - High liquidity, often volatile movements",
    "liquidityLevel": 4,
    "recommendedSession": "NewYork",
    "recommendationReason": "The New York session (12:00-21:00 UTC) offers strong liquidity for BTCUSD with US economic data releases often creating trading opportunities. Volatility can be high during this period.",
    "timeUntilNextSession": "5h 30m",
    "nextSession": "Asian",
    "currentTimeUtc": "2025-03-10T15:30:00Z",
    "nextSessionStartTimeUtc": "2025-03-10T23:00:00Z"
  },
  "sessionWarning": "Warning: Current market session (NewYork) is not optimal for trading BTCUSD. Consider waiting for the London session for better liquidity and trading conditions."
}
```

## New Features

### Order Types

The analysis now includes an `orderType` field that specifies the recommended type of order to place:

- **MarketBuy/MarketSell**: Execute the trade immediately at the current market price. Use when the current price is already at a good entry point.
  
- **LimitBuy/LimitSell**: Wait for the price to reach a better level before entering.
  - LimitBuy: Place a buy order at a price lower than the current price (waiting for price to drop)
  - LimitSell: Place a sell order at a price higher than the current price (waiting for price to rise)
  
- **StopBuy/StopSell**: Wait for a breakout/breakdown confirmation before entering.
  - StopBuy: Place a buy order at a price higher than the current price (waiting for upward breakout)
  - StopSell: Place a sell order at a price lower than the current price (waiting for downward breakdown)

The system automatically determines the appropriate order type based on:
- The relationship between current price and best entry price:
  - For buy orders: If best entry < current price → LimitBuy; if best entry > current price → StopBuy
  - For sell orders: If best entry > current price → LimitSell; if best entry < current price → StopSell
- The market context and trading setup
- The risk-reward profile of the trade

For example, if the recommendation is to buy BTCUSD with a current price of $45,000 but the best entry price is $44,500, the system will suggest a "LimitBuy" order type, indicating you should place a limit order to buy at the lower price of $44,500.

```json
"currentPrice": 45000.00,
"bestEntryPrice": 44500.00,
"orderType": "LimitBuy"
```

Similarly, if the recommendation is to sell BTCUSD with a current price of $45,000 but the best entry price is $46,000, the system will suggest a "LimitSell" order type, indicating you should place a limit order to sell at the higher price of $46,000.

```json
"currentPrice": 45000.00,
"bestEntryPrice": 46000.00,
"orderType": "LimitSell"
```

This feature helps traders execute trades more effectively by specifying not just what to trade, but how to enter the position.

### Entry Safety Indicator

The analysis now includes two fields that help you decide whether to enter a trade at the current market price:

1. `isSafeToEnterAtCurrentPrice`: A boolean flag indicating whether it's still acceptable to enter at the current price
2. `currentEntryReason`: A detailed explanation of why it's safe or unsafe to enter at the current price

```json
"currentPrice": 45120.50,
"bestEntryPrice": 44850.00,
"isSafeToEnterAtCurrentPrice": true,
"currentEntryReason": "While the best entry is at 44850.00, entering at the current price of 45120.50 still provides a favorable risk-reward ratio of 1.4:1. Price is currently consolidating above key support at 45000 with strong momentum indicators."
```

This feature helps traders make more informed decisions about:

- **When to wait vs. when to act immediately**: If `isSafeToEnterAtCurrentPrice` is `true`, you can enter at the current price without significantly compromising the trade setup, even if waiting for the best entry would be slightly better.

- **Risk management**: If `isSafeToEnterAtCurrentPrice` is `false`, it indicates that entering at the current price would significantly reduce the risk-reward ratio or increase risk compared to waiting for the best entry price.

- **Understanding the specific risks**: The `currentEntryReason` field provides detailed reasoning about why it's safe or unsafe to enter at the current price, including specific price levels, risk-reward calculations, or technical factors.

The AI considers several factors when determining entry safety:

- **Distance from best entry**: How far the current price is from the optimal entry point
- **Volatility**: Higher volatility may make immediate entry more risky
- **Proximity to key levels**: Whether the current price is near important support/resistance levels
- **Overall market conditions**: Trend strength, momentum, and other contextual factors

For example, if a buy recommendation shows:
```json
"currentPrice": 45120.50,
"bestEntryPrice": 44850.00,
"isSafeToEnterAtCurrentPrice": false,
"currentEntryReason": "Current price is too far from optimal entry, reducing risk-reward ratio from 2:1 to 1.2:1. Price is approaching resistance at 45200 and showing signs of short-term exhaustion. Wait for pullback to 44850 support level."
```

This detailed explanation helps you understand exactly why you should wait for a better entry point rather than entering immediately, providing specific technical reasons and risk-reward calculations.

### Best Entry Price

The analysis now includes a `bestEntryPrice` field that provides the optimal entry price for the trade, which may differ from the current price. This is especially useful when:

- The current price is in the middle of a range and a better entry would be at support/resistance
- A pullback to a key level would provide a better risk-reward ratio
- The market is overextended and a retracement would offer a better entry

### Time to Best Entry

The analysis now includes a `timeToBestEntry` field that provides an estimate of how long it might take for the price to reach the optimal entry level. This helps traders:

- Plan their trading schedule and set appropriate alerts
- Understand the urgency (or lack thereof) of the trading opportunity
- Make informed decisions about whether to wait for the best entry or execute immediately

For example:
```json
"timeToBestEntry": "2-3 hours"
```

This indicates that based on current market conditions and recent price action, the price is expected to reach the best entry level within 2-3 hours. If the estimate is "Unknown", it means the system cannot reliably predict when the price might reach the best entry level.

### Recommendation Validity Period

The analysis now includes a `validUntil` field that specifies when the recommendation expires. This helps traders:

- Understand how long the analysis remains relevant
- Know when to seek updated recommendations
- Avoid acting on outdated information

For example:
```json
"validUntil": "2025-03-11T12:34:56Z"
```

This indicates that the recommendation is valid until March 11, 2025, at 12:34:56 UTC. After this time, market conditions may have changed significantly, and the recommendation should be considered outdated.

The validity period is determined based on:
- The timeframes used in the analysis (higher timeframes typically result in longer validity periods)
- Market volatility (more volatile markets typically have shorter validity periods)
- Upcoming economic events or news that might impact the market
- The nature of the trading setup (some patterns have naturally shorter lifespans than others)

### Market Session Warnings

When the current market session isn't the recommended one for trading a particular currency pair, the API will now include a `sessionWarning` field with a message explaining:

- Which session is currently active
- Which session would be better for trading this pair
- A recommendation to consider waiting for the optimal session

This helps traders make more informed decisions about timing their trades for better liquidity and market conditions.

## Forex Market Sessions

The application now provides information about forex market sessions to help you time your trades for optimal liquidity:

### Market Session Information

Each analysis and recommendation includes market session data:

```json
"marketSession": {
  "currentSession": "LondonNewYorkOverlap",
  "description": "London-New York Overlap - 12:00-16:00 UTC - Highest liquidity period, often largest price movements",
  "liquidityLevel": 5,
  "recommendedSession": "LondonNewYorkOverlap",
  "recommendationReason": "The London-New York overlap (12:00-16:00 UTC) provides the highest liquidity for EURUSD, with maximum market participation and often the largest price movements of the day. This is generally the optimal trading window.",
  "timeUntilNextSession": "3h 45m",
  "nextSession": "Asian",
  "currentTimeUtc": "2025-03-10T19:15:00Z",
  "nextSessionStartTimeUtc": "2025-03-10T23:00:00Z"
}
```

### Understanding Market Sessions

The forex market operates 24 hours a day, but is divided into major sessions (all times in UTC/GMT):

1. **Asian Session** (23:00-08:00 UTC)
   - Tokyo, Singapore, Hong Kong markets
   - Moderate liquidity, often range-bound trading
   - Best for JPY, AUD, NZD pairs

2. **London Session** (07:00-16:00 UTC)
   - European markets
   - High liquidity, often trending movements
   - Best for EUR, GBP pairs

3. **New York Session** (12:00-21:00 UTC)
   - North American markets
   - High liquidity, often volatile movements
   - Best for USD, CAD pairs

4. **Session Overlaps**
   - Asian-London Overlap (07:00-08:00 UTC): Increasing liquidity
   - London-New York Overlap (12:00-16:00 UTC): Highest liquidity period

### Cryptocurrencies and Market Sessions

**Important Note**: Unlike forex, cryptocurrencies trade 24/7 with consistent liquidity across all sessions. For crypto pairs like BTCUSD and ETHUSD:

- No session warnings will be displayed as they can be traded at any time
- Session information is still provided for context, but all sessions have high liquidity
- While trading volume may vary slightly across different times of day, cryptocurrencies don't follow the traditional forex session restrictions
- The system automatically detects cryptocurrency pairs and treats them appropriately

### Using Session Information

- **Liquidity Level**: A rating from 1-5 indicating current liquidity (5 being highest)
- **Recommended Session**: The optimal session for trading a specific currency pair
- **Time Until Next Session**: Helps you plan when to check back for better conditions
- **Next Session Start Time**: The exact UTC time when the next session will begin

This information can help you:
- Time your trades for optimal liquidity
- Understand why certain pairs move more during specific sessions
- Plan your trading schedule around the most active periods for your preferred pairs

> **Note**: All times are provided in UTC/GMT for consistency. Convert to your local time zone as needed.

## Using Multiple Data Providers

This application supports multiple data providers for fetching market data:

1. **Polygon.io** - A comprehensive market data API with support for stocks, forex, and crypto
2. **TraderMade** - A real-time and historical data provider for forex and crypto
3. **Mock Provider** - A simulated data provider for testing without API keys

### Fetching Candle Data

You can specify which provider to use when fetching candle data:

```bash
# Using Polygon.io
curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/Polygon

# Using TraderMade
curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade

# Using Mock data
curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/Mock
```

### Different Timeframes

The API supports various timeframes for candle data:

```bash
# 5-minute timeframe
curl https://localhost:7001/api/forex/candles/EURUSD/Minutes5/100/TraderMade

# 15-minute timeframe
curl https://localhost:7001/api/forex/candles/EURUSD/Minutes15/100/TraderMade

# 1-hour timeframe
curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade

# 4-hour timeframe
curl https://localhost:7001/api/forex/candles/EURUSD/Hours4/100/TraderMade

# Daily timeframe
curl https://localhost:7001/api/forex/candles/EURUSD/Day1/100/TraderMade
```

Note: When using TraderMade as the provider, the Minutes15 timeframe is implemented using the 'minute' interval with a period of 15.

#### TraderMade API Limitations

TraderMade has the following limitations:
- For minute-based timeframes (Minutes5, Minutes15), the API limits data to 2 working days per request
- Valid interval values are 'minute', 'hourly', and 'daily' only
- For minute intervals, you can specify a period (5, 15, etc.)
- For hourly intervals, you can specify a period (4 for 4-hour timeframe)

#### TraderMade Live Rates

Our implementation uses TraderMade's Live Rates API to enhance the accuracy of current prices:
- Historical data is fetched from the timeseries endpoint
- Current prices are updated using the live rates endpoint
- This provides more accurate and up-to-date closing prices for the most recent candle
- The system automatically combines historical and live data for the best accuracy

### Chart Analysis

You can also specify which provider to use for chart analysis:

```bash
# Using Polygon.io
curl https://localhost:7001/api/trading/analyze/BTCUSD/Polygon

# Using TraderMade
curl https://localhost:7001/api/trading/analyze/BTCUSD/TraderMade

# Using Mock data
curl https://localhost:7001/api/trading/analyze/BTCUSD/Mock
```

If you don't specify a provider, the system will use the default provider based on available API keys:
1. Polygon.io (if configured)
2. TraderMade (if configured)
3. Mock provider (fallback)

## Troubleshooting

### API Key Issues

If you encounter problems with the API keys:

1. Use the configuration check endpoint to verify your keys:
   ```bash
   curl https://localhost:7001/api/diagnostics/config
   ```

2. For Perplexity API issues:
   - Ensure the API key starts with "pplx-"
   - Check rate limits on your Perplexity account
   - Try the test endpoint to verify key validity:
     ```bash
     curl -X POST https://localhost:7001/api/diagnostics/set-perplexity-key \
       -H "Content-Type: application/json" \
       -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": false}'
     ```

3. For Polygon.io API issues:
   - Verify your subscription is active
   - Check usage limits on your Polygon.io dashboard
   - Try the test endpoint to verify key validity:
     ```bash
     curl -X POST https://localhost:7001/api/diagnostics/set-polygon-key \
       -H "Content-Type: application/json" \
       -d '{"apiKey": "your-polygon-key-here", "saveToUserSecrets": false}'
     ```

4. For TraderMade API issues:
   - Verify your subscription is active
   - Check usage limits on your TraderMade dashboard
   - Try the test endpoint to verify key validity:
     ```bash
     curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
       -H "Content-Type: application/json" \
       -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": false}'
     ```

### Data Provider Fallback

If neither Polygon.io nor TraderMade is configured or returns an error, the system will automatically fall back to using the mock data provider. This allows you to test the API functionality without real market data.

### TraderMade API Limitations

If you encounter errors when using TraderMade, be aware of these limitations:

1. **Minute Data Restriction**: TraderMade limits minute-based data (5min, 15min) to 2 working days per request. If you need more historical data, consider using hourly or daily timeframes.

2. **Error Message**: If you see `"max 2 working days of 1 and 5 minute data allowed per request"`, it means you're trying to fetch too much minute-based data.

3. **Response Format Variations**: TraderMade API may return OHLC values as either strings or numbers in different responses. Our implementation handles both formats.

4. **Live Rates Enhancement**: The system automatically fetches live rates to update the most recent candle's close price for better accuracy. If you see different prices compared to historical data, this is expected and provides more accurate current prices.

5. **Free Tier Limitations**: The free tier has additional restrictions on the number of API calls and data points. Check your usage on the TraderMade dashboard.

6. **Workaround**: For analysis that requires more historical minute data, use Polygon.io instead:
   ```bash
   curl https://localhost:7001/api/trading/analyze/BTCUSD/Polygon
   ```

## Position Sizing Calculator

The API now includes a position sizing calculator that helps traders determine:

1. The maximum position size they can take based on their account balance and leverage
2. The position size required to achieve specific profit targets
3. The risk associated with each position

### Using the Position Sizing Calculator

By default, the calculator assumes:
- Account balance: 201 GBP
- Leverage: 1:1000
- Default profit targets: 50, 100, 200, 500, and 1000 GBP

You can customize these parameters in your API requests using query parameters:

- `accountBalance`: Your trading account balance in GBP (default: 201)
- `leverage`: Your account leverage as a number (e.g., 1000 for 1:1000 leverage)
- `targetProfits`: Comma-separated list of profit targets in GBP

#### Example cURL Commands

**Basic usage with default parameters:**
```bash
curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade"
```

**Custom account balance and leverage:**
```bash
curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?accountBalance=500&leverage=500"
```

**Custom profit targets:**
```bash
curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?targetProfits=100,200,300"
```

**All parameters combined:**
```bash
curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?accountBalance=500&leverage=500&targetProfits=100,200,300"
```

**With trading recommendations endpoint:**
```bash
curl "https://localhost:7001/api/trading/recommendations?count=3&accountBalance=1000&leverage=200&targetProfits=500,1000,2000"
```

### Specifying Multiple Target Profits

The `targetProfits` parameter accepts a comma-separated list of values, allowing you to calculate position sizes for multiple profit targets simultaneously. For example:

- `targetProfits=50,100,200` - Calculate position sizes needed to make 50, 100, and 200 GBP profit
- `targetProfits=100,500,1000,5000` - Calculate for larger profit targets
- `targetProfits=10,25,50,75,100` - Calculate for smaller, more granular profit targets

This is particularly useful for:
- Planning different profit scenarios (conservative, moderate, aggressive)
- Understanding how position size scales with profit targets
- Finding the optimal risk-reward balance for your trading style

### Position Sizing Response Example

The API response includes a `positionSizing` object with the following information:

```json
{
  // ... other response fields ...
  "positionSizing": {
    "accountBalance": 201,
    "leverage": 1000,
    "symbol": "EURUSD",
    "currentPrice": 1.0876,
    "maxPositionSize": 201000,
    "maxLotSize": 2.01,
    "profitTargets": {
      "50": {
        "targetProfit": 50,
        "requiredPositionSize": 50000,
        "requiredLotSize": 0.5,
        "priceMovementRequired": 0.00108,
        "priceMovementPercent": 0.1,
        "riskAmount": 50,
        "riskPercentage": 24.88
      },
      "100": {
        "targetProfit": 100,
        "requiredPositionSize": 100000,
        "requiredLotSize": 1.0,
        "priceMovementRequired": 0.00217,
        "priceMovementPercent": 0.2,
        "riskAmount": 100,
        "riskPercentage": 49.75
      }
      // Additional targets...
    }
  }
}
```

### Understanding the Position Sizing Response

- **accountBalance**: Your trading account balance in GBP
- **leverage**: Your account leverage (e.g., 1000 for 1:1000)
- **symbol**: The trading pair being analyzed
- **currentPrice**: The current market price of the symbol
- **maxPositionSize**: The maximum position size you can take with your account and leverage
- **maxLotSize**: The maximum position size expressed in standard lots

For each profit target, you'll see:
- **targetProfit**: The profit target in GBP
- **requiredPositionSize**: The position size needed to achieve this profit target
- **requiredLotSize**: The position size expressed in standard lots
- **priceMovementRequired**: The price movement needed to achieve the profit target
- **priceMovementPercent**: The price movement as a percentage
- **riskAmount**: The amount at risk in GBP (assuming 1:1 risk-reward)
- **riskPercentage**: The risk as a percentage of your account balance

This information helps traders understand:
- How much they can trade with their current account
- What position size is needed to achieve specific profit targets
- How much risk they're taking on with each position
- What price movement is required to reach their profit goals