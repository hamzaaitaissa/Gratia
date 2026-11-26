import { useEffect, useRef, useState, type ReactNode } from 'react';
import { isRateLimited, waitText } from '../../api/errors';
import { ApiError, NetworkError } from '../../api/http';
import type { DnsRecord, DomainCheckResult } from '../../api/types';
import { Icon } from '../../components/Icon';
import { useToast } from '../../components/Toast';
import s from './Domain.module.css';

/*
 * The pieces domain verification (#61) shares between the signed-in card and the claim page: the record to add, the
 * check, and what the check found.
 */

/** Why getting a record or checking for it failed, in words. A 400's detail is already written for the person. */
export function domainFailure(err: unknown): string {
  if (isRateLimited(err)) return `Too many checks from your network. Wait ${waitText((err as ApiError).retryAfter)} and try again.`;
  if (err instanceof ApiError && err.status === 400 && err.problem.detail) return err.problem.detail;
  if (err instanceof NetworkError) return 'You seem to be offline. Check your connection and try again.';
  return 'Something went wrong on our side. Try again in a moment.';
}

/** Step 1, add the record, and step 2, check for it. `children` (what the check found) goes under the button. */
export function RecordSteps({
  record,
  domain,
  checking,
  checked,
  onCheck,
  children,
}: {
  record: DnsRecord;
  domain: string;
  checking: boolean;
  /** Checked at least once: the button then says "Check again" */
  checked: boolean;
  onCheck: () => void;
  children?: ReactNode;
}) {
  return (
    <ol className={s.steps}>
      <li className={s.step}>
        <span aria-hidden="true" className={s.stepNumber}>
          1
        </span>
        <div className={s.stepBody}>
          <div className={s.stepTitle}>Add this record to {domain}’s DNS settings</div>
          <p className={s.fine}>You’ll find them where the domain is managed: its registrar or DNS provider.</p>
          <DnsRecordTable record={record} />
        </div>
      </li>
      <li className={s.step}>
        <span aria-hidden="true" className={s.stepNumber}>
          2
        </span>
        <div className={s.stepBody}>
          <div className={s.stepTitle}>Check for it</div>
          <p className={s.fine}>DNS changes can take from a few minutes to a few hours to show up. Once the record is found, you can remove it.</p>
          <div className={s.actions}>
            <button type="button" className="btn btn-primary" disabled={checking} aria-busy={checking} onClick={onCheck}>
              <Icon name="refresh" />
              {checking ? 'Checking…' : checked ? 'Check again' : 'Check now'}
            </button>
          </div>
          {children}
        </div>
      </li>
    </ol>
  );
}

function DnsRecordTable({ record }: { record: DnsRecord }) {
  return (
    <dl className={s.record}>
      <div className={s.recordRow}>
        <dt className={s.recordLabel}>Type</dt>
        <dd className={s.recordCell}>
          <span className={s.recordValue}>{record.type}</span>
        </dd>
      </div>
      <div className={s.recordRow}>
        <dt className={s.recordLabel}>Name</dt>
        <dd className={s.recordCell}>
          <span className={s.recordText}>
            <span className={s.recordValue}>{record.name}</span>
            <span className={s.recordNote}>At most DNS providers, that’s “@”, the root entry.</span>
          </span>
          <CopyButton value={record.name} label="name" />
        </dd>
      </div>
      <div className={s.recordRow}>
        <dt className={s.recordLabel}>Value</dt>
        <dd className={s.recordCell}>
          <span className={s.recordText}>
            <span className={s.recordValue}>{record.value}</span>
          </span>
          <CopyButton value={record.value} label="value" />
        </dd>
      </div>
    </dl>
  );
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const toast = useToast();
  const [copied, setCopied] = useState(false);
  const ref = useRef<HTMLButtonElement>(null);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      clearTimeout(timer.current);
      timer.current = setTimeout(() => setCopied(false), 2000);
    } catch {
      // No clipboard (an http page other than localhost, or refused): select the text for the person to copy
      const text = ref.current?.parentElement?.querySelector(`.${s.recordValue}`);
      if (text) window.getSelection()?.selectAllChildren(text);
      toast('Couldn’t copy it. The text is selected: copy it with Ctrl+C, or ⌘C on a Mac.', 'error');
    }
  };

  return (
    <button
      ref={ref}
      type="button"
      className={s.copy}
      aria-label={`${copied ? 'Copied' : 'Copy'} ${label}`}
      data-copied={copied || undefined}
      onClick={() => void copy()}
    >
      <Icon name={copied ? 'check' : 'content_copy'} size={18} />
      {copied ? 'Copied' : 'Copy'}
    </button>
  );
}

/** What the last check found, or why it failed. Verified is shown by whoever asked, since what it means differs. */
export function CheckOutcome({ result, failure, children }: { result: DomainCheckResult | null; failure: string; children?: ReactNode }) {
  if (failure)
    return (
      <div className={s.result} data-tone="danger">
        <Icon name="error" />
        <span>
          {failure}
          {children}
        </span>
      </div>
    );
  if (!result) return null;
  return (
    <div className={s.result} data-tone={result.status === 'Verified' ? 'ok' : 'warn'}>
      <Icon name={result.status === 'Verified' ? 'check_circle' : result.status === 'LookupFailed' ? 'cloud_off' : 'schedule'} />
      <span>{result.message}</span>
    </div>
  );
}
