import { useSyncExternalStore } from 'react';
import { Icon } from './Icon';
import styles from './OfflineBanner.module.css';

function subscribe(onChange: () => void) {
  window.addEventListener('online', onChange);
  window.addEventListener('offline', onChange);
  return () => {
    window.removeEventListener('online', onChange);
    window.removeEventListener('offline', onChange);
  };
}

export function useOnline(): boolean {
  return useSyncExternalStore(subscribe, () => navigator.onLine, () => true);
}

/** Sits above everything while the browser is offline (design 6.12). */
export function OfflineBanner() {
  const online = useOnline();
  if (online) return null;
  return (
    <div role="status" className={styles.banner}>
      <Icon name="cloud_off" size={18} />
      <span>You’re offline. We’ll reconnect automatically. Changes you make now won’t be saved until then.</span>
    </div>
  );
}
