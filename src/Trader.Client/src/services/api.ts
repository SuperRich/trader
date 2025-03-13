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
  async analyzePair(pair: string, provider: string = 'TwelveData'): Promise<TradingAnalysis> {
    // Remove both slashes and underscores, and convert to uppercase
    const formattedPair = pair.replace(/[/_]/g, '').toUpperCase();
    const apiUrl = `${config.apiBaseUrl}/api/trading/analyze/${formattedPair}/${provider}`;
    
    console.log('Analyzing pair:', formattedPair, 'with provider:', provider);
    console.log('API URL:', apiUrl);
    
    try {
      const response = await fetch(apiUrl, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
      });
      
      console.log('Response status:', response.status);
      
      if (!response.ok) {
        let errorMessage = response.statusText;
        try {
          const errorText = await response.text();
          console.error('Error response:', errorText);
          errorMessage = errorText || errorMessage;
        } catch (e) {
          console.error('Failed to read error response:', e);
        }
        
        throw new ApiError(
          response.status,
          `Analysis failed: ${errorMessage}`
        );
      }
      
      const data = await response.json();
      console.log('Analysis response:', data);
      return data;
    } catch (error) {
      console.error('Analysis error details:', error);
      
      // Check if it's a network error
      if (error instanceof TypeError && error.message === 'Failed to fetch') {
        throw new ApiError(
          0,
          'Unable to connect to the API. Please ensure the backend server is running.'
        );
      }
      
      if (error instanceof ApiError) {
        throw error;
      }
      
      throw new ApiError(
        0,
        `Connection failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      );
    }
  },
}; 