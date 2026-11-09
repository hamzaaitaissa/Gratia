import { request } from './http';
import type {
  AcceptInvitation,
  CompanySignup,
  CreateTransaction,
  Invitation,
  InviteUser,
  ReadCompany,
  ReadTransaction,
  ReadUser,
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
  // Signs out this device only (with no token, the API signs out all of the caller's devices). Anonymous on purpose:
  // an expired access token mustn't trigger a refresh just to sign out.
  revokeToken: (refreshToken: string) => request<void>('/api/auth/revoke-token', { method: 'POST', body: refreshToken, auth: false }),
  /** Always 202 for valid input: what happened is told only by email (TASKS.md #1). */
  registerCompany: (body: CompanySignup) => request<void>('/api/auth/register-company', { method: 'POST', body, auth: false }),

  myCompany: (signal?: AbortSignal) => request<ReadCompany>('/api/company/me', { signal }),
  updateCompany: (body: UpdateCompany) => request<ReadCompany>(`/api/company/${id(body.id)}`, { method: 'PUT', body }),

  companyUsers: (signal?: AbortSignal) => request<ReadUser[]>('/api/user', { signal }),
  deactivatedUsers: (signal?: AbortSignal) => request<ReadUser[]>('/api/user/deactivated', { signal }),
  updateUser: (body: UpdateUser & { id: string }) => request<ReadUser>(`/api/user/${id(body.id)}`, { method: 'PUT', body }),
  deactivateUser: (userId: string) => request<void>(`/api/user/${id(userId)}/deactivate`, { method: 'POST' }),
  reactivateUser: (userId: string) => request<void>(`/api/user/${id(userId)}/reactivate`, { method: 'POST' }),

  pendingInvitations: (signal?: AbortSignal) => request<Invitation[]>('/api/invitation/pending', { signal }),
  invite: (body: InviteUser) => request<Invitation>('/api/invitation', { method: 'POST', body }),
  resendInvitation: (invitationId: string) => request<Invitation>(`/api/invitation/${id(invitationId)}/resend`, { method: 'POST' }),
  cancelInvitation: (invitationId: string) => request<void>(`/api/invitation/${id(invitationId)}`, { method: 'DELETE' }),
  invitationByToken: (token: string, signal?: AbortSignal) =>
    request<Invitation>(`/api/invitation/token/${id(token)}`, { signal, auth: false }),
  acceptInvitation: (body: AcceptInvitation) => request<void>('/api/invitation/accept', { method: 'POST', body, auth: false }),

  companyFeed: (page: number, pageSize: number, signal?: AbortSignal) =>
    request<TransactionHistory>(`/api/transaction/company-history?page=${page}&pageSize=${pageSize}`, { signal }),
  received: (signal?: AbortSignal) => request<ReadTransaction[]>('/api/transaction/received', { signal }),
  myHistory: (signal?: AbortSignal) => request<ReadTransaction[]>('/api/transaction/my-history', { signal }),
  transaction: (txId: string, signal?: AbortSignal) => request<ReadTransaction>(`/api/transaction/${id(txId)}`, { signal }),
  give: (body: CreateTransaction) => request<ReadTransaction>('/api/transaction', { method: 'POST', body }),
};
