import { ApiError, NetworkError } from '../../api/http';
import { afterText, clampAmount, describeSendError, initialDraft, sendLabel, validate } from './giveForm';

describe('give form', () => {
  it('starts at 3 points, or fewer when fewer are left', () => {
    expect(initialDraft(null, 18).amount).toBe(3);
    expect(initialDraft(null, 2).amount).toBe(2);
  });

  it('keeps the amount between 1 and what’s left', () => {
    expect(clampAmount(0, 10)).toBe(1);
    expect(clampAmount('', 10)).toBe(1);
    expect(clampAmount(15, 10)).toBe(10);
    expect(clampAmount('4.6', 10)).toBe(5);
  });

  it('asks for everything that’s missing at once', () => {
    expect(validate(initialDraft(null, 10), 10)).toEqual({
      receiver: 'Choose who you want to recognize.',
      category: 'Pick a category or write your own.',
      message: 'Write a short message so they know what it was for.',
    });
  });

  it('wants a written category once “Something else” is chosen', () => {
    const d = { ...initialDraft('u2', 10), customOn: true, custom: '   ', message: 'Thanks' };
    expect(validate(d, 10)).toEqual({ category: 'Write your category, or pick one of the suggestions.' });
  });

  it('accepts a complete draft', () => {
    expect(validate({ ...initialDraft('u2', 10), category: 'Teamwork', message: 'Thanks!' }, 10)).toEqual({});
  });

  it('describes the balance after giving', () => {
    expect(afterText(18, 3, 25)).toBe('You’ll have 15 of 25 points to give left.');
    expect(afterText(3, 3, 25)).toBe('This uses all your points to give this month.');
    expect(sendLabel(1, false)).toBe('Give 1 point');
    expect(sendLabel(5, false)).toBe('Give 5 points');
    expect(sendLabel(5, true)).toBe('Sending…');
  });
});

describe('describeSendError', () => {
  const ctx = { amount: 5, receiverName: 'Priya Raman', isSelf: false, freshLeft: null };
  const badRequest = new ApiError(400, { status: 400, title: 'Bad request', detail: 'You only have 3 points left to give this month.' });

  it('explains a balance that changed since the drawer opened', () => {
    expect(describeSendError(badRequest, { ...ctx, freshLeft: 3 })).toEqual({
      title: 'Not enough points to give',
      detail: 'Your balance changed since you opened this. You have 3 points to give left, so lower the amount and try again.',
    });
  });

  it('names the colleague who wasn’t found', () => {
    expect(describeSendError(new ApiError(404, { status: 404 }), ctx).detail).toBe(
      'We couldn’t find Priya Raman. They may have left the company. Choose someone else.',
    );
  });

  it('catches sending to yourself', () => {
    expect(describeSendError(badRequest, { ...ctx, freshLeft: 10, isSelf: true }).title).toBe('You can’t recognize yourself');
  });

  it('says when the network is down, and falls back to a generic message', () => {
    expect(describeSendError(new NetworkError(new TypeError('Failed to fetch')), ctx).title).toBe('You seem to be offline');
    expect(describeSendError(new ApiError(500, { status: 500 }), ctx).title).toBe('We couldn’t send this');
  });
});
