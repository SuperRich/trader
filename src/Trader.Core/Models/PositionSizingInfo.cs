using System;
using System.Collections.Generic;

namespace Trader.Core.Models
{
    public class PositionSizingInfo
    {
        // Input parameters
        public decimal AccountBalance { get; set; }
        public decimal Leverage { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        
        // Risk calculations
        public decimal MaxPositionSize { get; set; }  // Maximum position size in base currency
        public decimal MaxLotSize { get; set; }       // Maximum position size in lots
        
        // Target profit calculations
        public Dictionary<decimal, PositionSizingTarget> ProfitTargets { get; set; } = new Dictionary<decimal, PositionSizingTarget>();
    }

    public class PositionSizingTarget
    {
        public decimal TargetProfit { get; set; }     // Target profit in account currency (GBP)
        public decimal RequiredPositionSize { get; set; } // Required position size to achieve target
        public decimal RequiredLotSize { get; set; }   // Required lot size to achieve target
        public decimal PriceMovementRequired { get; set; } // Price movement needed to achieve target
        public decimal PriceMovementPercent { get; set; } // Price movement as percentage
        public decimal RiskAmount { get; set; }       // Amount at risk in account currency
        public decimal RiskPercentage { get; set; }   // Risk as percentage of account
    }
} 