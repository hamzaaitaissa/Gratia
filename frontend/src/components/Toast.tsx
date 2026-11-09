import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Icon } from './Icon';
import styles from './Toast.module.css';

type Tone = 'success' | 'error';

interface ToastMessage {
  id: number;
  message: string;
  tone: Tone;
}

interface ToastContextValue {
  show: (message: string, tone?: Tone) => void;
  toast: ToastMessage | null;
  dismiss: () => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

/** One short message at a time, e.g. "Invitation sent". It lives above the routes, so it survives navigation. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const seq = useRef(0);

  const dismiss = useCallback(() => {
    window.clearTimeout(timer.current);
    setToast(null);
  }, []);

  const show = useCallback((message: string, tone: Tone = 'success') => {
    window.clearTimeout(timer.current);
    setToast({ id: ++seq.current, message, tone });
    // Errors stay longer: they usually need reading
    timer.current = window.setTimeout(() => setToast(null), tone === 'error' ? 8000 : 4500);
  }, []);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const value = useMemo(() => ({ show, toast, dismiss }), [show, toast, dismiss]);
  return <ToastContext.Provider value={value}>{children}</ToastContext.Provider>;
}

export function useToast(): (message: string, tone?: Tone) => void {
  const v = useContext(ToastContext);
  if (!v) throw new Error('useToast must be used inside ToastProvider');
  return v.show;
}

/** Where the toast appears: the shell lifts it above the mobile tab bar. */
export function ToastViewport({ bottom = 24 }: { bottom?: number }) {
  const v = useContext(ToastContext);
  const toast = v?.toast;
  return (
    // Always in the page, so screen readers hear the message when it's filled in
    <div className={styles.viewport} style={{ bottom }} role="status" aria-live="polite">
      {toast && (
        <div key={toast.id} className={styles.toast} data-tone={toast.tone}>
          <Icon name={toast.tone === 'error' ? 'error' : 'check'} />
          <span className={styles.message}>{toast.message}</span>
          <button type="button" className={styles.close} aria-label="Dismiss" onClick={v.dismiss}>
            <Icon name="close" />
          </button>
        </div>
      )}
    </div>
  );
}
