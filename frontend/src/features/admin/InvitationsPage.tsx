import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api } from '../../api/endpoints';
import { EMAIL_FAILED, isEmailFailure } from '../../api/errors';
import { ApiError } from '../../api/http';
import { fieldErrors, queryKeys, useCompany, usePendingInvitations } from '../../api/queries';
import type { Invitation, UserRole } from '../../api/types';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { ConfirmDialog } from '../../components/Dialog';
import { Icon } from '../../components/Icon';
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
  if (/invitation/i.test(detail)) return 'There’s already a pending invitation for this email. Resend it below instead.';
  return 'Someone with this email already has an account.';
}

function expiresText(i: Invitation) {
  if (i.isExpired) return `Expired ${shortDate(i.expiresAt)}`;
  const days = daysUntil(parseUtc(i.expiresAt));
  return days <= 1 ? 'Expires tomorrow' : `Expires in ${days} days`;
}

/**
 * Admin · Invitations (design 6.10b). The design's second card, "Import from spreadsheet", waits for the import
 * endpoints (TASKS.md #54), and so do the form's name and job title fields.
 */
export function InvitationsPage() {
  const pending = usePendingInvitations();
  const { data: company } = useCompany();
  const isMobile = useIsMobile();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<UserRole>('Employee');
  const [emailError, setEmailError] = useState('');
  const [sendFailure, setSendFailure] = useState('');
  const [cancelling, setCancelling] = useState<Invitation | null>(null);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.invitations });
  const invite = useMutation({
    mutationFn: () => api.invite({ email: email.trim(), role }),
    onSuccess: (created) => {
      refresh();
      setEmail('');
      toast(`Invitation sent to ${created.email}.`);
    },
    onError: (err) => {
      if (isEmailFailure(err)) setSendFailure(EMAIL_FAILED);
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
      toast(i.isExpired ? `New invitation sent to ${i.email}.` : `New invitation sent to ${i.email}. The earlier link no longer works.`);
    },
    onError: (err) => {
      refresh();
      // The invitation keeps its old link when the email fails, so the invitee's current link still works
      toast(isEmailFailure(err) ? EMAIL_FAILED : problemDetail(err), 'error');
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
    if (!isEmail(email)) return setEmailError('Enter a valid email address, like name@company.com.');
    setEmailError('');
    setSendFailure('');
    invite.mutate();
  };

  const restricted = company?.domain && !company.isDomainVerified ? company.domain : null;
  const list = pending.data ?? [];

  return (
    <>
      <PageHeader title="Invitations" sub={company ? `Invite colleagues to ${company.name}.` : undefined} />
      <div className={inv.page}>
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
                placeholder={`name@${company?.domain ?? 'company.com'}`}
                value={email}
                aria-invalid={emailError ? true : undefined}
                aria-describedby={emailError ? 'invite-email-e' : restricted ? 'invite-email-h' : undefined}
                onChange={(e) => {
                  setEmail(e.target.value);
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
          {/* While an error explains the problem, the hint would only repeat it */}
          {!emailError && (
            <p id="invite-email-h" className={inv.hint}>
              {restricted
                ? `Until ${restricted} is verified, you can only invite @${restricted} addresses. They get an email with a link that works for 7 days.`
                : 'They get an email with a link that works for 7 days.'}
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
                    <Avatar id={i.email} name={i.email} size={38} />
                    <div className={styles.cardText}>
                      <div className={`${styles.strong} ${styles.wrapAnywhere}`}>{i.email}</div>
                      <div className={styles.cardSub}>
                        {i.invitedRole} · invited by {i.invitedByUserName || 'the founder'} · {relativeTime(i.createdAt)}
                      </div>
                    </div>
                  </div>
                  <Status invitation={i} />
                  <div className={styles.cardActions}>
                    <button type="button" className={`${styles.cardAction} ${styles.primaryText}`} aria-label={`Resend the invitation to ${i.email}`} disabled={resend.isPending} onClick={() => resend.mutate(i)}>
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
                          <Avatar id={i.email} name={i.email} size={34} />
                          <span className={`${styles.strong} ${styles.wrapAnywhere}`}>{i.email}</span>
                        </div>
                      </td>
                      <td>{i.invitedRole}</td>
                      <td className={styles.muted}>{i.invitedByUserName || '—'}</td>
                      <td className={styles.muted} title={exactDate(i.createdAt)} style={{ whiteSpace: 'nowrap' }}>
                        {relativeTime(i.createdAt)}
                      </td>
                      <td>
                        <Status invitation={i} />
                      </td>
                      <td className={styles.actionsCell}>
                        <button type="button" className={styles.textButton} aria-label={`Resend the invitation to ${i.email}`} disabled={resend.isPending} onClick={() => resend.mutate(i)}>
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

/** Pending, or Expired: the link no longer works, and Resend sends a new one (#75). */
function Status({ invitation }: { invitation: Invitation }) {
  return (
    <div className={inv.status}>
      <span className={invitation.isExpired ? `${inv.badge} ${inv.expired}` : inv.badge}>
        <Icon name={invitation.isExpired ? 'timer_off' : 'schedule'} size={15} />
        {invitation.isExpired ? 'Expired' : 'Pending'}
      </span>
      <span className={inv.expires}>{expiresText(invitation)}</span>
    </div>
  );
}
