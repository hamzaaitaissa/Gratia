import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import type { Invitation } from '../../api/types';
import { getSession, setSession } from '../../auth/session';
import { fakeToken, mockApi, renderWithProviders } from '../../test/utils';
import { AcceptInvitationPage } from './AcceptInvitationPage';
import { CreateWorkspacePage } from './CreateWorkspacePage';

const invitation = (over: Partial<Invitation> = {}): Invitation => ({
  id: 'i1',
  email: 'noah.becker@fernhill.example',
  expiresAt: new Date(Date.now() + 5 * 864e5).toISOString(),
  isUsed: false,
  invitedRole: 'Employee',
  companyName: 'Fernhill Labs',
  invitedByUserName: 'Jonas Lindqvist',
  createdAt: new Date().toISOString(),
  isExpired: false,
  isValid: true,
  ...over,
});

function renderAccept(lookup: { status?: number; body?: unknown }, accept: { status?: number; body?: unknown } = { status: 204 }) {
  setSession(null);
  const api = mockApi({
    'GET /api/invitation/token/tok123': () => lookup,
    'POST /api/invitation/accept': () => accept,
    'POST /api/auth/login': () => ({
      body: { accessToken: fakeToken({ nameid: 'u9', CompanyId: 'c1', role: 'Employee', email: 'noah.becker@fernhill.example' }), refreshToken: 'r9', refreshTokenExpiryTime: '' },
    }),
  });
  renderWithProviders(
    <Routes>
      <Route path="/accept-invitation/:token" element={<AcceptInvitationPage />} />
      <Route path="/" element={<h1>Home page</h1>} />
      <Route path="/sign-in" element={<h1>Sign-in page</h1>} />
    </Routes>,
    { route: '/accept-invitation/tok123' },
  );
  return api;
}

describe('Accept invitation', () => {
  it('creates the account, signs in and lands in the app', async () => {
    const u = userEvent.setup();
    const { calls } = renderAccept({ body: invitation() });

    expect(await screen.findByRole('heading', { name: 'Join Fernhill Labs' })).toBeTruthy();
    expect(screen.getByText('Jonas Lindqvist invited you to Gratia, where colleagues say thank you to each other.')).toBeTruthy();

    await u.click(screen.getByRole('button', { name: 'Join Fernhill Labs' }));
    expect(screen.getByText('Enter your full name.')).toBeTruthy();
    expect(screen.getByText('Use at least 8 characters.')).toBeTruthy();

    await u.type(screen.getByLabelText('Full name'), 'Noah Becker');
    await u.type(screen.getByLabelText(/Job title/), 'Product Manager');
    await u.type(screen.getByLabelText('Password'), 'long-enough-1');
    await u.type(screen.getByLabelText('Confirm password'), 'long-enough-2');
    await u.click(screen.getByRole('button', { name: 'Join Fernhill Labs' }));
    expect(screen.getByText('The passwords don’t match.')).toBeTruthy();

    await u.clear(screen.getByLabelText('Confirm password'));
    await u.type(screen.getByLabelText('Confirm password'), 'long-enough-1');
    await u.click(screen.getByRole('button', { name: 'Join Fernhill Labs' }));

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeTruthy();
    expect(calls.find((c) => c.path === '/api/invitation/accept')?.body).toEqual({ token: 'tok123', fullName: 'Noah Becker', password: 'long-enough-1', jobTitle: 'Product Manager' });
    expect(calls.find((c) => c.path === '/api/auth/login')?.body).toEqual({ email: 'noah.becker@fernhill.example', hashedPassword: 'long-enough-1' });
    expect(getSession()?.userId).toBe('u9');
    expect(screen.getByText('Welcome to Fernhill Labs, Noah.')).toBeTruthy();
  });

  it('speaks to the founder of a new workspace', async () => {
    renderAccept({ body: invitation({ invitedByUserName: '', invitedRole: 'Admin' }) });
    expect(await screen.findByRole('heading', { name: 'Set up Fernhill Labs' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Finish setting up' })).toBeTruthy();
  });

  it.each([
    ['an expired link', { body: invitation({ isExpired: true, isValid: false }) }, 'This invitation has expired'],
    ['a used link', { body: invitation({ isUsed: true, isValid: false }) }, 'This invitation has already been used'],
    ['an unknown link', { status: 404, body: { status: 404 } }, 'This link doesn’t work'],
  ])('explains %s', async (_, lookup, heading) => {
    renderAccept(lookup);
    expect(await screen.findByRole('heading', { name: heading })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Go to sign in' })).toBeTruthy();
  });

  it('turns a 409 on accepting into “already used”', async () => {
    const u = userEvent.setup();
    renderAccept({ body: invitation() }, { status: 409, body: { status: 409, detail: 'User with this email already exists' } });
    await u.type(await screen.findByLabelText('Full name'), 'Noah Becker');
    await u.type(screen.getByLabelText('Password'), 'long-enough-1');
    await u.type(screen.getByLabelText('Confirm password'), 'long-enough-1');
    await u.click(screen.getByRole('button', { name: 'Join Fernhill Labs' }));

    expect(await screen.findByRole('heading', { name: 'This invitation has already been used' })).toBeTruthy();
  });
});

describe('Create a company workspace', () => {
  function renderSignup(response: { status?: number; body?: unknown }) {
    const api = mockApi({ 'POST /api/auth/register-company': () => response });
    renderWithProviders(<CreateWorkspacePage />, { route: '/create-workspace' });
    return api;
  }

  async function fill(u: ReturnType<typeof userEvent.setup>) {
    await u.type(screen.getByLabelText('Company name'), 'Kestrel Studio');
    await u.type(screen.getByLabelText('Your full name'), 'Ada Mwangi');
    await u.type(screen.getByLabelText('Work email'), 'ada@kestrel.example');
    await u.click(screen.getByRole('button', { name: 'Email me the setup link' }));
  }

  it('sends the three fields and says to check the inbox', async () => {
    const u = userEvent.setup();
    const { calls } = renderSignup({ status: 202 });
    await fill(u);

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeTruthy();
    expect(screen.getByText(/If ada@kestrel.example can start a workspace for Kestrel Studio/)).toBeTruthy();
    expect(calls[0]?.body).toEqual({ companyName: 'Kestrel Studio', fullName: 'Ada Mwangi', email: 'ada@kestrel.example' });
  });

  it('asks for a work email when the API refuses a personal one', async () => {
    const u = userEvent.setup();
    renderSignup({ status: 400, body: { status: 400, detail: 'Sign up with your work email address.' } });
    await fill(u);

    expect(await screen.findByText(/Use your work email address/)).toBeTruthy();
    expect(screen.getByLabelText('Work email').getAttribute('aria-invalid')).toBe('true');
  });

  it('says when the email couldn’t be sent', async () => {
    const u = userEvent.setup();
    renderSignup({ status: 503, body: { status: 503 } });
    await fill(u);

    expect(await screen.findByText('We couldn’t send the email. Nothing was changed; try again in a moment.')).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Check your email' })).toBeNull();
  });

  it('explains the rate limit', async () => {
    const u = userEvent.setup();
    renderSignup({ status: 429, body: { status: 429 } });
    await fill(u);

    expect(await screen.findByText('Too many attempts from your network. Try again in an hour.')).toBeTruthy();
  });
});
