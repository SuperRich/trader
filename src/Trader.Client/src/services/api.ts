import { config } from '../config';

export interface TradingAnalysis {
  sentiment: string;
  recommendation: string;
  entryPrice: number;
  stopLoss: number;
  takeProfit: number;
  riskRewardRatio: number;
  factors: string[];
  rationale: string;
  marketSession?: {
    currentSession: string;
    description: string;
    liquidityLevel: number;
    recommendedSession: string;
    recommendationReason: string;
    timeUntilNextSession: string;
    nextSession: string;
  };
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

export const tradingApi = {
  async analyzePair(pair: string): Promise<TradingAnalysis> {
    const response = await fetch(`${config.apiBaseUrl}/api/trading/analyze/${pair}`);
    
    if (!response.ok) {
      throw new ApiError(
        response.status,
        `Analysis failed: ${response.statusText}`
      );
    }
    
    return response.json();
  },
}; 