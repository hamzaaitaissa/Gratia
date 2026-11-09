import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { GiveDrawer } from './GiveDrawer';

interface GiveContextValue {
  /** Opens the drawer, optionally with a colleague already chosen (e.g. "Recognize" on a profile). */
  openGive: (receiverId?: string | null) => void;
  /** The gift just sent, outlined in the feed for a few seconds. */
  highlightId: string | null;
}

const GiveContext = createContext<GiveContextValue | null>(null);

const HIGHLIGHT_MS = 6000;

export function GiveProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [drawer, setDrawer] = useState<{ open: boolean; receiverId: string | null; key: number }>({ open: false, receiverId: null, key: 0 });
  const [highlightId, setHighlightId] = useState<string | null>(null);
  const timer = useRef<number | undefined>(undefined);

  // Every open starts a fresh form (a new key remounts the drawer)
  const openGive = useCallback((receiverId?: string | null) => {
    window.clearTimeout(timer.current);
    setDrawer((d) => ({ open: true, receiverId: receiverId ?? null, key: d.key + 1 }));
  }, []);

  const close = useCallback(() => {
    setDrawer((d) => ({ ...d, open: false }));
    // Let the highlight be seen, then fade it
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setHighlightId(null), HIGHLIGHT_MS);
  }, []);

  const seeFeed = useCallback(() => {
    close();
    navigate('/');
    document.getElementById('main')?.scrollTo({ top: 0 });
  }, [close, navigate]);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const value = useMemo(() => ({ openGive, highlightId }), [openGive, highlightId]);

  return (
    <GiveContext.Provider value={value}>
      {children}
      {drawer.open && (
        <GiveDrawer
          key={drawer.key}
          initialReceiverId={drawer.receiverId}
          onClose={close}
          onSent={(tx) => setHighlightId(tx.id)}
          onGiveAgain={() => openGive(null)}
          onSeeFeed={seeFeed}
        />
      )}
    </GiveContext.Provider>
  );
}

export function useGive(): GiveContextValue {
  const v = useContext(GiveContext);
  if (!v) throw new Error('useGive must be used inside GiveProvider');
  return v;
}
