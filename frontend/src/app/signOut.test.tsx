import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { getSession } from '../auth/session';
import { mockApi, renderWithProviders, signIn } from '../test/utils';
import { useSignOut } from './AppShell';

function SignOutButton() {
  const signOut = useSignOut();
  return (
    <button type="button" onClick={() => void signOut()}>
      Sign out
    </button>
  );
}

it('signs out this device only, by revoking its refresh token', async () => {
  signIn({ id: 'u1', companyId: 'c1', role: 'Employee', email: 'maya@fernhill.example' });
  const { calls } = mockApi({ 'POST /api/auth/revoke-token': () => ({ status: 204 }) });
  renderWithProviders(<SignOutButton />);

  await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

  const revoke = calls.find((c) => c.path === '/api/auth/revoke-token');
  expect(revoke?.body).toBe('refresh-1'); // with no token, the API would sign out every device
  expect(revoke?.headers.Authorization).toBeUndefined();
  expect(getSession()).toBeNull();
});

it('still signs out locally when the API can’t be reached', async () => {
  signIn({ id: 'u1', companyId: 'c1', role: 'Employee', email: 'maya@fernhill.example' });
  mockApi({ 'POST /api/auth/revoke-token': () => ({ status: 503 }) });
  renderWithProviders(<SignOutButton />);

  await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

  expect(getSession()).toBeNull();
});
