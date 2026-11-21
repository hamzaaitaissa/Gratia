import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import type { ImportPreviewRow, ImportRow } from '../../api/types';
import { csvCell } from '../../lib/files';
import { COMPANY, mockApi, renderWithProviders, signIn, user } from '../../test/utils';
import { ImportPage } from './ImportPage';
import { counts, fromPreview, recheck, skippedRows, toImport } from './importRows';

const me = user('u1', 'Maya Okonkwo', 18, 142, { role: 'Admin' });

const row = (n: number, email: string, status: ImportPreviewRow['status'] = 'Ready', message: string | null = null, extra: Partial<ImportPreviewRow> = {}): ImportPreviewRow => ({
  row: n,
  email,
  fullName: `Person ${n}`,
  jobTitle: 'Engineer',
  role: 'Employee',
  status,
  message,
  ...extra,
});

const PREVIEW = [
  row(2, 'elena@fernhilllabs.com'),
  row(3, 'not-an-email', 'Error', 'This isn’t an email address.'),
  row(4, 'elena@fernhilllabs.com', 'Duplicate', 'The same email as row 2.'),
  row(5, 'priya.raman@fernhilllabs.com', 'Member', 'Already a member.'),
  row(6, 'tomas@fernhilllabs.com', 'Ready', null, { role: 'Admin' }),
  row(7, '=HYPERLINK("x")', 'Error', 'This isn’t an email address.'),
];

let saved: Blob[] = [];
// jsdom's Blob has no text()
const readText = (b: Blob) => new Promise<string>((resolve) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result as string);
  reader.readAsText(b);
});
beforeEach(() => {
  saved = [];
  URL.createObjectURL = vi.fn((b: Blob) => {
    saved.push(b);
    return 'blob:file';
  });
  URL.revokeObjectURL = vi.fn();
  // jsdom can't follow a download link
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
});
afterEach(() => vi.restoreAllMocks());

function renderImport(routes: Parameters<typeof mockApi>[0] = {}) {
  signIn(me);
  const api = mockApi({
    'GET /api/company/me': () => ({ body: COMPANY }),
    'GET /api/invitation/import/template': () => ({ body: 'xlsx bytes' }),
    'POST /api/invitation/import/preview': () => ({ body: { rows: PREVIEW, ready: 2 } }),
    ...routes,
  });
  renderWithProviders(
    <Routes>
      <Route path="/admin/invitations/import" element={<ImportPage />} />
      <Route path="/admin/invitations" element={<h1>Invitations page</h1>} />
    </Routes>,
    { route: '/admin/invitations/import' },
  );
  return api;
}

const xlsx = (name = 'new-hires.xlsx', size = 2048) => new File([new Uint8Array(size)], name, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
const upload = (file: File) => fireEvent.change(document.querySelector('input[type=file]')!, { target: { files: [file] } });

async function toReview(u: ReturnType<typeof userEvent.setup>) {
  await u.click(await screen.findByRole('button', { name: 'I have my file' }));
  upload(xlsx());
  return screen.findByRole('heading', { name: 'Review 6 rows' });
}

describe('Import from spreadsheet', () => {
  it('downloads the template, then reviews, fixes and sends the rows', async () => {
    const u = userEvent.setup();
    const { calls } = renderImport({
      // The daily cap (#65) skips the last row the import was sent
      'POST /api/invitation/import': (req) => {
        const rows = (req.body as { rows: ImportRow[] }).rows;
        return { body: { invited: rows.length - 1, skipped: [{ row: rows.at(-1)!.row, email: rows.at(-1)!.email, status: 'Error', reason: 'Your company can send 50 invitation emails a day.' }] } };
      },
    });

    await u.click(await screen.findByRole('button', { name: 'Download template' }));
    expect(await screen.findByText('Downloaded gratia-invitations.xlsx.')).toBeTruthy();
    expect(saved).toHaveLength(1);

    await toReview(u);
    expect(screen.getByText('2 ready · 2 need attention · 2 skipped · from new-hires.xlsx')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send 2 invitations' })).toBeTruthy();

    // Fix row 3's email: it's checked again on the spot
    const email = screen.getByLabelText('Email for row 3');
    await u.clear(email);
    await u.type(email, 'zoe@fernhilllabs.com');
    expect(screen.getByText('Fixed. Will be sent.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send 3 invitations' })).toBeTruthy();

    // Leave row 7 out; the filter shows it with the skipped
    await u.click(screen.getByRole('button', { name: 'Exclude row 7' }));
    await u.click(screen.getByRole('button', { name: '3 skipped' }));
    expect(screen.getAllByRole('row').slice(1).map((r) => (r as HTMLTableRowElement).cells[0]!.textContent)).toEqual(['4', '5', '7']);
    expect(screen.getByText('Row 7: You excluded this row')).toBeTruthy();

    await u.click(screen.getByRole('button', { name: 'Send 3 invitations' }));
    expect(await screen.findByRole('heading', { name: '2 invitations created' })).toBeTruthy();
    expect(calls.find((c) => c.path === '/api/invitation/import')?.body).toEqual({
      rows: [
        { row: 2, email: 'elena@fernhilllabs.com', fullName: 'Person 2', jobTitle: 'Engineer', role: 'Employee' },
        { row: 3, email: 'zoe@fernhilllabs.com', fullName: 'Person 3', jobTitle: 'Engineer', role: 'Employee' },
        { row: 6, email: 'tomas@fernhilllabs.com', fullName: 'Person 6', jobTitle: 'Engineer', role: 'Admin' },
      ],
    });

    // Every row that wasn't invited, with why: the review's and the import's
    const skipped = screen.getByText('Skipped rows').parentElement!;
    expect(within(skipped).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      'Row 4elena@fernhilllabs.comThe same email as row 2.',
      'Row 5priya.raman@fernhilllabs.comAlready a member.',
      'Row 6tomas@fernhilllabs.comYour company can send 50 invitation emails a day.',
      'Row 7=HYPERLINK("x")Excluded during review',
    ]);
    await u.click(screen.getByRole('button', { name: 'Download skipped rows' }));
    const csv = await readText(saved.at(-1)!);
    expect(csv).toContain('Row,Email,Full name,Job title,Role,Why it was skipped\r\n');
    // A spreadsheet app mustn't run a cell as a formula
    expect(csv).toContain(`7,"'=HYPERLINK(""x"")",Person 7,Engineer,Employee,Excluded during review\r\n`);

    await u.click(screen.getByRole('button', { name: 'Go to pending invitations' }));
    expect(await screen.findByRole('heading', { name: 'Invitations page' })).toBeTruthy();
  });

  it.each([
    [xlsx('people.csv'), 'That file type isn’t supported'],
    [xlsx('huge.xlsx', 1024 * 1024 + 1), 'This file is too large'],
    [xlsx('empty.xlsx', 0), 'This file is empty'],
  ])('checks the file before uploading it: %s', async (file, title) => {
    const u = userEvent.setup();
    const { calls } = renderImport();
    await u.click(await screen.findByRole('button', { name: 'I have my file' }));
    upload(file);
    expect((await screen.findByRole('alert')).textContent).toContain(title);
    expect(calls.some((c) => c.path.includes('preview'))).toBe(false);
  });

  it('explains what the API couldn’t read in the file', async () => {
    const u = userEvent.setup();
    renderImport({
      'POST /api/invitation/import/preview': () => ({
        status: 400,
        body: { status: 400, detail: 'The first row must name the columns: Email, Full name, Job title and Role. Download the template and fill that in.' },
      }),
    });
    await u.click(await screen.findByRole('button', { name: 'I have my file' }));
    upload(xlsx());
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('No Email column found');
    expect(alert.textContent).toContain('The first row of “new-hires.xlsx” needs a column named Email.');
    expect(within(alert).getByRole('button', { name: 'Download the template' })).toBeTruthy();
  });

  it('asks before leaving with invitations ready to send', async () => {
    const u = userEvent.setup();
    renderImport();
    await toReview(u);

    await u.click(screen.getByRole('link', { name: 'Back to Invitations' }));
    const confirm = screen.getByRole('alertdialog', { name: 'Leave without sending?' });
    expect(confirm.textContent).toContain('You have 2 invitations ready to send.');
    await u.click(within(confirm).getByRole('button', { name: 'Stay' }));
    expect(screen.getByRole('heading', { name: 'Review 6 rows' })).toBeTruthy();

    await u.click(screen.getByRole('link', { name: 'Back to Invitations' }));
    await u.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Leave' }));
    expect(await screen.findByRole('heading', { name: 'Invitations page' })).toBeTruthy();
  });
});

describe('importRows', () => {
  const company = { domain: 'fernhilllabs.com', isDomainVerified: false };

  it('checks an edited row like the API does', () => {
    const rows = fromPreview(PREVIEW);
    const at = (n: number) => rows.find((r) => r.row === n)!;
    expect(recheck({ ...at(3), email: 'zoe@elsewhere.com' }, rows, company).message).toBe('Until your company’s domain is verified, you can only invite @fernhilllabs.com addresses.');
    expect(recheck({ ...at(3), email: 'zoe@elsewhere.com' }, rows, { ...company, isDomainVerified: true }).status).toBe('Ready');
    expect(recheck({ ...at(3), email: 'Elena@fernhilllabs.com' }, rows, company).message).toBe('The same email as row 2.');
    expect(recheck({ ...at(3), email: 'zoe@fernhilllabs.com', role: 'Manager' }, rows, company).message).toBe('The role must be Employee or Admin.');
    expect(recheck({ ...at(3), email: ' ' }, rows, company).message).toBe('There’s no email.');
  });

  it('counts, sends only the Ready rows and lists the rest', () => {
    const rows = fromPreview(PREVIEW).map((r) => (r.row === 6 ? { ...r, excluded: true } : r));
    expect(counts(rows)).toEqual({ ready: 1, attention: 2, skipped: 3, total: 6 });
    expect(toImport(rows).map((r) => r.row)).toEqual([2]);
    expect(skippedRows(rows, { invited: 1, skipped: [] }).map((s) => [s.row, s.reason])).toEqual([
      [3, 'This isn’t an email address.'],
      [4, 'The same email as row 2.'],
      [5, 'Already a member.'],
      [6, 'Excluded during review'],
      [7, 'This isn’t an email address.'],
    ]);
  });

  it('keeps CSV cells from running as formulas', () => {
    expect(['=1+1', '+1', '-1', '@SUM(A1)', '\tx', 'plain', 'a,b', 'say "hi"'].map(csvCell)).toEqual([
      "'=1+1",
      "'+1",
      "'-1",
      "'@SUM(A1)",
      "'\tx",
      'plain',
      '"a,b"',
      '"say ""hi"""',
    ]);
  });
});
