import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type DragEvent, type ReactNode } from 'react';
import { useBlocker, useNavigate } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError, NetworkError } from '../../api/http';
import { queryKeys, useCompany } from '../../api/queries';
import type { ImportResult, UserRole } from '../../api/types';
import { useIsMobile } from '../../app/useIsMobile';
import { ConfirmDialog } from '../../components/Dialog';
import { Icon, type IconName } from '../../components/Icon';
import { PageHeader } from '../../components/Page';
import { BackLink } from '../../components/States';
import { useToast } from '../../components/Toast';
import { saveFile, toCsv } from '../../lib/files';
import { plural } from '../../lib/format';
import styles from './Admin.module.css';
import { counts, fromPreview, inFilter, recheck, skippedRows, toImport, type ReviewRow, type RowFilter, type SkippedRow } from './importRows';
import im from './Import.module.css';

const MAX_BYTES = 1024 * 1024;
const offline = 'We couldn’t reach Gratia. Check your connection and try again.';
const broken = 'Something went wrong on our side. Try again in a moment.';

type Step = 1 | 2 | 3 | 4;
interface UploadProblem {
  title: string;
  body: string;
  /** The fix is the template: offer it right there */
  template?: boolean;
}

/**
 * Bulk import from a spreadsheet (design 6.13): Download → Upload → Review → Done. "Send N invitations" always
 * matches the Ready rows. The API reads Excel workbooks only, so unlike the design there's no CSV upload.
 */
export function ImportPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { data: company } = useCompany();
  const [step, setStep] = useState<Step>(1);
  const [fileName, setFileName] = useState('');
  const [rows, setRows] = useState<ReviewRow[]>([]);
  const [filter, setFilter] = useState<RowFilter>('all');
  const [problem, setProblem] = useState<UploadProblem | null>(null);
  const [skipped, setSkipped] = useState<SkippedRow[]>([]);
  const [invited, setInvited] = useState(0);
  const [sendFailure, setSendFailure] = useState('');
  const heading = useRef<HTMLHeadingElement>(null);

  // Each step's heading takes the focus, so a screen reader hears where the flow is now
  const firstStep = useRef(true);
  useEffect(() => {
    if (firstStep.current) firstStep.current = false;
    else heading.current?.focus();
  }, [step]);

  const template = useMutation({
    mutationFn: api.importTemplate,
    onSuccess: ({ blob, fileName: name }) => {
      saveFile(blob, name);
      toast(`Downloaded ${name}.`);
    },
    onError: (err) => toast(err instanceof NetworkError ? offline : broken, 'error'),
  });

  const preview = useMutation({
    mutationFn: api.previewImport,
    onSuccess: (result) => {
      if (result.rows.length === 0)
        return setProblem({ title: 'This file is empty', body: `We found no people in “${fileName}”. Add one person per row below the header row, and upload it again.` });
      setRows(fromPreview(result.rows));
      setFilter('all');
      setStep(3);
    },
    onError: (err) => setProblem(previewProblem(err, fileName)),
  });

  // The rows as they were sent, so the result is read against what the API saw
  const send = useMutation({
    mutationFn: (sent: ReviewRow[]) => api.importInvitations(toImport(sent)),
    onSuccess: (result: ImportResult, sent) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.invitations });
      setInvited(result.invited);
      setSkipped(skippedRows(sent, result));
      setStep(4);
    },
    onError: (err) => setSendFailure(err instanceof ApiError && err.status === 400 && err.problem.detail ? err.problem.detail : err instanceof NetworkError ? offline : broken),
  });

  const onFile = (file: File | undefined) => {
    if (!file) return;
    setProblem(null);
    setFileName(file.name);
    if (!/\.xlsx$/i.test(file.name))
      return setProblem({
        title: 'That file type isn’t supported',
        body: `“${file.name}” isn’t an Excel workbook (.xlsx). If it’s a CSV or another spreadsheet, open it and save it as .xlsx, then upload that.`,
      });
    if (file.size > MAX_BYTES)
      return setProblem({
        title: 'This file is too large',
        body: `“${file.name}” is ${(file.size / MAX_BYTES).toFixed(1)} MB and the limit is 1 MB. Remove extra sheets or split it into smaller files.`,
      });
    if (file.size === 0) return setProblem({ title: 'This file is empty', body: `“${file.name}” has nothing in it. Add one person per row and upload it again.` });
    preview.mutate(file);
  };

  const c = counts(rows);
  const reviewing = step === 3 && c.ready > 0 && !send.isPending;
  // Leaving the review loses it: ask first, for links, Back and the browser's own back button alike
  const blocker = useBlocker(({ currentLocation, nextLocation }) => reviewing && currentLocation.pathname !== nextLocation.pathname);
  useEffect(() => {
    if (!reviewing) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [reviewing]);

  const restart = () => {
    setRows([]);
    setProblem(null);
    setSendFailure('');
    setStep(2);
  };

  const editRow = (n: number, change: Partial<Pick<ReviewRow, 'email' | 'role'>>) =>
    setRows((all) => all.map((r) => (r.row === n ? recheck({ ...r, ...change }, all, company) : r)));
  const setExcluded = (n: number, excluded: boolean) => setRows((all) => all.map((r) => (r.row === n ? { ...r, excluded } : r)));

  const stepHeading = (text: ReactNode, id: string) => (
    <h2 id={id} ref={heading} tabIndex={-1} className={im.stepTitle}>
      {text}
    </h2>
  );

  return (
    <>
      <BackLink to="/admin/invitations">Back to Invitations</BackLink>
      <PageHeader title="Import from spreadsheet" sub="Invite up to 500 people at once." />
      <div className={im.page}>
        <Stepper step={step} />

        {step === 1 && (
          <section className={im.panel} aria-labelledby="import-download">
            <div>
              {stepHeading('Start from our template', 'import-download')}
              <p className={im.lead}>The template has the right columns, an example row and a sheet of instructions. Add one person per row, then come back and upload it.</p>
            </div>
            <div className={im.columns}>
              {(
                [
                  ['Email', true, 'Where we send the invitation.'],
                  ['Full name', false, 'Pre-fills their profile. They can change it.'],
                  ['Job title', false, 'Shown next to their name.'],
                  ['Role', false, 'Employee or Admin. Empty means Employee.'],
                ] as const
              ).map(([name, required, note]) => (
                <div key={name} className={im.column}>
                  <div className={im.columnName}>
                    {name} <span className={required ? im.required : im.optional}>{required ? 'Required' : 'Optional'}</span>
                  </div>
                  <div className={im.columnNote}>{note}</div>
                </div>
              ))}
            </div>
            <p className={im.note}>Already have a list? Any Excel workbook works, as long as its first row has an Email column.</p>
            <div className={im.actions}>
              <button type="button" className="btn btn-secondary" disabled={template.isPending} aria-busy={template.isPending} onClick={() => template.mutate()}>
                <Icon name="download" />
                {template.isPending ? 'Downloading…' : 'Download template'}
              </button>
              <button type="button" className="btn btn-primary" onClick={() => setStep(2)}>
                I have my file
                <Icon name="arrow_forward" />
              </button>
            </div>
          </section>
        )}

        {step === 2 && (
          <Upload
            heading={stepHeading('Upload your file', 'import-upload')}
            checking={preview.isPending ? fileName : null}
            problem={problem}
            onFile={onFile}
            onTemplate={() => template.mutate()}
            onBack={() => {
              setProblem(null);
              setStep(1);
            }}
          />
        )}

        {step === 3 && (
          <Review
            heading={stepHeading(`Review ${plural(c.total, 'row')}`, 'import-review')}
            rows={rows}
            fileName={fileName}
            filter={filter}
            onFilter={setFilter}
            onEdit={editRow}
            onExclude={setExcluded}
            onRestart={restart}
            sending={send.isPending}
            failure={sendFailure}
            onSend={() => {
              setSendFailure('');
              send.mutate(rows);
            }}
          />
        )}

        {step === 4 && (
          <section className={im.panel} aria-labelledby="import-done">
            <span aria-hidden="true" className={im.doneIcon}>
              <Icon name="mark_email_read" size={32} />
            </span>
            <div role="status">
              {stepHeading(`${plural(invited, 'invitation')} created`, 'import-done')}
              <p className={im.lead}>Emails go out in the background, so they may take a few minutes to arrive. The new people already appear in your pending invitations.</p>
            </div>
            {skipped.length > 0 && (
              <div className={im.skipped}>
                <div className={im.skippedHead}>Skipped rows</div>
                <ul className={im.skippedList}>
                  {skipped.map((s) => (
                    <li key={s.row}>
                      <span className={im.rowNumber}>Row {s.row}</span>
                      <span className={im.skippedEmail}>{s.email || '(no email)'}</span>
                      <span className={im.skippedReason}>{s.reason}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
            <div className={im.actions}>
              {skipped.length > 0 && (
                <button type="button" className="btn btn-secondary" onClick={() => saveFile(skippedCsv(skipped), 'gratia-skipped-rows.csv')}>
                  <Icon name="download" />
                  Download skipped rows
                </button>
              )}
              <button type="button" className="btn btn-primary" onClick={() => navigate('/admin/invitations')}>
                Go to pending invitations
              </button>
            </div>
          </section>
        )}
      </div>

      {blocker.state === 'blocked' && (
        <ConfirmDialog
          title="Leave without sending?"
          body={`You have ${plural(c.ready, 'invitation')} ready to send. If you leave now, you’ll need to upload the file again.`}
          confirmLabel="Leave"
          cancelLabel="Stay"
          onConfirm={() => blocker.proceed()}
          onCancel={() => blocker.reset()}
        />
      )}
    </>
  );
}

/** The skipped rows with the template's columns, so the list can be fixed and saved as .xlsx to import again. */
function skippedCsv(rows: SkippedRow[]) {
  return toCsv([['Row', 'Email', 'Full name', 'Job title', 'Role', 'Why it was skipped'], ...rows.map((r) => [r.row, r.email, r.fullName, r.jobTitle, r.role, r.reason])]);
}

/** The preview's 400s (a file it can't read, over 500 people, no Email column), each with its own title. */
function previewProblem(err: unknown, fileName: string): UploadProblem {
  if (err instanceof NetworkError) return { title: 'We couldn’t check the file', body: offline };
  const detail = err instanceof ApiError && err.status === 400 ? err.problem.detail : null;
  if (!detail) return { title: 'We couldn’t check the file', body: broken };
  if (/first row must name the columns/i.test(detail)) return { title: 'No Email column found', body: `The first row of “${fileName}” needs a column named Email.`, template: true };
  if (/500/.test(detail)) return { title: 'Too many rows', body: `${detail}` };
  if (/workbook/i.test(detail)) return { title: 'We couldn’t read this file', body: detail, template: true };
  return { title: 'We couldn’t use this file', body: detail };
}

const STEPS = ['Download', 'Upload', 'Review', 'Done'];

function Stepper({ step }: { step: Step }) {
  return (
    <ol aria-label="Import progress" className={im.stepper}>
      {STEPS.map((label, i) => {
        const n = (i + 1) as Step;
        const state = n < step ? 'done' : n === step ? 'current' : 'todo';
        return (
          <li key={label} aria-current={state === 'current' ? 'step' : undefined} className={im.stepItem} data-state={state}>
            <span aria-hidden="true" className={im.stepCircle}>
              {state === 'done' ? <Icon name="check" size={16} /> : n}
            </span>
            <span className={im.stepLabel}>
              {label}
              <span className="sr-only">{state === 'done' ? ' (done)' : state === 'current' ? ' (current step)' : ''}</span>
            </span>
            {i < STEPS.length - 1 && <span aria-hidden="true" className={im.stepLine} />}
          </li>
        );
      })}
    </ol>
  );
}

function Upload({
  heading,
  checking,
  problem,
  onFile,
  onTemplate,
  onBack,
}: {
  heading: ReactNode;
  checking: string | null;
  problem: UploadProblem | null;
  onFile: (f: File | undefined) => void;
  onTemplate: () => void;
  onBack: () => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [drag, setDrag] = useState(false);
  const drop = (e: DragEvent) => {
    e.preventDefault();
    setDrag(false);
    onFile(e.dataTransfer.files[0]);
  };

  return (
    <section className={im.panel} aria-labelledby="import-upload">
      <div>
        {heading}
        <p className={im.lead}>We check every row before anything is sent.</p>
      </div>
      {problem && !checking && (
        <div role="alert" className={im.problem}>
          <Icon name="error" size={22} className={im.problemIcon} />
          <div className={im.problemText}>
            <strong>{problem.title}</strong>
            <span>{problem.body}</span>
            {problem.template && (
              <button type="button" className={im.problemLink} onClick={onTemplate}>
                Download the template
              </button>
            )}
          </div>
        </div>
      )}
      {checking ? (
        <div role="status" className={im.checking}>
          <div className={im.checkingHead}>
            <Icon name="description" size={28} className={im.checkingIcon} />
            <div style={{ minWidth: 0 }}>
              <div className={im.checkingTitle}>Checking {checking}…</div>
              <div className={im.columnNote}>Reading rows and looking for duplicates and existing members.</div>
            </div>
          </div>
          <div aria-hidden="true" className={im.progress}>
            <div className={im.progressBar} />
          </div>
        </div>
      ) : (
        <>
          <button
            type="button"
            className={im.drop}
            data-drag={drag || undefined}
            data-problem={problem ? true : undefined}
            aria-describedby="import-file-h"
            onClick={() => input.current?.click()}
            onDragOver={(e) => {
              e.preventDefault();
              setDrag(true);
            }}
            onDragLeave={() => setDrag(false)}
            onDrop={drop}
          >
            <span aria-hidden="true" className={im.dropIcon}>
              <Icon name="upload_file" size={28} />
            </span>
            <span className={im.dropTitle}>{drag ? 'Drop the file to upload it' : 'Drag your file here, or choose one'}</span>
            <span id="import-file-h" className={im.columnNote}>
              Excel (.xlsx) · up to 1 MB · up to 500 people
            </span>
            <span aria-hidden="true" className={im.dropButton}>
              Choose a file
            </span>
          </button>
          <input
            ref={input}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            tabIndex={-1}
            aria-hidden="true"
            hidden
            onChange={(e) => {
              onFile(e.target.files?.[0]);
              e.target.value = '';
            }}
          />
          <div>
            <button type="button" className={im.quiet} onClick={onBack}>
              Back
            </button>
          </div>
        </>
      )}
    </section>
  );
}

const BADGES: Record<ReviewRow['status'] | 'excluded', [string, IconName, string | undefined]> = {
  Ready: ['Ready', 'check_circle', im.ok],
  Error: ['Error', 'error', im.danger],
  Duplicate: ['Duplicate in file', 'content_copy', im.warn],
  Member: ['Already a member', 'how_to_reg', im.neutral],
  Invited: ['Already invited', 'schedule_send', im.info],
  excluded: ['Excluded', 'block', im.neutral],
};

function Badge({ row }: { row: ReviewRow }) {
  const [label, icon, tone] = BADGES[row.excluded ? 'excluded' : row.status];
  return (
    <span className={`${im.badge} ${tone}`}>
      <Icon name={icon} size={15} />
      {label}
    </span>
  );
}

interface ReviewProps {
  heading: ReactNode;
  rows: ReviewRow[];
  fileName: string;
  filter: RowFilter;
  onFilter: (f: RowFilter) => void;
  onEdit: (row: number, change: Partial<Pick<ReviewRow, 'email' | 'role'>>) => void;
  onExclude: (row: number, excluded: boolean) => void;
  onRestart: () => void;
  sending: boolean;
  failure: string;
  onSend: () => void;
}

function Review({ heading, rows, fileName, filter, onFilter, onEdit, onExclude, onRestart, sending, failure, onSend }: ReviewProps) {
  const isMobile = useIsMobile();
  const c = counts(rows);
  const needs = `${c.attention} need${c.attention === 1 ? 's' : ''} attention`;
  const shown = rows.filter((r) => inFilter(r, filter));
  const chips: [RowFilter, string][] = [
    ['all', `All ${c.total}`],
    ['ready', `${c.ready} ready`],
    ['attention', needs],
    ['skipped', `${c.skipped} skipped`],
  ];

  // A row with an error stays editable once fixed, so its input doesn't vanish mid-word ("…@acme.co" before the "m")
  const editable = (r: ReviewRow) => !r.excluded && (r.status === 'Error' || r.fixed);
  const failing = (r: ReviewRow) => !r.excluded && r.status === 'Error';
  // Not when it only repeats the badge ("Already a member." under "Already a member")
  const reason = (r: ReviewRow) =>
    r.excluded ? 'You excluded this row' : r.message?.replace(/\.$/, '').toLowerCase() === BADGES[r.status][0].toLowerCase() ? null : r.message;
  const emailInput = (r: ReviewRow) => (
    <>
      <label htmlFor={`import-email-${r.row}`} className={isMobile ? im.cardLabel : 'sr-only'}>
        {isMobile ? 'Email' : `Email for row ${r.row}`}
      </label>
      <input
        id={`import-email-${r.row}`}
        className={`input ${im.cellInput}`}
        type="email"
        value={r.email}
        aria-invalid={failing(r) ? true : undefined}
        aria-describedby={reason(r) ? `import-reason-${r.row}` : undefined}
        onChange={(e) => onEdit(r.row, { email: e.target.value })}
      />
    </>
  );
  const roleSelect = (r: ReviewRow) => (
    <select
      id={`import-role-${r.row}`}
      aria-label={isMobile ? undefined : `Role for row ${r.row}`}
      className={im.cellSelect}
      value={r.role}
      onChange={(e) => onEdit(r.row, { role: e.target.value as UserRole })}
    >
      {!['Employee', 'Admin'].includes(r.role) && (
        <option value={r.role} disabled>
          {r.role || '(none)'}
        </option>
      )}
      <option value="Employee">Employee</option>
      <option value="Admin">Admin</option>
    </select>
  );
  const notes = (r: ReviewRow) => (
    <>
      {reason(r) && (
        <span id={`import-reason-${r.row}`} className={im.reason}>
          Row {r.row}: {reason(r)}
        </span>
      )}
      {r.fixed && !r.excluded && <span className={im.fixed}>Fixed. Will be sent.</span>}
    </>
  );
  const action = (r: ReviewRow, mobile: boolean) =>
    r.excluded ? (
      <button type="button" className={mobile ? `btn btn-secondary ${im.cardButton}` : styles.textButton} aria-label={mobile ? undefined : `Include row ${r.row} again`} onClick={() => onExclude(r.row, false)}>
        {mobile ? 'Include again' : 'Include'}
      </button>
    ) : editable(r) ? (
      <button type="button" className={mobile ? `btn btn-secondary ${im.cardButton}` : `${styles.textButton} ${im.muted}`} aria-label={mobile ? undefined : `Exclude row ${r.row}`} onClick={() => onExclude(r.row, true)}>
        {mobile ? 'Exclude this row' : 'Exclude'}
      </button>
    ) : null;

  return (
    <section className={im.review} aria-labelledby="import-review">
      <div className={im.reviewHead}>
        <div>
          {heading}
          <p role="status" className={im.lead}>
            {c.ready} ready · {needs} · {c.skipped} skipped · from {fileName}
          </p>
        </div>
        <button type="button" className="btn btn-secondary" onClick={onRestart}>
          <Icon name="restart_alt" />
          Upload a different file
        </button>
      </div>

      <div role="group" aria-label="Filter rows" className={im.chips}>
        {chips.map(([key, label]) => (
          <button key={key} type="button" aria-pressed={filter === key} className={im.chip} onClick={() => onFilter(key)}>
            {label}
          </button>
        ))}
      </div>

      {shown.length === 0 ? (
        <p className={im.none}>No rows in this filter.</p>
      ) : isMobile ? (
        <ul className={styles.cards}>
          {shown.map((r) => (
            <li key={r.row} className={`${styles.card} ${failing(r) ? im.errorCard : ''}`}>
              <div className={im.cardHead}>
                <span className={im.rowNumber}>Row {r.row}</span>
                <Badge row={r} />
              </div>
              {editable(r) ? (
                <>
                  {emailInput(r)}
                  <div className={im.cardMeta}>
                    {r.fullName || '—'} · {r.jobTitle || '—'}
                  </div>
                  <label htmlFor={`import-role-${r.row}`} className={im.cardLabel}>
                    Role
                  </label>
                  {roleSelect(r)}
                </>
              ) : (
                <>
                  <div className={`${styles.strong} ${styles.wrapAnywhere}`}>{r.email || '(no email)'}</div>
                  <div className={im.cardMeta}>
                    {r.fullName || '—'} · {r.jobTitle || '—'} · {r.role || '—'}
                  </div>
                </>
              )}
              {notes(r)}
              {action(r, true)}
            </li>
          ))}
        </ul>
      ) : (
        <div className={`${styles.tableWrap} ${im.tableWrap}`}>
          <table className={styles.table}>
            <thead>
              <tr>
                {['Row', 'Email', 'Full name', 'Job title', 'Role', 'Status'].map((h) => (
                  <th key={h} className={styles.plainTh}>
                    {h}
                  </th>
                ))}
                <th className={styles.plainTh}>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {shown.map((r) => (
                <tr key={r.row} className={failing(r) ? im.errorRow : undefined}>
                  <td className={im.rowNumber}>{r.row}</td>
                  <td className={im.emailCell}>{editable(r) ? emailInput(r) : <span className={styles.wrapAnywhere}>{r.email || '(no email)'}</span>}</td>
                  <td>{r.fullName || '—'}</td>
                  <td className={styles.muted}>{r.jobTitle || '—'}</td>
                  <td>{editable(r) ? roleSelect(r) : r.role || '—'}</td>
                  <td>
                    <div className={im.status}>
                      <Badge row={r} />
                      {notes(r)}
                    </div>
                  </td>
                  <td className={styles.actionsCell}>{action(r, false)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className={im.footer}>
        {failure ? (
          <p role="alert" className={im.footerFailure}>
            <Icon name="error" size={18} />
            {failure}
          </p>
        ) : (
          <p className={im.footerNote}>Rows marked Duplicate in file, Already a member or Already invited are skipped. Fix or exclude rows with errors.</p>
        )}
        <button type="button" className={`btn btn-primary ${im.send}`} disabled={c.ready === 0 || sending} aria-busy={sending} onClick={onSend}>
          <Icon name="send" />
          {sending ? 'Sending…' : `Send ${plural(c.ready, 'invitation')}`}
        </button>
      </div>
    </section>
  );
}
