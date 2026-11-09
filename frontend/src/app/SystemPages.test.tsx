import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from '../App';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { OfflineBanner } from '../components/OfflineBanner';
import { setSession } from '../auth/session';
import { mockApi, renderWithProviders, signIn, user } from '../test/utils';

const me = user('u1', 'Maya Okonkwo', 18, 142);

it('says the session expired when it can’t be refreshed, then signs in again', async () => {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ status: 401 }),
    'GET /api/company/me': () => ({ status: 401 }),
    'GET /api/transaction/company-history': () => ({ status: 401 }),
    'GET /api/transaction/received': () => ({ status: 401 }),
    'POST /api/auth/refresh-token': () => ({ status: 401 }),
  });
  renderWithProviders(<App />, { route: '/people' });

  expect(await screen.findByRole('heading', { name: 'Your session has expired' })).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Sign in again' }));
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy();
});

it('sends a first visit straight to sign-in', async () => {
  setSession(null); // a fresh visit: no session, and none that expired
  mockApi({});
  renderWithProviders(<App />, { route: '/' });
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeTruthy();
});

// Through App, which drops the query cache when a session ends: that must not cancel a public page's own queries
it('loads an invitation link when nobody is signed in', async () => {
  setSession(null);
  mockApi({
    'GET /api/invitation/token/abc': () => ({
      body: {
        id: 'i1', email: 'noah@fernhill.example', expiresAt: new Date(Date.now() + 864e5).toISOString(), isUsed: false, invitedRole: 'Employee',
        companyName: 'Fernhill Labs', invitedByUserName: 'Jonas Lindqvist', createdAt: new Date().toISOString(), isExpired: false, isValid: true,
      },
    }),
  });
  renderWithProviders(<App />, { route: '/accept-invitation/abc' });
  expect(await screen.findByRole('heading', { name: 'Join Fernhill Labs' })).toBeTruthy();
});

it('shows the offline banner while the browser is offline', () => {
  const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
  renderWithProviders(<OfflineBanner />);
  expect(screen.getByText(/You’re offline/)).toBeTruthy();

  online.mockReturnValue(true);
  act(() => {
    window.dispatchEvent(new Event('online'));
  });
  expect(screen.queryByText(/You’re offline/)).toBeNull();
  online.mockRestore();
});

it('replaces a crashed page with “Something went wrong”', () => {
  const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
  function Broken(): never {
    throw new Error('boom');
  }
  renderWithProviders(
    <ErrorBoundary resetKey="/x">
      <Broken />
    </ErrorBoundary>,
  );
  expect(screen.getByRole('heading', { name: 'Something went wrong' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Retry' })).toBeTruthy();
  spy.mockRestore();
});
