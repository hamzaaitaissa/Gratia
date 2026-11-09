import { Link } from 'react-router';
import { useCompany, useMe, usePointsCycle } from '../../api/queries';
import { useSignOut } from '../../app/AppShell';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { BreakableEmail } from '../../components/BreakableEmail';
import { Icon, type IconName } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { CardsSkeleton } from '../../components/States';
import { useThemeMode } from '../../theme/ThemeMode';
import styles from './ProfilePage.module.css';

const ADMIN_LINKS: { to: string; label: string; icon: IconName }[] = [
  { to: '/admin/members', label: 'Members', icon: 'badge' },
  { to: '/admin/invitations', label: 'Invitations', icon: 'forward_to_inbox' },
  { to: '/admin/settings', label: 'Company settings', icon: 'palette' },
];

/** "My profile" (design 6.9): both balances side by side. On mobile, the admin area is reached from here. */
export function ProfilePage() {
  const users = useMe();
  const { data: company } = useCompany();
  const cycle = usePointsCycle();
  const { mode, toggleMode } = useThemeMode();
  const signOut = useSignOut();
  const isMobile = useIsMobile();
  const { me } = users;

  if (users.isError || (users.isSuccess && !me))
    return (
      <>
        <PageHeader title="My profile" />
        <PageError error={users.error} onRetry={() => void users.refetch()} />
      </>
    );
  if (!me || !cycle) return <CardsSkeleton cards={2} />;


  return (
    <>
      <PageHeader title="My profile" />
      <div className={styles.page}>
        <section aria-labelledby="profile-name" className={styles.card}>
          <div className={styles.head}>
            <Avatar id={me.id} name={me.fullName} size={72} />
            <div className={styles.headText}>
              <h2 id="profile-name" className={styles.name}>
                {me.fullName}
              </h2>
              <p className={styles.job}>{me.jobTitle || 'No job title'}</p>
            </div>
          </div>
          <dl className={styles.facts}>
            <div className={styles.fact}>
              <dt>Email</dt>
              <dd>
                <BreakableEmail email={me.email} />
              </dd>
            </div>
            <div className={styles.fact}>
              <dt>Role</dt>
              <dd>{me.role}</dd>
            </div>
            <div className={styles.fact}>
              <dt>Company</dt>
              <dd>{company?.name ?? ''}</dd>
            </div>
          </dl>
        </section>

        {/* Points to give (brand, ring icon) and points received (teal, star) never share a treatment */}
        <div className={styles.balances}>
          <section aria-labelledby="profile-give" className={styles.balance}>
            <h2 id="profile-give" className={`${styles.balanceLabel} ${styles.give}`}>
              <Icon name="volunteer_activism" />
              Points to give
            </h2>
            <div className={styles.balanceLine}>
              <span className={styles.big}>{me.numberOfPointsAvailable}</span>
              <span className={styles.ofLeft}>of {cycle.allowance} left</span>
            </div>
            <div className={styles.balanceNote}>{cycle.refillText}</div>
          </section>
          <section aria-labelledby="profile-received" className={styles.balance}>
            <h2 id="profile-received" className={`${styles.balanceLabel} ${styles.recv}`}>
              <Icon name="stars" filled />
              Points received
            </h2>
            <div className={styles.balanceLine}>
              <span className={styles.big}>{me.numberOfPointsAcquired}</span>
              <span className={styles.inTotal}>in total</span>
            </div>
            <div className={styles.balanceNote}>Everything colleagues have given you</div>
          </section>
        </div>

        {isMobile && me.role === 'Admin' && (
          <nav aria-label="Admin" className={styles.admin}>
            <div className={styles.adminHeading}>Admin</div>
            {ADMIN_LINKS.map((l) => (
              <Link key={l.to} to={l.to} className={styles.adminLink}>
                <Icon name={l.icon} size={22} className={styles.adminIcon} />
                <span className={styles.adminLabel}>{l.label}</span>
                <Icon name="chevron_right" className={styles.adminChevron} />
              </Link>
            ))}
          </nav>
        )}

        <div className={styles.actions}>
          <button type="button" className="btn btn-secondary" onClick={toggleMode}>
            <Icon name={mode === 'dark' ? 'light_mode' : 'dark_mode'} />
            {mode === 'dark' ? 'Light mode' : 'Dark mode'}
          </button>
          <button type="button" className={`btn btn-secondary ${styles.signOut}`} onClick={() => void signOut()}>
            <Icon name="logout" />
            Sign out
          </button>
        </div>
      </div>
    </>
  );
}
