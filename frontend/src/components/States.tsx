import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { Icon, type IconName } from './Icon';
import styles from './States.module.css';

/** Loading for feeds and profiles: a title, optionally two summary blocks, then cards. */
export function CardsSkeleton({ blocks = false, cards = 3 }: { blocks?: boolean; cards?: number }) {
  return (
    <div aria-busy="true" aria-label="Loading" className={styles.skeleton}>
      <div className={styles.skelTitle} />
      {blocks && (
        <div className={styles.skelRow}>
          <div className={`${styles.skelBlock} ${styles.skelWide}`} />
          <div className={styles.skelBlock} />
        </div>
      )}
      {Array.from({ length: cards }, (_, k) => (
        <div key={k} className={styles.skelCard}>
          <div className={styles.skelCardHead}>
            <div className={styles.skelDot} />
            <div className={styles.skelLine} style={{ width: '45%', height: 14 }} />
          </div>
          <div className={styles.skelLine} style={{ width: '92%' }} />
          <div className={styles.skelLine} style={{ width: '68%' }} />
        </div>
      ))}
      <span className="sr-only" role="status">
        Loading…
      </span>
    </div>
  );
}

/** Loading for lists: a title, a search field, then rows. */
export function RowsSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <div aria-busy="true" aria-label="Loading" className={styles.skeleton}>
      <div className={styles.skelTitle} style={{ width: 'min(280px, 60%)' }} />
      <div className={styles.skelSearch} />
      <div className={styles.skelList}>
        {Array.from({ length: rows }, (_, k) => (
          <div key={k} className={styles.skelListRow}>
            <div className={styles.skelDot} style={{ width: 40, height: 40 }} />
            <div className={styles.skelListText}>
              <div className={styles.skelLine} style={{ width: '40%', height: 13 }} />
              <div className={styles.skelLine} style={{ width: '25%', height: 11 }} />
            </div>
            <div className={styles.skelButton} />
          </div>
        ))}
      </div>
      <span className="sr-only" role="status">
        Loading…
      </span>
    </div>
  );
}

/** Says why something is empty and nudges toward giving. */
export function EmptyState({ icon, title, children, action }: { icon: IconName; title: string; children: ReactNode; action?: ReactNode }) {
  return (
    <div className={styles.empty}>
      <span aria-hidden="true" className={styles.emptyIcon}>
        <Icon name={icon} size={28} />
      </span>
      <h3 className={styles.emptyTitle}>{title}</h3>
      <p className={styles.emptyText}>{children}</p>
      {action}
    </div>
  );
}

export function BackLink({ to, children }: { to: string; children: ReactNode }) {
  return (
    <Link to={to} className={styles.back}>
      <Icon name="arrow_back" />
      {children}
    </Link>
  );
}
