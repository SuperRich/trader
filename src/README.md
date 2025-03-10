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
  "takeProfitPrice": 46500.00,
  "stopLossPrice": 44200.00,
  "riskRewardRatio": 1.5,
  "factors": [
    "Price broke above key resistance at 45000",
    "Strong bullish momentum on 1h and 4h timeframes",
    "Support confluence at current levels"
  ],
  "rationale": "BTC is showing strong upside momentum after breaking the key psychological level of $45,000. Multiple timeframes align for a bullish continuation pattern.",
  "timestamp": "2025-03-10T12:34:56Z"
}
```

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