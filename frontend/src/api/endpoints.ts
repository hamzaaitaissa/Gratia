import { download, request } from './http';
import type {
  AcceptInvitation,
  ChangePassword,
  CompanySignup,
  CreateTransaction,
  DomainCheckResult,
  DomainClaim,
  DomainVerification,
  ImportPreview,
  ImportResult,
  ImportRow,
  Invitation,
  InviteUser,
  ReadCompany,
  ReadTransaction,
  ReadUser,
  ResetPassword,
  TokenResponse,
  TransactionHistory,
  UpdateCompany,
  UpdateUser,
} from './types';

const id = (value: string) => encodeURIComponent(value);

export const api = {
  // LoginDto still calls the plain password HashedPassword (TASKS.md #39)
  login: (email: string, password: string) =>
    request<TokenResponse>('/api/auth/login', { method: 'POST', body: { email, hashedPassword: password }, auth: false }),
  // Signs out this device only. Anonymous on purpose: an expired access token mustn't trigger a refresh just to sign out.
  revokeToken: (refreshToken: string) => request<void>('/api/auth/revoke-token', { method: 'POST', body: refreshToken, auth: false }),
  /** With no token, signs out every device of the caller, and their access tokens stop working at once (#50). */
  revokeAllTokens: () => request<void>('/api/auth/revoke-token', { method: 'POST' }),
  /** Always 202 for valid input: what happened is told only by email (TASKS.md #1). */
  registerCompany: (body: CompanySignup) => request<void>('/api/auth/register-company', { method: 'POST', body, auth: false }),
  /** Always 202, whether or not the address has an account: the email says which (#50). */
  forgotPassword: (email: string) => request<void>('/api/auth/forgot-password', { method: 'POST', body: { email }, auth: false }),
  /** Signs in: every other session has ended. */
  resetPassword: (body: ResetPassword) => request<TokenResponse>('/api/auth/reset-password', { method: 'POST', body, auth: false }),

  myCompany: (signal?: AbortSignal) => request<ReadCompany>('/api/company/me', { signal }),
  updateCompany: (body: UpdateCompany) => request<ReadCompany>(`/api/company/${id(body.id)}`, { method: 'PUT', body }),

  // Domain verification (#61), for every member: an admin verifies the domain, anyone else becomes an admin by it
  domainVerification: (signal?: AbortSignal) => request<DomainVerification>('/api/company/domain-verification', { signal }),
  /** The caller's own DNS record: the same one each time, until it has been found. */
  startDomainVerification: () => request<DomainVerification>('/api/company/domain-verification', { method: 'POST' }),
  /** 200 whether the record is there or not: `status` says which, `message` what to do. */
  checkDomainVerification: () => request<DomainCheckResult>('/api/company/domain-verification/check', { method: 'POST' }),
  /** The claim link from signup's "already on Gratia" email: the workspace and the record to add. */
  domainClaim: (token: string, signal?: AbortSignal) => request<DomainClaim>(`/api/auth/domain-claim/${id(token)}`, { signal, auth: false }),
  checkDomainClaim: (token: string) => request<DomainCheckResult>('/api/auth/domain-claim/check', { method: 'POST', body: { token }, auth: false }),

  /** New tokens for this device: every session, this one's old tokens included, has ended (#50). */
  changePassword: (body: ChangePassword) => request<TokenResponse>('/api/user/me/password', { method: 'POST', body }),
  companyUsers: (signal?: AbortSignal) => request<ReadUser[]>('/api/user', { signal }),
  deactivatedUsers: (signal?: AbortSignal) => request<ReadUser[]>('/api/user/deactivated', { signal }),
  updateUser: (body: UpdateUser & { id: string }) => request<ReadUser>(`/api/user/${id(body.id)}`, { method: 'PUT', body }),
  deactivateUser: (userId: string) => request<void>(`/api/user/${id(userId)}/deactivate`, { method: 'POST' }),
  reactivateUser: (userId: string) => request<void>(`/api/user/${id(userId)}/reactivate`, { method: 'POST' }),

  pendingInvitations: (signal?: AbortSignal) => request<Invitation[]>('/api/invitation/pending', { signal }),
  /** Queues the email, which goes out in the background (#55): the invitation's `emailStatus` says when. */
  invite: (body: InviteUser) => request<Invitation>('/api/invitation', { method: 'POST', body }),
  resendInvitation: (invitationId: string) => request<Invitation>(`/api/invitation/${id(invitationId)}/resend`, { method: 'POST' }),
  cancelInvitation: (invitationId: string) => request<void>(`/api/invitation/${id(invitationId)}`, { method: 'DELETE' }),
  invitationByToken: (token: string, signal?: AbortSignal) =>
    request<Invitation>(`/api/invitation/token/${id(token)}`, { signal, auth: false }),
  /** Signs the new member in (#66). */
  acceptInvitation: (body: AcceptInvitation) => request<TokenResponse>('/api/invitation/accept', { method: 'POST', body, auth: false }),

  // Bulk invitations (#54), admins only
  importTemplate: () => download('/api/invitation/import/template', 'gratia-invitations.xlsx'),
  /** What importing the file would do with each row. Saves nothing. */
  previewImport: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request<ImportPreview>('/api/invitation/import/preview', { method: 'POST', body: form });
  },
  /** Checks every row again, and invites the ones still Ready. */
  importInvitations: (rows: ImportRow[]) => request<ImportResult>('/api/invitation/import', { method: 'POST', body: { rows } }),

  companyFeed: (page: number, pageSize: number, signal?: AbortSignal) =>
    request<TransactionHistory>(`/api/transaction/company-history?page=${page}&pageSize=${pageSize}`, { signal }),
  received: (signal?: AbortSignal) => request<ReadTransaction[]>('/api/transaction/received', { signal }),
  myHistory: (signal?: AbortSignal) => request<ReadTransaction[]>('/api/transaction/my-history', { signal }),
  transaction: (txId: string, signal?: AbortSignal) => request<ReadTransaction>(`/api/transaction/${id(txId)}`, { signal }),
  give: (body: CreateTransaction) => request<ReadTransaction>('/api/transaction', { method: 'POST', body }),
};
