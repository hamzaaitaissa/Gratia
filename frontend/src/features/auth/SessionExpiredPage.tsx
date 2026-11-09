import { useLocation, useNavigate } from 'react-router';
import { Icon } from '../../components/Icon';
import styles from './Auth.module.css';
import { PublicLayout } from './PublicLayout';

/** Shown when a session can't be refreshed any more (design 6.12). Signing in again returns to the same page. */
export function SessionExpiredPage() {
  const navigate = useNavigate();
  const from = (useLocation().state as { from?: string } | null)?.from;
  return (
    <PublicLayout>
      <div role="alert" className={styles.plain}>
        <span aria-hidden="true" className={styles.plainIcon}>
          <Icon name="lock_clock" size={30} />
        </span>
        <h1 className={styles.title}>Your session has expired</h1>
        <p className={styles.lead}>For your security we sign you out after a while. Sign in again and we’ll take you back to where you were.</p>
        <button type="button" className={`btn btn-primary ${styles.submit}`} onClick={() => navigate('/sign-in', { replace: true, state: { from } })}>
          Sign in again
        </button>
      </div>
    </PublicLayout>
  );
}
