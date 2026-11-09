import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import { api } from '../api/endpoints';
import { useCompany, useMe } from '../api/queries';
import type { ReadCompany } from '../api/types';
import { setSession, syncFromStorage, useSession } from '../auth/session';
import { Avatar } from '../components/Avatar';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { Icon, type IconName } from '../components/Icon';
import { ToastViewport, useToast } from '../components/Toast';
import { useGive } from '../features/give/GiveContext';
import { monogram } from '../lib/format';
import { cachedBrand, rememberBrand, useBrandTheme, useThemeMode } from '../theme/ThemeMode';
import styles from './AppShell.module.css';
import { useIsMobile } from './useIsMobile';

type NavKey = 'home' | 'activity' | 'people' | 'profile' | 'members' | 'invitations' | 'settings';

interface NavItem {
  key: NavKey;
  to: string;
  label: string;
  short: string;
  icon: IconName;
}

const MAIN_NAV: NavItem[] = [
  { key: 'home', to: '/', label: 'Home', short: 'Home', icon: 'home' },
  { key: 'activity', to: '/activity', label: 'My activity', short: 'Activity', icon: 'history' },
  { key: 'people', to: '/people', label: 'People', short: 'People', icon: 'group' },
  { key: 'profile', to: '/me', label: 'My profile', short: 'Profile', icon: 'person' },
];

const ADMIN_NAV: NavItem[] = [
  { key: 'members', to: '/admin/members', label: 'Members', short: 'Members', icon: 'badge' },
  { key: 'invitations', to: '/admin/invitations', label: 'Invitations', short: 'Invitations', icon: 'forward_to_inbox' },
  { key: 'settings', to: '/admin/settings', label: 'Company settings', short: 'Settings', icon: 'palette' },
];

/** The nav item a route belongs to; none for a route that doesn't exist (the 404 page). */
function activeKey(path: string): NavKey | null {
  if (path === '/' || path.startsWith('/recognition/')) return 'home';
  if (path === '/activity') return 'activity';
  if (path === '/people' || path.startsWith('/people/')) return 'people';
  if (path === '/me') return 'profile';
  if (path === '/admin/members') return 'members';
  if (path.startsWith('/admin/invitations')) return 'invitations';
  if (path === '/admin/settings') return 'settings';
  return null;
}

export function AppShell() {
  const isMobile = useIsMobile();
  const session = useSession();
  const { data: company } = useCompany();
  const { me } = useMe();
  const { openGive } = useGive();
  const location = useLocation();

  // The company's colours, or the ones seen last time while the company loads
  const brand = company ? { primary: company.primaryColor, secondary: company.secondaryColor } : cachedBrand();
  useBrandTheme(brand?.primary, brand?.secondary);
  useEffect(() => {
    if (company) rememberBrand({ primary: company.primaryColor, secondary: company.secondaryColor });
  }, [company]);

  const active = activeKey(location.pathname);
  const isAdmin = (me?.role ?? session?.role) === 'Admin';
  const left = me?.numberOfPointsAvailable ?? 0;
  const giveLabel = me && left === 0 ? 'Give recognition (no points left)' : 'Give recognition';

  return (
    <div className={styles.root}>
      {!isMobile && (
        <nav aria-label="Main" className={styles.sidebar}>
          <div className={styles.brand}>
            <CompanyLogo company={company} size={36} />
            <span className={styles.companyName}>{company?.name ?? ''}</span>
          </div>
          <button type="button" className={`btn btn-primary ${styles.giveButton}`} aria-label={giveLabel} onClick={() => openGive()}>
            <Icon name="volunteer_activism" />
            Give recognition
          </button>
          <NavList items={MAIN_NAV} active={active} />
          {isAdmin && (
            <div className={styles.adminGroup}>
              <div id="nav-admin" className={styles.adminHeading}>
                Admin
              </div>
              <NavList items={ADMIN_NAV} active={active} labelledBy="nav-admin" />
            </div>
          )}
          <AccountMenu />
        </nav>
      )}

      <div className={styles.column}>
        {isMobile && (
          <header className={styles.mobileHeader}>
            <CompanyLogo company={company} size={32} />
            <span className={styles.companyName}>{company?.name ?? ''}</span>
            <Link to="/me" aria-label="My profile" className={styles.headerAvatar}>
              {me && <Avatar id={me.id} name={me.fullName} size={34} />}
            </Link>
          </header>
        )}

        <main id="main" className={styles.main}>
          <div className={styles.content}>
            {/* A page that crashes shows "Something went wrong" inside the shell; moving to another page recovers */}
            <ErrorBoundary resetKey={location.pathname}>
              <Outlet />
            </ErrorBoundary>
          </div>
        </main>
        {/* Above the mobile tab bar */}
        <ToastViewport bottom={isMobile ? 96 : 24} />

        {isMobile && (
          <nav aria-label="Main" className={styles.tabBar}>
            {MAIN_NAV.slice(0, 2).map((t) => (
              <Tab key={t.key} item={t} active={active === t.key} />
            ))}
            <div className={styles.fabSlot}>
              <button type="button" className={styles.fab} aria-label={giveLabel} onClick={() => openGive()}>
                <Icon name="volunteer_activism" size={28} />
              </button>
            </div>
            {MAIN_NAV.slice(2).map((t) => (
              // On mobile the admin area is reached from My profile
              <Tab key={t.key} item={t} active={active === t.key || (t.key === 'profile' && ADMIN_NAV.some((a) => a.key === active))} />
            ))}
          </nav>
        )}
      </div>
    </div>
  );
}

function NavList({ items, active, labelledBy }: { items: NavItem[]; active: NavKey | null; labelledBy?: string }) {
  return (
    <ul className={styles.navList} aria-labelledby={labelledBy}>
      {items.map((n) => {
        const on = n.key === active;
        return (
          <li key={n.key}>
            <Link to={n.to} className={styles.navItem} aria-current={on ? 'page' : undefined}>
              <Icon name={n.icon} size={22} filled={on} />
              {n.label}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}

function Tab({ item, active }: { item: NavItem; active: boolean }) {
  return (
    <Link to={item.to} className={styles.tab} aria-current={active ? 'page' : undefined}>
      <Icon name={item.icon} size={24} filled={active} />
      {item.short}
    </Link>
  );
}

function CompanyLogo({ company, size }: { company: ReadCompany | undefined; size: number }) {
  const [broken, setBroken] = useState(false);
  const url = company?.logoUrl;
  const radius = size === 36 ? 10 : 9;
  if (url && /^https?:\/\//i.test(url) && !broken) {
    return <img src={url} alt="" className={styles.logo} style={{ width: size, height: size, borderRadius: radius }} onError={() => setBroken(true)} />;
  }
  return (
    <span aria-hidden="true" className={styles.monogram} style={{ width: size, height: size, borderRadius: radius, fontSize: size === 36 ? 13 : 12 }}>
      {company ? monogram(company.name) : ''}
    </span>
  );
}

function AccountMenu() {
  const { me } = useMe();
  const { mode, toggleMode } = useThemeMode();
  const signOut = useSignOut();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    wrapRef.current?.querySelector<HTMLElement>('[role="menuitem"]')?.focus();
    const onDown = (e: PointerEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const onMenuKey = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const items = Array.from(wrapRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
    const i = items.indexOf(document.activeElement as HTMLElement);
    const next = items[(i + (e.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length];
    next?.focus();
  };

  const run = (action: () => void) => () => {
    setOpen(false);
    action();
  };

  return (
    <div ref={wrapRef} className={styles.account}>
      {open && (
        <div role="menu" className={styles.menu} onKeyDown={onMenuKey}>
          <button type="button" role="menuitem" className={styles.menuItem} onClick={run(() => navigate('/me'))}>
            <Icon name="person" />
            My profile
          </button>
          <button type="button" role="menuitem" className={styles.menuItem} onClick={run(toggleMode)}>
            <Icon name={mode === 'dark' ? 'light_mode' : 'dark_mode'} />
            {mode === 'dark' ? 'Light mode' : 'Dark mode'}
          </button>
          <button type="button" role="menuitem" className={styles.menuItem} onClick={run(() => void signOut())}>
            <Icon name="logout" />
            Sign out
          </button>
        </div>
      )}
      <button
        ref={buttonRef}
        type="button"
        className={styles.accountButton}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Account menu for ${me?.fullName ?? 'you'}`}
        onClick={() => setOpen((o) => !o)}
      >
        {me ? <Avatar id={me.id} name={me.fullName} size={36} /> : <span className={styles.avatarSkeleton} />}
        <span className={styles.accountText}>
          <span className={styles.accountName}>{me?.fullName ?? ''}</span>
          <span className={styles.accountRole}>{me?.role ?? ''}</span>
        </span>
        <Icon name="unfold_more" className={styles.accountChevron} />
      </button>
    </div>
  );
}

export function useSignOut() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  return async () => {
    // This device's session only; the user's other devices stay signed in
    const refreshToken = syncFromStorage()?.refreshToken;
    try {
      if (refreshToken) await api.revokeToken(refreshToken);
    } catch {
      // Signing out locally still works if the API can't be reached
    }
    setSession(null);
    rememberBrand(null);
    queryClient.clear();
    navigate('/sign-in', { replace: true });
    toast('You’ve signed out.');
  };
}
