import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router';
import { mockApi, renderWithProviders, signIn, tx, user } from '../../test/utils';
import { RecognitionPage } from './RecognitionPage';

const me = user('u1', 'Maya Okonkwo', 18, 142);
const priya = user('u3', 'Priya Raman', 12, 188);

function renderDetail(response: { status?: number; body?: unknown }, route: string | { pathname: string; state?: unknown } = '/recognition/t1') {
  signIn(me);
  const api = mockApi({ 'GET /api/transaction/t1': () => response });
  renderWithProviders(
    <Routes>
      <Route path="/recognition/:id" element={<RecognitionPage />} />
    </Routes>,
    { route },
  );
  return api;
}

it('shows the whole recognition on the large card', async () => {
  const long = 'Priya stayed late to get the accessibility fixes into the release. '.repeat(5).trim();
  renderDetail({ body: tx('t1', priya, me, 5, long) });

  const card = await screen.findByRole('article', { name: 'Priya Raman recognized you with 5 points for Teamwork' });
  expect(card.textContent).toContain('FromPriya Raman');
  expect(card.textContent).toContain('ToYou');
  expect(screen.getByText(`“${long}”`)).toBeTruthy(); // never cut short, unlike in the feed
  expect(screen.getByRole('link', { name: 'Home' }).getAttribute('href')).toBe('/');
});

it('goes back to where it was opened from', async () => {
  renderDetail({ body: tx('t1', priya, me, 5, 'Thanks!') }, { pathname: '/recognition/t1', state: { back: '/activity?tab=received', backLabel: 'My activity' } });

  expect((await screen.findByRole('link', { name: 'My activity' })).getAttribute('href')).toBe('/activity?tab=received');
});

it('ignores a back link that would leave the app', async () => {
  renderDetail({ body: tx('t1', priya, me, 5, 'Thanks!') }, { pathname: '/recognition/t1', state: { back: '//evil.example', backLabel: 'Back' } });

  expect((await screen.findByRole('link', { name: 'Home' })).getAttribute('href')).toBe('/');
});

it.each([403, 404])('explains that only the sender and receiver can open it (%i)', async (status) => {
  renderDetail({ status, body: { status } });

  expect(await screen.findByRole('heading', { name: 'This recognition isn’t available' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Go to Home' }).getAttribute('href')).toBe('/');
});

it('offers a retry when something else goes wrong', async () => {
  renderDetail({ status: 500, body: { status: 500, traceId: '00-xyz-01' } });

  expect(await screen.findByRole('heading', { name: 'We couldn’t load this page' })).toBeTruthy();
  expect(screen.getByText('00-xyz-01')).toBeTruthy();
});
