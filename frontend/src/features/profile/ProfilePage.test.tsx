import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { COMPANY, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { ProfilePage } from './ProfilePage';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin', jobTitle: 'Product Designer' });

function renderProfile() {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ body: [me] }),
    'GET /api/company/me': () => ({ body: COMPANY }),
  });
  renderWithProviders(<ProfilePage />, { route: '/me' });
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
