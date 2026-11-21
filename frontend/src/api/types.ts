// The API's DTOs, generated from its OpenAPI document into schema.d.ts (`npm run api:types`, TASKS.md #59).
// After an API change, regenerate it and `npm run typecheck` shows what to follow.
import type { components } from './schema';

type S = components['schemas'];

/**
 * Integers are `integer | string` in the document, because ASP.NET also reads numbers sent as strings. The API always
 * writes numbers, and the frontend sends numbers.
 */
type Wire<V> = number extends V ? Exclude<V, string> : V extends (infer I)[] ? Sent<I>[] : V extends object ? Sent<V> : V;

/**
 * A response as the API sends it: System.Text.Json writes every property, so none is missing.
 *
 * The DTOs declare their strings `string?`, so the document says nearly every one can be null, but the values behind
 * them can't be (the entities' columns are required; TASKS.md #89). So null is dropped, except from the properties
 * named in `Nullable`, which really can be null.
 */
type Sent<T, Nullable extends keyof T = never> = {
  [K in keyof T]-?: K extends Nullable ? Wire<Exclude<T[K], undefined>> : Wire<Exclude<T[K], undefined | null>>;
};

/** A request body: optional where the API allows leaving a field out. */
type Body<T> = { [K in keyof T]: Wire<T[K]> };

export type UserRole = S['UserRole'];
/** `deactivatedAt` is set only in the admins' list of deactivated users. */
export type ReadUser = Sent<S['ReadUserDto'], 'deactivatedAt'>;
/** `domain` is null for a company that never had one (made before onboarding, #49). */
export type ReadCompany = Sent<S['ReadCompanyDto'], 'domain'>;
export type ReadTransaction = Sent<S['ReadTransactionDto']>;
export type TransactionHistory = Sent<S['ReadTransactionHistoryDto']>;
/** `fullName` and `jobTitle` are what the admin or the spreadsheet filled in; `emailSentAt` is null until it's sent. */
export type Invitation = Sent<S['InvitationDto'], 'fullName' | 'jobTitle' | 'emailSentAt'>;
export type InvitationEmailStatus = S['InvitationEmailStatus'];
export type TokenResponse = Sent<S['TokenResponseDto']>;

export type ImportRowStatus = S['ImportRowStatus'];
/** `message` says why, when the row isn't Ready. */
export type ImportPreviewRow = Sent<S['ImportPreviewRowDto'], 'message'>;
export type ImportPreview = Sent<S['ImportPreviewDto']>;
export type ImportResult = Sent<S['ImportResultDto']>;
export type ImportSkippedRow = Sent<S['ImportSkippedRowDto']>;

export type CreateTransaction = Body<S['CreateTransactionDto']>;
export type UpdateUser = Body<S['UpdateUserDto']>;
export type UpdateCompany = Body<S['UpdateCompanyDto']>;
export type CompanySignup = Body<S['CompanySignupDto']>;
export type InviteUser = Body<S['InviteUserDto']>;
export type AcceptInvitation = Body<S['AcceptInvitationDto']>;
export type ChangePassword = Body<S['ChangePasswordDto']>;
export type ResetPassword = Body<S['ResetPasswordDto']>;
export type ImportRow = Body<S['ImportRowDto']>;

// Not in the OpenAPI document: errors are ProblemDetails
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string | null;
  traceId?: string;
  errors?: Record<string, string[]>;
}
