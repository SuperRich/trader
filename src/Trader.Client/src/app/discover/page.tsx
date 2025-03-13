'use client';

import React, { useState } from 'react';
import AuthModal from '@/components/auth-modal';
import { Card } from '@/components/ui/card';
import Link from 'next/link';
import Logo from '@/components/ui/logo';

interface DiscoverCardProps {
  title: string;
  category: string;
  description: string;
  image?: string;
  link: string;
}

const DiscoverCard = ({ title, category, description, image, link }: DiscoverCardProps) => {
  return (
    <Link href={link}>
      <Card className="bg-zinc-900 border-zinc-800 hover:bg-zinc-800 transition-colors p-5 cursor-pointer h-full">
        <div className="flex flex-col h-full">
          <div className="mb-2">
            <span className="text-xs font-medium text-teal-500">{category}</span>
          </div>
          <h3 className="text-lg font-medium text-white mb-2">{title}</h3>
          <p className="text-sm text-zinc-400 mb-4">{description}</p>
          {image && (
            <div className="mt-auto">
              <div
                className="w-full h-32 rounded-md bg-cover bg-center"
                style={{ backgroundImage: `url(${image})` }}
              />
            </div>
          )}
        </div>
      </Card>
    </Link>
  );
};

export default function Discover() {
  const [authModalOpen, setAuthModalOpen] = useState(false);

  const discoverItems: DiscoverCardProps[] = [
    {
      title: "The Global Economy in 2025",
      category: "Economy",
      description: "Insights on inflation trends, interest rates, and growth forecasts for the upcoming year.",
      link: "/search/the-global-economy",
    },
    {
      title: "Latest AI Breakthroughs",
      category: "Technology",
      description: "Explore recent advancements in artificial intelligence and machine learning.",
      link: "/search/ai-breakthroughs",
    },
    {
      title: "Climate Change Solutions",
      category: "Environment",
      description: "Innovative approaches to tackle climate change and build sustainable futures.",
      link: "/search/climate-solutions",
    },
    {
      title: "Space Exploration Milestones",
      category: "Science",
      description: "Recent achievements in space exploration and upcoming missions to watch.",
      link: "/search/space-exploration",
    },
    {
      title: "Health & Wellness Trends",
      category: "Health",
      description: "Emerging health practices and wellness approaches gaining popularity.",
      link: "/search/health-trends",
    },
    {
      title: "Geopolitical Developments",
      category: "Politics",
      description: "Analysis of shifting global political landscapes and international relations.",
      link: "/search/geopolitics",
    }
  ];

  return (
    <>
      <main className="flex-1 flex flex-col">
        <div className="flex justify-center py-6">
          <Link href="/" className="flex items-center gap-2">
            <Logo className="text-[#67bdc0]" />
            <span className="text-white text-xl font-medium">Forex Trader</span>
          </Link>
        </div>

        <div className="flex-1 px-4 md:px-8 py-8 max-w-7xl mx-auto w-full">
          <h1 className="text-3xl font-medium text-white mb-8">
            Discover
          </h1>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {discoverItems.map((item, index) => (
              <DiscoverCard
                key={index}
                title={item.title}
                category={item.category}
                description={item.description}
                image={item.image}
                link={item.link}
              />
            ))}
          </div>
        </div>
      </main>

      <AuthModal
        open={authModalOpen}
        onOpenChange={setAuthModalOpen}
      />
    </>
  );
}
