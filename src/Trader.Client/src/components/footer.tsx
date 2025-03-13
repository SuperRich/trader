import React from 'react';
import Link from 'next/link';
import { ChevronDown } from 'lucide-react';

interface FooterLinkProps {
  href: string;
  label: string;
}

const FooterLink = ({ href, label }: FooterLinkProps) => {
  return (
    <Link
      href={href}
      className="text-zinc-400 hover:text-white text-sm transition-colors"
    >
      {label}
    </Link>
  );
};

const Footer = () => {
  return (
    <footer className="flex items-center justify-between w-full py-4 px-8 border-t border-zinc-800">
      <div className="flex items-center gap-4">
        <FooterLink href="/pro" label="Pro" />
        <FooterLink href="/enterprise" label="Enterprise" />
        <FooterLink href="/api" label="API" />
        <FooterLink href="/blog" label="Blog" />
        <FooterLink href="/careers" label="Careers" />
        <FooterLink href="/store" label="Store" />
        <FooterLink href="/finance" label="Finance" />
      </div>

      <div className="flex items-center gap-2">
        <span className="text-zinc-400 text-sm">English</span>
        <ChevronDown size={14} className="text-zinc-400" />
      </div>
    </footer>
  );
};

export default Footer;
