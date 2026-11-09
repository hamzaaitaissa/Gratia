// The DOM test setup (matchMedia stub, cleanup) comes in with these helpers: see the note in vite.config.ts
import './setup';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';
import { vi } from 'vitest';
import type { ReadCompany, ReadTransaction, ReadUser } from '../api/types';
import { sessionFromTokens, setSession } from '../auth/session';
import { ToastProvider, ToastViewport } from '../components/Toast';
import { ThemeModeProvider } from '../theme/ThemeMode';

const b64url = (o: object) => btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** An unsigned JWT with the claims the API puts in its access tokens. */
export function fakeToken(claims: Record<string, string>) {
  return `${b64url({ alg: 'HS512', typ: 'JWT' })}.${b64url(claims)}.signature`;
}

export function signIn(user: Pick<ReadUser, 'id' | 'companyId' | 'role' | 'email'>, suffix = '1') {
  setSession(
    sessionFromTokens({
      accessToken: fakeToken({ nameid: user.id, CompanyId: user.companyId, role: user.role, email: user.email, n: suffix }),
      refreshToken: 'refresh-' + suffix,
    }),
  );
}

export const COMPANY: ReadCompany = {
  id: 'c1',
  name: 'Fernhill Labs',
  logoUrl: '',
  primaryColor: '#1B2A4A',
  secondaryColor: '#C8A15A',
  domain: 'fernhilllabs.com',
  isDomainVerified: false,
  monthlyPointsAllowance: 25,
  pointsResetDay: 21,
  // A period that started 19 days ago and ends in 12, whatever today is
  lastPointsResetAt: new Date(Date.now() - 19 * 864e5).toISOString(),
  nextPointsResetAt: new Date(Date.now() + 12 * 864e5).toISOString(),
};

export function user(id: string, fullName: string, available: number, acquired: number, extra: Partial<ReadUser> = {}): ReadUser {
  return {
    id,
    deactivatedAt: null,
    fullName,
    email: fullName.toLowerCase().replace(/\s+/g, '.') + '@fernhilllabs.com',
    jobTitle: 'Engineer',
    role: 'Employee',
    numberOfPointsAvailable: available,
    numberOfPointsAcquired: acquired,
    companyId: 'c1',
    ...extra,
  };
}

export function tx(id: string, from: ReadUser, to: ReadUser, amount: number, message: string, minutesAgo = 5): ReadTransaction {
  return {
    id,
    senderId: from.id,
    senderName: from.fullName,
    receiverId: to.id,
    receiverName: to.fullName,
    companyId: 'c1',
    companyName: 'Fernhill Labs',
    amount,
    message,
    typeOfDonation: 'Teamwork',
    createdAt: new Date(Date.now() - minutesAgo * 6e4).toISOString().replace('Z', ''),
  };
}

export interface MockRequest {
  method: string;
  path: string;
  body: unknown;
  headers: Record<string, string>;
}

type Handler = (req: MockRequest) => { status?: number; body?: unknown } | Promise<{ status?: number; body?: unknown }>;

/** Stubs fetch. Keys are "METHOD /path" (query string ignored); unknown routes answer 404. */
export function mockApi(routes: Record<string, Handler>) {
  const calls: MockRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost');
    const method = (init?.method ?? 'GET').toUpperCase();
    const req: MockRequest = {
      method,
      path: url.pathname + url.search,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
      headers: (init?.headers ?? {}) as Record<string, string>,
    };
    calls.push(req);
    const handler = routes[`${method} ${url.pathname}`];
    const res = handler ? await handler(req) : { status: 404, body: { title: 'Not found', status: 404 } };
    const status = res.status ?? 200;
    return new Response(res.body === undefined ? null : JSON.stringify(res.body), {
      status,
      headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return { calls, fetchMock };
}

/** `route` is a path, or a path with navigation state (what `navigate(to, { state })` would carry). */
export function renderWithProviders(ui: ReactElement, { route = '/' }: { route?: string | { pathname: string; state?: unknown } } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
          <ToastViewport />
        </ToastProvider>
      </ThemeModeProvider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}
