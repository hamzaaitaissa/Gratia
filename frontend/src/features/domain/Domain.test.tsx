import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import type { DomainClaim, DomainVerification } from '../../api/types';
import { setSession } from '../../auth/session';
import { COMPANY, invitation, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { CompanySettingsPage } from '../admin/CompanySettingsPage';
import { AcceptInvitationPage } from '../auth/AcceptInvitationPage';
import { ClaimWorkspacePage } from '../auth/ClaimWorkspacePage';
import { ProfilePage } from '../profile/ProfilePage';

const admin = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin' });
const employee = user('u3', 'Priya Raman', 12, 188);
const record = { type: 'TXT', name: 'fernhilllabs.com', value: 'gratia-verify=0123456789abcdef0123456789abcdef' };
const unverified: DomainVerification = { domain: 'fernhilllabs.com', isVerified: false, verifiedAt: null, isAdmin: true, record: null };
const notFound = {
  status: 'NotFound',
  message: "The record isn't on fernhilllabs.com yet. DNS changes can take from a few minutes to a few hours to show up, so check again later.",
  invitationToken: null,
};

afterEach(() => vi.unstubAllGlobals());

const outcome = () => screen.getAllByRole('status').find((el) => el.closest('section'))!;
// What a status says, without its icon's ligature name
const said = (el: HTMLElement) => el.textContent!.replace(/^(check_circle|schedule|cloud_off|error)/, '');

describe('Domain verification in Company settings', () => {
  function renderSettings(routes: Parameters<typeof mockApi>[0]) {
    signIn(admin);
    const api = mockApi({ 'GET /api/user': () => ({ body: [admin] }), 'GET /api/company/me': () => ({ body: COMPANY }), ...routes });
    renderWithProviders(<CompanySettingsPage />, { route: '/admin/settings' });
    return api;
  }

  it('gives the admin a record, checks for it until it’s found, and says what verifying changed', async () => {
    const u = userEvent.setup();
    let state = unverified;
    let checks = 0;
    const { calls } = renderSettings({
      'GET /api/company/domain-verification': () => ({ body: state }),
      'POST /api/company/domain-verification': () => ({ body: (state = { ...state, record }) }),
      'POST /api/company/domain-verification/check': () => {
        if (checks++ === 0) return { body: notFound };
        state = { ...state, isVerified: true, verifiedAt: '2026-11-25T10:00:00Z', record: null };
        return {
          body: {
            status: 'Verified',
            message: 'fernhilllabs.com is verified. Admins can now invite people on any domain, with no daily limit. You can remove the record from the DNS now.',
            invitationToken: null,
          },
        };
      },
    });

    const card = await screen.findByRole('region', { name: 'Verify fernhilllabs.com' });
    expect(card.textContent).toContain('Until it’s verified, admins can only invite @fernhilllabs.com addresses');
    await u.click(within(card).getByRole('button', { name: 'Get your DNS record' }));

    // The record, ready to copy: the name is the domain, "@" at most providers
    expect(await within(card).findByText(record.value)).toBeTruthy();
    expect(within(card).getByText('TXT')).toBeTruthy();
    expect(card.textContent).toContain('At most DNS providers, that’s “@”, the root entry.');
    await u.click(within(card).getByRole('button', { name: 'Copy value' }));
    expect(await navigator.clipboard.readText()).toBe(record.value);
    expect(within(card).getByRole('button', { name: 'Copied value' })).toBeTruthy();

    await u.click(within(card).getByRole('button', { name: 'Check now' }));
    await waitFor(() => expect(said(outcome())).toBe(notFound.message));
    await u.click(within(card).getByRole('button', { name: 'Check again' }));

    // Found: the card says so, and the company (whose isDomainVerified lifts the domain rule) is fetched again
    expect(await screen.findByRole('region', { name: 'fernhilllabs.com is verified' })).toBeTruthy();
    expect(said(outcome())).toMatch(/^fernhilllabs.com is verified. Admins can now invite people on any domain/);
    await waitFor(() => expect(calls.filter((c) => c.path === '/api/company/me').length).toBeGreaterThan(1));
  });

  it('shows a verified domain, and nothing for a workspace without one', async () => {
    renderSettings({
      'GET /api/company/domain-verification': () => ({ body: { ...unverified, isVerified: true, verifiedAt: '2026-11-20T09:00:00Z' } }),
    });
    const card = await screen.findByRole('region', { name: 'fernhilllabs.com is verified' });
    expect(card.textContent).toMatch(/Verified on November 20, 2026\. Admins can invite people on any domain/);
    expect(within(card).queryByRole('button')).toBeNull();
  });

  it('shows nothing for a workspace founded with a personal address', async () => {
    renderSettings({ 'GET /api/company/domain-verification': () => ({ body: { ...unverified, domain: null } }) });
    expect(await screen.findByRole('button', { name: 'Save changes' })).toBeTruthy();
    expect(screen.queryByRole('region', { name: /fernhilllabs/ })).toBeNull();
  });

  it('starts over when the record is gone, and says when the network checks too often', async () => {
    const u = userEvent.setup();
    let state: DomainVerification = { ...unverified, record };
    let checks = 0;
    renderSettings({
      'GET /api/company/domain-verification': () => ({ body: state }),
      'POST /api/company/domain-verification/check': () => {
        if (checks++ === 0) return { status: 429, body: { status: 429, title: 'Too many requests' } };
        // An admin changed this member's email, which deletes the record
        state = { ...state, record: null };
        return { status: 400, body: { status: 400, detail: "Get your record first, add it to fernhilllabs.com's DNS settings, then check again." } };
      },
    });

    const card = await screen.findByRole('region', { name: 'Verify fernhilllabs.com' });
    await u.click(await within(card).findByRole('button', { name: 'Check now' }));
    await waitFor(() => expect(said(outcome())).toBe('Too many checks from your network. Wait a minute and try again.'));

    await u.click(within(card).getByRole('button', { name: 'Check now' }));
    await waitFor(() => expect(said(outcome())).toBe("Get your record first, add it to fernhilllabs.com's DNS settings, then check again."));
    expect(await within(card).findByRole('button', { name: 'Get your DNS record' })).toBeTruthy();
  });
});

describe('Domain verification in My profile', () => {
  function renderProfile(me: typeof admin, routes: Parameters<typeof mockApi>[0]) {
    signIn(me);
    const api = mockApi({ 'GET /api/user': () => ({ body: [me] }), 'GET /api/company/me': () => ({ body: COMPANY }), ...routes });
    renderWithProviders(<ProfilePage />, { route: '/me' });
    return api;
  }

  it('is how someone who manages the DNS becomes an admin', async () => {
    const u = userEvent.setup();
    let state: DomainVerification = { ...unverified, isAdmin: false };
    let me = employee;
    const { calls } = renderProfile(employee, {
      'GET /api/user': () => ({ body: [me] }),
      'GET /api/company/domain-verification': () => ({ body: state }),
      'POST /api/company/domain-verification': () => ({ body: (state = { ...state, record }) }),
      'POST /api/company/domain-verification/check': () => {
        state = { ...state, isVerified: true, isAdmin: true, record: null };
        me = { ...employee, role: 'Admin' };
        return {
          body: {
            status: 'Verified',
            message: 'fernhilllabs.com is verified, and you’re now an admin of Fernhill Labs. You can remove the record from the DNS now.',
            invitationToken: null,
          },
        };
      },
    });

    const card = await screen.findByRole('region', { name: 'Manage fernhilllabs.com’s DNS?' });
    expect(card.textContent).toContain('you become an admin of Fernhill Labs');
    await u.click(within(card).getByRole('button', { name: 'Get your DNS record' }));
    await u.click(await within(card).findByRole('button', { name: 'Check now' }));

    // Still here to say how it went, though admins find the card in Company settings
    const verified = await screen.findByRole('region', { name: 'fernhilllabs.com is verified' });
    expect(said(within(verified).getByRole('status'))).toMatch(/you’re now an admin of Fernhill Labs/);
    expect((await within(verified).findByRole('link', { name: 'Open Company settings' })).getAttribute('href')).toBe('/admin/settings');
    // The people list, where the role comes from, is fetched again
    await waitFor(() => expect(screen.getByText('Admin')).toBeTruthy());
    expect(calls.filter((c) => c.path === '/api/user').length).toBeGreaterThan(1);
  });

  it('isn’t shown to admins, who verify from Company settings', async () => {
    renderProfile(admin, { 'GET /api/company/domain-verification': () => ({ body: unverified }) });
    expect(await screen.findByRole('heading', { name: 'Maya Okonkwo' })).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole('region', { name: /fernhilllabs/ })).toBeNull());
  });
});

describe('Claim workspace', () => {
  const claim: DomainClaim = {
    companyName: 'Fernhill Labs',
    domain: 'fernhilllabs.com',
    email: 'it@fernhilllabs.com',
    isVerified: false,
    isUsed: false,
    record,
    expiresAt: '2026-12-02T10:00:00Z',
  };

  function renderClaim(routes: Parameters<typeof mockApi>[0]) {
    setSession(null);
    const api = mockApi(routes);
    renderWithProviders(
      <Routes>
        <Route path="/claim-workspace/:token" element={<ClaimWorkspacePage />} />
        <Route path="/accept-invitation/:token" element={<AcceptInvitationPage />} />
        <Route path="/create-workspace" element={<h1>Create workspace</h1>} />
      </Routes>,
      { route: '/claim-workspace/claim-1' },
    );
    return api;
  }

  it('shows the record, checks for it, and goes on to the admin invitation it brings', async () => {
    const u = userEvent.setup();
    let checks = 0;
    const message = 'fernhilllabs.com is verified. Choose a password to join Fernhill Labs as an admin. You can remove the record from the DNS now.';
    const { calls } = renderClaim({
      'GET /api/auth/domain-claim/claim-1': () => ({ body: claim }),
      'POST /api/auth/domain-claim/check': () => ({ body: checks++ === 0 ? notFound : { status: 'Verified', message, invitationToken: 'inv-1' } }),
      'GET /api/invitation/token/inv-1': () => ({
        body: invitation({ email: claim.email, invitedRole: 'Admin', invitedByUserName: '' }),
      }),
    });

    expect(await screen.findByRole('heading', { name: 'Claim Fernhill Labs' })).toBeTruthy();
    expect(screen.getByText(record.value)).toBeTruthy();
    expect(screen.getByText(/This link works until December 2, 2026/)).toBeTruthy();
    await u.click(screen.getByRole('button', { name: 'Check now' }));
    expect(await screen.findByText(notFound.message)).toBeTruthy();
    expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ token: 'claim-1' });

    await u.click(screen.getByRole('button', { name: 'Check again' }));
    // The accept page, worded for a workspace that already exists rather than a new one
    expect(await screen.findByRole('heading', { name: 'Join Fernhill Labs' })).toBeTruthy();
    expect(screen.getByText(message)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Join Fernhill Labs' })).toBeTruthy();
  });

  it('sends someone with an account there to sign in', async () => {
    const u = userEvent.setup();
    const message = 'fernhilllabs.com is verified, and your account is now an admin of Fernhill Labs. Sign in to continue.';
    renderClaim({
      'GET /api/auth/domain-claim/claim-1': () => ({ body: claim }),
      'POST /api/auth/domain-claim/check': () => ({ body: { status: 'Verified', message, invitationToken: null } }),
    });

    await u.click(await screen.findByRole('button', { name: 'Check now' }));
    expect(await screen.findByRole('heading', { name: 'You’re an admin of Fernhill Labs' })).toBeTruthy();
    expect(screen.getByText(message)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/sign-in');
  });

  it('explains a dead link', async () => {
    const detail = 'This link is invalid or has expired. Sign up again with your work email to get a new one.';
    renderClaim({ 'GET /api/auth/domain-claim/claim-1': () => ({ status: 404, body: { status: 404, detail } }) });

    expect(await screen.findByRole('heading', { name: 'This link doesn’t work any more' })).toBeTruthy();
    expect(screen.getByText(detail)).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Sign up again' }).getAttribute('href')).toBe('/create-workspace');
  });

  it('only continues a claim that was used, and says when it can’t', async () => {
    const u = userEvent.setup();
    const detail = 'This claim has been used. If you have an account, sign in. Otherwise sign up again with your work email to get a new link and record.';
    renderClaim({
      'GET /api/auth/domain-claim/claim-1': () => ({ body: { ...claim, isUsed: true, isVerified: true } }),
      'POST /api/auth/domain-claim/check': () => ({ status: 400, body: { status: 400, detail } }),
    });

    expect(await screen.findByText('You’ve already claimed Fernhill Labs with this link. Continue to finish joining it.')).toBeTruthy();
    expect(screen.queryByText(record.value)).toBeNull();
    expect(screen.getByText('Verified')).toBeTruthy();
    await u.click(screen.getByRole('button', { name: 'Continue' }));
    expect((await screen.findByText(detail, { exact: false })).closest('[role="status"]')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Go to sign in' }).getAttribute('href')).toBe('/sign-in');
  });
});
