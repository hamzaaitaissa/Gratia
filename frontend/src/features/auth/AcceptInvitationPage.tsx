import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError, NetworkError } from '../../api/http';
import { fieldErrors } from '../../api/queries';
import type { Invitation } from '../../api/types';
import { sessionFromTokens, setSession } from '../../auth/session';
import { BreakableEmail } from '../../components/BreakableEmail';
import { Icon, type IconName } from '../../components/Icon';
import { useToast } from '../../components/Toast';
import { monogram } from '../../lib/format';
import styles from './Auth.module.css';
import { PublicLayout } from './PublicLayout';

type Problem = 'expired' | 'used' | 'invalid';

/** /accept-invitation/{token}, the link in every invitation email (design 6.2). */
export function AcceptInvitationPage() {
  const { token = '' } = useParams();
  const invitation = useQuery({ queryKey: ['invitation', token], queryFn: ({ signal }) => api.invitationByToken(token, signal), retry: false });
  const [problem, setProblem] = useState<Problem | null>(null);

  const notFound = invitation.error instanceof ApiError && invitation.error.status === 404;
  const state: Problem | 'form' | 'loading' | 'error' = problem
    ? problem
    : notFound
      ? 'invalid'
      : invitation.isError
        ? 'error'
        : !invitation.data
          ? 'loading'
          : invitation.data.isUsed
            ? 'used'
            : invitation.data.isExpired || !invitation.data.isValid
              ? 'expired'
              : 'form';

  return (
    <PublicLayout>
      <div className={`${styles.form} ${styles.wide}`}>
        {invitation.data && (
          <div className={styles.company}>
            <span aria-hidden="true" className={styles.companyTile}>
              {monogram(invitation.data.companyName)}
            </span>
            <div>
              <div className={styles.companyName}>{invitation.data.companyName}</div>
              <div className={styles.companySub}>on Gratia</div>
            </div>
          </div>
        )}
        {state === 'loading' && (
          <p role="status" className={styles.lead}>
            Checking your invitation…
          </p>
        )}
        {state === 'error' && (
          <div role="alert" className={styles.card}>
            <h1 className={styles.cardTitle}>We couldn’t check this invitation</h1>
            <p className={styles.cardText}>
              {invitation.error instanceof NetworkError ? 'You seem to be offline. Check your connection and try again.' : 'Something went wrong on our side. Try again in a moment.'}
            </p>
            <div className={styles.cardActions}>
              <button type="button" className="btn btn-primary" onClick={() => void invitation.refetch()}>
                <Icon name="refresh" />
                Retry
              </button>
            </div>
          </div>
        )}
        {state === 'form' && invitation.data && (
          <AcceptForm invitation={invitation.data} token={token} onProblem={setProblem} />
        )}
        {(state === 'expired' || state === 'used' || state === 'invalid') && <Unusable problem={state} invitation={invitation.data} />}
      </div>
    </PublicLayout>
  );
}

function AcceptForm({ invitation, token, onProblem }: { invitation: Invitation; token: string; onProblem: (p: Problem) => void }) {
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [fullName, setFullName] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<{ fullName?: string; password?: string; confirm?: string }>({});
  const [failure, setFailure] = useState('');
  const [busy, setBusy] = useState(false);

  // A company's founding invitation comes from nobody: its first admin is setting the workspace up
  const founding = !invitation.invitedByUserName;
  const company = invitation.companyName;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!fullName.trim()) errs.fullName = 'Enter your full name.';
    if (password.length < 8) errs.password = 'Use at least 8 characters.';
    else if (confirm !== password) errs.confirm = 'The passwords don’t match.';
    setErrors(errs);
    setFailure('');
    if (Object.keys(errs).length) return;

    setBusy(true);
    try {
      await api.acceptInvitation({ token, fullName: fullName.trim(), password, jobTitle: jobTitle.trim() });
    } catch (err) {
      setBusy(false);
      if (err instanceof ApiError && err.status === 409) return onProblem('used');
      const fields = fieldErrors(err);
      if (fields.fullName || fields.password) return setErrors({ fullName: fields.fullName, password: fields.password });
      if (err instanceof ApiError && err.status === 400) return onProblem('expired');
      return setFailure(err instanceof NetworkError ? 'You seem to be offline. Check your connection and try again.' : 'Something went wrong on our side. Try again in a moment.');
    }

    // Accepting doesn't sign you in yet (TASKS.md #66), but this page knows the email and the new password
    try {
      const session = sessionFromTokens(await api.login(invitation.email, password));
      if (!session) throw new Error('No usable token');
      queryClient.clear();
      setSession(session);
      navigate('/', { replace: true });
      toast(founding ? `Welcome to Gratia. ${company} is ready.` : `Welcome to ${company}, ${fullName.trim().split(/\s+/)[0]}.`);
    } catch {
      navigate('/sign-in', { replace: true, state: { email: invitation.email } });
      toast('Your account is ready. Sign in to continue.');
    }
  };

  return (
    <form className={styles.form} noValidate aria-labelledby="accept-title" onSubmit={(e) => void submit(e)}>
      <div className={styles.intro}>
        <h1 id="accept-title" className={styles.title}>
          {founding ? `Set up ${company}` : `Join ${company}`}
        </h1>
        <p className={styles.lead}>
          {founding
            ? `You’re creating ${company}’s workspace on Gratia, where colleagues say thank you to each other. You’ll be its first admin.`
            : `${invitation.invitedByUserName} invited you to Gratia, where colleagues say thank you to each other.`}
        </p>
      </div>

      <div className={styles.facts}>
        <div style={{ flex: '1 1 200px', minWidth: 0 }}>
          <div className={styles.factLabel}>Email</div>
          <div className={styles.factValue}>
            <Icon name="lock" size={16} className={styles.factIcon} />
            <span>
              <BreakableEmail email={invitation.email} />
            </span>
          </div>
        </div>
        <div style={{ flex: 'none' }}>
          <div className={styles.factLabel}>Role</div>
          <div className={styles.factValue}>{invitation.invitedRole}</div>
        </div>
      </div>

      {failure && (
        <div role="alert" className={styles.failure}>
          <Icon name="error" />
          <span>{failure}</span>
        </div>
      )}

      <div className={styles.field}>
        <label htmlFor="accept-name" className="field-label">
          Full name
        </label>
        <input
          id="accept-name"
          className="input"
          autoComplete="name"
          maxLength={100}
          value={fullName}
          aria-invalid={errors.fullName ? true : undefined}
          aria-describedby={errors.fullName ? 'accept-name-e' : undefined}
          onChange={(e) => {
            setFullName(e.target.value);
            setErrors((x) => ({ ...x, fullName: undefined }));
          }}
        />
        {errors.fullName && (
          <p id="accept-name-e" role="alert" className="field-error">
            <Icon name="error" size={18} />
            {errors.fullName}
          </p>
        )}
      </div>

      <div className={styles.field}>
        <label htmlFor="accept-job" className="field-label">
          Job title <span className={styles.optional}>(optional)</span>
        </label>
        <input id="accept-job" className="input" autoComplete="organization-title" maxLength={100} value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
      </div>

      <div className={styles.field}>
        <label htmlFor="accept-password" className="field-label">
          Password
        </label>
        <input
          id="accept-password"
          className="input"
          type="password"
          autoComplete="new-password"
          value={password}
          aria-invalid={errors.password ? true : undefined}
          aria-describedby="accept-password-h"
          onChange={(e) => {
            setPassword(e.target.value);
            setErrors((x) => ({ ...x, password: undefined }));
          }}
        />
        {errors.password && (
          <p role="alert" className="field-error">
            <Icon name="error" size={18} />
            {errors.password}
          </p>
        )}
        <p id="accept-password-h" className={styles.hint}>
          At least 8 characters.
        </p>
      </div>

      <div className={styles.field}>
        <label htmlFor="accept-confirm" className="field-label">
          Confirm password
        </label>
        <input
          id="accept-confirm"
          className="input"
          type="password"
          autoComplete="new-password"
          value={confirm}
          aria-invalid={errors.confirm ? true : undefined}
          onChange={(e) => {
            setConfirm(e.target.value);
            setErrors((x) => ({ ...x, confirm: undefined }));
          }}
        />
        {errors.confirm && (
          <p role="alert" className="field-error">
            <Icon name="error" size={18} />
            {errors.confirm}
          </p>
        )}
      </div>

      <button type="submit" className={`btn btn-primary ${styles.submit}`} disabled={busy} aria-busy={busy}>
        {busy ? 'Setting up your account…' : founding ? 'Finish setting up' : `Join ${company}`}
      </button>
    </form>
  );
}

function Unusable({ problem, invitation }: { problem: Problem; invitation: Invitation | undefined }) {
  const founding = invitation && !invitation.invitedByUserName;
  const copy: Record<Problem, [IconName, string, string]> = {
    expired: [
      'timer_off',
      'This invitation has expired',
      founding
        ? 'Setup links work for 7 days. Start again and we’ll email you a new one.'
        : `Invitations are valid for 7 days. Ask ${invitation?.invitedByUserName || 'your admin'} or another admin at ${invitation?.companyName ?? 'your company'} to send you a new one.`,
    ],
    used: [
      'how_to_reg',
      'This invitation has already been used',
      `You already have an account. Sign in${invitation ? ` with ${invitation.email}` : ''}. If that wasn’t you, contact your admin.`,
    ],
    invalid: [
      'link_off',
      'This link doesn’t work',
      'It may be incomplete or mistyped. Open the link from your invitation email again, or ask your admin for a new invitation.',
    ],
  };
  const [icon, title, body] = copy[problem];
  return (
    <div role="alert" className={styles.card}>
      <span aria-hidden="true" className={styles.cardIcon}>
        <Icon name={icon} size={28} />
      </span>
      <h1 className={styles.cardTitle}>{title}</h1>
      <p className={styles.cardText}>{body}</p>
      <div className={styles.cardActions}>
        {problem === 'expired' && founding ? (
          <Link to="/create-workspace" className="btn btn-primary">
            Start again
          </Link>
        ) : (
          <Link to="/sign-in" state={invitation ? { email: invitation.email } : undefined} className="btn btn-secondary">
            Go to sign in
          </Link>
        )}
      </div>
    </div>
  );
}
