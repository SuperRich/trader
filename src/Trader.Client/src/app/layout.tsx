import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Forex Trader - Forex & Crypto Analysis Platform",
  description: "Forex Trader is a platform for analyzing forex and cryptocurrency pairs with technical indicators and market news.",
  icons: {
    icon: [
      {
        url: "/favicon.svg",
        type: "image/svg+xml",
      }
    ],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="dark">
      <body className={`${inter.className} bg-zinc-950 text-white antialiased`}>
        <div className="min-h-screen w-full px-4">
          {children}
        </div>
      </body>
    </html>
  );
}
