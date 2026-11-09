import { useSyncExternalStore } from 'react';
import type { TokenResponse, UserRole } from '../api/types';

/*
 * The signed-in session: the API's access and refresh tokens, kept in localStorage so a reload stays signed in.
 * The tokens travel in the Authorization header (the API allows no credentials over CORS), so there are no cookies.
 */

export interface Session {
  accessToken: string;
  refreshToken: string;
  userId: string;
  companyId: string;
  role: UserRole;
  email: string;
}

const STORAGE_KEY = 'gratia.session';

// ClaimTypes.* are written as these short names by JwtSecurityTokenHandler's outbound claim map
const CLAIMS = {
  userId: ['nameid', 'sub', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
  role: ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
  email: ['email', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
  companyId: ['CompanyId'],
};

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const part = token.split('.')[1];
  if (!part) return null;
  try {
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '=');
    const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function claim(payload: Record<string, unknown>, names: string[]): string {
  for (const n of names) {
    const v = payload[n];
    if (typeof v === 'string') return v;
    if (Array.isArray(v) && typeof v[0] === 'string') return v[0];
  }
  return '';
}

export function sessionFromTokens(tokens: Pick<TokenResponse, 'accessToken' | 'refreshToken'>): Session | null {
  const payload = decodeJwtPayload(tokens.accessToken);
  if (!payload) return null;
  const userId = claim(payload, CLAIMS.userId);
  const companyId = claim(payload, CLAIMS.companyId);
  if (!userId || !companyId) return null;
  return {
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    userId,
    companyId,
    role: claim(payload, CLAIMS.role) === 'Admin' ? 'Admin' : 'Employee',
    email: claim(payload, CLAIMS.email),
  };
}

function load(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const stored = JSON.parse(raw) as { accessToken?: string; refreshToken?: string };
    if (!stored.accessToken || !stored.refreshToken) return null;
    return sessionFromTokens({ accessToken: stored.accessToken, refreshToken: stored.refreshToken });
  } catch {
    return null;
  }
}

let current: Session | null = load();
const listeners = new Set<() => void>();

export function getSession(): Session | null {
  return current;
}

/**
 * Re-reads the session another tab may have written. A refresh token works only once, so after one tab
 * refreshes, the others must pick up its new tokens rather than reuse the old ones.
 */
export function syncFromStorage(): Session | null {
  const stored = load();
  if (stored?.accessToken !== current?.accessToken || stored?.refreshToken !== current?.refreshToken) {
    current = stored;
    listeners.forEach((l) => l());
  }
  return current;
}

// Signing in, refreshing or signing out in another tab applies here too
if (typeof window !== 'undefined')
  window.addEventListener('storage', (e) => {
    if (e.key === STORAGE_KEY || e.key === null) syncFromStorage();
  });

let expired = false;

/** True when the session ended because the refresh token stopped working, rather than by signing out. */
export function sessionExpired(): boolean {
  return expired;
}

/** Ends the session because it can't be refreshed any more: the app then shows "Your session has expired". */
export function expireSession() {
  setSession(null);
  expired = true;
  listeners.forEach((l) => l());
}

export function setSession(next: Session | null) {
  current = next;
  expired = false;
  try {
    if (next) localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken: next.accessToken, refreshToken: next.refreshToken }));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage can be blocked (private mode); the session then lasts for this page only
  }
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useSession(): Session | null {
  return useSyncExternalStore(subscribe, getSession, getSession);
}
