import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api } from '../../api/endpoints';
import { limitDetail } from '../../api/errors';
import { ApiError } from '../../api/http';
import { fieldErrors, queryKeys, useCompany, usePendingInvitations } from '../../api/queries';
import type { Invitation, UserRole } from '../../api/types';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { ConfirmDialog } from '../../components/Dialog';
import { Icon, type IconName } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { RowsSkeleton } from '../../components/States';
import { useToast } from '../../components/Toast';
import { exactDate, parseUtc, relativeTime, shortDate } from '../../lib/format';
import { daysUntil } from '../../lib/pointsCycle';
import styles from './Admin.module.css';
import inv from './Invitations.module.css';

const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e.trim());
const problemDetail = (err: unknown) => (err instanceof ApiError && err.problem.detail) || 'Something went wrong. Try again in a moment.';

/** The API's 409s for an invite, in words that say what to do instead. */
function conflictMessage(err: ApiError) {
  const detail = err.problem.detail ?? '';
  if (/deactivated/i.test(detail)) return 'This person’s account was deactivated. Reactivate them in Members instead of inviting them again.';
  // "…couldn't be sent. Resend it from the list of pending invitations." (#55)
  if (/couldn.t be sent/i.test(detail)) return 'There’s already an invitation for this email, but it couldn’t be sent. Resend it below.';
  if (/invitation/i.test(detail)) return 'There’s already a pending invitation for this email. Resend it below instead.';
  return 'Someone with this email already has an account.';
}

function expiresText(i: Invitation) {
  if (i.isExpired) return `Expired ${shortDate(i.expiresAt)}`;
  const days = daysUntil(parseUtc(i.expiresAt));
  return days <= 1 ? 'Expires tomorrow' : `Expires in ${days} days`;
}

/** The name the admin or the spreadsheet gave, when there is one, then the email. */
const displayName = (i: Invitation) => i.fullName?.trim() || i.email;

/**
 * Admin · Invitations (design 6.10b): invite one person, or many from a spreadsheet (#54), and the invitations not
 * accepted yet. Emails go out in the background (#55), so each row says whether its email has gone.
 */
export function InvitationsPage() {
  const pending = usePendingInvitations();
  const { data: company } = useCompany();
  const isMobile = useIsMobile();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [values, setValues] = useState({ email: '', fullName: '', jobTitle: '' });
  const [role, setRole] = useState<UserRole>('Employee');
  const [emailError, setEmailError] = useState('');
  const [sendFailure, setSendFailure] = useState('');
  const [cancelling, setCancelling] = useState<Invitation | null>(null);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.invitations });
  const invite = useMutation({
    mutationFn: () => api.invite({ email: values.email.trim(), role, fullName: values.fullName.trim() || null, jobTitle: values.jobTitle.trim() || null }),
    onSuccess: (created) => {
      refresh();
      setValues({ email: '', fullName: '', jobTitle: '' });
      toast(`Sending an invitation to ${created.email}.`);
    },
    onError: (err) => {
      // The daily cap of an unverified workspace (#65), in words for the admin
      const cap = limitDetail(err);
      if (cap) setSendFailure(cap);
      else if (fieldErrors(err).email) setEmailError('Enter a valid email address, like name@company.com.');
      else if (err instanceof ApiError && err.status === 409) setEmailError(conflictMessage(err));
      // The domain rule, already in words: "Until your company's domain is verified, you can only invite @… addresses"
      else if (err instanceof ApiError && err.status === 400) setEmailError(problemDetail(err));
      else toast(problemDetail(err), 'error');
    },
  });
  const resend = useMutation({
    mutationFn: (i: Invitation) => api.resendInvitation(i.id),
    onSuccess: (_, i) => {
      refresh();
      // The old link keeps working until the new email has gone out (#55)
      toast(i.isExpired || i.emailStatus === 'Failed' ? `Sending a new invitation to ${i.email}.` : `Sending a new invitation to ${i.email}. The earlier link stops working once it’s sent.`);
    },
    onError: (err) => {
      refresh();
      toast(limitDetail(err) ?? problemDetail(err), 'error');
    },
  });
  const cancel = useMutation({
    mutationFn: (i: Invitation) => api.cancelInvitation(i.id),
    onSuccess: (_, i) => {
      setCancelling(null);
      refresh();
      toast(`Invitation for ${i.email} cancelled.`);
    },
    onError: (err) => {
      setCancelling(null);
      refresh();
      toast(problemDetail(err), 'error');
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!isEmail(values.email)) return setEmailError('Enter a valid email address, like name@company.com.');
    setEmailError('');
    setSendFailure('');
    invite.mutate();
  };

  const restricted = company?.domain && !company.isDomainVerified ? company.domain : null;
  const list = pending.data ?? [];
  // Resending while the email is still going out would only queue a second one
  const canResend = (i: Invitation) => i.emailStatus !== 'Pending' && !resend.isPending;

  return (
    <>
      <PageHeader title="Invitations" sub="Invite colleagues one at a time or many at once from a spreadsheet." />
      <div className={inv.page}>
        <div className={inv.cards}>
          <form className={inv.formCard} noValidate aria-labelledby="invite-title" onSubmit={submit}>
            <h2 id="invite-title" className={inv.cardTitle}>
              <span aria-hidden="true" className={inv.cardIcon}>
                <Icon name="person_add" />
              </span>
              Invite people
            </h2>
            <div className={inv.formRow}>
              <div className={inv.emailField}>
                <label htmlFor="invite-email" className="field-label">
                  Email
                </label>
                <input
                  id="invite-email"
                  className="input"
                  type="email"
                  autoComplete="off"
                  maxLength={254}
                  placeholder={`name@${company?.domain ?? 'company.com'}`}
                  value={values.email}
                  aria-invalid={emailError ? true : undefined}
                  aria-describedby={emailError ? 'invite-email-e' : 'invite-email-h'}
                  onChange={(e) => {
                    setValues((v) => ({ ...v, email: e.target.value }));
                    setEmailError('');
                    setSendFailure('');
                  }}
                />
                {emailError && (
                  <p id="invite-email-e" role="alert" className="field-error">
                    <Icon name="error" size={18} />
                    <span>
                      {emailError}
                      {/deactivated/.test(emailError) && (
                        <>
                          {' '}
                          <Link to="/admin/members">Go to Members</Link>
                        </>
                      )}
                    </span>
                  </p>
                )}
              </div>
              <div role="radiogroup" aria-labelledby="invite-role" className={inv.roleField}>
                <span id="invite-role" className="field-label">
                  Role
                </span>
                <div className={inv.segmented}>
                  {(['Employee', 'Admin'] as const).map((r) => (
                    <button key={r} type="button" role="radio" aria-checked={role === r} className={inv.segment} onClick={() => setRole(r)}>
                      {r}
                    </button>
                  ))}
                </div>
              </div>
            </div>
            <div className={inv.formRow}>
              {(
                [
                  ['fullName', 'Full name', 'off'],
                  ['jobTitle', 'Job title', 'off'],
                ] as const
              ).map(([key, label, autoComplete]) => (
                <div key={key} className={inv.optionalField}>
                  <label htmlFor={`invite-${key}`} className="field-label">
                    {label} <span className={inv.optional}>(optional)</span>
                  </label>
                  <input
                    id={`invite-${key}`}
                    className="input"
                    autoComplete={autoComplete}
                    maxLength={200}
                    value={values[key]}
                    onChange={(e) => setValues((v) => ({ ...v, [key]: e.target.value }))}
                  />
                </div>
              ))}
            </div>
            {/* While an error explains the problem, the hint would only repeat it */}
            {!emailError && (
              <p id="invite-email-h" className={inv.hint}>
                {restricted
                  ? `Until ${restricted} is verified, you can only invite @${restricted} addresses. They get an email with a link that works for 7 days.`
                  : 'They get an email with a link that works for 7 days. The name and job title are filled in for them, and they can change both.'}
              </p>
            )}
            {sendFailure && (
              <div role="alert" className={inv.failure}>
                <Icon name="error" />
                <span>{sendFailure}</span>
              </div>
            )}
            <button type="submit" className={`btn btn-primary ${inv.send}`} disabled={invite.isPending} aria-busy={invite.isPending}>
              {invite.isPending ? 'Sending…' : 'Send invitation'}
            </button>
          </form>

          <section className={inv.formCard} aria-labelledby="import-title">
            <h2 id="import-title" className={inv.cardTitle}>
              <span aria-hidden="true" className={inv.cardIcon}>
                <Icon name="table_view" />
              </span>
              Import from spreadsheet
            </h2>
            <p className={inv.cardText}>Invite up to 500 people at once from an Excel file. You’ll review every row before anything is sent.</p>
            <ol className={inv.steps}>
              {['Download the template', 'Fill in emails, names, job titles and roles', 'Upload, review and send'].map((s, n) => (
                <li key={s}>
                  <span aria-hidden="true" className={inv.stepNumber}>
                    {n + 1}
                  </span>
                  {s}
                </li>
              ))}
            </ol>
            <Link to="/admin/invitations/import" className={`btn btn-primary ${inv.send}`}>
              <Icon name="upload_file" />
              Import from spreadsheet
            </Link>
          </section>
        </div>

        <section aria-labelledby="pending-title" className={styles.section}>
          <div className={styles.sectionHead}>
            <h2 id="pending-title" className={styles.sectionTitle}>
              Pending invitations
            </h2>
            {pending.data && <span className={styles.sectionNote}>{list.length}</span>}
          </div>

          {pending.isError ? (
            <PageError error={pending.error} onRetry={() => void pending.refetch()} />
          ) : !pending.data ? (
            <RowsSkeleton rows={3} />
          ) : list.length === 0 ? (
            <div className={inv.empty}>
              <Icon name="mark_email_read" size={32} className={inv.emptyIcon} />
              <p className={inv.emptyTitle}>No pending invitations</p>
              <p className={inv.emptyText}>Everyone you invited has joined. New invitations show up here until they’re accepted.</p>
            </div>
          ) : isMobile ? (
            <ul className={styles.cards}>
              {list.map((i) => (
                <li key={i.id} className={styles.card}>
                  <div className={styles.cardHead}>
                    <Avatar id={i.email} name={displayName(i)} size={38} />
                    <div className={styles.cardText}>
                      <div className={`${styles.strong} ${styles.wrapAnywhere}`}>{displayName(i)}</div>
                      {i.fullName && <div className={`${styles.cardSub} ${styles.wrapAnywhere}`}>{i.email}</div>}
                      <div className={styles.cardSub}>
                        {i.invitedRole} · invited by {i.invitedByUserName || 'the founder'} · {relativeTime(i.createdAt)}
                      </div>
                    </div>
                  </div>
                  <Status invitation={i} />
                  <div className={styles.cardActions}>
                    <button type="button" className={`${styles.cardAction} ${styles.primaryText}`} aria-label={`Resend the invitation to ${i.email}`} disabled={!canResend(i)} onClick={() => resend.mutate(i)}>
                      Resend
                    </button>
                    <button type="button" className={`${styles.cardAction} ${styles.dangerText}`} aria-label={`Cancel the invitation to ${i.email}`} onClick={() => setCancelling(i)}>
                      Cancel
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th className={styles.plainTh}>Person</th>
                    <th className={styles.plainTh}>Role</th>
                    <th className={styles.plainTh}>Invited by</th>
                    <th className={styles.plainTh}>Sent</th>
                    <th className={styles.plainTh}>Status</th>
                    <th className={styles.plainTh}>
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {list.map((i) => (
                    <tr key={i.id}>
                      <td>
                        <div className={styles.person}>
                          <Avatar id={i.email} name={displayName(i)} size={34} />
                          <div style={{ minWidth: 0 }}>
                            <div className={`${styles.strong} ${styles.wrapAnywhere}`}>{displayName(i)}</div>
                            {i.fullName && <div className={`${styles.muted} ${styles.wrapAnywhere}`}>{i.email}</div>}
                          </div>
                        </div>
                      </td>
                      <td>{i.invitedRole}</td>
                      <td className={styles.muted}>{i.invitedByUserName || '—'}</td>
                      <td className={styles.muted} title={exactDate(i.emailSentAt ?? i.createdAt)} style={{ whiteSpace: 'nowrap' }}>
                        {relativeTime(i.emailSentAt ?? i.createdAt)}
                      </td>
                      <td>
                        <Status invitation={i} />
                      </td>
                      <td className={styles.actionsCell}>
                        <button type="button" className={styles.textButton} aria-label={`Resend the invitation to ${i.email}`} disabled={!canResend(i)} onClick={() => resend.mutate(i)}>
                          Resend
                        </button>
                        <button type="button" className={`${styles.textButton} ${styles.textDanger}`} aria-label={`Cancel the invitation to ${i.email}`} onClick={() => setCancelling(i)}>
                          Cancel
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      {cancelling && (
        <ConfirmDialog
          title="Cancel this invitation?"
          body={`The link sent to ${cancelling.email} will stop working. You can invite them again later.`}
          confirmLabel="Cancel invitation"
          cancelLabel="Keep invitation"
          busy={cancel.isPending}
          onConfirm={() => cancel.mutate(cancelling)}
          onCancel={() => setCancelling(null)}
        />
      )}
    </>
  );
}

/**
 * Where an invitation is: its email still going out, or failed after 5 tries over about 2.5 hours (#55); then
 * pending until it's accepted, or expired, when Resend sends a new link (#75).
 */
function Status({ invitation: i }: { invitation: Invitation }) {
  const [tone, icon, label, note]: [string | undefined, IconName, string, string] =
    i.emailStatus === 'Pending'
      ? ['', 'schedule_send', 'Sending…', 'Usually takes a few seconds']
      : i.emailStatus === 'Failed'
        ? [inv.failed, 'error', 'Couldn’t send', 'We tried 5 times. Resend to try again.']
        : i.isExpired
          ? [inv.expired, 'timer_off', 'Expired', expiresText(i)]
          : ['', 'schedule', 'Pending', expiresText(i)];
  return (
    <div className={inv.status}>
      <span className={`${inv.badge} ${tone}`}>
        <Icon name={icon} size={15} />
        {label}
      </span>
      <span className={inv.expires}>{note}</span>
    </div>
  );
}
