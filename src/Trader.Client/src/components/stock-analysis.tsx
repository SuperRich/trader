import React from 'react';
import { Card } from '@/components/ui/card';
import { TrendingUp, TrendingDown, BarChart3, ArrowRight } from 'lucide-react';
import { TradingAnalysis } from '@/services/api';

interface StockAnalysisProps {
  pair: string;
  analysis: TradingAnalysis;
}

const StockAnalysis = ({ pair, analysis }: StockAnalysisProps) => {
  const isPositive = analysis.sentiment === 'Bullish';

  // Format metrics based on analysis
  const metrics = [
    { 
      name: 'Current Price', 
      value: analysis.currentPrice.toFixed(pair.includes('BTC') ? 2 : 4), 
      status: 'neutral' 
    },
    { 
      name: 'Best Entry', 
      value: analysis.bestEntryPrice.toFixed(pair.includes('BTC') ? 2 : 4), 
      status: 'neutral' 
    },
    { 
      name: 'Stop Loss', 
      value: analysis.stopLossPrice.toFixed(pair.includes('BTC') ? 2 : 4), 
      status: 'negative' 
    },
    { 
      name: 'Take Profit', 
      value: analysis.takeProfitPrice.toFixed(pair.includes('BTC') ? 2 : 4), 
      status: 'positive' 
    },
    { 
      name: 'Risk/Reward', 
      value: analysis.riskRewardRatio.toFixed(2), 
      status: analysis.riskRewardRatio >= 1.5 ? 'positive' : 'neutral' 
    },
  ];

  return (
    <div className="w-full mt-8 mx-auto max-w-2xl">
      <Card className="bg-zinc-900 border-zinc-800 p-5">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{pair}</h2>
            <div className="flex items-center mt-1">
              <span className="text-xl font-medium text-white">
                {pair.includes('/USD') ? '$' : ''}{analysis.currentPrice.toFixed(pair.includes('BTC') ? 2 : 4)}
              </span>
              <div className={`flex items-center ml-3 ${isPositive ? 'text-green-500' : 'text-red-500'}`}>
                {isPositive ? <TrendingUp size={18} /> : <TrendingDown size={18} />}
                <span className="ml-1">{analysis.sentiment}</span>
              </div>
            </div>
          </div>
          <div className="bg-zinc-800 p-2 rounded-full">
            <BarChart3 size={24} className="text-[#67bdc0]" />
          </div>
        </div>

        <div className="mb-6">
          <h3 className="text-lg font-semibold text-white mb-3">Trade Setup</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {metrics.map((metric, index) => (
              <div key={index} className="bg-zinc-800 p-3 rounded-lg">
                <div className="text-zinc-400 text-sm">{metric.name}</div>
                <div className={`text-lg font-medium ${
                  metric.status === 'positive' ? 'text-green-500' :
                  metric.status === 'negative' ? 'text-red-500' : 'text-white'
                }`}>
                  {metric.value}
                </div>
              </div>
            ))}
          </div>
        </div>

        {analysis.marketSession && (
          <div className="mb-6">
            <h3 className="text-lg font-semibold text-white mb-3">Market Session</h3>
            <div className="bg-zinc-800 p-4 rounded-lg">
              <div className="flex justify-between mb-2">
                <span className="text-zinc-400">Current Session:</span>
                <span className="text-white font-medium">{analysis.marketSession.currentSession}</span>
              </div>
              <div className="flex justify-between mb-2">
                <span className="text-zinc-400">Next Session:</span>
                <span className="text-white">{analysis.marketSession.nextSession} (in {analysis.marketSession.timeUntilNextSession})</span>
              </div>
              <div className="mt-3 text-sm text-zinc-300">
                {analysis.marketSession.description}
              </div>
            </div>
          </div>
        )}

        <div className="mb-6">
          <h3 className="text-lg font-semibold text-white mb-3">Analysis Factors</h3>
          <div className="space-y-2">
            {analysis.factors.map((factor, index) => (
              <div key={index} className="flex items-center text-sm">
                <ArrowRight size={12} className="text-[#67bdc0] mr-2 flex-shrink-0" />
                <span className="text-zinc-300">{factor}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-6 pt-4 border-t border-zinc-800">
          <h3 className="text-lg font-semibold text-white mb-2">Analysis</h3>
          <p className="text-zinc-300 text-sm leading-relaxed">
            {analysis.summary}
          </p>
        </div>

        {analysis.positionSizing && (
          <div className="mt-6 pt-4 border-t border-zinc-800">
            <h3 className="text-lg font-semibold text-white mb-2">Position Sizing</h3>
            <div className="grid grid-cols-3 gap-3">
              <div className="bg-zinc-800 p-3 rounded-lg">
                <div className="text-zinc-400 text-sm">Lot Size</div>
                <div className="text-lg font-medium text-white">
                  {analysis.positionSizing.lotSize.toFixed(2)}
                </div>
              </div>
              <div className="bg-zinc-800 p-3 rounded-lg">
                <div className="text-zinc-400 text-sm">Risk Amount</div>
                <div className="text-lg font-medium text-white">
                  ${analysis.positionSizing.riskAmount.toFixed(2)}
                </div>
              </div>
              <div className="bg-zinc-800 p-3 rounded-lg">
                <div className="text-zinc-400 text-sm">Potential Profit</div>
                <div className="text-lg font-medium text-green-500">
                  ${analysis.positionSizing.potentialProfit.toFixed(2)}
                </div>
              </div>
            </div>
          </div>
        )}

        {analysis.relatedNews && analysis.relatedNews.length > 0 && (
          <div className="mt-6 pt-4 border-t border-zinc-800">
            <h3 className="text-lg font-semibold text-white mb-3">Related News</h3>
            <div className="space-y-3">
              {analysis.relatedNews.map((news, index) => (
                <a
                  key={index}
                  href={news.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="block bg-zinc-800 p-3 rounded-lg hover:bg-zinc-700 transition-colors"
                >
                  <h4 className="text-white font-medium mb-1">{news.title}</h4>
                  <p className="text-zinc-400 text-sm mb-2">{news.description}</p>
                  <div className="flex justify-between text-xs text-zinc-500">
                    <span>{news.source}</span>
                    <span>{new Date(news.publishedAt).toLocaleDateString()}</span>
                  </div>
                </a>
              ))}
            </div>
          </div>
        )}

        <div className="mt-6 pt-4 border-t border-zinc-800 text-center">
          <div className="text-sm text-zinc-400">
            Analysis powered by {analysis.modelUsed || 'AI'} • Valid until {new Date(analysis.validUntil).toLocaleString()}
          </div>
        </div>
      </Card>
    </div>
  );
};

export default StockAnalysis;
