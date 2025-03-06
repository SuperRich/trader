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
}