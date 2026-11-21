import { useMutation } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api } from '../../api/endpoints';
import { isRateLimited, waitText } from '../../api/errors';
import { ApiError, NetworkError } from '../../api/http';
import { fieldErrors } from '../../api/queries';
import { sessionFromTokens, setSession } from '../../auth/session';
import { dialogStyles as d, Modal } from '../../components/Dialog';
import { Icon } from '../../components/Icon';
import { useToast } from '../../components/Toast';

type Field = 'current' | 'next' | 'confirm';

/**
 * Change password (#50). It ends every session, this one's old tokens included, and answers with new tokens for this
 * device, so the page carries on signed in.
 */
export function ChangePasswordDialog({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const [values, setValues] = useState({ current: '', next: '', confirm: '' });
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [failure, setFailure] = useState('');

  const save = useMutation({
    mutationFn: () => api.changePassword({ currentPassword: values.current, newPassword: values.next }),
    onSuccess: (tokens) => {
      const session = sessionFromTokens(tokens);
      // Without usable tokens this device is signed out like the others: the app then asks to sign in
      setSession(session);
      toast(session ? 'Password changed. You’re signed out on every other device.' : 'Password changed. Sign in with your new password.');
      onClose();
    },
    onError: (err) => {
      if (fieldErrors(err).newPassword) return setErrors({ next: 'Use at least 8 characters.' });
      // "Your current password isn't right."
      if (err instanceof ApiError && err.status === 400) return setErrors({ current: err.problem.detail || 'Your current password isn’t right.' });
      if (isRateLimited(err)) return setFailure(`Too many attempts. Try again in ${waitText((err as ApiError).retryAfter)}.`);
      setFailure(err instanceof NetworkError ? 'We couldn’t reach Gratia. Check your connection and try again.' : 'Something went wrong on our side. Try again in a moment.');
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!values.current) errs.current = 'Enter your current password.';
    if (values.next.length < 8) errs.next = 'Use at least 8 characters.';
    else if (values.next === values.current) errs.next = 'Choose a password different from your current one.';
    else if (values.confirm !== values.next) errs.confirm = 'The passwords don’t match.';
    setErrors(errs);
    setFailure('');
    if (!Object.keys(errs).length) save.mutate();
  };

  const field = (key: Field, label: string, autoComplete: string, hint?: string) => (
    <div className={d.field}>
      <label htmlFor={`password-${key}`} className="field-label">
        {label}
      </label>
      <input
        id={`password-${key}`}
        className="input"
        type="password"
        autoComplete={autoComplete}
        value={values[key]}
        data-autofocus={key === 'current' ? '' : undefined}
        aria-invalid={errors[key] ? true : undefined}
        aria-describedby={[errors[key] && `password-${key}-e`, hint && `password-${key}-h`].filter(Boolean).join(' ') || undefined}
        onChange={(e) => {
          setValues((v) => ({ ...v, [key]: e.target.value }));
          setErrors((x) => ({ ...x, [key]: undefined }));
        }}
      />
      {errors[key] && (
        <p id={`password-${key}-e`} role="alert" className="field-error">
          <Icon name="error" size={18} />
          {errors[key]}
        </p>
      )}
      {hint && (
        <p id={`password-${key}-h`} className={d.hint}>
          {hint}
        </p>
      )}
    </div>
  );

  return (
    <Modal title="Change password" onClose={onClose} busy={save.isPending}>
      <form noValidate onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {failure && (
          <div role="alert" className={d.formError}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}
        {field('current', 'Current password', 'current-password')}
        {field('next', 'New password', 'new-password', 'At least 8 characters. You’ll be signed out on your other devices.')}
        {field('confirm', 'Confirm new password', 'new-password')}
        <div className={d.actions}>
          <button type="button" className="btn btn-secondary" onClick={onClose} disabled={save.isPending}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={save.isPending} aria-busy={save.isPending}>
            {save.isPending ? 'Saving…' : 'Change password'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
