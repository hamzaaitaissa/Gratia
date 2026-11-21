import { useState } from 'react';
import { Link } from 'react-router';
import { useCompany, useMe, usePointsCycle } from '../../api/queries';
import { useSignOut } from '../../app/AppShell';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { BreakableEmail } from '../../components/BreakableEmail';
import { ConfirmDialog } from '../../components/Dialog';
import { Icon, type IconName } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { CardsSkeleton } from '../../components/States';
import { useThemeMode } from '../../theme/ThemeMode';
import { ChangePasswordDialog } from './ChangePasswordDialog';
import styles from './ProfilePage.module.css';

const ADMIN_LINKS: { to: string; label: string; icon: IconName }[] = [
  { to: '/admin/members', label: 'Members', icon: 'badge' },
  { to: '/admin/invitations', label: 'Invitations', icon: 'forward_to_inbox' },
  { to: '/admin/settings', label: 'Company settings', icon: 'palette' },
];

/**
 * "My profile" (design 6.9): both balances side by side. On mobile, the admin area is reached from here. Below them,
 * the password and signed-in devices (#85), which the design doesn't have.
 */
export function ProfilePage() {
  const users = useMe();
  const { data: company } = useCompany();
  const cycle = usePointsCycle();
  const { mode, toggleMode } = useThemeMode();
  const signOut = useSignOut();
  const isMobile = useIsMobile();
  const [dialog, setDialog] = useState<'password' | 'everywhere' | null>(null);
  const [signingOut, setSigningOut] = useState(false);
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

        <section aria-labelledby="profile-security" className={styles.security}>
          <h2 id="profile-security" className={styles.securityHeading}>
            Password and devices
          </h2>
          <div className={styles.securityRow}>
            <Icon name="key" size={22} className={styles.securityIcon} />
            <div className={styles.securityText}>
              <div className={styles.securityTitle}>Password</div>
              <div className={styles.securityNote}>Changing it signs you out on your other devices.</div>
            </div>
            <button type="button" className="btn btn-secondary" onClick={() => setDialog('password')}>
              Change password
            </button>
          </div>
          <div className={styles.securityRow}>
            <Icon name="devices" size={22} className={styles.securityIcon} />
            <div className={styles.securityText}>
              <div className={styles.securityTitle}>Signed-in devices</div>
              <div className={styles.securityNote}>Lost a phone or used a shared computer? Sign out everywhere at once.</div>
            </div>
            <button type="button" className="btn btn-secondary" onClick={() => setDialog('everywhere')}>
              Sign out everywhere
            </button>
          </div>
        </section>

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

      {dialog === 'password' && <ChangePasswordDialog onClose={() => setDialog(null)} />}
      {dialog === 'everywhere' && (
        <ConfirmDialog
          title="Sign out everywhere?"
          body="You’ll be signed out on every device and browser, this one included. Anyone using your account elsewhere is signed out at once."
          confirmLabel="Sign out everywhere"
          cancelLabel="Stay signed in"
          busy={signingOut}
          onConfirm={() => {
            setSigningOut(true);
            void signOut({ everywhere: true }).finally(() => {
              setSigningOut(false);
              setDialog(null);
            });
          }}
          onCancel={() => setDialog(null)}
        />
      )}
    </>
  );
}
