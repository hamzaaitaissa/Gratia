import { useEffect, useId, useLayoutEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from './Icon';
import styles from './Dialog.module.css';

interface ModalProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** alertdialog for destructive confirmations. */
  role?: 'dialog' | 'alertdialog';
  describedBy?: string;
  icon?: ReactNode;
  /** While true, the dialog can't be dismissed (a request is in flight). */
  busy?: boolean;
}

/**
 * A centred modal dialog: the app behind is inert, Escape and the scrim close it, focus starts on the element marked
 * data-autofocus (or the dialog) and returns where it was afterwards.
 */
export function Modal({ title, onClose, children, role = 'dialog', describedBy, icon, busy }: ModalProps) {
  const titleId = useId();
  const ref = useRef<HTMLDivElement>(null);
  const close = useRef(onClose);
  close.current = busy ? () => {} : onClose;

  useLayoutEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const root = document.getElementById('root');
    root?.setAttribute('inert', '');
    const t = window.setTimeout(() => (ref.current?.querySelector<HTMLElement>('[data-autofocus]') ?? ref.current)?.focus(), 30);
    return () => {
      window.clearTimeout(t);
      root?.removeAttribute('inert');
      if (opener?.isConnected) opener.focus();
    };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !e.defaultPrevented) close.current();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  return createPortal(
    <div className={styles.overlay}>
      <div className={styles.scrim} aria-hidden="true" onClick={() => close.current()} />
      <div ref={ref} role={role} aria-modal="true" aria-labelledby={titleId} aria-describedby={describedBy} tabIndex={-1} className={styles.dialog}>
        {icon}
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {children}
      </div>
    </div>,
    document.body,
  );
}

interface ConfirmProps {
  title: string;
  body: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
}

/** A destructive confirmation. Focus starts on the safe choice. */
export function ConfirmDialog({ title, body, confirmLabel, cancelLabel = 'Cancel', onConfirm, onCancel, busy }: ConfirmProps) {
  const bodyId = useId();
  return (
    <Modal
      title={title}
      role="alertdialog"
      describedBy={bodyId}
      onClose={onCancel}
      busy={busy}
      icon={
        <span aria-hidden="true" className={styles.warning}>
          <Icon name="warning" size={24} />
        </span>
      }
    >
      <p id={bodyId} className={styles.body}>
        {body}
      </p>
      <div className={styles.actions}>
        <button type="button" className="btn btn-secondary" data-autofocus="" onClick={onCancel} disabled={busy}>
          {cancelLabel}
        </button>
        <button type="button" className={`btn ${styles.danger}`} onClick={onConfirm} disabled={busy} aria-busy={busy}>
          {busy ? 'Working…' : confirmLabel}
        </button>
      </div>
    </Modal>
  );
}

export const dialogStyles = styles;
