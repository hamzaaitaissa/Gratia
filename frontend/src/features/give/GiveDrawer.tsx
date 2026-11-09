import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';
import { api } from '../../api/endpoints';
import { ApiError } from '../../api/http';
import { queryKeys, sameId, useMe, usePointsCycle } from '../../api/queries';
import type { ReadTransaction, ReadUser } from '../../api/types';
import { Icon } from '../../components/Icon';
import { RecognitionCard, type CardTransaction } from '../../components/RecognitionCard';
import type { PointsCycle } from '../../lib/pointsCycle';
import { ColleaguePicker } from './ColleaguePicker';
import {
  afterText,
  CATEGORIES,
  CATEGORY_MAX,
  categoryOf,
  clampAmount,
  describeSendError,
  initialDraft,
  MESSAGE_MAX,
  QUICK_AMOUNTS,
  sendLabel,
  validate,
  type GiveDraft,
  type GiveErrors,
  type ServerError,
} from './giveForm';
import styles from './GiveDrawer.module.css';

interface GiveDrawerProps {
  initialReceiverId: string | null;
  onClose: () => void;
  onSent: (tx: ReadTransaction) => void;
  onGiveAgain: () => void;
  onSeeFeed: () => void;
}

/** "Give recognition": a drawer on desktop, a full-screen sheet on mobile, over any screen. */
export function GiveDrawer({ initialReceiverId, onClose, onSent, onGiveAgain, onSeeFeed }: GiveDrawerProps) {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const { me, data: users, isError, refetch } = useMe();
  const cycle = usePointsCycle();

  // Modal: the app behind is inert while the drawer is open, and focus goes back where it was afterwards
  useLayoutEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const root = document.getElementById('root');
    root?.setAttribute('inert', '');
    return () => {
      root?.removeAttribute('inert');
      if (opener?.isConnected) opener.focus();
    };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !e.defaultPrevented) onClose();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const focusSoon = useCallback((selector: string) => {
    window.setTimeout(() => {
      const el = dialogRef.current?.querySelector<HTMLElement>(selector) ?? dialogRef.current;
      el?.focus();
    }, 30);
  }, []);

  useEffect(() => focusSoon('[data-autofocus]'), [focusSoon]);

  return createPortal(
    <div className={styles.overlay}>
      <div className={styles.scrim} aria-hidden="true" onClick={onClose} />
      <div ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} className={styles.dialog}>
        <div className={styles.header}>
          <h2 id={titleId} className={styles.title}>
            Give recognition
          </h2>
          {me && (
            <span className={styles.leftPill}>
              <Icon name="volunteer_activism" size={17} />
              {me.numberOfPointsAvailable} to give
            </span>
          )}
          <button type="button" className={styles.close} aria-label="Close" onClick={onClose}>
            <Icon name="close" size={24} />
          </button>
        </div>

        {me && users && cycle ? (
          <GiveBody
            me={me}
            users={users}
            initialReceiverId={initialReceiverId}
            cycle={cycle}
            focusSoon={focusSoon}
            onClose={onClose}
            onSent={onSent}
            onGiveAgain={onGiveAgain}
            onSeeFeed={onSeeFeed}
          />
        ) : isError ? (
          <div role="alert" className={styles.centered}>
            <p className={styles.centeredText}>We couldn’t load your colleagues.</p>
            <button type="button" className="btn btn-secondary" data-autofocus="" onClick={() => void refetch()}>
              <Icon name="refresh" />
              Retry
            </button>
          </div>
        ) : (
          <div className={styles.centered} aria-busy="true">
            <p className={styles.centeredText} role="status">
              Loading…
            </p>
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}

interface GiveBodyProps extends Omit<GiveDrawerProps, 'initialReceiverId'> {
  me: ReadUser;
  users: ReadUser[];
  initialReceiverId: string | null;
  focusSoon: (selector: string) => void;
  cycle: PointsCycle;
}

function GiveBody({ me, users, initialReceiverId, focusSoon, cycle, onClose, onSent, onGiveAgain, onSeeFeed }: GiveBodyProps) {
  const queryClient = useQueryClient();
  const ids = useId();
  const left = me.numberOfPointsAvailable;
  const { allowance } = cycle;

  const colleagues = useMemo(() => users.filter((u) => !sameId(u.id, me.id)), [users, me.id]);
  const [draft, setDraft] = useState<GiveDraft>(() =>
    initialDraft(initialReceiverId && !sameId(initialReceiverId, me.id) ? initialReceiverId : null, left),
  );
  const [errors, setErrors] = useState<GiveErrors>({});
  const [server, setServer] = useState<ServerError | null>(null);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState<{ name: string; amount: number } | null>(null);

  const receiver = draft.receiverId ? colleagues.find((u) => u.id === draft.receiverId) : undefined;
  const category = categoryOf(draft);

  const update = (patch: Partial<GiveDraft>, clear?: keyof GiveErrors) => {
    setDraft((d) => ({ ...d, ...patch }));
    if (clear) setErrors((e) => ({ ...e, [clear]: undefined }));
    setServer(null);
  };
  const setAmount = (n: number | string) => update({ amount: clampAmount(n, left) }, 'amount');

  const give = useMutation({ mutationFn: api.give });

  const send = async () => {
    const errs = validate(draft, left);
    if (Object.values(errs).some(Boolean)) {
      setErrors(errs);
      setServer(null);
      focusSoon('[aria-invalid="true"]');
      return;
    }
    setErrors({});
    setServer(null);
    setSending(true);
    try {
      const tx = await give.mutateAsync({
        receiverId: draft.receiverId!,
        amount: draft.amount,
        message: draft.message.trim(),
        typeOfDonation: category,
      });
      // Show the new balances straight away, then let the server have the last word
      queryClient.setQueryData<ReadUser[]>(queryKeys.users, (old) =>
        old?.map((u) =>
          sameId(u.id, me.id)
            ? { ...u, numberOfPointsAvailable: Math.max(0, u.numberOfPointsAvailable - tx.amount) }
            : sameId(u.id, tx.receiverId)
              ? { ...u, numberOfPointsAcquired: u.numberOfPointsAcquired + tx.amount }
              : u,
        ),
      );
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      void queryClient.invalidateQueries({ queryKey: queryKeys.feed });
      void queryClient.invalidateQueries({ queryKey: queryKeys.transactions });
      setSent({ name: receiver?.fullName ?? tx.receiverName, amount: tx.amount });
      onSent(tx);
      focusSoon('[data-autofocus]');
    } catch (err) {
      let freshLeft: number | null = null;
      if (err instanceof ApiError && err.status === 400) {
        // Re-read the balance: it may have changed since the drawer opened (another tab, a reset)
        try {
          const fresh = await queryClient.fetchQuery({ queryKey: queryKeys.users, queryFn: () => api.companyUsers(), staleTime: 0 });
          freshLeft = fresh.find((u) => sameId(u.id, me.id))?.numberOfPointsAvailable ?? null;
        } catch {
          // keep the generic message
        }
      }
      if (err instanceof ApiError && err.status === 404) void queryClient.invalidateQueries({ queryKey: queryKeys.users });
      setServer(
        describeSendError(err, {
          amount: draft.amount,
          receiverName: receiver?.fullName ?? '',
          isSelf: sameId(draft.receiverId, me.id),
          freshLeft,
        }),
      );
    } finally {
      setSending(false);
    }
  };

  if (sent) {
    return (
      <div role="status" className={styles.success}>
        <Celebration />
        <h3 className={styles.successTitle}>Thank you, sent</h3>
        <p className={styles.successText}>
          You gave <strong>{sent.amount} points</strong> to <strong>{sent.name}</strong>.
        </p>
        <p className={styles.successPill}>
          <Icon name="volunteer_activism" size={18} />
          {left === 0
            ? `You’ve given all your points this month. They refill on ${cycle.refillDate}.`
            : `${left} of ${allowance} points to give left · refills ${cycle.refillDate}`}
        </p>
        <div className={styles.successActions}>
          {left > 0 && (
            <button type="button" className="btn btn-secondary" onClick={onGiveAgain}>
              Give again
            </button>
          )}
          <button type="button" className="btn btn-primary" data-autofocus="" onClick={onSeeFeed}>
            See it in the feed
          </button>
        </div>
      </div>
    );
  }

  if (left === 0) {
    return (
      <div role="status" className={styles.zero}>
        <div className={styles.zeroRing}>
          <svg width="96" height="96" viewBox="0 0 96 96" aria-hidden="true">
            <circle cx="48" cy="48" r="38" fill="none" stroke="var(--g-primary-soft)" strokeWidth="9" />
          </svg>
          <Icon name="hourglass_top" size={32} className={styles.zeroIcon} />
        </div>
        <h3 className={styles.zeroTitle}>You’ve given all your points this month.</h3>
        <p className={styles.zeroText}>
          They refill on {cycle.refillDate}. Until then you can read the company feed and see what you’ve received.
        </p>
        <button type="button" className="btn btn-secondary" data-autofocus="" onClick={onClose}>
          Got it
        </button>
      </div>
    );
  }

  const preview: CardTransaction = {
    id: 'preview',
    senderId: me.id,
    senderName: me.fullName,
    receiverId: receiver?.id ?? '',
    receiverName: receiver?.fullName ?? '',
    amount: draft.amount,
    typeOfDonation: category,
    message: draft.message,
    createdAt: new Date().toISOString(),
  };

  return (
    <>
      <div className={styles.body}>
        <ColleaguePicker
          colleagues={colleagues}
          value={draft.receiverId}
          onChange={(id) => update({ receiverId: id }, 'receiver')}
          error={errors.receiver}
        />

        <fieldset className={styles.fieldset}>
          <legend className={`field-label ${styles.legend}`}>Amount</legend>
          <div className={styles.amountRow}>
            {QUICK_AMOUNTS.map((n) => (
              <button
                key={n}
                type="button"
                className={styles.quick}
                aria-pressed={draft.amount === n}
                aria-label={`${n} points`}
                disabled={n > left}
                onClick={() => setAmount(n)}
              >
                +{n}
              </button>
            ))}
            <div className={styles.stepper}>
              <button type="button" aria-label="One point less" disabled={draft.amount <= 1} onClick={() => setAmount(draft.amount - 1)}>
                <Icon name="remove" />
              </button>
              <label htmlFor={`${ids}-amount`} className="sr-only">
                Amount in points
              </label>
              <input
                id={`${ids}-amount`}
                type="number"
                inputMode="numeric"
                min={1}
                max={left}
                value={draft.amount}
                aria-invalid={errors.amount ? true : undefined}
                onChange={(e) => setAmount(e.target.value)}
              />
              <button type="button" aria-label="One point more" disabled={draft.amount >= left} onClick={() => setAmount(draft.amount + 1)}>
                <Icon name="add" />
              </button>
            </div>
          </div>
          <p aria-live="polite" className={styles.after}>
            <Icon name="volunteer_activism" size={18} />
            {afterText(left, draft.amount, allowance)}
          </p>
          {errors.amount && (
            <p role="alert" className="field-error">
              {errors.amount}
            </p>
          )}
        </fieldset>

        <fieldset className={styles.fieldset}>
          <legend className={`field-label ${styles.legend}`}>Category</legend>
          <div className={styles.chips}>
            {CATEGORIES.map((c) => (
              <button
                key={c}
                type="button"
                className={styles.chip}
                aria-pressed={!draft.customOn && draft.category === c}
                onClick={() => update({ category: c, customOn: false }, 'category')}
              >
                {c}
              </button>
            ))}
            <button
              type="button"
              className={`${styles.chip} ${styles.customChip}`}
              aria-pressed={draft.customOn}
              onClick={() => {
                update({ customOn: true, category: '' }, 'category');
                focusSoon('[data-custom]');
              }}
            >
              <Icon name="edit" size={18} />
              Something else
            </button>
          </div>
          {draft.customOn && (
            <div className={styles.customField}>
              <div className={styles.labelRow}>
                <label htmlFor={`${ids}-custom`} className={styles.smallLabel}>
                  Your category
                </label>
                <span aria-hidden="true" className={styles.count}>
                  {draft.custom.length} / {CATEGORY_MAX}
                </span>
              </div>
              <input
                id={`${ids}-custom`}
                data-custom=""
                className="input"
                maxLength={CATEGORY_MAX}
                placeholder="For example, Calm under pressure"
                value={draft.custom}
                aria-invalid={errors.category ? true : undefined}
                onChange={(e) => update({ custom: e.target.value.slice(0, CATEGORY_MAX) }, 'category')}
              />
            </div>
          )}
          {errors.category && (
            <p role="alert" className="field-error">
              <Icon name="error" size={18} />
              {errors.category}
            </p>
          )}
        </fieldset>

        <div className={styles.field}>
          <div className={styles.labelRow}>
            <label htmlFor={`${ids}-msg`} className="field-label">
              Message
            </label>
            <span id={`${ids}-msg-count`} className={styles.count}>
              {draft.message.length} / {MESSAGE_MAX}
            </span>
          </div>
          <textarea
            id={`${ids}-msg`}
            rows={4}
            maxLength={MESSAGE_MAX}
            className={styles.textarea}
            placeholder="What did they do, and what difference did it make?"
            value={draft.message}
            aria-invalid={errors.message ? true : undefined}
            aria-describedby={`${ids}-msg-count`}
            onChange={(e) => update({ message: e.target.value.slice(0, MESSAGE_MAX) }, 'message')}
          />
          {errors.message && (
            <p role="alert" className="field-error">
              <Icon name="error" size={18} />
              {errors.message}
            </p>
          )}
        </div>

        <div className={styles.previewBlock}>
          <span className={styles.previewLabel}>Preview</span>
          <RecognitionCard tx={preview} meId={me.id} preview />
        </div>
      </div>

      <div className={styles.footer}>
        {server && (
          <div role="alert" className={styles.serverError}>
            <Icon name="error" className={styles.serverIcon} />
            <div>
              <strong>{server.title}</strong>
              <span>{server.detail}</span>
            </div>
          </div>
        )}
        <div className={styles.footerActions}>
          <button type="button" className={`btn btn-secondary ${styles.footerBtn}`} onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className={`btn btn-primary ${styles.footerBtn} ${styles.sendBtn}`}
            disabled={sending}
            aria-busy={sending}
            onClick={() => void send()}
          >
            <Icon name="volunteer_activism" />
            {sendLabel(draft.amount, sending)}
          </button>
        </div>
      </div>
    </>
  );
}

function Celebration() {
  const reducedMotion = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  return (
    <div aria-hidden="true" className={styles.celebration}>
      {!reducedMotion &&
        Array.from({ length: 14 }, (_, i) => (
          <span
            key={i}
            className={styles.burst}
            style={
              {
                width: i % 3 ? 8 : 6,
                height: i % 3 ? 8 : 6,
                borderRadius: i % 2 ? '50%' : 2,
                background: i % 2 ? 'var(--g-primary)' : 'var(--g-secondary)',
                '--a': `${(i * 360) / 14}deg`,
                animationDelay: `${(i % 4) * 40}ms`,
              } as CSSProperties
            }
          />
        ))}
      <div className={styles.check}>
        <Icon name="check" size={42} />
      </div>
    </div>
  );
}
