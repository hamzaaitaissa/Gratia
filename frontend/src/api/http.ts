import { expireSession, getSession, sessionFromTokens, setSession, syncFromStorage } from '../auth/session';
import type { ProblemDetails, TokenResponse } from './types';

export const API_BASE_URL: string = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';

/** Any non-2xx answer, carrying the API's ProblemDetails when there is one. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  /** A 429's Retry-After, in seconds, when the API sent one. */
  readonly retryAfter: number | undefined;

  constructor(status: number, problem: ProblemDetails, retryAfter?: number) {
    super(problem.detail || problem.title || `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
    this.retryAfter = retryAfter;
  }

  get traceId(): string | undefined {
    return this.problem.traceId;
  }
}

/** The network itself failed (offline, API down, CORS). */
export class NetworkError extends Error {
  constructor(cause: unknown) {
    super('The network request failed.', { cause });
    this.name = 'NetworkError';
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  /** Sent as JSON, or as it is when it's a FormData (a file upload). */
  body?: unknown;
  /** Send the access token, and refresh it once on a 401. Defaults to true. */
  auth?: boolean;
  signal?: AbortSignal;
}

async function send(path: string, opts: RequestOptions, accessToken: string | null): Promise<Response> {
  const form = opts.body instanceof FormData ? opts.body : null;
  const headers: Record<string, string> = { Accept: 'application/json' };
  // A FormData's Content-Type carries its boundary, which fetch writes itself
  if (opts.body !== undefined && !form) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  try {
    return await fetch(API_BASE_URL + path, {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body === undefined ? undefined : (form ?? JSON.stringify(opts.body)),
      signal: opts.signal,
    });
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e;
    throw new NetworkError(e);
  }
}

async function toError(res: Response): Promise<ApiError> {
  let problem: ProblemDetails = { status: res.status };
  try {
    const text = await res.text();
    if (text) problem = { status: res.status, ...(JSON.parse(text) as ProblemDetails) };
  } catch {
    // Not JSON: keep the status only
  }
  const retryAfter = Number(res.headers.get('Retry-After'));
  return new ApiError(res.status, problem, Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : undefined);
}

// A refresh token works only once: the API swaps it for a new one. So one refresh at a time, across tabs too
// (Web Locks), and whoever waited first checks whether someone else already got new tokens.
let refreshing: Promise<boolean> | null = null;

function withRefreshLock(fn: () => Promise<boolean>): Promise<boolean> {
  return 'locks' in navigator && navigator.locks ? navigator.locks.request('gratia-session-refresh', fn) : fn();
}

function refreshSession(tokenUsed: string): Promise<boolean> {
  refreshing ??= withRefreshLock(async () => {
    // Another tab may have refreshed while this one waited: its tokens are in storage
    const session = syncFromStorage();
    if (!session) return false;
    if (session.accessToken !== tokenUsed) return true;
    try {
      const res = await send('/api/auth/refresh-token', { method: 'POST', body: session.refreshToken, auth: false }, null);
      if (!res.ok) return false;
      const next = sessionFromTokens((await res.json()) as TokenResponse);
      if (!next) return false;
      setSession(next);
      return true;
    } catch {
      return false;
    }
  }).finally(() => {
    refreshing = null;
  });
  return refreshing;
}

/** Sends the request, refreshing the session once on a 401, and returns the response if it's a success. */
async function fetchOk(path: string, opts: RequestOptions): Promise<Response> {
  const auth = opts.auth ?? true;
  const tokenUsed = auth ? (getSession()?.accessToken ?? null) : null;
  let res = await send(path, opts, tokenUsed);

  if (res.status === 401 && auth && tokenUsed) {
    // This tab or another one may already have refreshed while this request was in flight
    const fresh = syncFromStorage()?.accessToken;
    const ok = fresh && fresh !== tokenUsed ? true : await refreshSession(tokenUsed);
    if (!ok) {
      expireSession(); // the refresh token is gone or expired: "Your session has expired", then sign-in
      throw await toError(res);
    }
    res = await send(path, opts, getSession()?.accessToken ?? null);
  }

  if (!res.ok) throw await toError(res);
  return res;
}

export async function request<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const res = await fetchOk(path, opts);
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** A file the API sends (a download), named as its Content-Disposition says when it says. */
export async function download(path: string, fallbackName: string): Promise<{ blob: Blob; fileName: string }> {
  const res = await fetchOk(path, {});
  const disposition = res.headers.get('Content-Disposition') ?? '';
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1];
  return { blob: await res.blob(), fileName: encoded ? decodeURIComponent(encoded) : (plain ?? fallbackName) };
}
