import { QueryClient, useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { useSession } from '../auth/session';
import { pointsCycle, type PointsCycle } from '../lib/pointsCycle';
import { api } from './endpoints';
import { ApiError } from './http';
import type { ReadTransaction } from './types';

export const queryKeys = {
  company: ['company', 'me'] as const,
  users: ['users'] as const,
  feed: ['feed'] as const,
  /** Prefix of every list of the caller's own recognitions: invalidate it after giving. */
  transactions: ['transactions'] as const,
  received: ['transactions', 'received'] as const,
  mine: ['transactions', 'mine'] as const,
  transaction: (id: string) => ['transaction', id] as const,
  /** Under the users prefix, so invalidating users refreshes it too. */
  deactivated: ['users', 'deactivated'] as const,
  invitations: ['invitations', 'pending'] as const,
};

export const FEED_PAGE_SIZE = 10;

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // A 4xx won't fix itself on a retry
        retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
      },
    },
  });
}

export const sameId = (a: string | null | undefined, b: string | null | undefined) => !!a && !!b && a.toLowerCase() === b.toLowerCase();

export function useCompany() {
  return useQuery({ queryKey: queryKeys.company, queryFn: ({ signal }) => api.myCompany(signal), staleTime: 5 * 60_000 });
}

/** The company's allowance and refill dates; undefined until the company has loaded. */
export function usePointsCycle(): PointsCycle | undefined {
  const { data } = useCompany();
  return useMemo(() => (data ? pointsCycle(data) : undefined), [data]);
}

/** Everyone in the caller's company, by name. Also the source of the caller's own balances (no /api/user/me yet, #50). */
export function useUsers() {
  return useQuery({ queryKey: queryKeys.users, queryFn: ({ signal }) => api.companyUsers(signal) });
}

export function useMe() {
  const session = useSession();
  const users = useUsers();
  const me = useMemo(() => users.data?.find((u) => sameId(u.id, session?.userId)), [users.data, session?.userId]);
  return { ...users, me };
}

export function useCompanyFeed() {
  const query = useInfiniteQuery({
    queryKey: queryKeys.feed,
    queryFn: ({ pageParam, signal }) => api.companyFeed(pageParam, FEED_PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });

  // Offset paging: a gift sent between two page loads shifts the rows, so the same one can come back twice
  const items = useMemo(() => {
    const seen = new Set<string>();
    const out: ReadTransaction[] = [];
    for (const page of query.data?.pages ?? [])
      for (const t of page.transactions)
        if (!seen.has(t.id)) {
          seen.add(t.id);
          out.push(t);
        }
    return out;
  }, [query.data]);

  const total = query.data?.pages.at(-1)?.totalCount ?? 0;
  return { ...query, items, total: Math.max(total, items.length) };
}

export function useReceived() {
  return useQuery({ queryKey: queryKeys.received, queryFn: ({ signal }) => api.received(signal) });
}

/** Everything the caller gave or received, newest first. */
export function useMyHistory() {
  return useQuery({ queryKey: queryKeys.mine, queryFn: ({ signal }) => api.myHistory(signal) });
}

export function useDeactivatedUsers() {
  return useQuery({ queryKey: queryKeys.deactivated, queryFn: ({ signal }) => api.deactivatedUsers(signal) });
}

export function usePendingInvitations() {
  return useQuery({ queryKey: queryKeys.invitations, queryFn: ({ signal }) => api.pendingInvitations(signal) });
}

/**
 * The fields of a 400 ValidationProblemDetails, keyed by camelCase field name ("JobTitle" → "jobTitle"), so a form
 * can show the server's complaint next to the right field.
 */
export function fieldErrors(error: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  if (!(error instanceof ApiError) || !error.problem.errors) return out;
  for (const [key, messages] of Object.entries(error.problem.errors)) {
    const field = key.replace(/^\$\./, '');
    out[field.charAt(0).toLowerCase() + field.slice(1)] = messages[0] ?? '';
  }
  return out;
}

/** One recognition. Only its sender and receiver may read it: anyone else gets a 403, a wrong id a 404. */
export function useTransaction(id: string) {
  return useQuery({ queryKey: queryKeys.transaction(id), queryFn: ({ signal }) => api.transaction(id, signal) });
}
