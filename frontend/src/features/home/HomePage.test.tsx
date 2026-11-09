import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReadTransaction } from '../../api/types';
import { COMPANY, mockApi, renderWithProviders, signIn, tx, user } from '../../test/utils';
import { GiveProvider } from '../give/GiveContext';
import { HomePage } from './HomePage';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin' });
const priya = user('u3', 'Priya Raman', 12, 188);
const tomas = user('u4', 'Tomás Herrera', 20, 74);
const zoe = user('u5', 'Zoe Chen', 21, 0);

const page = (transactions: ReadTransaction[], pageNo: number, totalCount: number, pageSize = 10) => ({
  transactions,
  page: pageNo,
  pageSize,
  totalCount,
  totalPages: Math.ceil(totalCount / pageSize),
});

function renderHome(feed: (pageNo: number) => ReturnType<typeof page>, received: ReadTransaction[] = []) {
  signIn(me);
  const api = mockApi({
    'GET /api/user': () => ({ body: [me, priya, tomas, zoe] }),
    'GET /api/company/me': () => ({ body: COMPANY }),
    'GET /api/transaction/received': () => ({ body: received }),
    'GET /api/transaction/company-history': (req) => ({ body: feed(Number(new URL(req.path, 'http://x').searchParams.get('page'))) }),
  });
  renderWithProviders(
    <GiveProvider>
      <HomePage />
    </GiveProvider>,
  );
  return api;
}

it('shows both balances, the feed and the most recognized', async () => {
  const fromPriya = tx('t2', priya, me, 4, 'Great design review.', 30);
  renderHome(() => page([tx('t1', tomas, priya, 3, 'Saved the Ortega account.'), fromPriya], 1, 2), [fromPriya]);

  expect(await screen.findByRole('heading', { level: 1, name: /^Good (morning|afternoon|evening), Maya$/ })).toBeTruthy();

  const give = screen.getByRole('region', { name: 'Points to give' });
  expect(give.textContent).toContain('18of 25 left');
  const received = screen.getByRole('region', { name: 'Points received' });
  expect(received.textContent).toContain('142');
  expect(await within(received).findByText('+4 from Priya Raman')).toBeTruthy();

  const feed = screen.getByRole('region', { name: 'Company feed' });
  expect(within(feed).getByText('Showing 2 of 2')).toBeTruthy();
  expect(within(feed).getAllByRole('article')).toHaveLength(2);
  expect(within(feed).getByRole('article', { name: 'Priya Raman recognized you with 4 points for Teamwork' })).toBeTruthy();
  expect(within(feed).getByText('You’re all caught up.')).toBeTruthy();

  // Ranked by points received; nobody with 0 is listed
  const most = screen.getByRole('complementary', { name: 'Most recognized' });
  expect(within(most).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
    expect.stringContaining('Priya Raman'),
    expect.stringContaining('Maya Okonkwo'),
    expect.stringContaining('Tomás Herrera'),
  ]);
});

it('uses the company’s own allowance and refill day', async () => {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ body: [me, priya] }),
    'GET /api/company/me': () => ({
      body: { ...COMPANY, monthlyPointsAllowance: 40, pointsResetDay: 5, nextPointsResetAt: new Date(Date.now() + 3 * 864e5).toISOString() },
    }),
    'GET /api/transaction/received': () => ({ body: [] }),
    'GET /api/transaction/company-history': () => ({ body: page([], 1, 0) }),
  });
  renderWithProviders(
    <GiveProvider>
      <HomePage />
    </GiveProvider>,
  );

  const give = await screen.findByRole('region', { name: 'Points to give' });
  expect(give.textContent).toContain('18of 40 left');
  expect(give.textContent).toContain('Refills in 3 days');
});

it('loads the feed ten at a time', async () => {
  const many = Array.from({ length: 12 }, (_, i) => tx('t' + i, tomas, priya, 1, 'Thanks #' + i, i));
  const { calls } = renderHome((p) => page(many.slice((p - 1) * 10, p * 10), p, 12));

  const feed = await screen.findByRole('region', { name: 'Company feed' });
  expect(within(feed).getByText('Showing 10 of 12')).toBeTruthy();

  await userEvent.click(within(feed).getByRole('button', { name: 'Load more' }));

  expect(await within(feed).findByText('Showing 12 of 12')).toBeTruthy();
  expect(calls.filter((c) => c.path.startsWith('/api/transaction/company-history')).map((c) => c.path)).toEqual([
    '/api/transaction/company-history?page=1&pageSize=10',
    '/api/transaction/company-history?page=2&pageSize=10',
  ]);
});

it('nudges toward giving when nobody has recognized anyone yet', async () => {
  renderHome(() => page([], 1, 0));

  expect(await screen.findByRole('heading', { name: 'No recognitions yet' })).toBeTruthy();
  expect(screen.getByText(/When someone at Fernhill Labs says thank you/)).toBeTruthy();
  expect(screen.getByText('When colleagues recognize you, it adds up here.')).toBeTruthy();
});

it('keeps the shell and offers a retry when the feed fails', async () => {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ body: [me] }),
    'GET /api/transaction/received': () => ({ body: [] }),
    'GET /api/transaction/company-history': () => ({ status: 500, body: { status: 500, title: 'Error', traceId: '00-abc-01' } }),
  });
  renderWithProviders(
    <GiveProvider>
      <HomePage />
    </GiveProvider>,
  );

  expect(await screen.findByRole('heading', { name: 'We couldn’t load this page' })).toBeTruthy();
  expect(screen.getByText('00-abc-01')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Retry' })).toBeTruthy();
});
