import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { getSession } from '../../auth/session';
import { COMPANY, fakeToken, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { ProfilePage } from './ProfilePage';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin', jobTitle: 'Product Designer' });

function renderProfile(routes: Parameters<typeof mockApi>[0] = {}) {
  signIn(me);
  const api = mockApi({
    'GET /api/user': () => ({ body: [me] }),
    'GET /api/company/me': () => ({ body: COMPANY }),
    ...routes,
  });
  renderWithProviders(<ProfilePage />, { route: '/me' });
  return api;
}

afterEach(() => vi.unstubAllGlobals());

it('shows who you are and both balances, kept apart', async () => {
  renderProfile();

  expect(await screen.findByRole('heading', { name: 'Maya Okonkwo' })).toBeTruthy();
  expect(screen.getByText(me.email)).toBeTruthy();
  expect(await screen.findByText('Fernhill Labs')).toBeTruthy();
  expect(screen.getByRole('region', { name: 'Points to give' }).textContent).toContain('18of 25 left');
  expect(screen.getByRole('region', { name: 'Points received' }).textContent).toContain('142in total');
  // On desktop the admin area is in the sidebar, not here
  expect(screen.queryByRole('navigation', { name: 'Admin' })).toBeNull();
});

it('switches between light and dark mode', async () => {
  renderProfile();
  const toggle = await screen.findByRole('button', { name: 'Dark mode' });

  await userEvent.click(toggle);

  // The shell applies the theme to the page; here, the choice itself flips
  expect(screen.getByRole('button', { name: 'Light mode' })).toBeTruthy();
});

it('reaches the admin area from here on mobile', async () => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('max-width'),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
  renderProfile();

  const admin = await screen.findByRole('navigation', { name: 'Admin' });
  expect([...admin.querySelectorAll('a')].map((a) => a.getAttribute('href'))).toEqual(['/admin/members', '/admin/invitations', '/admin/settings']);
});

it('changes the password and stays signed in with the new tokens (#85)', async () => {
  const u = userEvent.setup();
  const fresh = fakeToken({ nameid: 'u1', CompanyId: 'c1', role: 'Admin', email: me.email, n: 'new' });
  let wrong = true;
  const { calls } = renderProfile({
    'POST /api/user/me/password': () =>
      wrong ? { status: 400, body: { status: 400, detail: 'Your current password isn\'t right.' } } : { body: { accessToken: fresh, refreshToken: 'r-new', refreshTokenExpiryTime: '' } },
  });

  await u.click(await screen.findByRole('button', { name: 'Change password' }));
  const dialog = screen.getByRole('dialog', { name: 'Change password' });
  await u.type(within(dialog).getByLabelText('Current password'), 'old-password');
  await u.type(within(dialog).getByLabelText('New password'), 'old-password');
  await u.click(within(dialog).getByRole('button', { name: 'Change password' }));
  expect(within(dialog).getByText('Choose a password different from your current one.')).toBeTruthy();

  await u.clear(within(dialog).getByLabelText('New password'));
  await u.type(within(dialog).getByLabelText('New password'), 'a-new-password');
  await u.type(within(dialog).getByLabelText('Confirm new password'), 'a-new-password');
  await u.click(within(dialog).getByRole('button', { name: 'Change password' }));
  expect(await within(dialog).findByText('Your current password isn\'t right.')).toBeTruthy();

  wrong = false;
  await u.click(within(dialog).getByRole('button', { name: 'Change password' }));
  expect(await screen.findByText('Password changed. You’re signed out on every other device.')).toBeTruthy();
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(getSession()?.accessToken).toBe(fresh);
  expect(calls.filter((c) => c.method === 'POST').at(-1)?.body).toEqual({ currentPassword: 'old-password', newPassword: 'a-new-password' });
});

it('signs out every device after a confirmation', async () => {
  const u = userEvent.setup();
  const { calls } = renderProfile({ 'POST /api/auth/revoke-token': () => ({ status: 204 }) });

  await u.click(await screen.findByRole('button', { name: 'Sign out everywhere' }));
  const confirm = screen.getByRole('alertdialog', { name: 'Sign out everywhere?' });
  await u.click(within(confirm).getByRole('button', { name: 'Sign out everywhere' }));

  expect(await screen.findByText('You’ve signed out on every device.')).toBeTruthy();
  expect(getSession()).toBeNull();
  // No token in the body: every device, not just this one
  const revoke = calls.find((c) => c.path === '/api/auth/revoke-token');
  expect(revoke?.body).toBeUndefined();
  expect(revoke?.headers.Authorization).toMatch(/^Bearer /);
});
