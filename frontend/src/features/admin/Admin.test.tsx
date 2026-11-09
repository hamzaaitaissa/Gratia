import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Invitation, ReadCompany, ReadUser } from '../../api/types';
import { COMPANY, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { CompanySettingsPage } from './CompanySettingsPage';
import { InvitationsPage } from './InvitationsPage';
import { MembersPage } from './MembersPage';
import { RequireAdmin } from './RequireAdmin';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin' });
const priya = user('u3', 'Priya Raman', 12, 188, { jobTitle: 'Senior Backend Engineer' });
const zoe = user('u5', 'Zoe Chen', 21, 4);
const gone = user('u7', 'Daniel Novak', 0, 52, { deactivatedAt: '2026-11-01T10:00:00Z' });

describe('Members', () => {
  function renderMembers(routes: Parameters<typeof mockApi>[0] = {}) {
    signIn(me);
    const api = mockApi({
      'GET /api/user': () => ({ body: [me, priya, zoe] }),
      'GET /api/user/deactivated': () => ({ body: [gone] }),
      'GET /api/company/me': () => ({ body: COMPANY }),
      ...routes,
    });
    renderWithProviders(<MembersPage />, { route: '/admin/members' });
    return api;
  }

  it('lists members in a table you can sort', async () => {
    const u = userEvent.setup();
    renderMembers();

    const table = await screen.findByRole('table');
    // The first cell holds the avatar (initials), the name and "(you)"
    const names = () => within(table).getAllByRole('row').slice(1).map((r) => [...r.querySelectorAll('td:first-child span:not([aria-hidden])')].map((s) => s.textContent).join(''));
    expect(names()).toEqual(['Maya Okonkwo(you)', 'Priya Raman', 'Zoe Chen']);

    await u.click(within(table).getByRole('button', { name: /Points received/ }));
    expect(names()).toEqual(['Priya Raman', 'Maya Okonkwo(you)', 'Zoe Chen']); // most first
    expect(within(table).getByRole('columnheader', { name: /Points received/ }).getAttribute('aria-sort')).toBe('descending');
    // You can't deactivate yourself
    expect((screen.getByRole('button', { name: 'Deactivate Maya Okonkwo' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('edits a member without touching their password', async () => {
    const u = userEvent.setup();
    const { calls } = renderMembers({ 'PUT /api/user/u3': (req) => ({ body: { ...priya, ...(req.body as object) } }) });

    await u.click(await screen.findByRole('button', { name: 'Edit Priya Raman' }));
    const dialog = screen.getByRole('dialog', { name: 'Edit member' });
    await u.clear(within(dialog).getByLabelText(/Job title/));
    await u.type(within(dialog).getByLabelText(/Job title/), 'Staff Engineer');
    await u.selectOptions(within(dialog).getByLabelText('Role'), 'Admin');
    await u.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Changes to Priya Raman saved.')).toBeTruthy();
    expect(calls.find((c) => c.method === 'PUT')?.body).toEqual({
      id: 'u3',
      fullName: 'Priya Raman',
      email: priya.email,
      jobTitle: 'Staff Engineer',
      role: 'Admin',
    });
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('shows a duplicate email next to the field', async () => {
    const u = userEvent.setup();
    renderMembers({ 'PUT /api/user/u3': () => ({ status: 409, body: { status: 409, detail: 'Another account already uses this email' } }) });

    await u.click(await screen.findByRole('button', { name: 'Edit Priya Raman' }));
    await u.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Another account already uses this email.')).toBeTruthy();
    expect(screen.getByRole('dialog', { name: 'Edit member' })).toBeTruthy();
  });

  it('deactivates after a confirmation, and reactivates', async () => {
    const u = userEvent.setup();
    const { calls } = renderMembers({
      'POST /api/user/u5/deactivate': () => ({ status: 204 }),
      'POST /api/user/u7/reactivate': () => ({ status: 204 }),
    });

    await u.click(await screen.findByRole('button', { name: 'Deactivate Zoe Chen' }));
    const confirm = screen.getByRole('alertdialog', { name: 'Deactivate Zoe Chen?' });
    await waitFor(() => expect(document.activeElement?.textContent).toBe('Cancel')); // the safe choice has focus
    await u.click(within(confirm).getByRole('button', { name: 'Deactivate' }));
    expect(await screen.findByText('Zoe Chen was deactivated and can’t sign in any more.')).toBeTruthy();

    const deactivated = screen.getByRole('region', { name: 'Deactivated' });
    await u.click(within(deactivated).getByRole('button', { name: 'Reactivate Daniel Novak' }));
    expect(await screen.findByText('Daniel Novak can sign in again.')).toBeTruthy();
    expect(calls.filter((c) => c.method === 'POST').map((c) => c.path)).toEqual(['/api/user/u5/deactivate', '/api/user/u7/reactivate']);
  });
});

describe('Invitations', () => {
  const pending: Invitation = {
    id: 'i1',
    email: 'clara.jensen@fernhilllabs.com',
    expiresAt: new Date(Date.now() + 5 * 864e5 + 3600e3).toISOString(),
    isUsed: false,
    invitedRole: 'Employee',
    companyName: 'Fernhill Labs',
    invitedByUserName: 'Maya Okonkwo',
    createdAt: new Date(Date.now() - 2 * 864e5).toISOString(),
    isExpired: false,
    isValid: true,
  };

  function renderInvitations(routes: Parameters<typeof mockApi>[0] = {}, company: ReadCompany = COMPANY) {
    signIn(me);
    const api = mockApi({
      'GET /api/invitation/pending': () => ({ body: [pending] }),
      'GET /api/company/me': () => ({ body: company }),
      ...routes,
    });
    renderWithProviders(<InvitationsPage />, { route: '/admin/invitations' });
    return api;
  }

  it('invites someone, with the domain rule spelled out while it applies', async () => {
    const u = userEvent.setup();
    const { calls } = renderInvitations({ 'POST /api/invitation': (req) => ({ status: 201, body: { ...pending, id: 'i2', email: (req.body as { email: string }).email } }) });

    expect(await screen.findByText(/Until fernhilllabs.com is verified, you can only invite @fernhilllabs.com addresses/)).toBeTruthy();
    await u.type(screen.getByLabelText('Email'), 'elena.petrova@fernhilllabs.com');
    await u.click(screen.getByRole('radio', { name: 'Admin' }));
    await u.click(screen.getByRole('button', { name: 'Send invitation' }));

    expect(await screen.findByText('Invitation sent to elena.petrova@fernhilllabs.com.')).toBeTruthy();
    expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ email: 'elena.petrova@fernhilllabs.com', role: 'Admin' });
    expect((screen.getByLabelText('Email') as HTMLInputElement).value).toBe('');
  });

  it.each([
    [400, 'Until your company\'s domain is verified, you can only invite @fernhilllabs.com addresses', 'Until your company\'s domain is verified, you can only invite @fernhilllabs.com addresses'],
    [409, 'This person\'s account was deactivated. Reactivate it instead of inviting them again.', 'This person’s account was deactivated. Reactivate them in Members instead of inviting them again.'],
    [409, 'An active invitation already exists for this email in the company', 'There’s already a pending invitation for this email. Resend it below instead.'],
  ])('explains a %i next to the email', async (status, detail, shown) => {
    const u = userEvent.setup();
    renderInvitations({ 'POST /api/invitation': () => ({ status, body: { status, detail } }) });
    await u.type(await screen.findByLabelText('Email'), 'someone@elsewhere.example');
    await u.click(screen.getByRole('button', { name: 'Send invitation' }));

    expect(await screen.findByText(shown)).toBeTruthy();
  });

  it('lists pending invitations, resends and cancels after a confirmation', async () => {
    const u = userEvent.setup();
    const { calls } = renderInvitations({
      'POST /api/invitation/i1/resend': () => ({ body: pending }),
      'DELETE /api/invitation/i1': () => ({ status: 204 }),
    });

    const row = (await screen.findByText('clara.jensen@fernhilllabs.com')).closest('tr')!;
    expect(row.textContent).toContain('Maya Okonkwo');
    expect(row.textContent).toContain('2 days ago');
    expect(row.textContent).toContain('PendingExpires in 6 days');

    await u.click(within(row).getByRole('button', { name: 'Resend the invitation to clara.jensen@fernhilllabs.com' }));
    expect(await screen.findByText('New invitation sent to clara.jensen@fernhilllabs.com. The earlier link no longer works.')).toBeTruthy();

    await u.click(within(row).getByRole('button', { name: 'Cancel the invitation to clara.jensen@fernhilllabs.com' }));
    await u.click(within(screen.getByRole('alertdialog', { name: 'Cancel this invitation?' })).getByRole('button', { name: 'Cancel invitation' }));
    expect(await screen.findByText('Invitation for clara.jensen@fernhilllabs.com cancelled.')).toBeTruthy();
    expect(calls.filter((c) => c.method !== 'GET').map((c) => `${c.method} ${c.path}`)).toEqual(['POST /api/invitation/i1/resend', 'DELETE /api/invitation/i1']);
  });

  it('marks an expired invitation and resends it', async () => {
    const u = userEvent.setup();
    const expired: Invitation = { ...pending, id: 'i9', email: 'late@fernhilllabs.com', isExpired: true, isValid: false, expiresAt: new Date(Date.now() - 2 * 864e5).toISOString() };
    const { calls } = renderInvitations({
      'GET /api/invitation/pending': () => ({ body: [pending, expired] }),
      'POST /api/invitation/i9/resend': () => ({ body: { ...expired, isExpired: false } }),
    });

    const row = (await screen.findByText('late@fernhilllabs.com')).closest('tr')!;
    expect(row.textContent).toMatch(/ExpiredExpired [A-Z][a-z]{2} \d{1,2}/);
    await u.click(within(row).getByRole('button', { name: 'Resend the invitation to late@fernhilllabs.com' }));
    expect(await screen.findByText('New invitation sent to late@fernhilllabs.com.')).toBeTruthy();
    expect(calls.some((c) => c.path === '/api/invitation/i9/resend')).toBe(true);
  });

  it('says when the email couldn’t be sent, and that nothing changed', async () => {
    const u = userEvent.setup();
    renderInvitations({
      'POST /api/invitation': () => ({ status: 503, body: { status: 503, title: 'Email unavailable' } }),
      'POST /api/invitation/i1/resend': () => ({ status: 503, body: { status: 503 } }),
    });
    await u.type(await screen.findByLabelText('Email'), 'elena@fernhilllabs.com');
    await u.click(screen.getByRole('button', { name: 'Send invitation' }));
    expect(await screen.findByText('We couldn’t send the email. Nothing was changed; try again in a moment.')).toBeTruthy();
    expect((screen.getByLabelText('Email') as HTMLInputElement).value).toBe('elena@fernhilllabs.com'); // kept, to try again

    await u.click(screen.getByRole('button', { name: 'Resend the invitation to clara.jensen@fernhilllabs.com' }));
    expect(await screen.findAllByText('We couldn’t send the email. Nothing was changed; try again in a moment.')).toHaveLength(2);
  });

  it('says when nobody is waiting', async () => {
    renderInvitations({ 'GET /api/invitation/pending': () => ({ body: [] }) });
    expect(await screen.findByText('No pending invitations')).toBeTruthy();
  });
});

describe('Company settings', () => {
  function renderSettings(routes: Parameters<typeof mockApi>[0] = {}) {
    signIn(me);
    const api = mockApi({
      'GET /api/company/me': () => ({ body: COMPANY }),
      'PUT /api/company/c1': (req) => ({ body: { ...COMPANY, ...(req.body as object) } }),
      ...routes,
    });
    renderWithProviders(<CompanySettingsPage />, { route: '/admin/settings' });
    return api;
  }

  it('only saves when something changed, and explains an adjusted colour', async () => {
    const u = userEvent.setup();
    renderSettings();
    const save = (await screen.findByRole('button', { name: 'Save changes' })) as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    expect(screen.queryByText(/We adjusted your colour/)).toBeNull(); // navy is fine as it is

    await u.click(screen.getByRole('button', { name: 'Yellow' }));
    expect(save.disabled).toBe(false);
    expect(screen.getByText('Unsaved changes')).toBeTruthy();
    expect(screen.getByText(/Buttons and highlights use #[0-9A-F]{6} instead of #FFD60A/)).toBeTruthy();

    await u.click(screen.getByRole('button', { name: 'Discard' }));
    expect((screen.getByLabelText('Primary colour') as HTMLInputElement).value).toBe('#1B2A4A');
  });

  it('saves, deriving the secondary colour when it’s left empty', async () => {
    const u = userEvent.setup();
    const { calls } = renderSettings();
    await u.click(await screen.findByRole('button', { name: 'Clear secondary colour' }));
    const derived = (screen.getByLabelText(/Secondary colour/) as HTMLInputElement).placeholder.replace('Derived: ', '');
    await u.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText(/Company settings saved/)).toBeTruthy();
    expect(calls.find((c) => c.method === 'PUT')?.body).toEqual({
      id: 'c1',
      name: 'Fernhill Labs',
      logoUrl: null,
      primaryColor: '#1B2A4A',
      secondaryColor: derived,
      monthlyPointsAllowance: 25,
      pointsResetDay: 21,
    });
    expect(derived).toMatch(/^#[0-9A-F]{6}$/);
  });

  it('sets the monthly allowance and the refill day', async () => {
    const u = userEvent.setup();
    const { calls } = renderSettings();
    expect(await screen.findByText(/A new number applies from the next refill/)).toBeTruthy();

    await u.clear(screen.getByLabelText('Points to give each month'));
    await u.type(screen.getByLabelText('Points to give each month'), '40');
    await u.selectOptions(screen.getByLabelText('Refill day'), '5');
    expect((screen.getByLabelText('Refill day') as HTMLSelectElement).selectedOptions[0]?.textContent).toBe('The 5th of each month');
    await u.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText(/Company settings saved/)).toBeTruthy();
    expect(calls.find((c) => c.method === 'PUT')?.body).toMatchObject({ monthlyPointsAllowance: 40, pointsResetDay: 5 });
  });

  it('keeps the allowance between 1 and 1000', async () => {
    const u = userEvent.setup();
    const { calls } = renderSettings();
    await u.clear(await screen.findByLabelText('Points to give each month'));
    await u.type(screen.getByLabelText('Points to give each month'), '1001');
    await u.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(screen.getByText('Choose a whole number from 1 to 1000.')).toBeTruthy();
    expect(calls.some((c) => c.method === 'PUT')).toBe(false);
  });

  it('checks the fields before saving', async () => {
    const u = userEvent.setup();
    const { calls } = renderSettings();
    await u.clear(await screen.findByLabelText('Company name'));
    await u.type(screen.getByLabelText(/Logo URL/), 'ftp://logo');
    await u.clear(screen.getByLabelText('Primary colour'));
    await u.type(screen.getByLabelText('Primary colour'), 'navy');
    await u.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(screen.getByText('Enter your company name.')).toBeTruthy();
    expect(screen.getByText('Use a full link starting with https://')).toBeTruthy();
    expect(screen.getByText('Use a hex colour like #1B2A4A.')).toBeTruthy();
    expect(calls.some((c) => c.method === 'PUT')).toBe(false);
  });
});

it('shows employees a 403 instead of an admin page', async () => {
  const employee: ReadUser = { ...zoe, role: 'Employee' };
  signIn(employee);
  mockApi({ 'GET /api/user': () => ({ body: [employee] }), 'GET /api/company/me': () => ({ body: COMPANY }) });
  renderWithProviders(
    <RequireAdmin>
      <h1>Secret admin page</h1>
    </RequireAdmin>,
  );

  expect(await screen.findByRole('heading', { name: 'You don’t have access to this page' })).toBeTruthy();
  expect(screen.queryByText('Secret admin page')).toBeNull();
});
