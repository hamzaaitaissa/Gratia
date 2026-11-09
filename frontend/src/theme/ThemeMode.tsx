import { createContext, useCallback, useContext, useLayoutEffect, useMemo, useState, type ReactNode } from 'react';
import { applyTheme, buildTheme, GRATIA_PRIMARY, type Mode } from './theme';

const MODE_KEY = 'gratia.mode';
const BRAND_KEY = 'gratia.brand';

interface Brand {
  primary: string;
  secondary: string | null;
}

function initialMode(): Mode {
  try {
    const stored = localStorage.getItem(MODE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    // ignore blocked storage
  }
  return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

/** The last company brand seen on this device, so a reload doesn't flash the neutral colours first. */
export function cachedBrand(): Brand | null {
  try {
    const raw = localStorage.getItem(BRAND_KEY);
    return raw ? (JSON.parse(raw) as Brand) : null;
  } catch {
    return null;
  }
}

export function rememberBrand(brand: Brand | null) {
  try {
    if (brand) localStorage.setItem(BRAND_KEY, JSON.stringify(brand));
    else localStorage.removeItem(BRAND_KEY);
  } catch {
    // ignore blocked storage
  }
}

interface ThemeModeValue {
  mode: Mode;
  toggleMode: () => void;
}

const ThemeModeContext = createContext<ThemeModeValue | null>(null);

export function ThemeModeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<Mode>(initialMode);
  const toggleMode = useCallback(() => {
    setMode((m) => {
      const next = m === 'dark' ? 'light' : 'dark';
      try {
        localStorage.setItem(MODE_KEY, next);
      } catch {
        // ignore blocked storage
      }
      return next;
    });
  }, []);
  const value = useMemo(() => ({ mode, toggleMode }), [mode, toggleMode]);
  return <ThemeModeContext.Provider value={value}>{children}</ThemeModeContext.Provider>;
}

export function useThemeMode(): ThemeModeValue {
  const v = useContext(ThemeModeContext);
  if (!v) throw new Error('useThemeMode must be used inside ThemeModeProvider');
  return v;
}

/**
 * Applies a brand to the whole page. Public pages pass nothing and get Gratia's own neutral brand;
 * the signed-in app passes the company's colours.
 */
export function useBrandTheme(primary: string | null | undefined = GRATIA_PRIMARY, secondary: string | null | undefined = '') {
  const { mode } = useThemeMode();
  useLayoutEffect(() => {
    const { vars } = buildTheme(primary, secondary, mode);
    applyTheme(document.documentElement, vars, mode);
  }, [primary, secondary, mode]);
}
