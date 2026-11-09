import { ApiError } from './http';

/**
 * Invite, resend and signup answer 503 when the email can't be sent, and then change nothing (#20): no invitation is
 * created, a resent one keeps its old link, signing up can simply be tried again.
 */
export const isEmailFailure = (err: unknown) => err instanceof ApiError && err.status === 503;

export const EMAIL_FAILED = 'We couldn’t send the email. Nothing was changed; try again in a moment.';
