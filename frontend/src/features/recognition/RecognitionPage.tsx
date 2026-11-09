import { Link, useLocation, useParams } from 'react-router';
import { ApiError } from '../../api/http';
import { useTransaction } from '../../api/queries';
import { useSession } from '../../auth/session';
import { Icon } from '../../components/Icon';
import { PageError } from '../../components/Page';
import { LargeRecognitionCard } from '../../components/RecognitionCard';
import { BackLink, CardsSkeleton } from '../../components/States';
import styles from './RecognitionPage.module.css';

/**
 * One recognition, as the large card (design 6.5). Only its sender and receiver can open it: the API answers 403
 * to anyone else and 404 to an id that doesn't exist, and both get the same "isn't available" page.
 */
export function RecognitionPage() {
  const { id = '' } = useParams();
  const session = useSession();
  const location = useLocation();
  const tx = useTransaction(id);
  // Opened from My activity, the way back goes there; otherwise Home, as in the design
  const state = location.state as { back?: string; backLabel?: string } | null;
  const inApp = state?.back?.startsWith('/') && !state.back.startsWith('//'); // never an external link
  const back = inApp ? { to: state!.back!, label: state!.backLabel ?? 'Back' } : { to: '/', label: 'Home' };

  const unavailable = tx.error instanceof ApiError && (tx.error.status === 403 || tx.error.status === 404);

  return (
    <div className={styles.page}>
      <BackLink to={back.to}>{back.label}</BackLink>
      {tx.data ? (
        <LargeRecognitionCard tx={tx.data} meId={session?.userId} />
      ) : unavailable ? (
        <div className={styles.unavailable}>
          <span aria-hidden="true" className={styles.unavailableIcon}>
            <Icon name="visibility_off" size={28} />
          </span>
          <h1 className={styles.unavailableTitle}>This recognition isn’t available</h1>
          <p className={styles.unavailableText}>
            Only the person who gave it and the person who received it can open it. You’ll find the latest recognitions in the company feed.
          </p>
          <Link to="/" className="btn btn-primary">
            Go to Home
          </Link>
        </div>
      ) : tx.isError ? (
        <PageError error={tx.error} onRetry={() => void tx.refetch()} />
      ) : (
        <CardsSkeleton cards={1} />
      )}
    </div>
  );
}
