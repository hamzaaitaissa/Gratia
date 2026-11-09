import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError, NetworkError } from '../../api/http';
import { EMAIL_FAILED, isEmailFailure } from '../../api/errors';
import { fieldErrors } from '../../api/queries';
import { Icon } from '../../components/Icon';
import styles from './Auth.module.css';
import { PublicLayout } from './PublicLayout';

const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e.trim());
type Field = 'companyName' | 'fullName' | 'email';

/**
 * /create-workspace (design 6.11, adapted). The design creates the workspace on the spot in three steps; the API's
 * signup takes these three fields, always answers 202 and tells what happened only by email, so the founder finishes
 * through the link (the accept page) and sets branding and invitations from the admin pages afterwards.
 */
export function CreateWorkspacePage() {
  const [values, setValues] = useState<Record<Field, string>>({ companyName: '', fullName: '', email: '' });
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [failure, setFailure] = useState('');
  const [busy, setBusy] = useState(false);
  const [sentTo, setSentTo] = useState<{ email: string; company: string } | null>(null);

  const set = (field: Field) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setValues((v) => ({ ...v, [field]: e.target.value }));
    setErrors((x) => ({ ...x, [field]: undefined }));
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!values.companyName.trim()) errs.companyName = 'Enter your company’s name.';
    if (!values.fullName.trim()) errs.fullName = 'Enter your full name.';
    if (!isEmail(values.email)) errs.email = 'Enter a valid email address, like name@company.com.';
    setErrors(errs);
    setFailure('');
    if (Object.keys(errs).length) return;

    setBusy(true);
    try {
      await api.registerCompany({ companyName: values.companyName.trim(), fullName: values.fullName.trim(), email: values.email.trim() });
      setSentTo({ email: values.email.trim(), company: values.companyName.trim() });
    } catch (err) {
      const fields = fieldErrors(err);
      if (Object.keys(fields).length) setErrors(fields);
      else if (err instanceof ApiError && err.status === 400)
        // The API's one business rule here: free email providers can't start a workspace
        setErrors({ email: 'Use your work email address. A personal address (Gmail, Outlook…) can’t start a company workspace.' });
      else if (err instanceof ApiError && err.status === 429) setFailure('Too many attempts from your network. Try again in an hour.');
      // Nothing was created: signing up again sends a new link
      else if (isEmailFailure(err)) setFailure(EMAIL_FAILED);
      else if (err instanceof NetworkError) setFailure('We couldn’t reach Gratia. Check your connection and try again.');
      else setFailure('Something went wrong on our side. Try again in a moment.');
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
            <p className={styles.cardText}>
              If {sentTo.email} can start a workspace for {sentTo.company}, a link to finish setting it up is on its way. It works for 7 days.
            </p>
            <p className={styles.hint}>Nothing after a few minutes? Check your spam folder. If your company already uses Gratia, the email tells you who to ask instead.</p>
            <div className={styles.cardActions}>
              <button type="button" className="btn btn-secondary" onClick={() => setSentTo(null)}>
                Use a different email
              </button>
              <Link to="/sign-in" className="btn btn-secondary">
                Go to sign in
              </Link>
            </div>
          </div>
        </div>
      </PublicLayout>
    );

  const fields: { key: Field; label: string; type?: string; autoComplete: string; max: number; hint?: string }[] = [
    { key: 'companyName', label: 'Company name', autoComplete: 'organization', max: 100 },
    { key: 'fullName', label: 'Your full name', autoComplete: 'name', max: 100 },
    { key: 'email', label: 'Work email', type: 'email', autoComplete: 'email', max: 254, hint: 'Its domain becomes your company’s: you can invite colleagues on it straight away.' },
  ];

  return (
    <PublicLayout>
      <form className={`${styles.form} ${styles.wide}`} noValidate aria-labelledby="ws-title" onSubmit={(e) => void submit(e)}>
        <div className={styles.intro}>
          <h1 id="ws-title" className={styles.title}>
            Create a company workspace
          </h1>
          <p className={styles.lead}>Start Gratia for your company. We’ll email you a link to finish setting it up, and you’ll be its first admin.</p>
        </div>

        {failure && (
          <div role="alert" className={styles.failure}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}

        {fields.map((f) => (
          <div key={f.key} className={styles.field}>
            <label htmlFor={`ws-${f.key}`} className="field-label">
              {f.label}
            </label>
            <input
              id={`ws-${f.key}`}
              className="input"
              type={f.type ?? 'text'}
              autoComplete={f.autoComplete}
              maxLength={f.max}
              value={values[f.key]}
              aria-invalid={errors[f.key] ? true : undefined}
              aria-describedby={errors[f.key] ? `ws-${f.key}-e` : f.hint ? `ws-${f.key}-h` : undefined}
              onChange={set(f.key)}
            />
            {errors[f.key] && (
              <p id={`ws-${f.key}-e`} role="alert" className="field-error">
                <Icon name="error" size={18} />
                {errors[f.key]}
              </p>
            )}
            {f.hint && (
              <p id={`ws-${f.key}-h`} className={styles.hint}>
                {f.hint}
              </p>
            )}
          </div>
        ))}

        <button type="submit" className={`btn btn-primary ${styles.submit}`} disabled={busy} aria-busy={busy}>
          {busy ? 'Sending…' : 'Email me the setup link'}
        </button>

        <p className={styles.note}>
          Already on Gratia? <Link to="/sign-in">Sign in</Link>
        </p>
      </form>
    </PublicLayout>
  );
}
