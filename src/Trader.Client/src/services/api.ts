import { config } from '@/config';

export interface TradingAnalysis {
  currencyPair: string;
  sentiment: string;
  confidence: number;
  factors: string[];
  summary: string;
  sources: string[];
  timestamp: string;
  modelUsed: string;
  currentPrice: number;
  tradeRecommendation: string;
  stopLossPrice: number;
  takeProfitPrice: number;
  bestEntryPrice: number;
  orderType: string;
  riskLevel: string;
  riskRewardRatio: number;
  isTradeRecommended: boolean;
  marketSession: {
    currentSession: string;
    description: string;
    liquidityLevel: number;
    recommendedSession: string;
    recommendationReason: string;
    timeUntilNextSession: string;
    nextSession: string;
    currentTimeUtc: string;
    nextSessionStartTimeUtc: string;
  };
  sessionWarning: string | null;
  timeToBestEntry: string;
  validUntil: string;
  isSafeToEnterAtCurrentPrice: boolean;
  currentEntryReason: string;
  positionSizing: {
    lotSize: number;
    riskAmount: number;
    potentialProfit: number;
  } | null;
  modelReasoning: string;
  inOutPlay: {
    available: boolean;
    direction: string;
    entryPrice: number;
    stopLoss: number;
    takeProfit: number;
    timeframe: string;
    reason: string;
  };
}

export class ApiError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

// Custom fetch that ignores SSL certificate errors in development
const devFetch = async (url: string, options: RequestInit = {}) => {
  // In development, use node-fetch with rejectUnauthorized: false
  if (process.env.NODE_ENV === 'development') {
    console.log('Making API request to:', url);
    const response = await fetch(url, {
      ...options,
      headers: {
        ...options.headers,
        'Accept': 'application/json',
      },
    });
    
    // Log the response status
    console.log('API response status:', response.status);
    
    // If response is not ok, try to get the error message
    if (!response.ok) {
      const errorText = await response.text();
      console.error('API error response:', errorText);
    }
    
    return response;
  }
  
  // In production, use regular fetch
  return fetch(url, options);
};

export const tradingApi = {
  analyzePair: async (pair: string, provider: string = 'TwelveData'): Promise<TradingAnalysis> => {
    try {
      // Format the pair by removing slashes and underscores and converting to uppercase
      const formattedPair = pair.replace(/[/_]/g, '').toUpperCase();
      
      // Log the request details
      console.log('Analyzing pair:', {
        pair: formattedPair,
        provider,
        url: `${config.apiBaseUrl}/api/trading/analyze/${formattedPair}/${provider}`
      });

      const response = await fetch(
        `${config.apiBaseUrl}/api/trading/analyze/${formattedPair}/${provider}`,
        {
          method: 'GET',
          headers: {
            'Accept': 'application/json'
          }
        }
      );

      // Log the response status
      console.log('Response status:', response.status);

      if (!response.ok) {
        const errorText = await response.text();
        console.error('API Error:', {
          status: response.status,
          statusText: response.statusText,
          error: errorText
        });
        throw new ApiError(`Failed to analyze pair: ${errorText}`);
      }

      const data = await response.json();
      
      // Log the successful response data
      console.log('Analysis response:', data);
      
      return data;
    } catch (error) {
      console.error('Error in analyzePair:', error);
      if (error instanceof ApiError) {
        throw error;
      }
      throw new ApiError('Failed to connect to the analysis service');
    }
  },

  getMarketMovers: async (provider: string = 'TwelveData', count: number = 1): Promise<any> => {
    try {
      const response = await fetch(
        `${config.apiBaseUrl}/api/market-movers/forex/ema-filtered?provider=${provider}&Count=${count}`,
        {
          method: 'GET',
          headers: {
            'Accept': 'application/json'
          }
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Market Movers API Error:', {
          status: response.status,
          statusText: response.statusText,
          error: errorText
        });
        throw new ApiError(`Failed to get market movers: ${errorText}`);
      }

      const data = await response.json();
      console.log('Market movers response:', data);
      return data;
    } catch (error) {
      console.error('Error in getMarketMovers:', error);
      if (error instanceof ApiError) {
        throw error;
      }
      throw new ApiError('Failed to connect to the market movers service');
    }
  }
}; 