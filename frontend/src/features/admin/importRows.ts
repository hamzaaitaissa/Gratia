import type { ImportPreviewRow, ImportResult, ImportRow, ImportRowStatus, ReadCompany } from '../../api/types';

/**
 * The review step of a bulk import (#54, design 6.13): the preview's rows, which the admin can fix (email and role of
 * a row with an error) or leave out. The API checks every row again on import, so these checks only keep the
 * "Send N invitations" count honest; the API has the last word.
 */
export interface ReviewRow extends ImportPreviewRow {
  excluded: boolean;
  /** Had an error, and the admin's edit fixed it */
  fixed: boolean;
}

export type RowFilter = 'all' | 'ready' | 'attention' | 'skipped';

const MAX_NAME = 200; // CompanyInvitation.MaxNameLength
const ROLES = ['Employee', 'Admin'];
const SKIPPED: ImportRowStatus[] = ['Duplicate', 'Member', 'Invited'];

const normalize = (email: string) => email.trim().toLowerCase();
const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e);

export const fromPreview = (rows: ImportPreviewRow[]): ReviewRow[] => rows.map((r) => ({ ...r, excluded: false, fixed: false }));

/** The API's checks of one row (InvitationImport.Check), for a row the admin edited. */
export function recheck(row: ReviewRow, rows: ReviewRow[], company: Pick<ReadCompany, 'domain' | 'isDomainVerified'> | undefined): ReviewRow {
  const email = normalize(row.email);
  const domain = company?.domain && !company.isDomainVerified ? company.domain.toLowerCase() : null;
  const earlier = rows.find((r) => r.row !== row.row && !r.excluded && r.status !== 'Error' && normalize(r.email) === email);
  const problem = !email
    ? 'There’s no email.'
    : email.length > 254 || !isEmail(email)
      ? 'This isn’t an email address.'
      : row.fullName.length > MAX_NAME
        ? `The full name is longer than ${MAX_NAME} characters.`
        : row.jobTitle.length > MAX_NAME
          ? `The job title is longer than ${MAX_NAME} characters.`
          : !ROLES.includes(row.role)
            ? 'The role must be Employee or Admin.'
            : domain && email.split('@')[1] !== domain
              ? `Until your company’s domain is verified, you can only invite @${domain} addresses.`
              : earlier
                ? `The same email as row ${earlier.row}.`
                : null;
  return { ...row, status: problem ? 'Error' : 'Ready', message: problem, fixed: !problem };
}

export function counts(rows: ReviewRow[]) {
  let ready = 0;
  let attention = 0;
  let skipped = 0;
  for (const r of rows) {
    if (r.excluded || SKIPPED.includes(r.status)) skipped++;
    else if (r.status === 'Error') attention++;
    else ready++;
  }
  return { ready, attention, skipped, total: rows.length };
}

export function inFilter(r: ReviewRow, filter: RowFilter) {
  if (filter === 'ready') return !r.excluded && r.status === 'Ready';
  if (filter === 'attention') return !r.excluded && r.status === 'Error';
  if (filter === 'skipped') return r.excluded || SKIPPED.includes(r.status);
  return true;
}

/** What the import sends: the rows still Ready. The others would only come back skipped. */
export const toImport = (rows: ReviewRow[]): ImportRow[] =>
  rows
    .filter((r) => !r.excluded && r.status === 'Ready')
    .map((r) => ({ row: r.row, email: r.email.trim(), fullName: r.fullName, jobTitle: r.jobTitle, role: r.role }));

export interface SkippedRow {
  row: number;
  email: string;
  fullName: string;
  jobTitle: string;
  role: string;
  reason: string;
}

/** Every row that wasn't invited: the ones the review left out, then the ones the import skipped, by row. */
export function skippedRows(rows: ReviewRow[], result: ImportResult): SkippedRow[] {
  const byRow = new Map(rows.map((r) => [r.row, r]));
  const out: SkippedRow[] = rows
    .filter((r) => r.excluded || r.status !== 'Ready')
    .map((r) => ({ ...pick(r), reason: r.excluded ? 'Excluded during review' : (r.message ?? r.status) }));
  for (const s of result.skipped) {
    const r = byRow.get(s.row);
    out.push({ ...(r ? pick(r) : { row: s.row, email: s.email, fullName: '', jobTitle: '', role: '' }), email: s.email, reason: s.reason });
  }
  return out.sort((a, b) => a.row - b.row);
}

const pick = (r: ReviewRow) => ({ row: r.row, email: r.email, fullName: r.fullName, jobTitle: r.jobTitle, role: r.role });
