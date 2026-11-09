import type { ReactNode } from 'react';
import { ToastViewport } from '../../components/Toast';
import { useBrandTheme } from '../../theme/ThemeMode';
import styles from './PublicLayout.module.css';

/** Public pages use Gratia's own neutral brand: no company is known yet. */
export function PublicLayout({ children }: { children: ReactNode }) {
  useBrandTheme();
  return (
    <div className={styles.root}>
      <header className={styles.header}>
        <div role="img" aria-label="Gratia" className={styles.wordmark}>
          <span aria-hidden="true" className={styles.mark}>
            <span className={styles.markDot} />
          </span>
          <span aria-hidden="true" className={styles.word}>
            Gratia
          </span>
        </div>
      </header>
      <main className={styles.main}>{children}</main>
      <ToastViewport />
    </div>
  );
}
