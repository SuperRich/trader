# Forex Trading Prediction Application

A C# backend application for analyzing forex charts at different timeframes (5min, 15min, 1h, 4h, 1d) and generating trading predictions with stop loss and take profit levels.

## Project Structure

- `src/Trader.Api` - ASP.NET Core Web API application
- `src/Trader.Core` - Business logic and domain models
- `src/Trader.Infrastructure` - Data access and external services integration
- `tests/Trader.Tests` - Unit and integration tests

## Features

- View forex candlestick data at multiple timeframes
- Generate trading predictions with:
  - Entry price
  - Stop loss level
  - Take profit level
  - Risk/reward ratio
- Support for multiple timeframes:
  - 5 minute
  - 15 minute
  - 1 hour
  - 4 hour
  - 1 day

## API Endpoints

- `GET /api/forex/prediction/{currencyPair}/{timeframe}` - Get a prediction for a specific currency pair and timeframe
- `GET /api/forex/multi-timeframe/{currencyPair}` - Get predictions across all timeframes for a currency pair
- `GET /api/forex/candles/{currencyPair}/{timeframe}/{count}` - Get historical candle data
- `GET /api/forex/sentiment/{currencyPair}` - Get market sentiment analysis using Perplexity AI

## Setup Instructions

### Prerequisites

- .NET 8.0 SDK or later
- Perplexity API key (for sentiment analysis)

### Configuring Perplexity API Key

For security, the Perplexity API key should not be stored directly in appsettings.json. Choose one of these options:

#### Option 1: Environment Variables

Set the environment variable before running the application:

```bash
# For Windows PowerShell
$env:TRADER_PERPLEXITY_API_KEY="your-api-key-here"

# For Windows Command Prompt
set TRADER_PERPLEXITY_API_KEY=your-api-key-here

# For Linux/macOS
export TRADER_PERPLEXITY_API_KEY=your-api-key-here
```

#### Option 2: User Secrets (Development)

Use .NET User Secrets for development environments:

```bash
cd src/Trader.Api
dotnet user-secrets set "Perplexity:ApiKey" "your-api-key-here"
```

#### Option 3: Azure Key Vault (Production)

For production environments, consider using Azure Key Vault to store your API key.

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
5. Access the Swagger UI at `https://localhost:5001/swagger`

## Future Enhancements

- Implement machine learning models for prediction
- Add technical indicators (RSI, MACD, Moving Averages)
- Create a web frontend for visualization
- Add backtesting capabilities
- Implement real-time data feeds from forex providers
- Enhance sentiment analysis with historical data comparison