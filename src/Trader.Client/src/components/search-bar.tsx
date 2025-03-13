import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { TrendingUp, ArrowUp, ArrowDown, Loader2 } from 'lucide-react';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import StockAnalysis from './stock-analysis';
import { Card } from '@/components/ui/card';
import { tradingApi, ApiError, TradingAnalysis } from '@/services/api';
import { Alert, AlertDescription } from '@/components/ui/alert';

const SearchBar = () => {
  const [pairInput, setPairInput] = useState('');
  const [showPairInput, setShowPairInput] = useState(false);
  const [analysisPair, setAnalysisPair] = useState<string | null>(null);
  const [showTopMovers, setShowTopMovers] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<TradingAnalysis | null>(null);
  const [selectedProvider, setSelectedProvider] = useState('TwelveData');

  // Available data providers
  const providers = [
    { value: 'TwelveData', label: 'TwelveData' },
    { value: 'TraderMade', label: 'TraderMade' },
    { value: 'Polygon', label: 'Polygon' },
  ];

  // Mock data for top movers
  const topMovers = [
    { pair: 'EUR/USD', change: +0.72, price: '1.1234' },
    { pair: 'BTC/USD', change: +3.25, price: '52,345.67' },
    { pair: 'GBP/JPY', change: -0.44, price: '165.432' },
    { pair: 'ETH/USD', change: +2.18, price: '3,654.32' },
    { pair: 'USD/CAD', change: -0.65, price: '1.2765' },
    { pair: 'XRP/USD', change: +5.43, price: '0.7623' },
  ];

  const handleAnalyzeClick = () => {
    setShowPairInput(true);
    setError(null);
  };

  const handleAnalyzeSubmit = async () => {
    if (!pairInput.trim()) return;
    
    setIsLoading(true);
    setError(null);
    
    try {
      const data = await tradingApi.analyzePair(pairInput.trim(), selectedProvider);
      setAnalysisPair(pairInput);
      setAnalysis(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('An unexpected error occurred while analyzing the pair');
      }
      console.error('Analysis error:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleAnalyzeSubmit();
    }
  };

  const handleGetTopMovers = () => {
    setShowTopMovers(true);
  };

  const handleSelectPair = (pair: string) => {
    setPairInput(pair);
    setAnalysisPair(pair);
    setShowPairInput(true);
  };

  return (
    <div className="w-full max-w-2xl mx-auto">
      {/* Step 1: Get Top Movers */}
      <div className="mb-8">
        <div className="flex flex-col items-center">
          <h2 className="text-xl font-medium text-white mb-3">Step 1: Get Top Movers</h2>
          {!showTopMovers ? (
            <Button
              onClick={handleGetTopMovers}
              className="bg-teal-600 hover:bg-teal-700 text-white flex items-center gap-2"
              size="lg"
            >
              <TrendingUp size={18} />
              Get Top Movers
            </Button>
          ) : (
            <Card className="w-full bg-zinc-900 border-zinc-800 p-4">
              <h3 className="text-lg font-medium text-white mb-3 text-center">Today's Top Movers</h3>
              <div className="grid grid-cols-2 gap-3">
                {topMovers.map((mover, index) => (
                  <div
                    key={index}
                    className="bg-zinc-800 p-3 rounded-lg flex justify-between items-center cursor-pointer hover:bg-zinc-700 transition-colors"
                    onClick={() => handleSelectPair(mover.pair)}
                  >
                    <div>
                      <div className="font-medium text-white">{mover.pair}</div>
                      <div className="text-sm text-zinc-400">{mover.price}</div>
                    </div>
                    <div className={`flex items-center ${mover.change >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                      {mover.change >= 0 ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
                      <span className="font-medium ml-1">{mover.change >= 0 ? '+' : ''}{mover.change}%</span>
                    </div>
                  </div>
                ))}
              </div>
              <p className="text-xs text-zinc-400 mt-4 text-center">
                Click on any pair to analyze
              </p>
            </Card>
          )}
        </div>
      </div>

      {/* Step 2: Analyze Forex/Crypto */}
      <div className="mb-8">
        <div className="flex flex-col items-center">
          <h2 className="text-xl font-medium text-white mb-3">Step 2: Get AI-Powered Trading Advice</h2>
          {!showPairInput ? (
            <Button
              onClick={handleAnalyzeClick}
              className="bg-teal-600 hover:bg-teal-700 text-white flex items-center gap-2"
              size="lg"
            >
              <TrendingUp size={18} />
              Analyze Forex/Crypto
            </Button>
          ) : (
            <div className="w-full max-w-md">
              <div className="flex gap-2">
                <Input
                  value={pairInput}
                  onChange={(e) => setPairInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  placeholder="Enter a pair (e.g., EUR/USD, BTC/USD)"
                  className="bg-zinc-800 border-zinc-700 text-white placeholder:text-zinc-400"
                  disabled={isLoading}
                />
                <Select
                  value={selectedProvider}
                  onValueChange={setSelectedProvider}
                >
                  <SelectTrigger className="w-[140px] bg-zinc-800 border-zinc-700 text-white">
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent className="bg-zinc-800 border-zinc-700">
                    {providers.map((provider) => (
                      <SelectItem
                        key={provider.value}
                        value={provider.value}
                        className="text-white hover:bg-zinc-700 focus:bg-zinc-700"
                      >
                        {provider.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  onClick={handleAnalyzeSubmit}
                  className="bg-teal-600 hover:bg-teal-700 text-white"
                  disabled={isLoading}
                >
                  {isLoading ? (
                    <>
                      <Loader2 size={18} className="animate-spin mr-2" />
                      Analyzing...
                    </>
                  ) : (
                    'Analyze'
                  )}
                </Button>
              </div>
              <p className="text-xs text-zinc-400 mt-2 text-center">
                Examples: EUR/USD, GBP/JPY, BTC/USD, ETH/USD
              </p>
            </div>
          )}

          {error && (
            <Alert variant="destructive" className="mt-4">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>
      </div>

      {/* Show analysis if a pair is selected */}
      {analysisPair && analysis && (
        <StockAnalysis pair={analysisPair} analysis={analysis} />
      )}
    </div>
  );
};

export default SearchBar;
