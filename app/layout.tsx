import type { Metadata, Viewport } from 'next';
import { PwaRegistration } from '@/components/pwa-registration';
import './globals.css';

const appBasePath = process.env.GITHUB_PAGES === 'true' ? '/crashout' : '';

export const metadata: Metadata = {
  title: 'Crashout — Tabletop Crash Game',
  description:
    'Thread the gap, hit the junction, and turn one impact into a spectacular chain reaction.',
  applicationName: 'Crashout',
  appleWebApp: {
    capable: true,
    statusBarStyle: 'black-translucent',
    title: 'Crashout',
  },
  icons: {
    icon: `${appBasePath}/favicon.svg`,
    apple: `${appBasePath}/favicon.svg`,
  },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: '#07090d',
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <head>
        <link rel="manifest" href={`${appBasePath}/manifest.webmanifest`} />
      </head>
      <body>
        {children}
        <PwaRegistration />
      </body>
    </html>
  );
}
