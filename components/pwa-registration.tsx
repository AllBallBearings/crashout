'use client';

import { Download, X } from 'lucide-react';
import { useEffect, useState, useSyncExternalStore } from 'react';

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform?: string }>;
};

function isStandaloneDisplayMode() {
  const standaloneNavigator = window.navigator as Navigator & { standalone?: boolean };
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    standaloneNavigator.standalone === true
  );
}

function subscribeToStandaloneMode(onChange: () => void) {
  const mediaQuery = window.matchMedia('(display-mode: standalone)');
  mediaQuery.addEventListener('change', onChange);
  window.addEventListener('appinstalled', onChange);

  return () => {
    mediaQuery.removeEventListener('change', onChange);
    window.removeEventListener('appinstalled', onChange);
  };
}

export function PwaRegistration() {
  const [installPrompt, setInstallPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [showGuide, setShowGuide] = useState(false);
  const installed = useSyncExternalStore(subscribeToStandaloneMode, isStandaloneDisplayMode, () => false);

  useEffect(() => {
    const onBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      setInstallPrompt(event as BeforeInstallPromptEvent);
    };

    const onAppInstalled = () => {
      setInstallPrompt(null);
      setShowGuide(false);
    };

    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt);
    window.addEventListener('appinstalled', onAppInstalled);

    const register = () => {
      if (!('serviceWorker' in navigator)) return;
      const appBasePath = window.location.pathname.startsWith('/crashout/') ? '/crashout' : '';
      navigator.serviceWorker.register(`${appBasePath}/sw.js`, { scope: `${appBasePath || ''}/` }).catch(() => {
        // The game remains playable online if registration is unavailable.
      });
    };

    if (document.readyState === 'complete') {
      register();
    } else {
      window.addEventListener('load', register, { once: true });
    }

    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt);
      window.removeEventListener('appinstalled', onAppInstalled);
      window.removeEventListener('load', register);
    };
  }, []);

  const handleInstall = async () => {
    if (installPrompt) {
      try {
        await installPrompt.prompt();
        const { outcome } = await installPrompt.userChoice;
        if (outcome === 'accepted') setShowGuide(false);
      } catch {
        // Some browsers expose the event but reject the prompt after navigation.
        setShowGuide(true);
      } finally {
        setInstallPrompt(null);
      }
      return;
    }

    setShowGuide(true);
  };

  if (installed) return null;

  return (
    <div className="pointer-events-none fixed inset-x-0 bottom-4 z-40 flex justify-center px-4 sm:justify-end sm:px-6">
      <div className="pointer-events-auto flex w-full max-w-[360px] flex-col items-stretch gap-2">
        {showGuide && (
          <dialog
            open
            aria-label="Install Crashout"
            className="hud-glass m-0 rounded-2xl border border-white/10 p-4 text-left text-white shadow-[0_20px_70px_rgb(0_0_0/55%)]"
          >
            <div className="flex items-start gap-3">
              <div className="min-w-0 flex-1">
                <p className="font-mono text-[11px] font-bold uppercase tracking-[0.16em] text-[#ff9a3e]">
                  Install Crashout
                </p>
                <p className="mt-2 text-xs leading-relaxed text-white/75">
                  On iPhone or iPad, use Safari&apos;s Share button, then choose{' '}
                  <span className="font-semibold text-white">Add to Home Screen</span>.
                </p>
                <p className="mt-2 text-xs leading-relaxed text-white/75">
                  On Android, open the browser menu and choose{' '}
                  <span className="font-semibold text-white">Install app</span> or{' '}
                  <span className="font-semibold text-white">Add to Home screen</span>.
                </p>
                <p className="mt-3 border-t border-white/10 pt-3 text-[10px] leading-relaxed text-white/45">
                  AR mode is not part of this crash-only milestone.
                </p>
              </div>
              <button
                type="button"
                aria-label="Close install instructions"
                onClick={() => setShowGuide(false)}
                className="grid size-7 shrink-0 place-items-center rounded-lg text-white/45 transition hover:bg-white/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ff9a3e]/70"
              >
                <X className="size-4" />
              </button>
            </div>
          </dialog>
        )}
        <button
          type="button"
          onClick={handleInstall}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-[#ff9a3e]/60 bg-[#ff9a3e] px-4 font-mono text-[11px] font-bold uppercase tracking-[0.14em] text-[#1a1007] shadow-[0_12px_40px_rgb(0_0_0/38%)] transition hover:bg-[#ffb46b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#ffcf9f] active:translate-y-px"
        >
          <Download className="size-4" />
          Install app
        </button>
      </div>
    </div>
  );
}
