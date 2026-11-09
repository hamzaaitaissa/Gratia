import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import type { ReadUser } from '../../api/types';
import { COMPANY, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { GiveProvider } from '../give/GiveContext';
import { PeoplePage } from './PeoplePage';
import { PersonPage } from './PersonPage';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin', jobTitle: 'Product Designer' });
const priya = user('u3', 'Priya Raman', 12, 188, { jobTitle: 'Senior Backend Engineer', role: 'Admin' });
const emile = user('u6', 'Émile Durand', 25, 9, { jobTitle: 'Support Specialist' });
const zoe = user('u5', 'Zoe Chen', 21, 4, { jobTitle: '' });

function renderAt(route: string, people: ReadUser[] = [me, priya, emile, zoe]) {
  signIn(me);
  mockApi({
    'GET /api/user': () => ({ body: people }),
    'GET /api/company/me': () => ({ body: COMPANY }),
  });
  renderWithProviders(
    <GiveProvider>
      <Routes>
        <Route path="/people" element={<PeoplePage />} />
        <Route path="/people/:id" element={<PersonPage />} />
        <Route path="/me" element={<h1>My profile page</h1>} />
      </Routes>
    </GiveProvider>,
    { route },
  );
}

describe('People', () => {
  it('files everyone A–Z, accents included, and marks you and the admins', async () => {
    renderAt('/people');

    expect((await screen.findAllByRole('heading', { level: 2 })).map((h) => h.textContent)).toEqual(['E', 'M', 'P', 'Z']);
    expect(screen.getByText('4 people')).toBeTruthy();
    const maya = screen.getByRole('link', { name: /Maya Okonkwo/ });
    expect(maya.textContent).toContain('(you)');
    expect(maya.getAttribute('href')).toBe('/me');
    expect(screen.getByRole('link', { name: /Priya Raman/ }).textContent).toContain('Admin');
    expect(screen.getByRole('link', { name: /Zoe Chen/ }).textContent).toContain('No job title');
    // You can recognize anyone but yourself
    expect(screen.queryByRole('button', { name: 'Recognize Maya Okonkwo' })).toBeNull();
  });

  it('searches by name or job title, and says when nobody matches', async () => {
    const u = userEvent.setup();
    renderAt('/people');
    const search = await screen.findByRole('searchbox', { name: 'Search by name or job title' });

    await u.type(search, 'backend');
    expect(screen.getAllByRole('listitem').map((li) => li.textContent)).toEqual([expect.stringContaining('Priya Raman')]);
    expect(screen.getByText('1 person')).toBeTruthy();

    await u.clear(search);
    await u.type(search, 'nobody');
    expect(screen.getByText('No one matches “nobody”')).toBeTruthy();
    await u.click(screen.getByRole('button', { name: 'Clear search' }));
    expect(screen.getAllByRole('listitem')).toHaveLength(4);
  });

  it('opens Give recognition with the colleague already chosen', async () => {
    renderAt('/people');
    await userEvent.click(await screen.findByRole('button', { name: 'Recognize Priya Raman' }));

    const dialog = await screen.findByRole('dialog', { name: 'Give recognition' });
    expect((await within(dialog).findByRole('group', { name: 'Colleague' })).textContent).toContain('Priya Raman');
  });

  it('says when it’s just you, and lets an admin invite people', async () => {
    renderAt('/people', [me]);

    expect(await screen.findByRole('heading', { name: 'It’s just you so far' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Invite people' }).getAttribute('href')).toBe('/admin/invitations');
  });
});

describe('Person profile', () => {
  it('shows a colleague’s details and lets you recognize them', async () => {
    renderAt('/people/u3');

    expect(await screen.findByRole('heading', { level: 1, name: 'Priya Raman' })).toBeTruthy();
    expect(screen.getByText('Senior Backend Engineer')).toBeTruthy();
    expect(screen.getByRole('link', { name: priya.email }).getAttribute('href')).toBe(`mailto:${priya.email}`);
    expect(screen.getByText('Points received').nextElementSibling?.textContent).toContain('188');
    expect(screen.getByRole('link', { name: 'People' }).getAttribute('href')).toBe('/people');

    await userEvent.click(screen.getByRole('button', { name: 'Recognize' }));
    const dialog = await screen.findByRole('dialog', { name: 'Give recognition' });
    expect((await within(dialog).findByRole('group', { name: 'Colleague' })).textContent).toContain('Priya Raman');
  });

  it('sends you to My profile for yourself', async () => {
    renderAt('/people/u1');
    expect(await screen.findByRole('heading', { name: 'My profile page' })).toBeTruthy();
  });

  it('is a 404 for someone who isn’t in your company', async () => {
    renderAt('/people/someone-else');
    expect(await screen.findByRole('heading', { name: 'We can’t find that page' })).toBeTruthy();
  });
});
