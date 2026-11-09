import type { ReactNode } from 'react';
import { useCompany, useMe } from '../../api/queries';
import { useSession } from '../../auth/session';
import { Forbidden } from '../../components/Page';
import { RowsSkeleton } from '../../components/States';

/**
 * Admin pages show "You don't have access" to employees (design 6.12). The API checks the role on every call
 * anyway; this only spares employees a page of failing requests.
 */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const session = useSession();
  const { me, isPending } = useMe();
  const { data: company } = useCompany();
  const role = me?.role ?? (isPending ? session?.role : undefined);
  if (isPending && !role) return <RowsSkeleton />;
  if (role !== 'Admin') return <Forbidden companyName={company?.name} />;
  return children;
}
