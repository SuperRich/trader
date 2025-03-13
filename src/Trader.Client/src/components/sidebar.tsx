import React from 'react';
import Link from 'next/link';
import { Home, Compass, Layers, BookOpen } from 'lucide-react';
import { Button } from '@/components/ui/button';
import Logo from '@/components/ui/logo';

interface SidebarLinkProps {
  href: string;
  icon: React.ReactNode;
  label: string;
  active?: boolean;
}

const SidebarLink = ({ href, icon, label, active = false }: SidebarLinkProps) => {
  return (
    <Link href={href} className="flex items-center w-full">
      <div className={`flex items-center gap-3 py-2 px-4 rounded-md w-full ${active ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:bg-zinc-800 hover:text-white'}`}>
        {icon}
        <span className="text-sm font-medium">{label}</span>
      </div>
    </Link>
  );
};

const Sidebar = () => {
  return (
    <div className="flex flex-col h-full w-56 bg-zinc-900 border-r border-zinc-800">
      <div className="p-4">
        <Link href="/" className="flex items-center gap-2 mb-4">
          <Logo className="text-[#67bdc0]" />
          <span className="text-white text-xl font-medium">perplexity</span>
        </Link>

        <Button
          variant="outline"
          className="w-full mb-4 border-zinc-700 bg-zinc-800 text-white hover:bg-zinc-700 justify-between"
        >
          New Thread
          <kbd className="text-xs bg-zinc-900 px-2 py-0.5 rounded">K</kbd>
        </Button>
      </div>

      <div className="flex flex-col gap-1 px-2">
        <SidebarLink
          href="/"
          icon={<Home size={18} />}
          label="Home"
          active={true}
        />
        <SidebarLink
          href="/discover"
          icon={<Compass size={18} />}
          label="Discover"
        />
        <SidebarLink
          href="/spaces"
          icon={<Layers size={18} />}
          label="Spaces"
        />
        <SidebarLink
          href="/library"
          icon={<BookOpen size={18} />}
          label="Library"
        />
      </div>

      <div className="mt-auto p-4 space-y-2">
        <Button variant="outline" className="w-full bg-teal-600 hover:bg-teal-700 border-none text-white">
          Sign Up
        </Button>
        <Button variant="ghost" className="w-full text-white hover:bg-zinc-800">
          Log in
        </Button>
      </div>
    </div>
  );
};

export default Sidebar;
