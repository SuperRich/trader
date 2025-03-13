'use client';

import React, { useState } from 'react';
import SearchBar from '@/components/search-bar';
import AuthModal from '@/components/auth-modal';
import Logo from '@/components/ui/logo';
import Link from 'next/link';

export default function Home() {
  const [authModalOpen, setAuthModalOpen] = useState(false);

  return (
    <>
      <main className="flex-1 flex flex-col">
        <div className="flex justify-center py-6">
          <Link href="/" className="flex items-center gap-2">
            <Logo className="text-[#67bdc0]" />
            <span className="text-white text-xl font-medium">Forex Trader</span>
          </Link>
        </div>

        <div className="flex-1 flex flex-col items-center justify-center px-4 md:px-8 py-10 max-w-7xl mx-auto w-full">
          <div className="w-full flex flex-col items-center justify-center">
            <SearchBar />
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
