using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp;
using Trader.Core.Services;
using Trader.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace Trader.Tests;

/// <summary>
/// Tests for the sentiment analysis functionality.
/// </summary>
public class SentimentAnalyzerTests
{
    private readonly ISentimentAnalyzer _sentimentAnalyzer;
    private readonly HttpClient _httpClient;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PerplexitySentimentAnalyzer> _logger;

    /// <summary>
    /// Setup the test context with mocked dependencies.
    /// </summary>
    public SentimentAnalyzerTests()
    {
        // Create mock configuration
        _configuration = Substitute.For<IConfiguration>();
        _configuration["Perplexity:ApiKey"].Returns("test-api-key");
        
        // Create mock logger
        _logger = Substitute.For<ILogger<PerplexitySentimentAnalyzer>>();
        
        // Create mock HTTP handler
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHttp)
        {
            BaseAddress = new Uri("https://api.perplexity.ai/")
        };
        
        // Create the sentiment analyzer with mocked dependencies
        _sentimentAnalyzer = new PerplexitySentimentAnalyzer(_httpClient, _configuration, _logger);
    }

    /// <summary>
    /// Test that ParseSentiment correctly maps string values to enum values.
    /// </summary>
    [Theory]
    [InlineData("bullish", SentimentType.Bullish)]
    [InlineData("Bullish", SentimentType.Bullish)]
    [InlineData("BULLISH", SentimentType.Bullish)]
    [InlineData("bearish", SentimentType.Bearish)]
    [InlineData("neutral", SentimentType.Neutral)]
    [InlineData("", SentimentType.Neutral)]
    [InlineData(null, SentimentType.Neutral)]
    [InlineData("unknown", SentimentType.Neutral)]
    public void ParseSentiment_MapsStringValuesToEnumValues(string input, SentimentType expected)
    {
        // Arrange
        var method = typeof(PerplexitySentimentAnalyzer).GetMethod("ParseSentiment", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = (SentimentType)method!.Invoke(_sentimentAnalyzer, new object[] { input })!;
        
        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Test that AnalyzeSentimentAsync handles API errors gracefully.
    /// </summary>
    [Fact]
    public async Task AnalyzeSentimentAsync_WhenApiErrors_ReturnsFallbackResult()
    {
        // Arrange - Set up the HttpClient mock to simulate a failure
        _mockHttp.When(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            .Respond(HttpStatusCode.InternalServerError);
        
        // Act
        var result = await _sentimentAnalyzer.AnalyzeSentimentAsync("EURUSD");
        
        // Assert
        result.Should().NotBeNull();
        result.CurrencyPair.Should().Be("EURUSD");
        result.Sentiment.Should().Be(SentimentType.Neutral);
        result.Confidence.Should().Be(0.5m);
        result.Factors.Should().ContainSingle(f => f == "Error fetching sentiment data");
        result.Summary.Should().Be("Could not retrieve sentiment data at this time");
    }

    /// <summary>
    /// Test that AnalyzeSentimentAsync correctly parses a successful response.
    /// </summary>
    [Fact]
    public async Task AnalyzeSentimentAsync_WithValidResponse_ReturnsExpectedResult()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""123"",
            ""model"": ""llama-3-sonar-large-32k-online"",
            ""choices"": [
                {
                    ""index"": 0,
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""Based on my analysis, here's the current market sentiment for EURUSD:\n\n```json\n{\n  \""sentiment\"": \""bullish\"",\n  \""confidence\"": 0.75,\n  \""factors\"": [\n    \""Strong Eurozone economic data\"",\n    \""ECB maintaining hawkish stance on rates\"",\n    \""Technical breakout above 1.0850 resistance\""  \n  ],\n  \""summary\"": \""The EURUSD pair shows bullish sentiment due to stronger-than-expected Eurozone economic indicators and the ECB's commitment to maintaining higher rates for longer, while the USD faces pressure from expectations of Fed rate cuts.\""  \n}\n```\n\nThis analysis is based on recent economic releases and central bank communications. Market conditions can change rapidly, so always perform your own due diligence before trading.""
                    }
                }
            ]
        }";
        
        _mockHttp.When(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            .Respond("application/json", responseJson);
        
        // Act
        var result = await _sentimentAnalyzer.AnalyzeSentimentAsync("EURUSD");
        
        // Assert
        result.Should().NotBeNull();
        result.CurrencyPair.Should().Be("EURUSD");
        result.Sentiment.Should().Be(SentimentType.Bullish);
        result.Confidence.Should().Be(0.75m);
        result.Factors.Should().HaveCount(3);
        result.Factors.Should().Contain("Strong Eurozone economic data");
        result.Summary.Should().Contain("EURUSD pair shows bullish sentiment");
    }

    /// <summary>
    /// Test that AnalyzeSentimentAsync handles invalid JSON in the response.
    /// </summary>
    [Fact]
    public async Task AnalyzeSentimentAsync_WithInvalidJsonResponse_ReturnsFallbackResult()
    {
        // Arrange - Set up response with invalid JSON structure
        var responseJson = @"{
            ""id"": ""123"",
            ""model"": ""llama-3-sonar-large-32k-online"",
            ""choices"": [
                {
                    ""index"": 0,
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""This is not a valid JSON response""
                    }
                }
            ]
        }";
        
        _mockHttp.When(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            .Respond("application/json", responseJson);
        
        // Act
        var result = await _sentimentAnalyzer.AnalyzeSentimentAsync("EURUSD");
        
        // Assert
        result.Should().NotBeNull();
        result.CurrencyPair.Should().Be("EURUSD");
        result.Sentiment.Should().Be(SentimentType.Neutral);
        result.Factors.Should().ContainSingle(f => f == "Error fetching sentiment data");
    }
    
    /// <summary>
    /// Test that GetTradingRecommendationsAsync correctly parses a successful response.
    /// </summary>
    [Fact]
    public async Task GetTradingRecommendationsAsync_WithValidResponse_ReturnsExpectedResult()
    {
        // Arrange
        var responseJson = @"{
            ""id"": ""456"",
            ""model"": ""llama-3-sonar-large-32k-online"",
            ""choices"": [
                {
                    ""index"": 0,
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""Based on my analysis of the current forex market, here are the most promising trading opportunities:\n\n```json\n{\n  \""recommendations\"": [\n    {\n      \""pair\"": \""EURUSD\"",\n      \""direction\"": \""Buy\"",\n      \""sentiment\"": \""bullish\"",\n      \""confidence\"": 0.85,\n      \""currentPrice\"": 1.0925,\n      \""takeProfitPrice\"": 1.1050,\n      \""stopLossPrice\"": 1.0850,\n      \""factors\"": [\n        \""ECB hawkish stance\"",\n        \""USD weakness on Fed rate cut expectations\"",\n        \""Technical breakout above 1.0900 resistance\""      \n      ],\n      \""rationale\"": \""EURUSD shows bullish momentum as the ECB maintains a hawkish policy while the Fed signals potential rate cuts. Price has broken key resistance at 1.0900.\""      \n    },\n    {\n      \""pair\"": \""GBPJPY\"",\n      \""direction\"": \""Sell\"",\n      \""sentiment\"": \""bearish\"",\n      \""confidence\"": 0.75,\n      \""currentPrice\"": 190.50,\n      \""takeProfitPrice\"": 188.00,\n      \""stopLossPrice\"": 191.75,\n      \""factors\"": [\n        \""Overbought conditions on daily chart\"",\n        \""BOJ hawkish shift in policy\"",\n        \""UK economic slowdown\""      \n      ],\n      \""rationale\"": \""GBPJPY shows signs of exhaustion after a strong rally, with the BOJ adopting a more hawkish stance while UK economic data disappoints.\""      \n    }\n  ]\n}\n```\n\nThese recommendations are based on current market conditions, technical analysis, and fundamental factors. Always conduct your own analysis and use proper risk management before entering any trades.""
                    }
                }
            ]
        }";
        
        _mockHttp.When(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            .Respond("application/json", responseJson);
        
        // Act
        var result = await _sentimentAnalyzer.GetTradingRecommendationsAsync(2);
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        
        // Verify first recommendation
        var firstRec = result[0];
        firstRec.CurrencyPair.Should().Be("EURUSD");
        firstRec.Direction.Should().Be("Buy");
        firstRec.Sentiment.Should().Be(SentimentType.Bullish);
        firstRec.Confidence.Should().Be(0.85m);
        firstRec.CurrentPrice.Should().Be(1.0925m);
        firstRec.TakeProfitPrice.Should().Be(1.1050m);
        firstRec.StopLossPrice.Should().Be(1.0850m);
        firstRec.Factors.Should().HaveCount(3);
        firstRec.Rationale.Should().Contain("EURUSD shows bullish momentum");
        
        // Verify second recommendation
        var secondRec = result[1];
        secondRec.CurrencyPair.Should().Be("GBPJPY");
        secondRec.Direction.Should().Be("Sell");
        secondRec.Sentiment.Should().Be(SentimentType.Bearish);
        secondRec.Confidence.Should().Be(0.75m);
        secondRec.CurrentPrice.Should().Be(190.50m);
        
        // Calculate expected risk reward ratio and verify
        decimal reward = Math.Abs(188.00m - 190.50m); // TP distance
        decimal risk = Math.Abs(191.75m - 190.50m);   // SL distance
        decimal expectedRatio = reward / risk;
        secondRec.RiskRewardRatio.Should().BeApproximately(expectedRatio, 0.001m);
    }
    
    /// <summary>
    /// Test that RiskRewardRatio handles potential divide-by-zero scenarios.
    /// </summary>
    [Fact]
    public void RiskRewardRatio_WhenDivideByZeroIsPossible_ReturnsZeroInstead()
    {
        // Arrange
        var recommendation = new ForexRecommendation
        {
            CurrencyPair = "EURUSD",
            CurrentPrice = 1.1000m,
            StopLossPrice = 1.1000m, // Same as current price - would cause divide by zero
            TakeProfitPrice = 1.1100m
        };
        
        // Act - this should not throw an exception
        var ratio = recommendation.RiskRewardRatio;
        
        // Assert
        ratio.Should().Be(0);
        
        // Test zero values too
        recommendation.CurrentPrice = 0;
        recommendation.RiskRewardRatio.Should().Be(0);
        
        recommendation.CurrentPrice = 1.1000m;
        recommendation.TakeProfitPrice = 0;
        recommendation.RiskRewardRatio.Should().Be(0);
        
        recommendation.TakeProfitPrice = 1.1100m;
        recommendation.StopLossPrice = 0;
        recommendation.RiskRewardRatio.Should().Be(0);
    }
    
    /// <summary>
    /// Test that GetTradingRecommendationsAsync handles API errors gracefully.
    /// </summary>
    [Fact]
    public async Task GetTradingRecommendationsAsync_WhenApiErrors_ReturnsFallbackResult()
    {
        // Arrange - Set up the HttpClient mock to simulate a failure
        _mockHttp.When(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            .Respond(HttpStatusCode.InternalServerError);
        
        // Act
        var result = await _sentimentAnalyzer.GetTradingRecommendationsAsync();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].CurrencyPair.Should().Be("EURUSD");
        result[0].Direction.Should().Be("None");
        result[0].Sentiment.Should().Be(SentimentType.Neutral);
        result[0].Factors.Should().ContainSingle(f => f == "Error fetching recommendations");
        
        // Check that we have non-zero price values to avoid divide-by-zero
        result[0].CurrentPrice.Should().NotBe(0);
        result[0].TakeProfitPrice.Should().NotBe(0);
        result[0].StopLossPrice.Should().NotBe(0);
        result[0].CurrentPrice.Should().NotBe(result[0].StopLossPrice); // Ensure no divide by zero
        result[0].RiskRewardRatio.Should().Be(1.0m); // 1.01-1.0 / 1.0-0.99 = 0.01/0.01 = 1.0
        result[0].Rationale.Should().Contain("Could not retrieve trading recommendations");
    }
}