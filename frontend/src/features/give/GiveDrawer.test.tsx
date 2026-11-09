import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { CreateTransaction } from '../../api/types';
import { COMPANY, mockApi, renderWithProviders, signIn, tx, user } from '../../test/utils';
import { GiveProvider, useGive } from './GiveContext';

const priya = user('u3', 'Priya Raman', 12, 188, { jobTitle: 'Senior Backend Engineer' });
const tomas = user('u4', 'Tomás Herrera', 20, 74, { jobTitle: 'Customer Success Lead' });

function Opener() {
  const { openGive } = useGive();
  return (
    <button type="button" onClick={() => openGive()}>
      Open
    </button>
  );
}

/** A tiny in-memory API: the balance goes down when a gift is accepted. */
function setup(available: number, onGive?: (body: CreateTransaction, state: { available: number }) => { status?: number; body?: unknown } | undefined) {
  const state = { available };
  const me = () => user('u1', 'Maya Okonkwo', state.available, 142, { role: 'Admin' });
  signIn(me());
  const api = mockApi({
    'GET /api/user': () => ({ body: [me(), priya, tomas] }),
    'GET /api/company/me': () => ({ body: COMPANY }),
    'POST /api/transaction': (req) => {
      const body = req.body as CreateTransaction;
      const custom = onGive?.(body, state);
      if (custom) return custom;
      state.available -= body.amount;
      return { body: { ...tx('t9', me(), priya, body.amount, body.message), typeOfDonation: body.typeOfDonation } };
    },
  });
  renderWithProviders(
    <GiveProvider>
      <Opener />
    </GiveProvider>,
  );
  return api;
}

async function openDrawer() {
  const u = userEvent.setup();
  await u.click(screen.getByRole('button', { name: 'Open' }));
  const dialog = await screen.findByRole('dialog', { name: 'Give recognition' });
  return { u, dialog };
}

it('checks the form, then sends the gift and celebrates', async () => {
  const { calls } = setup(18);
  const { u, dialog } = await openDrawer();
  const combo = await within(dialog).findByRole('combobox', { name: 'Colleague' });

  await u.click(within(dialog).getByRole('button', { name: /Give 3 points/ }));
  expect(within(dialog).getByText('Choose who you want to recognize.')).toBeTruthy();
  expect(within(dialog).getByText('Pick a category or write your own.')).toBeTruthy();
  expect(within(dialog).getByText('Write a short message so they know what it was for.')).toBeTruthy();
  expect(calls.some((c) => c.method === 'POST')).toBe(false);

  await u.type(combo, 'backend'); // job titles are searchable too
  expect(within(dialog).getAllByRole('option')).toHaveLength(1);
  await u.keyboard('{Enter}');
  expect(within(dialog).getByRole('group', { name: 'Colleague' }).textContent).toContain('Priya Raman');

  await u.click(within(dialog).getByRole('button', { name: '5 points' }));
  expect(within(dialog).getByText('You’ll have 13 of 25 points to give left.')).toBeTruthy();
  await u.click(within(dialog).getByRole('button', { name: 'Teamwork' }));
  await u.type(within(dialog).getByLabelText('Message'), 'Thanks for pairing!');
  expect(within(dialog).getByRole('article').textContent).toContain('Thanks for pairing!'); // the live preview

  await u.click(within(dialog).getByRole('button', { name: 'Give 5 points' }));

  await within(dialog).findByText('Thank you, sent');
  expect(calls.find((c) => c.method === 'POST')?.body).toEqual({
    receiverId: 'u3',
    amount: 5,
    message: 'Thanks for pairing!',
    typeOfDonation: 'Teamwork',
  });
  expect(dialog.textContent).toContain('You gave 5 points to Priya Raman.');
  await waitFor(() => expect(dialog.textContent).toContain('13 of 25 points to give left'));
});

it('sends a category of your own with “Something else”', async () => {
  const { calls } = setup(18);
  const { u, dialog } = await openDrawer();
  await u.type(await within(dialog).findByRole('combobox', { name: 'Colleague' }), 'tom');
  await u.keyboard('{Enter}');
  await u.click(within(dialog).getByRole('button', { name: 'Something else' }));
  await u.type(within(dialog).getByLabelText('Your category'), 'Calm under pressure');
  await u.type(within(dialog).getByLabelText('Message'), 'Thanks for staying on the call.');
  await u.click(within(dialog).getByRole('button', { name: 'Give 3 points' }));

  await within(dialog).findByText('Thank you, sent');
  expect(calls.find((c) => c.method === 'POST')?.body).toMatchObject({ receiverId: 'u4', typeOfDonation: 'Calm under pressure' });
});

it('explains when the balance changed before sending', async () => {
  setup(18, (_body, state) => {
    state.available = 3; // spent in another tab meanwhile
    return { status: 400, body: { status: 400, title: 'Bad request', detail: 'You only have 3 points left to give this month.' } };
  });
  const { u, dialog } = await openDrawer();
  await u.type(await within(dialog).findByRole('combobox', { name: 'Colleague' }), 'pri');
  await u.keyboard('{Enter}');
  await u.click(within(dialog).getByRole('button', { name: '5 points' }));
  await u.click(within(dialog).getByRole('button', { name: 'Leadership' }));
  await u.type(within(dialog).getByLabelText('Message'), 'Thank you!');
  await u.click(within(dialog).getByRole('button', { name: 'Give 5 points' }));

  const alert = await within(dialog).findByText('Not enough points to give');
  expect(alert.parentElement?.textContent).toContain('You have 3 points to give left');
  expect(within(dialog).getByText('3 to give')).toBeTruthy(); // the header pill shows the fresh balance
});

it('says so when the colleague no longer exists', async () => {
  setup(18, () => ({ status: 404, body: { status: 404, title: 'Not found', detail: 'Receiver not found' } }));
  const { u, dialog } = await openDrawer();
  await u.type(await within(dialog).findByRole('combobox', { name: 'Colleague' }), 'pri');
  await u.keyboard('{Enter}');
  await u.click(within(dialog).getByRole('button', { name: 'Teamwork' }));
  await u.type(within(dialog).getByLabelText('Message'), 'Thanks!');
  await u.click(within(dialog).getByRole('button', { name: 'Give 3 points' }));

  expect(await within(dialog).findByText('Colleague not found')).toBeTruthy();
  expect(dialog.textContent).toContain('We couldn’t find Priya Raman.');
});

it('shows when there are no points left to give, and closes', async () => {
  setup(0);
  const { u, dialog } = await openDrawer();
  expect(await within(dialog).findByText('You’ve given all your points this month.')).toBeTruthy();
  expect(within(dialog).queryByRole('combobox')).toBeNull();

  await u.click(within(dialog).getByRole('button', { name: 'Got it' }));
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('closes with Escape, but Escape in the colleague list only closes the list', async () => {
  setup(18);
  const { u, dialog } = await openDrawer();
  const combo = await within(dialog).findByRole('combobox', { name: 'Colleague' });
  await u.click(combo);
  expect(within(dialog).getByRole('listbox')).toBeTruthy();

  await u.keyboard('{Escape}');
  expect(within(dialog).queryByRole('listbox')).toBeNull();
  expect(screen.getByRole('dialog')).toBeTruthy();

  await u.keyboard('{Escape}');
  expect(screen.queryByRole('dialog')).toBeNull();
});
