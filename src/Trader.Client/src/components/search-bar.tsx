import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { TrendingUp, ArrowUp, ArrowDown, Loader2 } from 'lucide-react';
import { Input } from '@/components/ui/input';
import StockAnalysis from './stock-analysis';
import NewsFeed from './news-feed';
import { Card } from '@/components/ui/card';
import { tradingApi, ApiError, TradingAnalysis } from '@/services/api';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

const SearchBar = () => {
  const [pairInput, setPairInput] = useState('');
  const [showPairInput, setShowPairInput] = useState(false);
  const [analysisPair, setAnalysisPair] = useState<string | null>(null);
  const [showTopMovers, setShowTopMovers] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<TradingAnalysis | null>(null);
  const [selectedProvider, setSelectedProvider] = useState('TwelveData');
  const [marketMoversCount, setMarketMoversCount] = useState(1);
  const [marketMovers, setMarketMovers] = useState<any[]>([]);
  const [isLoadingMovers, setIsLoadingMovers] = useState(false);

  // Available providers
  const providers = ['TwelveData', 'Polygon', 'Mock', 'TraderMade'];

  const handleGetTopMovers = async () => {
    setShowTopMovers(true);
    setIsLoadingMovers(true);
    setError(null);

    try {
      const data = await tradingApi.getMarketMovers(selectedProvider, marketMoversCount);
      setMarketMovers(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('An unexpected error occurred while fetching market movers');
      }
      console.error('Market movers error:', err);
    } finally {
      setIsLoadingMovers(false);
    }
  };

  const handleAnalyzeClick = () => {
    setShowPairInput(true);
    setError(null);
  };

  const handleAnalyzeSubmit = async () => {
    if (!pairInput.trim()) return;
    
    setIsLoading(true);
    setError(null);
    
    try {
      // Use the provider-specific endpoint
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
            <div className="w-full">
              <Card className="w-full bg-zinc-900 border-zinc-800 p-4">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-lg font-medium text-white">Today's Top Movers</h3>
                  <div className="flex items-center gap-2">
                    <Select value={selectedProvider} onValueChange={setSelectedProvider}>
                      <SelectTrigger className="w-[140px] bg-zinc-800 border-zinc-700">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {providers.map((provider) => (
                          <SelectItem key={provider} value={provider}>
                            {provider}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Select
                      value={marketMoversCount.toString()}
                      onValueChange={(value) => setMarketMoversCount(parseInt(value))}
                    >
                      <SelectTrigger className="w-[80px] bg-zinc-800 border-zinc-700">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {[1, 3, 5, 10].map((count) => (
                          <SelectItem key={count} value={count.toString()}>
                            {count}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      onClick={handleGetTopMovers}
                      className="bg-teal-600 hover:bg-teal-700 text-white"
                      disabled={isLoadingMovers}
                    >
                      {isLoadingMovers ? (
                        <>
                          <Loader2 size={18} className="animate-spin mr-2" />
                          Loading...
                        </>
                      ) : (
                        'Refresh'
                      )}
                    </Button>
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-3">
                    {isLoadingMovers ? (
                      <div className="flex justify-center py-8">
                        <Loader2 size={24} className="animate-spin text-teal-500" />
                      </div>
                    ) : marketMovers.length > 0 ? (
                      marketMovers.map((mover, index) => (
                        <div
                          key={index}
                          className="bg-zinc-800 p-3 rounded-lg flex justify-between items-center cursor-pointer hover:bg-zinc-700 transition-colors"
                          onClick={() => handleSelectPair(mover.symbol)}
                        >
                          <div>
                            <div className="font-medium text-white">{mover.symbol}</div>
                            <div className="text-sm text-zinc-400">{mover.price}</div>
                          </div>
                          <div className={`flex items-center ${mover.change >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                            {mover.change >= 0 ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
                            <span className="font-medium ml-1">{mover.change >= 0 ? '+' : ''}{mover.change}%</span>
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="text-center py-4 text-zinc-400">
                        No market movers data available
                      </div>
                    )}
                  </div>
                  <div>
                    <NewsFeed marketMovers={marketMovers} isLoading={isLoadingMovers} />
                  </div>
                </div>
                {error && (
                  <Alert variant="destructive" className="mt-4">
                    <AlertDescription>{error}</AlertDescription>
                  </Alert>
                )}
              </Card>
            </div>
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
              <div className="flex flex-col gap-2">
                <div className="flex gap-2">
                  <Input
                    value={pairInput}
                    onChange={(e) => setPairInput(e.target.value)}
                    onKeyDown={handleKeyDown}
                    placeholder="Enter a pair (e.g., EUR/USD, BTC/USD)"
                    className="bg-zinc-800 border-zinc-700 text-white placeholder:text-zinc-400"
                    disabled={isLoading}
                  />
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
                <Select value={selectedProvider} onValueChange={setSelectedProvider}>
                  <SelectTrigger className="bg-zinc-800 border-zinc-700 text-white">
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent className="bg-zinc-800 border-zinc-700">
                    {providers.map((provider) => (
                      <SelectItem key={provider} value={provider} className="text-white hover:bg-zinc-700">
                        {provider}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
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
