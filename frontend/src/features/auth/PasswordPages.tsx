import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { api } from '../../api/endpoints';
import { EMAIL_FAILED, isEmailFailure, isRateLimited, waitText } from '../../api/errors';
import { ApiError, NetworkError } from '../../api/http';
import { fieldErrors } from '../../api/queries';
import { sessionFromTokens, setSession } from '../../auth/session';
import { Icon } from '../../components/Icon';
import { useToast } from '../../components/Toast';
import styles from './Auth.module.css';
import { PublicLayout } from './PublicLayout';

const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e.trim());
const offline = 'We couldn’t reach Gratia. Check your connection and try again.';
const broken = 'Something went wrong on our side. Try again in a moment.';

/**
 * /forgot-password: asks for a link to choose a new password (#50). The API answers the same whether or not the
 * address has an account, and the email says which, so the page can't tell anyone who has one.
 */
export function ForgotPasswordPage() {
  const initial = (useLocation().state as { email?: string } | null)?.email ?? '';
  const [email, setEmail] = useState(initial);
  const [error, setError] = useState('');
  const [failure, setFailure] = useState('');
  const [sentTo, setSentTo] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setFailure('');
    if (!isEmail(email)) return setError('Enter a valid email address, like name@company.com.');
    setError('');
    setBusy(true);
    try {
      await api.forgotPassword(email.trim());
      setSentTo(email.trim());
    } catch (err) {
      if (fieldErrors(err).email) setError('Enter a valid email address, like name@company.com.');
      else if (isRateLimited(err)) setFailure(`Too many requests from your network. Try again in ${waitText((err as ApiError).retryAfter)}.`);
      else if (isEmailFailure(err)) setFailure(EMAIL_FAILED);
      else setFailure(err instanceof NetworkError ? offline : broken);
    } finally {
      setBusy(false);
    }
  };

  if (sentTo)
    return (
      <PublicLayout>
        <div className={`${styles.form} ${styles.wide}`}>
          <div role="status" className={styles.card}>
            <span aria-hidden="true" className={styles.cardIcon}>
              <Icon name="forward_to_inbox" size={28} />
            </span>
            <h1 className={styles.cardTitle}>Check your email</h1>
            <p className={styles.cardText}>If there’s an account with {sentTo}, we’ve emailed it a link to choose a new password. The link works for an hour.</p>
            <p className={styles.hint}>Nothing after a few minutes? Check your spam folder, and that this is the address you sign in with.</p>
            <div className={styles.cardActions}>
              <button type="button" className="btn btn-secondary" onClick={() => setSentTo('')}>
                Use a different email
              </button>
              <Link to="/sign-in" state={{ email: sentTo }} className="btn btn-secondary">
                Back to sign in
              </Link>
            </div>
          </div>
        </div>
      </PublicLayout>
    );

  return (
    <PublicLayout>
      <form className={styles.form} noValidate aria-labelledby="forgot-title" onSubmit={(e) => void submit(e)}>
        <div className={styles.intro}>
          <h1 id="forgot-title" className={styles.title}>
            Reset your password
          </h1>
          <p className={styles.lead}>Enter the email you sign in with, and we’ll send you a link to choose a new password.</p>
        </div>

        {failure && (
          <div role="alert" className={styles.failure}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}

        <div className={styles.field}>
          <label htmlFor="forgot-email" className="field-label">
            Email
          </label>
          <input
            id="forgot-email"
            className="input"
            type="email"
            autoComplete="email"
            maxLength={254}
            value={email}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? 'forgot-email-e' : undefined}
            onChange={(e) => {
              setEmail(e.target.value);
              setError('');
            }}
          />
          {error && (
            <p id="forgot-email-e" role="alert" className="field-error">
              <Icon name="error" size={18} />
              {error}
            </p>
          )}
        </div>

        <button type="submit" className={`btn btn-primary ${styles.submit}`} disabled={busy} aria-busy={busy}>
          {busy ? 'Sending…' : 'Email me a link'}
        </button>

        <p className={styles.note}>
          Remembered it? <Link to="/sign-in" state={{ email: email.trim() }}>Sign in</Link>
        </p>
      </form>
    </PublicLayout>
  );
}

/**
 * /reset-password/{token}, the link in the reset email. Choosing a new password signs in here and ends every other
 * session (#50). There's no way to check a link before using it, so a bad one shows up when the form is sent.
 */
export function ResetPasswordPage() {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<{ password?: string; confirm?: string }>({});
  const [failure, setFailure] = useState('');
  const [deadLink, setDeadLink] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (password.length < 8) errs.password = 'Use at least 8 characters.';
    else if (confirm !== password) errs.confirm = 'The passwords don’t match.';
    setErrors(errs);
    setFailure('');
    if (Object.keys(errs).length) return;

    setBusy(true);
    try {
      const session = sessionFromTokens(await api.resetPassword({ token, newPassword: password }));
      if (!session) throw new Error('No usable token');
      // Whoever was signed in on this browser before isn't any more
      queryClient.clear();
      setSession(session);
      navigate('/', { replace: true });
      toast('Your new password is saved. You’re signed in here and signed out everywhere else.');
    } catch (err) {
      setBusy(false);
      if (fieldErrors(err).newPassword) return setErrors({ password: 'Use at least 8 characters.' });
      // An unknown, used or expired link (1 hour): "This link is invalid or has expired. Ask for a new one."
      if (err instanceof ApiError && err.status === 400) return setDeadLink(err.problem.detail || 'This link is invalid or has expired. Ask for a new one.');
      if (isRateLimited(err)) return setFailure(`Too many attempts from your network. Try again in ${waitText((err as ApiError).retryAfter)}.`);
      setFailure(err instanceof NetworkError ? offline : broken);
    }
  };

  if (deadLink)
    return (
      <PublicLayout>
        <div className={`${styles.form} ${styles.wide}`}>
          <div role="alert" className={styles.card}>
            <span aria-hidden="true" className={styles.cardIcon}>
              <Icon name="link_off" size={28} />
            </span>
            <h1 className={styles.cardTitle}>This link doesn’t work any more</h1>
            <p className={styles.cardText}>{deadLink} Reset links work once, for an hour.</p>
            <div className={styles.cardActions}>
              <Link to="/forgot-password" className="btn btn-primary">
                Get a new link
              </Link>
              <Link to="/sign-in" className="btn btn-secondary">
                Go to sign in
              </Link>
            </div>
          </div>
        </div>
      </PublicLayout>
    );

  return (
    <PublicLayout>
      <form className={styles.form} noValidate aria-labelledby="reset-title" onSubmit={(e) => void submit(e)}>
        <div className={styles.intro}>
          <h1 id="reset-title" className={styles.title}>
            Choose a new password
          </h1>
          <p className={styles.lead}>You’ll be signed in with it here, and signed out on every other device.</p>
        </div>

        {failure && (
          <div role="alert" className={styles.failure}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}

        <div className={styles.field}>
          <label htmlFor="reset-password" className="field-label">
            New password
          </label>
          <input
            id="reset-password"
            className="input"
            type="password"
            autoComplete="new-password"
            value={password}
            aria-invalid={errors.password ? true : undefined}
            aria-describedby="reset-password-h"
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
          <p id="reset-password-h" className={styles.hint}>
            At least 8 characters.
          </p>
        </div>

        <div className={styles.field}>
          <label htmlFor="reset-confirm" className="field-label">
            Confirm new password
          </label>
          <input
            id="reset-confirm"
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
          {busy ? 'Saving…' : 'Save and sign in'}
        </button>
      </form>
    </PublicLayout>
  );
}
