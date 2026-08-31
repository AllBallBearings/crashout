'use client';

import { useEffect } from 'react';

export function PwaRegistration() {
  useEffect(() => {
    if (!('serviceWorker' in navigator)) return;
    const register = () => {
      const appBasePath = window.location.pathname.startsWith('/crashout/') ? '/crashout' : '';
      navigator.serviceWorker.register(`${appBasePath}/sw.js`, { scope: `${appBasePath || ''}/` }).catch(() => {
        // The game remains playable online if registration is unavailable.
      });
    };
    window.addEventListener('load', register, { once: true });
    return () => window.removeEventListener('load', register);
  }, []);

  return null;
}
