import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import { getSession, setSession } from '../../auth/session';
import { fakeToken, mockApi, renderWithProviders } from '../../test/utils';
import { ForgotPasswordPage, ResetPasswordPage } from './PasswordPages';
import { SignInPage } from './SignInPage';

// An alert's text without its icon's ligature name
const alertText = async () => (await screen.findByRole('alert')).textContent!.replace(/^error/, '');

const tokens = { accessToken: fakeToken({ nameid: 'u1', CompanyId: 'c1', role: 'Employee', email: 'maya@fernhill.example' }), refreshToken: 'r1', refreshTokenExpiryTime: '' };

function renderAt(route: string | { pathname: string; state?: unknown }, routes: Parameters<typeof mockApi>[0]) {
  setSession(null);
  const api = mockApi(routes);
  renderWithProviders(
    <Routes>
      <Route path="/sign-in" element={<SignInPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password/:token" element={<ResetPasswordPage />} />
      <Route path="/" element={<h1>Home page</h1>} />
    </Routes>,
    { route },
  );
  return api;
}

async function signInWith(u: ReturnType<typeof userEvent.setup>, email = 'maya@fernhill.example') {
  await u.type(screen.getByLabelText('Email'), email);
  await u.type(screen.getByLabelText('Password'), 'not-the-password');
  await u.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('Sign in', () => {
  it('tells a locked email from a busy network (#81)', async () => {
    const u = userEvent.setup();
    let lock = true;
    renderAt('/sign-in', {
      'POST /api/auth/login': () =>
        lock
          ? { status: 429, body: { status: 429, title: 'Too many attempts', detail: 'Too many failed sign-ins with this email. Try again in 15 minutes.' } }
          : { status: 429, body: { status: 429, title: 'Too many requests', detail: 'Too many attempts. Try again later.' } },
    });

    await signInWith(u);
    expect(await alertText()).toBe('Too many failed sign-ins with this email. Try again in 15 minutes. Or reset your password to sign in now.');
    // Resetting the password lifts the lock, and the email comes along
    await u.click(screen.getByRole('link', { name: 'reset your password' }));
    expect((await screen.findByLabelText('Email') as HTMLInputElement).value).toBe('maya@fernhill.example');

    lock = false;
    await u.click(screen.getByRole('link', { name: 'Sign in' }));
    await u.type(await screen.findByLabelText('Password'), 'not-the-password');
    await u.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await alertText()).toBe('Too many sign-in attempts from your network. Wait a minute and try again.');
  });

  it('links to the forgotten-password page', async () => {
    renderAt('/sign-in', {});
    expect(screen.getByRole('link', { name: 'Forgot your password?' }).getAttribute('href')).toBe('/forgot-password');
  });
});

describe('Forgot password', () => {
  it('asks for a link and says the same thing whether or not there’s an account', async () => {
    const u = userEvent.setup();
    const { calls } = renderAt('/forgot-password', { 'POST /api/auth/forgot-password': () => ({ status: 202 }) });

    await u.click(screen.getByRole('button', { name: 'Email me a link' }));
    expect(screen.getByText('Enter a valid email address, like name@company.com.')).toBeTruthy();

    await u.type(screen.getByLabelText('Email'), ' maya@fernhill.example ');
    await u.click(screen.getByRole('button', { name: 'Email me a link' }));
    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeTruthy();
    expect(screen.getByText('If there’s an account with maya@fernhill.example, we’ve emailed it a link to choose a new password. The link works for an hour.')).toBeTruthy();
    expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ email: 'maya@fernhill.example' });
  });

  it.each([
    [429, { title: 'Too many requests' }, 'Too many requests from your network. Try again in a minute.'],
    [503, { title: 'Email unavailable' }, 'We couldn’t send the email. Nothing was changed; try again in a moment.'],
  ])('explains a %i', async (status, body, shown) => {
    const u = userEvent.setup();
    renderAt({ pathname: '/forgot-password', state: { email: 'maya@fernhill.example' } }, { 'POST /api/auth/forgot-password': () => ({ status, body: { status, ...body } }) });
    await u.click(screen.getByRole('button', { name: 'Email me a link' }));
    expect(await alertText()).toBe(shown);
  });
});

describe('Reset password', () => {
  it('saves the new password and signs in with the tokens it returns', async () => {
    const u = userEvent.setup();
    const { calls } = renderAt('/reset-password/reset-tok', { 'POST /api/auth/reset-password': () => ({ body: tokens }) });

    await u.type(screen.getByLabelText('New password'), 'short');
    await u.click(screen.getByRole('button', { name: 'Save and sign in' }));
    expect(screen.getByText('Use at least 8 characters.', { selector: '[role=alert]' })).toBeTruthy();

    await u.clear(screen.getByLabelText('New password'));
    await u.type(screen.getByLabelText('New password'), 'a-new-password');
    await u.type(screen.getByLabelText('Confirm new password'), 'a-new-password');
    await u.click(screen.getByRole('button', { name: 'Save and sign in' }));

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeTruthy();
    expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ token: 'reset-tok', newPassword: 'a-new-password' });
    expect(getSession()?.userId).toBe('u1');
    expect(screen.getByText('Your new password is saved. You’re signed in here and signed out everywhere else.')).toBeTruthy();
  });

  it('offers a new link when this one is used up or expired', async () => {
    const u = userEvent.setup();
    renderAt('/reset-password/old-tok', {
      'POST /api/auth/reset-password': () => ({ status: 400, body: { status: 400, detail: 'This link is invalid or has expired. Ask for a new one.' } }),
    });
    await u.type(screen.getByLabelText('New password'), 'a-new-password');
    await u.type(screen.getByLabelText('Confirm new password'), 'a-new-password');
    await u.click(screen.getByRole('button', { name: 'Save and sign in' }));

    expect(await screen.findByRole('heading', { name: 'This link doesn’t work any more' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Get a new link' }).getAttribute('href')).toBe('/forgot-password');
    expect(getSession()).toBeNull();
  });
});
