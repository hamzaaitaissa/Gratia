import { ApiError, NetworkError } from '../../api/http';

// Suggestions only: TypeOfDonation is free text on the API (max 100), so "Something else" sends whatever is typed
export const CATEGORIES = ['Teamwork', 'Innovation', 'Customer focus', 'Going the extra mile', 'Leadership', 'Helping out'] as const;
export const QUICK_AMOUNTS = [1, 3, 5, 10] as const;
export const MESSAGE_MAX = 500;
export const CATEGORY_MAX = 100;

export interface GiveDraft {
  receiverId: string | null;
  amount: number;
  category: string;
  customOn: boolean;
  custom: string;
  message: string;
}

export type GiveField = 'receiver' | 'amount' | 'category' | 'message';
export type GiveErrors = Partial<Record<GiveField, string>>;

export interface ServerError {
  title: string;
  detail: string;
}

export function initialDraft(receiverId: string | null, left: number): GiveDraft {
  return { receiverId, amount: Math.max(1, Math.min(3, left)), category: '', customOn: false, custom: '', message: '' };
}

/** Whole points from 1 to what's left to give. */
export function clampAmount(value: number | string, left: number): number {
  const n = Math.round(Number(value) || 1);
  return Math.max(1, Math.min(left, n));
}

export const categoryOf = (d: GiveDraft) => (d.customOn ? d.custom.trim() : d.category);

export function validate(d: GiveDraft, left: number): GiveErrors {
  const errs: GiveErrors = {};
  if (!d.receiverId) errs.receiver = 'Choose who you want to recognize.';
  if (!categoryOf(d)) errs.category = d.customOn ? 'Write your category, or pick one of the suggestions.' : 'Pick a category or write your own.';
  if (!d.message.trim()) errs.message = 'Write a short message so they know what it was for.';
  if (d.amount < 1 || d.amount > left) errs.amount = `Choose between 1 and ${left}.`;
  return errs;
}

export const afterText = (left: number, amount: number, allowance: number) => {
  const after = left - amount;
  return after <= 0 ? 'This uses all your points to give this month.' : `You’ll have ${after} of ${allowance} points to give left.`;
};

export const sendLabel = (amount: number, sending: boolean) => (sending ? 'Sending…' : `Give ${amount} point${amount === 1 ? '' : 's'}`);

const GENERIC: ServerError = {
  title: 'We couldn’t send this',
  detail: 'Something went wrong on our side. Nothing was sent, so try again in a moment.',
};

/**
 * Turns a failed send into the message the drawer shows. The API's 400s are business-rule messages meant for
 * developers, so the balance is re-read instead (`freshLeft`) to tell "not enough points" apart from the rest.
 */
export function describeSendError(
  error: unknown,
  ctx: { amount: number; receiverName: string; isSelf: boolean; freshLeft: number | null },
): ServerError {
  if (error instanceof NetworkError)
    return { title: 'You seem to be offline', detail: 'Nothing was sent. Check your connection and try again.' };
  if (!(error instanceof ApiError)) return GENERIC;

  if (error.status === 404)
    return {
      title: 'Colleague not found',
      detail: `We couldn’t find ${ctx.receiverName || 'this colleague'}. They may have left the company. Choose someone else.`,
    };

  if (error.status === 400) {
    if (ctx.freshLeft !== null && ctx.freshLeft < ctx.amount)
      return {
        title: 'Not enough points to give',
        detail: `Your balance changed since you opened this. You have ${ctx.freshLeft} point${ctx.freshLeft === 1 ? '' : 's'} to give left, so lower the amount and try again.`,
      };
    if (ctx.isSelf) return { title: 'You can’t recognize yourself', detail: 'Choose a colleague instead.' };
    if (error.problem.errors) return { title: 'Check the form', detail: 'Something in the form isn’t valid. Review it and try again.' };
  }

  return GENERIC;
}
