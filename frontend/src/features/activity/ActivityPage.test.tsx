import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReadTransaction } from '../../api/types';
import { COMPANY, mockApi, renderWithProviders, signIn, tx, user } from '../../test/utils';
import { GiveProvider } from '../give/GiveContext';
import { ActivityPage } from './ActivityPage';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin' });
const priya = user('u3', 'Priya Raman', 12, 188);
const tomas = user('u4', 'Tomás Herrera', 20, 74);
const DAY = 60 * 24;

function renderActivity(history: ReadTransaction[], route = '/activity') {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ body: [me, priya, tomas] }),
    'GET /api/company/me': () => ({ body: COMPANY }),
    'GET /api/transaction/my-history': () => ({ body: history }),
  });
  renderWithProviders(
    <GiveProvider>
      <ActivityPage />
    </GiveProvider>,
    { route },
  );
}

const history = [
  tx('t1', priya, me, 5, 'Thanks for the review.', 60),
  { ...tx('t2', me, tomas, 3, 'Great demo!', 120), typeOfDonation: 'Innovation' },
  tx('t3', tomas, me, 2, 'From an earlier cycle.', 40 * DAY), // before the last refill, whatever today is
];

it('shows tabs with counts and this cycle’s summary', async () => {
  renderActivity(history);

  const tabs = await screen.findAllByRole('tab');
  expect(tabs.map((t) => t.textContent)).toEqual(['All3', 'Received2', 'Sent1']);
  expect(tabs[0]!.getAttribute('aria-selected')).toBe('true');
  expect(screen.getAllByRole('article')).toHaveLength(3);

  const panel = screen.getByRole('tabpanel');
  expect(panel.textContent).toContain('Received this month5 points'); // the older gift isn't this cycle's
  expect(panel.textContent).toContain('Given this month3 of 25 points to give');
});

it('switches tabs with the mouse and the arrow keys', async () => {
  const u = userEvent.setup();
  renderActivity(history);

  await u.click(await screen.findByRole('tab', { name: /Received/ }));
  expect(screen.getAllByRole('article').map((a) => a.getAttribute('aria-label'))).toEqual([
    'Priya Raman recognized you with 5 points for Teamwork',
    'Tomás Herrera recognized you with 2 points for Teamwork',
  ]);

  await u.keyboard('{ArrowRight}');
  expect(screen.getByRole('tab', { name: /Sent/ }).getAttribute('aria-selected')).toBe('true');
  expect(document.activeElement).toBe(screen.getByRole('tab', { name: /Sent/ }));
  expect(screen.getAllByRole('article')).toHaveLength(1);
});

it('filters by category, and offers to clear a filter with no match', async () => {
  const u = userEvent.setup();
  renderActivity(history);

  const filters = await screen.findByRole('group', { name: 'Filter by category' });
  await u.click(within(filters).getByRole('button', { name: 'Innovation' }));
  expect(screen.getAllByRole('article')).toHaveLength(1);

  // Received has only Teamwork, so there's nothing to filter there
  await u.click(screen.getByRole('tab', { name: /Received/ }));
  expect(screen.queryByRole('group', { name: 'Filter by category' })).toBeNull();
});

it('shows eight at a time', async () => {
  const many = Array.from({ length: 10 }, (_, i) => tx('m' + i, priya, me, 1, 'Thanks #' + i, i + 1));
  renderActivity(many);

  expect(await screen.findByText('Showing 8 of 10')).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
  expect(screen.getByText('Showing 10 of 10')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull();
});

it('explains each empty tab and nudges toward giving', async () => {
  renderActivity([], '/activity?tab=sent');

  expect(await screen.findByRole('heading', { name: 'You haven’t given any recognition yet' })).toBeTruthy();
  expect(screen.getByText('You have 18 points to give this month. Who made your week better?')).toBeTruthy();

  await userEvent.click(screen.getByRole('button', { name: 'Give recognition' }));
  expect(await screen.findByRole('dialog', { name: 'Give recognition' })).toBeTruthy();
});
