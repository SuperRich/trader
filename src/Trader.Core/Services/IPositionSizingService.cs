using System.Threading.Tasks;
using System.Collections.Generic;
using Trader.Core.Models;

namespace Trader.Core.Services
{
    public interface IPositionSizingService
    {
        /// <summary>
        /// Calculates position sizing information based on account details and current market conditions
        /// </summary>
        /// <param name="symbol">Trading pair symbol (e.g., "EURUSD", "BTCUSD")</param>
        /// <param name="currentPrice">Current market price</param>
        /// <param name="accountBalance">Account balance in GBP</param>
        /// <param name="leverage">Account leverage (e.g., 1000 for 1:1000)</param>
        /// <param name="targetProfits">List of target profits in GBP to calculate for</param>
        /// <returns>Position sizing information including max position and targets</returns>
        Task<PositionSizingInfo> CalculatePositionSizingAsync(
            string symbol, 
            decimal currentPrice, 
            decimal accountBalance = 201m, 
            decimal leverage = 1000m,
            decimal[]? targetProfits = null);
    }
} 