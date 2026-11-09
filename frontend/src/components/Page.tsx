import { Link } from 'react-router';
import { ApiError } from '../api/http';
import { Icon } from './Icon';
import styles from './Page.module.css';

/** 404 inside the app shell: an unknown route, or a person or page that doesn't exist (design 6.12). */
export function NotFound() {
  return (
    <div className={styles.system}>
      <span aria-hidden="true" className={styles.systemIcon}>
        <Icon name="explore_off" size={28} />
      </span>
      <h1 className={styles.systemTitle}>We can’t find that page</h1>
      <p className={styles.systemText}>The link may be old, or the page may have moved. Check the address, or head back to Home.</p>
      <Link to="/" className="btn btn-primary">
        Go to Home
      </Link>
      <p className={styles.systemCode}>Error 404</p>
    </div>
  );
}

export function PageHeader({ title, sub }: { title: string; sub?: string }) {
  return (
    <div className={styles.header}>
      <h1 className={styles.title}>{title}</h1>
      {sub && <p className={styles.sub}>{sub}</p>}
    </div>
  );
}

/** A page-level error: keeps the shell, explains, offers Retry and shows the API's traceId as a reference. */
export function PageError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const trace = error instanceof ApiError ? error.traceId : undefined;
  return (
    <div role="alert" className={styles.error}>
      <span aria-hidden="true" className={styles.errorIcon}>
        <Icon name="cloud_alert" size={28} />
      </span>
      <h2 className={styles.errorTitle}>We couldn’t load this page</h2>
      <p className={styles.errorText}>
        Something went wrong on our side. Try again in a moment. If it keeps happening, send the reference below to your admin.
      </p>
      <button type="button" className="btn btn-primary" onClick={onRetry}>
        <Icon name="refresh" />
        Retry
      </button>
      {trace && (
        <p className={styles.reference}>
          Reference ID <code>{trace}</code>
        </p>
      )}
    </div>
  );
}

/** 403 inside the app shell: an admin page opened by an employee (design 6.12). */
export function Forbidden({ companyName }: { companyName?: string }) {
  return (
    <div className={styles.system}>
      <span aria-hidden="true" className={styles.systemIcon}>
        <Icon name="lock" size={28} />
      </span>
      <h1 className={styles.systemTitle}>You don’t have access to this page</h1>
      <p className={styles.systemText}>
        This part of Gratia is for admins of {companyName ?? 'your company'}. If you need access, ask one of your admins.
      </p>
      <Link to="/" className="btn btn-primary">
        Go to Home
      </Link>
      <p className={styles.systemCode}>Error 403</p>
    </div>
  );
}

/** Something broke that we didn't plan for (design 6.12): Retry, and the API's traceId when there is one. */
export function ServerError({ error, onRetry }: { error?: unknown; onRetry: () => void }) {
  const trace = error instanceof ApiError ? error.traceId : undefined;
  return (
    <div role="alert" className={styles.system}>
      <span aria-hidden="true" className={`${styles.systemIcon} ${styles.systemDanger}`}>
        <Icon name="error" size={28} />
      </span>
      <h1 className={styles.systemTitle}>Something went wrong</h1>
      <p className={styles.systemText}>
        That’s on us, not you. Try again in a moment. If it keeps happening, share the reference ID with your admin so they can look into it.
      </p>
      <button type="button" className="btn btn-primary" onClick={onRetry}>
        <Icon name="refresh" />
        Retry
      </button>
      {trace && (
        <div className={styles.traceBox}>
          <span className={styles.traceLabel}>Reference ID</span>
          <code>{trace}</code>
        </div>
      )}
    </div>
  );
}
