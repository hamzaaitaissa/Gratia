import { ApiError } from './http';

/**
 * Signup and forgot password answer 503 when their email can't be sent, and then change nothing (#20): trying again
 * is safe. Invitation emails go out in the background instead (#55), so inviting never answers 503.
 */
export const isEmailFailure = (err: unknown) => err instanceof ApiError && err.status === 503;

export const EMAIL_FAILED = 'We couldn’t send the email. Nothing was changed; try again in a moment.';

/**
 * A 429 is one of two things. The API's own limits (a locked email after failed sign-ins #72, the daily invitation
 * cap #65) have the title "Too many attempts" and a detail written for the person. The per-IP rate limiter's
 * title is "Too many requests", with no detail worth showing.
 */
export function limitDetail(err: unknown): string | null {
  if (!(err instanceof ApiError) || err.status !== 429) return null;
  return err.problem.title === 'Too many attempts' && err.problem.detail ? err.problem.detail : null;
}

export const isRateLimited = (err: unknown) => err instanceof ApiError && err.status === 429;

/** "a minute", "15 minutes", "7 hours": a Retry-After in words, rounded up. */
export function waitText(seconds: number | undefined): string {
  if (!seconds || seconds <= 60) return 'a minute';
  const minutes = Math.ceil(seconds / 60);
  if (minutes < 60) return `${minutes} minutes`;
  const hours = Math.ceil(minutes / 60);
  return hours === 1 ? 'an hour' : `${hours} hours`;
}
