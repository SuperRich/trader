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
- AI-powered market analysis:
  - Sentiment analysis for any forex pair
  - Trading recommendations with precise TP/SL levels
  - Live market price data fetched in real-time
  - Risk/reward calculation and trade rationale

## API Endpoints

- `GET /api/forex/prediction/{currencyPair}/{timeframe}` - Get a prediction for a specific currency pair and timeframe
- `GET /api/forex/multi-timeframe/{currencyPair}` - Get predictions across all timeframes for a currency pair
- `GET /api/forex/candles/{currencyPair}/{timeframe}/{count}` - Get historical candle data
- `GET /api/forex/sentiment/{currencyPair}` - Get market sentiment analysis using Perplexity AI
- `GET /api/forex/recommendations?count=3` - Get recommended forex pairs to trade with real-time prices, entry, TP, and SL levels

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
dotnet user-secrets init  # Only needed if you haven't set up user secrets yet
dotnet user-secrets set "Perplexity:ApiKey" "your-api-key-here"
```

You can verify your secrets are set correctly:
```bash
dotnet user-secrets list
```

**Troubleshooting User Secrets:**
- User secrets are tied to the `UserSecretsId` in the project file
- Make sure you're running the commands from the correct directory (src/Trader.Api)
- If using VS Code or another editor, restart it after setting secrets
- User secrets are stored in a JSON file at:
  - Windows: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
  - macOS/Linux: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

#### Option 3: Azure Key Vault (Production)

For production environments, consider using Azure Key Vault to store your API key.

### Troubleshooting Perplexity API Authorization

> **Note**: We use the `sonar` model from Perplexity's API. If this model becomes unavailable, you may need to update the code to use a different model from their [current model list](https://docs.perplexity.ai/guides/model-cards).

If you encounter "Unauthorized" errors when using the sentiment analysis:

1. Verify your API key is set correctly:
   - Use the diagnostic endpoint `/api/diagnostics/perplexity-config` to check if your API key is being detected
   - Ensure the API key starts with "pplx-" (this is the standard prefix for Perplexity API keys)
   - Check that your API key hasn't expired or been revoked

2. Common issues:
   - API key not being loaded from environment variables - try restarting your terminal/command prompt
   - Incorrect format - ensure there are no extra spaces or characters
   - Rate limiting - Perplexity may limit requests on free or trial plans

3. **Quick API Key Fix**: 
   - Use our special helper endpoint to verify and set your key:
   ```bash
   curl -X POST http://localhost:5000/api/diagnostics/set-perplexity-key \
     -H "Content-Type: application/json" \
     -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": true}'
   ```
   - This endpoint:
     - Verifies your key with the Perplexity API
     - Saves valid keys to your user secrets
     - Creates a .env file with the key (as TRADER_PERPLEXITY_API_KEY)
     - Updates appsettings.Development.json (if it exists)
   - After setting the key, restart the application for it to take effect

3. Testing the API directly:
   ```bash
   curl -X POST https://api.perplexity.ai/chat/completions \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer YOUR_API_KEY" \
     -d '{
       "model": "sonar",
       "messages": [
         {"role": "system", "content": "You are a helpful assistant."},
         {"role": "user", "content": "Hello, how are you?"}
       ]
     }'
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
5. Access the Swagger UI at `https://localhost:5001/swagger`

## Future Enhancements

- Implement machine learning models for prediction
- Add technical indicators (RSI, MACD, Moving Averages)
- Create a web frontend for visualization
- Add backtesting capabilities
- Implement real-time data feeds from forex providers
- Enhance sentiment analysis with historical data comparison