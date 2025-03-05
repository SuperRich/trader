using Xunit;
using FluentAssertions;
using Trader.Infrastructure.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Tests
{
    public class ForexDataProviderTests
    {
        private readonly ForexDataProvider _dataProvider;

        public ForexDataProviderTests()
        {
            _dataProvider = new ForexDataProvider();
        }

        [Fact]
        public async Task GetCandleDataAsync_ReturnsCandleData()
        {
            // Arrange
            string currencyPair = "EURUSD";
            ChartTimeframe timeframe = ChartTimeframe.Hours1;

            // Act
            var result = await _dataProvider.GetCandleDataAsync(currencyPair, timeframe);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Should().AllBeOfType<CandleData>();
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task GetCandleDataAsync_WithSpecificCount_ReturnsCandleDataWithCount()
        {
            // Arrange
            string currencyPair = "EURUSD";
            ChartTimeframe timeframe = ChartTimeframe.Minutes5;
            int count = 10;

            // Act
            var result = await _dataProvider.GetCandleDataAsync(currencyPair, timeframe, count);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(count);
        }

        [Fact]
        public async Task GetCandleDataAsync_TimestampsShouldBeInChronologicalOrder()
        {
            // Arrange
            string currencyPair = "EURUSD";
            ChartTimeframe timeframe = ChartTimeframe.Minutes15;

            // Act
            var result = await _dataProvider.GetCandleDataAsync(currencyPair, timeframe);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeInAscendingOrder(c => c.Timestamp);
        }
    }
}