import React from 'react';
import { ArrowUp, ArrowDown } from 'lucide-react';
import { Card } from '@/components/ui/card';
import Link from 'next/link';

interface NewsItem {
  title: string;
  description: string;
  url: string;
  publishedAt: string;
  source: string;
}

interface MarketMover {
  symbol: string;
  price: number | string;
  change: number;
  relatedNews?: NewsItem[];
}

interface NewsFeedProps {
  marketMovers?: MarketMover[];
  isLoading?: boolean;
}

const NewsCard = ({ item }: { item: NewsItem }) => {
  return (
    <a href={item.url} target="_blank" rel="noopener noreferrer">
      <Card className="bg-zinc-900 border-zinc-800 hover:bg-zinc-800 transition-colors p-3 cursor-pointer">
        <h3 className="text-sm text-white font-medium mb-1">{item.title}</h3>
        <p className="text-xs text-zinc-400 mb-2">{item.description}</p>
        <div className="flex justify-between text-xs text-zinc-500">
          <span>{item.source}</span>
          <span>{new Date(item.publishedAt).toLocaleDateString()}</span>
        </div>
      </Card>
    </a>
  );
};

const MarketMoverCard = ({ item }: { item: MarketMover }) => {
  const isPositive = item.change >= 0;
  
  // Safely format the price
  const formatPrice = (price: number | string, symbol: string) => {
    const numericPrice = typeof price === 'string' ? parseFloat(price) : price;
    if (isNaN(numericPrice)) return price; // Return original if parsing fails
    return numericPrice.toFixed(symbol.includes('BTC') ? 2 : 4);
  };

  return (
    <Card className="bg-zinc-900 border-zinc-800 hover:bg-zinc-800 transition-colors p-3 cursor-pointer">
      <div className="flex justify-between items-center">
        <div>
          <h3 className="text-sm font-bold text-white">{item.symbol}</h3>
          <p className="text-xs text-zinc-400">{formatPrice(item.price, item.symbol)}</p>
        </div>
        <div className={`flex items-center ${isPositive ? 'text-green-500' : 'text-red-500'}`}>
          {isPositive ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
          <span className="text-xs font-medium">{isPositive ? '+' : ''}{item.change}%</span>
        </div>
      </div>
    </Card>
  );
};

const NewsFeed = ({ marketMovers = [], isLoading = false }: NewsFeedProps) => {
  // Collect all news items from market movers
  const allNews = marketMovers
    .flatMap(mover => mover.relatedNews || [])
    .sort((a, b) => new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime());

  if (isLoading) {
    return (
      <div className="flex flex-col gap-4 w-full max-w-md">
        <div className="animate-pulse space-y-2">
          {[1, 2, 3].map((i) => (
            <Card key={i} className="bg-zinc-900 border-zinc-800 p-3">
              <div className="h-4 bg-zinc-800 rounded w-3/4 mb-2"></div>
              <div className="h-3 bg-zinc-800 rounded w-1/2"></div>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  if (!marketMovers.length) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4 w-full max-w-md">
      <div className="space-y-2">
        {allNews.slice(0, 5).map((item, index) => (
          <NewsCard key={index} item={item} />
        ))}
      </div>

      <div className="space-y-2">
        {marketMovers.map((item) => (
          <MarketMoverCard key={item.symbol} item={item} />
        ))}
      </div>
    </div>
  );
};

export default NewsFeed;
