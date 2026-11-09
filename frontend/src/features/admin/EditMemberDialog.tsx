import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api } from '../../api/endpoints';
import { ApiError } from '../../api/http';
import { fieldErrors, queryKeys } from '../../api/queries';
import type { ReadUser, UserRole } from '../../api/types';
import { dialogStyles as d, Modal } from '../../components/Dialog';
import { Icon } from '../../components/Icon';
import { useToast } from '../../components/Toast';
import styles from './Admin.module.css';

const isEmail = (e: string) => /^[^\s@,;]+@[^\s@,;]+\.[A-Za-z]{2,}$/.test(e.trim());
type Field = 'fullName' | 'email' | 'jobTitle';

/** Edit member (design 6.10a): name, email, job title and role. The password is the member's own business (#17). */
export function EditMemberDialog({ member, isSelf, onClose }: { member: ReadUser; isSelf: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [values, setValues] = useState({ fullName: member.fullName, email: member.email, jobTitle: member.jobTitle, role: member.role });
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});
  const [failure, setFailure] = useState('');

  const save = useMutation({
    mutationFn: () => api.updateUser({ id: member.id, ...values, fullName: values.fullName.trim(), email: values.email.trim(), jobTitle: values.jobTitle.trim() }),
    onSuccess: (updated) => {
      queryClient.setQueryData<ReadUser[]>(queryKeys.users, (old) => old?.map((u) => (u.id === updated.id ? { ...u, ...updated } : u)));
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      toast(`Changes to ${updated.fullName} saved.`);
      onClose();
    },
    onError: (err) => {
      const fields = fieldErrors(err);
      if (fields.fullName || fields.email || fields.jobTitle)
        return setErrors({
          fullName: fields.fullName && 'Enter a full name.',
          email: fields.email && 'Enter a valid email address, like name@company.com.',
          jobTitle: fields.jobTitle,
        });
      if (err instanceof ApiError && err.status === 409) return setErrors({ email: 'Another account already uses this email.' });
      // The domain rule: until the domain is verified, members keep addresses on it
      if (err instanceof ApiError && err.status === 400 && err.problem.detail) return setErrors({ email: err.problem.detail });
      if (err instanceof ApiError && err.status === 404) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.users });
        return setFailure('This member no longer exists. They may have been deactivated in the meantime.');
      }
      setFailure('Something went wrong on our side. Try again in a moment.');
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!values.fullName.trim()) errs.fullName = 'Enter a full name.';
    if (!isEmail(values.email)) errs.email = 'Enter a valid email address, like name@company.com.';
    setErrors(errs);
    setFailure('');
    if (!Object.keys(errs).length) save.mutate();
  };

  const field = (key: Field, label: string, opts: { type?: string; optional?: boolean; autofocus?: boolean } = {}) => (
    <div className={d.field}>
      <label htmlFor={`member-${key}`} className="field-label">
        {label} {opts.optional && <span className={d.optional}>(optional)</span>}
      </label>
      <input
        id={`member-${key}`}
        className="input"
        type={opts.type ?? 'text'}
        maxLength={key === 'email' ? 254 : 100}
        value={values[key]}
        data-autofocus={opts.autofocus ? '' : undefined}
        aria-invalid={errors[key] ? true : undefined}
        aria-describedby={errors[key] ? `member-${key}-e` : undefined}
        onChange={(e) => {
          setValues((v) => ({ ...v, [key]: e.target.value }));
          setErrors((x) => ({ ...x, [key]: undefined }));
        }}
      />
      {errors[key] && (
        <p id={`member-${key}-e`} role="alert" className="field-error">
          <Icon name="error" size={18} />
          {errors[key]}
        </p>
      )}
    </div>
  );

  return (
    <Modal title="Edit member" onClose={onClose} busy={save.isPending}>
      <form noValidate onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {failure && (
          <div role="alert" className={d.formError}>
            <Icon name="error" />
            <span>{failure}</span>
          </div>
        )}
        {field('fullName', 'Full name', { autofocus: true })}
        {field('email', 'Email', { type: 'email' })}
        {field('jobTitle', 'Job title', { optional: true })}
        <div className={d.field}>
          <label htmlFor="member-role" className="field-label">
            Role
          </label>
          <select
            id="member-role"
            className={d.select}
            value={values.role}
            onChange={(e) => setValues((v) => ({ ...v, role: e.target.value as UserRole }))}
          >
            <option value="Employee">Employee</option>
            <option value="Admin">Admin</option>
          </select>
          <p className={d.hint}>Admins can manage members, invitations and company settings.</p>
          {isSelf && member.role === 'Admin' && values.role === 'Employee' && (
            <p role="alert" className={styles.warnInline}>
              <Icon name="warning" size={18} />
              You’ll lose access to the admin pages, including this one.
            </p>
          )}
        </div>
        <div className={d.actions}>
          <button type="button" className="btn btn-secondary" onClick={onClose} disabled={save.isPending}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={save.isPending} aria-busy={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save changes'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
