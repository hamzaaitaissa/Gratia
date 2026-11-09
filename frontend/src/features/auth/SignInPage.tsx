import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError, NetworkError } from '../../api/http';
import { sessionFromTokens, setSession, useSession } from '../../auth/session';
import { Icon } from '../../components/Icon';
import { PublicLayout } from './PublicLayout';
import styles from './Auth.module.css';

const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e.trim());

/** Sign in (design 6.1): Gratia's own neutral brand, since no company is known yet. */
export function SignInPage() {
  const session = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as { from?: string; email?: string } | null;
  const from = state?.from?.startsWith('/') && !state.from.startsWith('//') ? state.from : '/';

  const [email, setEmail] = useState(state?.email ?? '');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [failure, setFailure] = useState('');
  const [busy, setBusy] = useState(false);

  if (session) return <Navigate to={from} replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!isEmail(email)) errs.email = 'Enter a valid email address, like name@company.com.';
    if (password.length < 8) errs.password = 'Your password has at least 8 characters.';
    setErrors(errs);
    setFailure('');
    if (errs.email || errs.password) return;

    setBusy(true);
    try {
      const next = sessionFromTokens(await api.login(email.trim(), password));
      if (!next) throw new Error('The sign-in response had no usable token.');
      setSession(next);
      navigate(from, { replace: true });
    } catch (err) {
      setFailure(
        err instanceof ApiError && err.status === 400
          ? 'Email or password is incorrect.'
          : err instanceof ApiError && err.status === 403
            ? 'This account has been deactivated. Ask an admin of your workspace to reactivate it.'
            : err instanceof ApiError && err.status === 429
              ? 'Too many sign-in attempts from your network. Wait a minute and try again.'
              : err instanceof NetworkError
                ? 'We couldn’t reach Gratia. Check your connection and try again.'
                : 'Something went wrong on our side. Try again in a moment.',
      );
      setPassword('');
    } finally {
      setBusy(false);
    }
  };

  return (
    <PublicLayout>
      <form className={styles.form} noValidate aria-labelledby="signin-title" onSubmit={(e) => void submit(e)}>
        <div className={styles.intro}>
          <h1 id="signin-title" className={styles.title}>
            Sign in
          </h1>
          <p className={styles.lead}>Use the email address your company invited you with.</p>
        </div>

        {failure && (
          <div role="alert" className={styles.failure}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}

        <div className={styles.field}>
          <label htmlFor="signin-email" className="field-label">
            Email
          </label>
          <input
            id="signin-email"
            className="input"
            type="email"
            autoComplete="email"
            value={email}
            aria-invalid={errors.email ? true : undefined}
            aria-describedby={errors.email ? 'signin-email-e' : undefined}
            onChange={(e) => {
              setEmail(e.target.value);
              setErrors((x) => ({ ...x, email: undefined }));
            }}
          />
          {errors.email && (
            <p id="signin-email-e" className="field-error">
              <Icon name="error" size={18} />
              {errors.email}
            </p>
          )}
        </div>

        <div className={styles.field}>
          <label htmlFor="signin-password" className="field-label">
            Password
          </label>
          <input
            id="signin-password"
            className="input"
            type="password"
            autoComplete="current-password"
            value={password}
            aria-invalid={errors.password ? true : undefined}
            aria-describedby={errors.password ? 'signin-password-e' : undefined}
            onChange={(e) => {
              setPassword(e.target.value);
              setErrors((x) => ({ ...x, password: undefined }));
            }}
          />
          {errors.password && (
            <p id="signin-password-e" className="field-error">
              <Icon name="error" size={18} />
              {errors.password}
            </p>
          )}
        </div>

        <button type="submit" className={`btn btn-primary ${styles.submit}`} disabled={busy} aria-busy={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>

        <p className={styles.note}>
          Accounts are created by invitation from your company’s admin. Setting up Gratia for a new company?{' '}
          <Link to="/create-workspace">Create a company workspace</Link>
        </p>
      </form>
    </PublicLayout>
  );
}
