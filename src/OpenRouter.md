# OpenRouter Integration

## What is OpenRouter?

OpenRouter is a unified API service that provides access to hundreds of AI models through a single endpoint. It acts as a gateway to various AI providers, including OpenAI, Anthropic, Google, and many others. Key benefits include:

- **Single API for multiple models**: Access models from different providers with one API key
- **Automatic fallbacks**: If one model is unavailable, OpenRouter can automatically route to an alternative
- **Cost optimization**: OpenRouter can select the most cost-effective option for your needs
- **Simplified integration**: No need to manage multiple API keys and endpoints

## Setting Up OpenRouter

### 1. Get an API Key

1. Visit [OpenRouter.ai](https://openrouter.ai/) and create an account
2. Navigate to the API Keys section and generate a new key
3. Copy your API key for use in the Trader application

### 2. Configure in Trader Application

You can set up OpenRouter in the application using one of these methods:

#### Method 1: Using the API Endpoint

Make a POST request to `/api/config/openrouter-key` with the following JSON:

```json
{
  "apiKey": "your-openrouter-api-key",
  "saveToUserSecrets": true
}
```

Using curl:

```bash
curl -X POST https://localhost:7001/api/config/openrouter-key \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "your-openrouter-api-key", "saveToUserSecrets": true}'
```

The `saveToUserSecrets` parameter is optional and defaults to `false`. Set it to `true` if you want to save the key to your user secrets (recommended for development).

#### Method 2: Environment Variables

Set the environment variable `TRADER_OPENROUTER_API_KEY` with your OpenRouter API key.

#### Method 3: User Secrets (Development)

If you're developing the application, you can use .NET User Secrets:

```bash
dotnet user-secrets set "OpenRouter:ApiKey" "your-openrouter-api-key"
```

### 3. Verify Configuration

To check if your OpenRouter API key is properly configured, make a GET request to `/api/diagnostics/config`. The response will include `HasOpenRouterApiKey` which should be `true` if the key is set.

Using curl:

```bash
curl -X GET https://localhost:7001/api/diagnostics/config
```

## Model Selection

### Available Models

OpenRouter provides access to a wide range of models from different providers. Some popular models include:

- `anthropic/claude-3-opus:beta` - Anthropic's most powerful model
- `anthropic/claude-3-sonnet:beta` - Balanced performance and cost
- `anthropic/claude-3-haiku:beta` - Fastest and most cost-effective Claude model
- `openai/gpt-4o` - OpenAI's latest GPT-4 model
- `openai/gpt-4-turbo` - OpenAI's GPT-4 Turbo model
- `google/gemini-pro` - Google's Gemini Pro model

For a complete list of available models, visit the [OpenRouter Models page](https://openrouter.ai/models).

### Setting the Model

By default, the application uses `openrouter/auto` if no model is specified. This allows OpenRouter to automatically select the best model for your request. You can change the model using the API endpoint:

```
POST /api/config/openrouter-model
```

With the following JSON body:

```json
{
  "model": "anthropic/claude-3-sonnet:beta",
  "saveToUserSecrets": true
}
```

Using curl:

```bash
curl -X POST https://localhost:7001/api/config/openrouter-model \
  -H "Content-Type: application/json" \
  -d '{"model": "anthropic/claude-3-sonnet:beta", "saveToUserSecrets": true}'
```

### Smart Model Selection

OpenRouter provides special routing identifiers for automatic model selection:

- `openrouter/auto` (default) - Let OpenRouter choose the best model based on your request
- `openrouter/auto-claude` - Use the best available Claude model
- `openrouter/auto-gpt` - Use the best available GPT model

To set the model back to the default auto-routing:

```bash
curl -X POST https://localhost:7001/api/config/openrouter-model \
  -H "Content-Type: application/json" \
  -d '{"model": "openrouter/auto", "saveToUserSecrets": true}'
```

### Viewing Which Model Was Used

When using `openrouter/auto` or other auto-routing options, you might want to know which model OpenRouter actually chose for your analysis. The application logs this information, and you can access it in several ways:

1. **Check the Application Logs**: The application logs which model was used for each analysis. Look for log entries containing "OpenRouter selected model:" followed by the model identifier.

2. **View Response Headers**: When using curl with the `-v` (verbose) flag, you can see the response headers which include information about the model used:

```bash
curl -v -X GET "https://localhost:7001/api/trading/analyze/EURUSD"
```

3. **Model Information in Analysis**: The application includes the model information in the analysis response when using the standard endpoints. Look for a field called `ModelUsed` in the JSON response:

```bash
curl -X GET "https://localhost:7001/api/trading/analyze/EURUSD" | jq '.ModelUsed'
```

#### Important Note About Provider-Specific Endpoints

When using provider-specific endpoints (those that include a data provider in the URL), the model information may not be included in the response. For example:

```bash
curl -X GET "https://localhost:7001/api/trading/analyze/GBPJPY/TraderMade?accountBalance=201&leverage=1000&targetProfits=1,2,3"
```

In these cases, the model information is still logged in the application logs but may not appear in the response. This is because provider-specific endpoints create a new analyzer instance for each request, which may handle response formatting differently.

To see which model was used with provider-specific endpoints:

1. Check the application logs (most reliable method)
2. Use the standard endpoint without specifying a provider when possible:

```bash
curl -X GET "https://localhost:7001/api/trading/analyze/GBPJPY?accountBalance=201&leverage=1000&targetProfits=1,2,3"
```

### Accessing Application Logs

The application logs contain detailed information about OpenRouter API calls, including which model was selected for each request. Here's how to access these logs in different environments:

#### Development Environment

When running the application locally in development mode, logs are output to:

1. **Console Output**: The terminal or command prompt window where you started the application will display logs in real-time. Look for lines containing "OpenRouter" or "selected model".

2. **Visual Studio/VS Code Debug Console**: If running through an IDE, check the Debug Console or Output window for log messages.

3. **Log Files**: By default, .NET applications in development mode may also write logs to files in the following locations:
   - Windows: `%APPDATA%\Trader\logs\` or the project directory under `logs\`
   - macOS/Linux: `~/.local/share/Trader/logs/` or the project directory under `logs/`

#### Production Environment

In production, logs may be stored in various locations depending on your deployment:

1. **Application Insights**: If Azure Application Insights is configured, logs can be viewed in the Azure portal.

2. **File Logs**: Check the application's configured log directory, typically:
   - Windows: `C:\ProgramData\Trader\logs\`
   - Linux: `/var/log/trader/`

3. **Docker Logs**: If running in Docker, use the following command to view logs:
   ```bash
   docker logs trader-api
   ```

4. **Kubernetes Logs**: If deployed to Kubernetes, use:
   ```bash
   kubectl logs -n <namespace> <pod-name>
   ```

#### Filtering for OpenRouter Information

To find specific OpenRouter model information in the logs, you can use grep (Linux/macOS) or findstr (Windows):

```bash
# Linux/macOS
grep "OpenRouter selected model" /path/to/logfile.log

# Windows
findstr "OpenRouter selected model" C:\path\to\logfile.log

# Docker
docker logs trader-api | grep "OpenRouter selected model"
```

The log entries will typically look like:
```
[INFO] OpenRouter selected model: anthropic/claude-3-opus:beta for request ID: 12345
```

This helps you understand which model was chosen for your specific analysis and can be useful for:
- Tracking costs (different models have different pricing)
- Evaluating performance of different models
- Debugging if results aren't as expected

Example response showing the model used (from standard endpoint):
```json
{
  "CurrencyPair": "EURUSD",
  "Sentiment": "Bullish",
  "ModelUsed": "anthropic/claude-3-opus:beta",
  "ConfidenceScore": 0.85,
  ...
}
```

## How It Works in the Application

When OpenRouter is configured, the application will:

1. Prioritize OpenRouter over Perplexity for sentiment analysis and trading recommendations
2. Use the specified model (or the default `openrouter/auto` if none is specified)
3. Send requests to OpenRouter's API with your prompt and parameters
4. Process the responses to extract sentiment analysis and trading recommendations

The OpenRouterAnalyzer implements the same interface as PerplexitySentimentAnalyzer, so all existing functionality works seamlessly with either service.

## Using Web Search with OpenRouter

OpenRouter supports models with web search capabilities, which can provide up-to-date market information and news that may affect trading decisions. To leverage this feature:

1. Ensure you're using a model that supports web search (like Claude 3 Opus or GPT-4o)
2. The application automatically formats prompts to request current market information when needed
3. Web search is particularly useful for:
   - Getting the latest news about specific currency pairs
   - Analyzing recent economic events that might impact markets
   - Checking for breaking news that could cause market volatility

When using the trading recommendations endpoint (`/api/trading/recommendations`), the analyzer will automatically incorporate relevant web search results to improve the quality of recommendations.

Example request to analyze a currency pair with web search capabilities:

```bash
curl -X GET "https://localhost:7001/api/trading/analyze/EURUSD"
```

The response will include analysis that may incorporate recent market news and events if the selected model supports web search.

## Benefits of Using OpenRouter in Trader

- **Access to more powerful models**: Claude 3 Opus and GPT-4o can provide more accurate market analysis
- **Reliability**: If one provider has downtime, OpenRouter can route to alternatives
- **Flexibility**: Easily switch between models to find the best balance of cost and performance
- **Future-proof**: As new models are released, they become available through OpenRouter without code changes
- **Web search integration**: Get analysis based on the latest market news and events
- **Transparency**: See which model was used for each analysis

## Troubleshooting

If you encounter issues with OpenRouter:

1. Verify your API key is correct and has sufficient credits
2. Check if the selected model is available (some models may have usage restrictions)
3. Ensure your requests comply with OpenRouter's usage policies
4. Check the application logs for detailed error messages

For more information, visit the [OpenRouter Documentation](https://openrouter.ai/docs). 