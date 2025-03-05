using Xunit;
using FluentAssertions;
using Trader.Core.Models;
using System.Collections.Generic;
using System;

namespace Trader.Tests
{
    public class ForexPredictionTests
    {
        [Fact]
        public void ForexPrediction_Properties_SetCorrectly()
        {
            // Arrange
            string currencyPair = "USDJPY";
            ChartTimeframe timeframe = ChartTimeframe.Hours1;
            TradingDirection direction = TradingDirection.Buy;
            decimal entryPrice = 110.50m;
            decimal stopLoss = 110.40m;
            decimal takeProfit = 110.70m;

            // Act
            var prediction = new ForexPrediction
            {
                CurrencyPair = currencyPair,
                Timeframe = timeframe,
                Direction = direction,
                EntryPrice = entryPrice,
                StopLoss = stopLoss,
                TakeProfit = takeProfit
            };

            // Assert
            prediction.CurrencyPair.Should().Be(currencyPair);
            prediction.Timeframe.Should().Be(timeframe);
            prediction.Direction.Should().Be(direction);
            prediction.EntryPrice.Should().Be(entryPrice);
            prediction.StopLoss.Should().Be(stopLoss);
            prediction.TakeProfit.Should().Be(takeProfit);
        }

        [Fact]
        public void RiskRewardRatio_CalculatesCorrectly()
        {
            // Arrange
            var prediction = new ForexPrediction
            {
                CurrencyPair = "EURUSD",
                EntryPrice = 1.1000m,
                StopLoss = 1.0950m,
                TakeProfit = 1.1100m
            };

            // Act & Assert
            // RR = (TP - Entry) / (Entry - SL) = (1.1100 - 1.1000) / (1.1000 - 1.0950) = 0.01 / 0.005 = 2.0
            prediction.RiskRewardRatio.Should().Be(2.0m);
        }
    }
}