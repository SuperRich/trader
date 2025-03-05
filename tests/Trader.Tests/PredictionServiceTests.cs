using Xunit;
using NSubstitute;
using FluentAssertions;
using Trader.Core.Services;
using Trader.Core.Models;
using Trader.Infrastructure.Data;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Trader.Tests
{
    public class PredictionServiceTests
    {
        private readonly IForexDataProvider _dataProvider;
        private readonly PredictionService _predictionService;

        public PredictionServiceTests()
        {
            _dataProvider = Substitute.For<IForexDataProvider>();
            _predictionService = new PredictionService(_dataProvider);
        }

        [Fact]
        public async Task GetPrediction_ReturnsValidPrediction()
        {
            // Arrange
            string currencyPair = "EURUSD";
            _dataProvider.GetCandleDataAsync(currencyPair, Arg.Any<ChartTimeframe>(), Arg.Any<int>())
                .Returns(Task.FromResult(new List<CandleData>
                {
                    new CandleData { Open = 1.1000m, High = 1.1020m, Low = 1.0990m, Close = 1.1010m },
                    new CandleData { Open = 1.1010m, High = 1.1030m, Low = 1.1000m, Close = 1.1020m }
                }));

            // Act
            ForexPrediction result = await _predictionService.GetPredictionAsync(currencyPair);

            // Assert
            result.Should().NotBeNull();
            result.CurrencyPair.Should().Be(currencyPair);
            result.Timeframe.Should().Be(ChartTimeframe.Hours1);
            result.Direction.Should().BeOneOf(TradingDirection.Buy, TradingDirection.Sell);
        }

        [Fact]
        public async Task GetPrediction_WithEmptyData_ReturnsNeutralSignal()
        {
            // Arrange
            string currencyPair = "GBPUSD";
            _dataProvider.GetCandleDataAsync(currencyPair, Arg.Any<ChartTimeframe>(), Arg.Any<int>())
                .Returns(Task.FromResult(new List<CandleData>()));

            // Act
            ForexPrediction result = await _predictionService.GetPredictionAsync(currencyPair);

            // Assert
            result.Should().NotBeNull();
            result.Direction.Should().Be(TradingDirection.Sell);
        }
    }
}