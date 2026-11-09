// The API's DTOs, generated from its OpenAPI document into schema.d.ts (`npm run api:types`, TASKS.md #59).
// After an API change, regenerate it and `npm run typecheck` shows what to follow.
import type { components } from './schema';

type S = components['schemas'];

/**
 * Integers are `integer | string` in the document, because ASP.NET also reads numbers sent as strings. The API always
 * writes numbers, and the frontend sends numbers.
 */
type Wire<V> = number extends V ? Exclude<V, string> : V extends (infer I)[] ? Sent<I>[] : V extends object ? Sent<V> : V;

/** A response as the API sends it: System.Text.Json writes every property, so none is missing (some may be null). */
type Sent<T> = { [K in keyof T]-?: Wire<Exclude<T[K], undefined>> };

/** A request body: optional where the API allows leaving a field out. */
type Body<T> = { [K in keyof T]: Wire<T[K]> };

export type UserRole = S['UserRole'];
export type ReadUser = Sent<S['ReadUserDto']>;
export type ReadCompany = Sent<S['ReadCompanyDto']>;
export type ReadTransaction = Sent<S['ReadTransactionDto']>;
export type TransactionHistory = Sent<S['ReadTransactionHistoryDto']>;
export type Invitation = Sent<S['InvitationDto']>;

export type CreateTransaction = Body<S['CreateTransactionDto']>;
export type UpdateUser = Body<S['UpdateUserDto']>;
export type UpdateCompany = Body<S['UpdateCompanyDto']>;
export type CompanySignup = Body<S['CompanySignupDto']>;
export type InviteUser = Body<S['InviteUserDto']>;
export type AcceptInvitation = Body<S['AcceptInvitationDto']>;

// Not in the OpenAPI document (login and refresh return untyped results; errors are ProblemDetails)
export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  refreshTokenExpiryTime: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string | null;
  traceId?: string;
  errors?: Record<string, string[]>;
}
