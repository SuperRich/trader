using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services;

/// <summary>
/// Implementation of ISentimentAnalyzer using Perplexity AI's API to analyze forex market sentiment.
/// </summary>
public class PerplexitySentimentAnalyzer : ISentimentAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<PerplexitySentimentAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the PerplexitySentimentAnalyzer class.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for API requests.</param>
    /// <param name="configuration">Configuration containing the Perplexity API key.</param>
    /// <param name="logger">Logger for capturing errors and information.</param>
    /// <exception cref="ArgumentNullException">Thrown when the API key is not found in configuration.</exception>
    public PerplexitySentimentAnalyzer(HttpClient httpClient, IConfiguration configuration, ILogger<PerplexitySentimentAnalyzer> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        // Try to get the API key from configuration (checking both regular config and environment variables)
        _apiKey = configuration["Perplexity:ApiKey"] ?? 
                 configuration["TRADER_PERPLEXITY_API_KEY"] ?? 
                 throw new ArgumentNullException(nameof(configuration), "Perplexity API key is required");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Set up HttpClient
        _httpClient.BaseAddress = new Uri("https://api.perplexity.ai/");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    /// <summary>
    /// Analyzes market sentiment for the specified currency pair using Perplexity AI.
    /// </summary>
    /// <param name="currencyPair">The currency pair to analyze (e.g., "EURUSD", "GBPJPY").</param>
    /// <returns>A SentimentAnalysisResult containing sentiment data for the specified currency pair.</returns>
    public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string currencyPair)
    {
        try
        {
            var prompt = $"Analyze the current market sentiment for the {currencyPair} forex pair. Consider recent economic news, technical analysis, and market trends. Provide a summary of bullish and bearish factors, and conclude with whether the overall sentiment is bullish, bearish, or neutral. Format your response as JSON with the following structure: {{\"sentiment\": \"bullish|bearish|neutral\", \"confidence\": 0.0-1.0, \"factors\": [list of factors], \"summary\": \"brief summary\"}}";

            // Using current Perplexity API models from https://docs.perplexity.ai/guides/model-cards
            var requestBody = new
            {
                model = "sonar", // Using current model from Perplexity docs
                messages = new[]
                {
                    new { role = "system", content = "You are a financial analyst specializing in forex markets. Provide objective analysis with current data." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 1000
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
                
            // Ensure the Authorization header is set for each request
            // Some HttpClient implementations might reset headers between requests
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            _logger.LogInformation("Sending request to Perplexity API for {CurrencyPair} with auth header: {AuthHeader}", 
                currencyPair, request.Headers.Authorization?.ToString() ?? "None");
                
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Perplexity API request failed with status {StatusCode}: {ErrorMessage}", 
                    response.StatusCode, errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Authorization failed. API Key: {ApiKey} (first/last 4 chars)", 
                        _apiKey.Length > 8 ? $"{_apiKey[..4]}...{_apiKey[^4..]}" : "Too short");
                }
                
                response.EnsureSuccessStatusCode(); // This will throw and be caught by the catch block
            }

            var responseString = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response: {Response}", responseString);
            var responseObject = JsonSerializer.Deserialize<PerplexityResponse>(responseString);
            
            if (responseObject?.choices == null || responseObject.choices.Length == 0)
            {
                throw new Exception("Invalid response from Perplexity API");
            }

            // Extract the JSON from the response text
            var responseContent = responseObject.choices[0].message.content;
            var jsonStartIndex = responseContent.IndexOf('{');
            var jsonEndIndex = responseContent.LastIndexOf('}');
            
            if (jsonStartIndex == -1 || jsonEndIndex == -1)
            {
                throw new Exception("Could not extract JSON from response");
            }
                
            var jsonContent = responseContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            var sentimentData = JsonSerializer.Deserialize<SentimentData>(jsonContent);
            
            if (sentimentData == null)
            {
                throw new Exception("Could not parse sentiment data");
            }

            return new SentimentAnalysisResult
            {
                CurrencyPair = currencyPair,
                Sentiment = ParseSentiment(sentimentData.sentiment),
                Confidence = sentimentData.confidence,
                Factors = sentimentData.factors ?? new List<string>(),
                Summary = sentimentData.summary,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for {CurrencyPair}", currencyPair);
            
            // Return a fallback result
            return new SentimentAnalysisResult
            {
                CurrencyPair = currencyPair,
                Sentiment = SentimentType.Neutral,
                Confidence = 0.5m,
                Factors = new List<string> { "Error fetching sentiment data" },
                Summary = "Could not retrieve sentiment data at this time",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Parses a string sentiment indicator into the SentimentType enum.
    /// </summary>
    /// <param name="sentiment">String sentiment indicator (e.g., "bullish", "bearish").</param>
    /// <returns>The corresponding SentimentType enum value.</returns>
    private SentimentType ParseSentiment(string sentiment)
    {
        return sentiment?.ToLower() switch
        {
            "bullish" => SentimentType.Bullish,
            "bearish" => SentimentType.Bearish,
            _ => SentimentType.Neutral
        };
    }

    /// <summary>
    /// Response structure from the Perplexity API.
    /// </summary>
    private class PerplexityResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
    }

    /// <summary>
    /// Represents a choice in the Perplexity API response.
    /// </summary>
    private class Choice
    {
        public Message message { get; set; } = new Message();
    }

    /// <summary>
    /// Represents a message in the Perplexity API response.
    /// </summary>
    private class Message
    {
        public string content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Structure for parsing the sentiment data from the Perplexity response.
    /// </summary>
    private class SentimentData
    {
        public string sentiment { get; set; } = string.Empty;
        public decimal confidence { get; set; }
        public List<string>? factors { get; set; }
        public string summary { get; set; } = string.Empty;
    }
}