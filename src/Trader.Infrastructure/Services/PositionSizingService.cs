using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Trader.Core.Models;
using Trader.Core.Services;

namespace Trader.Infrastructure.Services
{
    public class PositionSizingService : IPositionSizingService
    {
        private readonly ILogger<PositionSizingService> _logger;
        
        // Standard lot sizes for different asset classes
        private readonly Dictionary<string, decimal> _lotSizes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            // Forex pairs (standard lot = 100,000 units)
            { "USD", 100000m },
            { "EUR", 100000m },
            { "GBP", 100000m },
            { "JPY", 100000m },
            { "AUD", 100000m },
            { "CAD", 100000m },
            { "CHF", 100000m },
            { "NZD", 100000m },
            
            // Crypto (varies by exchange, using common values)
            { "BTC", 1m },
            { "ETH", 1m },
            { "XRP", 1m }
        };
        
        // Pip values for different currency pairs
        private readonly Dictionary<string, decimal> _pipValues = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            // Major forex pairs
            { "EURUSD", 0.0001m },
            { "GBPUSD", 0.0001m },
            { "USDJPY", 0.01m },
            { "USDCHF", 0.0001m },
            { "AUDUSD", 0.0001m },
            { "USDCAD", 0.0001m },
            { "NZDUSD", 0.0001m },
            
            // Cross pairs
            { "EURGBP", 0.0001m },
            { "EURJPY", 0.01m },
            { "GBPJPY", 0.01m },
            
            // Crypto (using percentage for simplicity)
            { "BTCUSD", 0.01m }, // 1% movement
            { "ETHUSD", 0.01m }  // 1% movement
        };
        
        public PositionSizingService(ILogger<PositionSizingService> logger)
        {
            _logger = logger;
        }
        
        public async Task<PositionSizingInfo> CalculatePositionSizingAsync(
            string symbol, 
            decimal currentPrice, 
            decimal accountBalance = 201m, 
            decimal leverage = 1000m,
            decimal[]? targetProfits = null,
            string? tradeDirection = null,
            decimal? stopLossPrice = null,
            decimal? takeProfitPrice = null)
        {
            _logger.LogInformation($"Calculating position sizing for {symbol} at price {currentPrice}, direction: {tradeDirection ?? "Not specified"}");
            
            // Default target profits if none provided
            if (targetProfits == null || targetProfits.Length == 0)
            {
                targetProfits = new[] { 1m, 2m, 3m, 5m, 10m };
            }
            
            var result = new PositionSizingInfo
            {
                AccountBalance = accountBalance,
                Leverage = leverage,
                Symbol = symbol,
                CurrentPrice = currentPrice
            };
            
            // Extract base and quote currencies from symbol
            string baseCurrency = GetBaseCurrency(symbol);
            string quoteCurrency = GetQuoteCurrency(symbol);
            
            decimal standardLotSize = GetStandardLotSize(baseCurrency);
            decimal pipValue = GetPipValue(symbol);
            
            // Calculate maximum position size with full leverage
            result.MaxPositionSize = accountBalance * leverage;
            result.MaxLotSize = Math.Round(result.MaxPositionSize / standardLotSize, 2);
            
            // Determine if this is a buy or sell trade
            bool isBuyTrade = true; // Default to buy
            if (!string.IsNullOrEmpty(tradeDirection))
            {
                isBuyTrade = tradeDirection.Equals("Buy", StringComparison.OrdinalIgnoreCase);
            }
            
            // Calculate for each target profit
            foreach (var targetProfit in targetProfits)
            {
                // For crypto and forex, calculate differently
                bool isCrypto = IsCryptoPair(symbol);
                
                // Calculate required price movement to achieve target profit
                decimal priceMovementPercent;
                decimal priceMovementRequired;
                decimal requiredPositionSize;
                decimal requiredLotSize;
                
                if (isCrypto)
                {
                    // For crypto, we use percentage movement
                    // Formula: targetProfit = positionSize * priceMovementPercent
                    // Therefore: positionSize = targetProfit / priceMovementPercent
                    
                    // Start with a 1% price movement and calculate required position
                    priceMovementPercent = 0.01m; // 1%
                    priceMovementRequired = currentPrice * priceMovementPercent;
                    
                    // Calculate position size needed for this profit with 1% movement
                    requiredPositionSize = targetProfit / priceMovementPercent;
                    
                    // If this exceeds max position size, recalculate with larger price movement
                    if (requiredPositionSize > result.MaxPositionSize)
                    {
                        // Calculate minimum price movement needed with max position
                        priceMovementPercent = targetProfit / result.MaxPositionSize;
                        priceMovementRequired = currentPrice * priceMovementPercent;
                        requiredPositionSize = result.MaxPositionSize;
                    }
                }
                else
                {
                    // For forex, we use pip movement
                    // Formula: targetProfit = (positionSize / standardLotSize) * (pipMovement * pipValue)
                    // Therefore: positionSize = (targetProfit * standardLotSize) / (pipMovement * pipValue)
                    
                    // Start with a 10 pip movement
                    decimal pipMovement = 10m;
                    decimal pipValuePerLot = CalculatePipValuePerLot(symbol, currentPrice);
                    
                    // Calculate position size needed for this profit with 10 pip movement
                    requiredPositionSize = (targetProfit * standardLotSize) / (pipMovement * pipValuePerLot);
                    
                    // If this exceeds max position size, recalculate with larger pip movement
                    if (requiredPositionSize > result.MaxPositionSize)
                    {
                        // Calculate minimum pip movement needed with max position
                        pipMovement = (targetProfit * standardLotSize) / (result.MaxPositionSize * pipValuePerLot);
                        requiredPositionSize = result.MaxPositionSize;
                    }
                    
                    priceMovementRequired = pipMovement * pipValue;
                    priceMovementPercent = priceMovementRequired / currentPrice;
                }
                
                requiredLotSize = Math.Round(requiredPositionSize / standardLotSize, 2);
                
                // Calculate risk (assuming a 1:1 risk-reward ratio for simplicity)
                decimal riskAmount = targetProfit;
                decimal riskPercentage = Math.Round((riskAmount / accountBalance) * 100, 2);
                
                // Use provided stop loss and take profit if available
                decimal finalStopLossPrice, finalTakeProfitPrice, invalidationPrice;
                decimal finalRiskRewardRatio;
                
                if (stopLossPrice.HasValue && takeProfitPrice.HasValue)
                {
                    // Use the provided values
                    finalStopLossPrice = stopLossPrice.Value;
                    finalTakeProfitPrice = takeProfitPrice.Value;
                    
                    // Calculate invalidation price based on direction
                    if (isBuyTrade)
                    {
                        // For buy trades, invalidation is below stop loss
                        invalidationPrice = Math.Round(finalStopLossPrice * (1 - (isCrypto ? 0.01m : pipValue)), isCrypto ? 2 : 5);
                    }
                    else
                    {
                        // For sell trades, invalidation is above stop loss
                        invalidationPrice = Math.Round(finalStopLossPrice * (1 + (isCrypto ? 0.01m : pipValue)), isCrypto ? 2 : 5);
                    }
                    
                    // Calculate actual risk-reward ratio based on direction
                    if (isBuyTrade)
                    {
                        finalRiskRewardRatio = Math.Round((finalTakeProfitPrice - currentPrice) / (currentPrice - finalStopLossPrice), 2);
                    }
                    else
                    {
                        finalRiskRewardRatio = Math.Round((currentPrice - finalTakeProfitPrice) / (finalStopLossPrice - currentPrice), 2);
                    }
                }
                else
                {
                    // Calculate price levels based on a 2:1 risk-reward ratio
                    decimal baseStopLoss = isCrypto ? 0.02m : pipValue * 30; // 2% for crypto, 30 pips for forex
                    decimal stopLossDistance = Math.Max(baseStopLoss, priceMovementRequired / 2); // Scale SL with target
                    
                    if (isBuyTrade)
                    {
                        // For buy trades (long position)
                        if (isCrypto)
                        {
                            // For crypto, round to 2 decimal places
                            finalStopLossPrice = Math.Round(currentPrice * (1 - stopLossDistance), 2);
                            finalTakeProfitPrice = Math.Round(currentPrice * (1 + (stopLossDistance * 2)), 2); // 2:1 ratio
                            invalidationPrice = Math.Round(finalStopLossPrice * (1 - 0.01m), 2); // 1% below SL
                        }
                        else
                        {
                            // For forex, round to 5 decimal places
                            finalStopLossPrice = Math.Round(currentPrice * (1 - stopLossDistance), 5);
                            finalTakeProfitPrice = Math.Round(currentPrice * (1 + (stopLossDistance * 2)), 5); // 2:1 ratio
                            invalidationPrice = Math.Round(finalStopLossPrice * (1 - pipValue), 5); // 1 pip below SL
                        }
                        
                        // Calculate actual risk-reward ratio
                        finalRiskRewardRatio = Math.Round((finalTakeProfitPrice - currentPrice) / (currentPrice - finalStopLossPrice), 2);
                    }
                    else
                    {
                        // For sell trades (short position)
                        if (isCrypto)
                        {
                            // For crypto, round to 2 decimal places
                            finalStopLossPrice = Math.Round(currentPrice * (1 + stopLossDistance), 2);
                            finalTakeProfitPrice = Math.Round(currentPrice * (1 - (stopLossDistance * 2)), 2); // 2:1 ratio
                            invalidationPrice = Math.Round(finalStopLossPrice * (1 + 0.01m), 2); // 1% above SL
                        }
                        else
                        {
                            // For forex, round to 5 decimal places
                            finalStopLossPrice = Math.Round(currentPrice * (1 + stopLossDistance), 5);
                            finalTakeProfitPrice = Math.Round(currentPrice * (1 - (stopLossDistance * 2)), 5); // 2:1 ratio
                            invalidationPrice = Math.Round(finalStopLossPrice * (1 + pipValue), 5); // 1 pip above SL
                        }
                        
                        // Calculate actual risk-reward ratio
                        finalRiskRewardRatio = Math.Round((currentPrice - finalTakeProfitPrice) / (finalStopLossPrice - currentPrice), 2);
                    }
                }
                
                result.ProfitTargets[targetProfit] = new PositionSizingTarget
                {
                    TargetProfit = targetProfit,
                    RequiredPositionSize = Math.Round(requiredPositionSize, 2),
                    RequiredLotSize = requiredLotSize,
                    PriceMovementRequired = Math.Round(priceMovementRequired, isCrypto ? 2 : 5),
                    PriceMovementPercent = Math.Round(priceMovementPercent * 100, 2), // Convert to percentage
                    RiskAmount = riskAmount,
                    RiskPercentage = riskPercentage,
                    StopLossPrice = finalStopLossPrice,
                    TakeProfitPrice = finalTakeProfitPrice,
                    InvalidationPrice = invalidationPrice,
                    RiskRewardRatio = finalRiskRewardRatio
                };
            }
            
            return await Task.FromResult(result);
        }
        
        private string GetBaseCurrency(string symbol)
        {
            // Extract the base currency (first part of the pair)
            if (symbol.Length >= 6)
            {
                return symbol.Substring(0, 3);
            }
            
            // For crypto pairs like BTCUSD
            if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
            {
                return symbol.Substring(0, symbol.Length - 3);
            }
            
            // Default to first 3 characters
            return symbol.Length >= 3 ? symbol.Substring(0, 3) : symbol;
        }
        
        private string GetQuoteCurrency(string symbol)
        {
            // Extract the quote currency (second part of the pair)
            if (symbol.Length >= 6)
            {
                return symbol.Substring(3, 3);
            }
            
            // For crypto pairs like BTCUSD
            if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
            {
                return "USD";
            }
            
            // Default to USD if we can't determine
            return "USD";
        }
        
        private decimal GetStandardLotSize(string currency)
        {
            if (_lotSizes.TryGetValue(currency, out decimal lotSize))
            {
                return lotSize;
            }
            
            // Default to forex standard lot
            return 100000m;
        }
        
        private decimal GetPipValue(string symbol)
        {
            if (_pipValues.TryGetValue(symbol, out decimal pipValue))
            {
                return pipValue;
            }
            
            // Default pip values based on currency
            if (symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase))
            {
                return 0.01m; // JPY pairs typically have 2 decimal places
            }
            
            // Default for forex
            return 0.0001m; // Most forex pairs have 4 decimal places
        }
        
        private decimal CalculatePipValuePerLot(string symbol, decimal currentPrice)
        {
            string quoteCurrency = GetQuoteCurrency(symbol);
            decimal pipValue = GetPipValue(symbol);
            
            // For USD quote currency, pip value is fixed
            if (quoteCurrency == "USD")
            {
                return 10m; // $10 per pip for a standard lot
            }
            
            // For USD base currency (e.g., USDJPY)
            if (GetBaseCurrency(symbol) == "USD")
            {
                return 10m / currentPrice; // Convert to USD
            }
            
            // For cross pairs, this is an approximation
            return 10m; // Simplified for this implementation
        }
        
        private bool IsCryptoPair(string symbol)
        {
            // Common crypto base currencies
            string[] cryptoCurrencies = { "BTC", "ETH", "XRP", "LTC", "BCH", "ADA", "DOT", "LINK" };
            
            foreach (var crypto in cryptoCurrencies)
            {
                if (symbol.StartsWith(crypto, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
} 