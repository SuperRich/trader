import React from 'react';
import { ArrowUp, ArrowDown } from 'lucide-react';
import { Card } from '@/components/ui/card';
import Link from 'next/link';

interface NewsItem {
  id: string;
  title: string;
  image?: string;
  link: string;
}

interface StockItem {
  symbol: string;
  name: string;
  price: string;
  change: number;
  link: string;
}

const NewsCard = ({ item }: { item: NewsItem }) => {
  return (
    <Link href={item.link}>
      <Card className="bg-zinc-900 border-zinc-800 hover:bg-zinc-800 transition-colors p-3 cursor-pointer">
        <h3 className="text-sm text-white font-medium">{item.title}</h3>
      </Card>
    </Link>
  );
};

const StockCard = ({ item }: { item: StockItem }) => {
  const isPositive = item.change >= 0;

  return (
    <Link href={item.link}>
      <Card className="bg-zinc-900 border-zinc-800 hover:bg-zinc-800 transition-colors p-3 cursor-pointer">
        <div className="flex justify-between items-center">
          <div>
            <h3 className="text-sm font-bold text-white">{item.symbol}</h3>
            <p className="text-xs text-zinc-400">{item.price}</p>
          </div>
          <div className={`flex items-center ${isPositive ? 'text-green-500' : 'text-red-500'}`}>
            {isPositive ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
            <span className="text-xs font-medium">{isPositive ? '+' : ''}{item.change}%</span>
          </div>
        </div>
      </Card>
    </Link>
  );
};

const NewsFeed = () => {
  const newsItems: NewsItem[] = [
    {
      id: '1',
      title: 'Ukraine Agrees to Ceasefire Proposal',
      link: '/page/ukraine-agrees-to-ceasefire-pr-11ptN0mxQTSm63WAfbSMnQ'
    },
    {
      id: '2',
      title: 'Europe Launches Retaliatory Tariffs',
      link: '/page/europe-launches-retaliatory-ta-KqTiwIC3RMWxo..8CZHMyw'
    }
  ];

  const stockItems: StockItem[] = [
    {
      symbol: 'Dow',
      name: 'Dow Jones',
      price: '41,350.93',
      change: -0.20,
      link: '/search/new?q=$^DJI&source=homepage_widget'
    },
    {
      symbol: 'META',
      name: 'Meta Platforms Inc',
      price: '619.56',
      change: 2.29,
      link: '/search/new?q=$META&source=homepage_widget'
    },
    {
      symbol: 'NVDA',
      name: 'NVIDIA Corporation',
      price: '115.74',
      change: 0.43,
      link: '/search/new?q=$NVDA&source=homepage_widget'
    }
  ];

  return (
    <div className="flex flex-col gap-4 w-full max-w-md">
      <div className="space-y-2">
        {newsItems.map((item) => (
          <NewsCard key={item.id} item={item} />
        ))}
      </div>

      <div className="space-y-2">
        {stockItems.map((item) => (
          <StockCard key={item.symbol} item={item} />
        ))}
      </div>
    </div>
  );
};

export default NewsFeed;
