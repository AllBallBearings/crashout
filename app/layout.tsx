import type { Metadata, Viewport } from 'next';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL('https://crashout-junction.thegools.chatgpt.site'),
  title: 'Crashout — One Driver. Total Chaos.',
  description:
    'Crashout is a shared tabletop crash game for iPhone and iPad. Drive into live traffic and build the biggest chain reaction.',
  applicationName: 'Crashout',
  openGraph: {
    title: 'Crashout — One Driver. Total Chaos.',
    description: 'A shared tabletop crash game for iPhone and iPad.',
    type: 'website',
    images: [{ url: '/og.png', width: 1200, height: 630, alt: 'Crashout tabletop crash game' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'Crashout — One Driver. Total Chaos.',
    description: 'A shared tabletop crash game for iPhone and iPad.',
    images: ['/og.png'],
  },
  icons: { icon: '/favicon.svg' },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  themeColor: '#f3efe4',
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
