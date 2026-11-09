import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { api } from '../../api/endpoints';
import { ApiError } from '../../api/http';
import { queryKeys, sameId, useCompany, useDeactivatedUsers, useMe } from '../../api/queries';
import type { ReadUser } from '../../api/types';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { BreakableEmail } from '../../components/BreakableEmail';
import { ConfirmDialog } from '../../components/Dialog';
import { Icon } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { RowsSkeleton } from '../../components/States';
import { useToast } from '../../components/Toast';
import { shortDate } from '../../lib/format';
import styles from './Admin.module.css';
import { EditMemberDialog } from './EditMemberDialog';

type SortKey = 'fullName' | 'email' | 'jobTitle' | 'role' | 'numberOfPointsAvailable' | 'numberOfPointsAcquired';
const NUMERIC: SortKey[] = ['numberOfPointsAvailable', 'numberOfPointsAcquired'];
const MOBILE_SORTS: [SortKey, string][] = [
  ['fullName', 'Name'],
  ['numberOfPointsAcquired', 'Points received'],
  ['numberOfPointsAvailable', 'Points to give'],
  ['role', 'Role'],
];

const problemDetail = (err: unknown) => (err instanceof ApiError && err.problem.detail) || 'Something went wrong. Try again in a moment.';

/** Admin · Members (design 6.10a): a sortable table on desktop, stacked cards on mobile. */
export function MembersPage() {
  const users = useMe();
  const deactivated = useDeactivatedUsers();
  const { data: company } = useCompany();
  const isMobile = useIsMobile();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<{ key: SortKey; dir: 1 | -1 }>({ key: 'fullName', dir: 1 });
  const [editing, setEditing] = useState<ReadUser | null>(null);
  const [confirming, setConfirming] = useState<ReadUser | null>(null);
  const { me } = users;

  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.users });
  const deactivate = useMutation({
    mutationFn: (u: ReadUser) => api.deactivateUser(u.id),
    onSuccess: (_, u) => {
      setConfirming(null);
      refresh();
      toast(`${u.fullName} was deactivated and can’t sign in any more.`);
    },
    onError: (err) => {
      setConfirming(null);
      toast(problemDetail(err), 'error');
    },
  });
  const reactivate = useMutation({
    mutationFn: (u: ReadUser) => api.reactivateUser(u.id),
    onSuccess: (_, u) => {
      refresh();
      toast(`${u.fullName} can sign in again.`);
    },
    onError: (err) => toast(problemDetail(err), 'error'),
  });

  const list = useMemo(() => {
    const q = query.trim().toLowerCase();
    const filtered = (users.data ?? []).filter(
      (u) => !q || u.fullName.toLowerCase().includes(q) || u.email.toLowerCase().includes(q) || (u.jobTitle || '').toLowerCase().includes(q),
    );
    return filtered.sort((a, b) => {
      const x = a[sort.key];
      const y = b[sort.key];
      const c = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y));
      return c * sort.dir || a.fullName.localeCompare(b.fullName);
    });
  }, [users.data, query, sort]);

  const header = <PageHeader title="Members" sub={company ? `Everyone with access to Gratia at ${company.name}.` : undefined} />;
  if (users.isError) return <>{header}<PageError error={users.error} onRetry={() => void users.refetch()} /></>;
  if (!users.data || !me) return <RowsSkeleton />;

  const sortBy = (key: SortKey) =>
    setSort((s) => (s.key === key ? { key, dir: s.dir === 1 ? -1 : 1 } : { key, dir: NUMERIC.includes(key) ? -1 : 1 }));
  const head = (key: SortKey, label: string, className?: string) => {
    const on = sort.key === key;
    return (
      <th aria-sort={on ? (sort.dir === 1 ? 'ascending' : 'descending') : 'none'} className={NUMERIC.includes(key) ? styles.num : undefined}>
        <button type="button" className={`${styles.sortButton} ${className ?? ''}`} onClick={() => sortBy(key)}>
          {label}
          <Icon name={on ? (sort.dir === 1 ? 'arrow_upward' : 'arrow_downward') : 'unfold_more'} size={16} className={on ? styles.sortIconOn : styles.sortIcon} />
        </button>
      </th>
    );
  };
  const isMe = (u: ReadUser) => sameId(u.id, me.id);

  return (
    <>
      {header}
      <div className={styles.page}>
        <div className={styles.toolbar}>
          <div className={styles.search}>
            <label htmlFor="members-search" className="sr-only">
              Search members
            </label>
            <Icon name="search" className={styles.searchIcon} />
            <input
              id="members-search"
              type="search"
              className={`input ${styles.searchInput}`}
              placeholder="Search by name, email or job title"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </div>
          {isMobile && (
            <label className={styles.sortLabel}>
              Sort
              <select
                className={styles.select}
                value={sort.key}
                onChange={(e) => {
                  const key = e.target.value as SortKey;
                  setSort({ key, dir: NUMERIC.includes(key) ? -1 : 1 });
                }}
              >
                {MOBILE_SORTS.map(([k, l]) => (
                  <option key={k} value={k}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
          )}
          <span role="status" className={styles.count}>
            {list.length} {list.length === 1 ? 'member' : 'members'}
          </span>
        </div>

        {list.length === 0 ? (
          <p className={styles.noMatch}>No members match “{query.trim()}”.</p>
        ) : isMobile ? (
          <ul className={styles.cards}>
            {list.map((u) => (
              <li key={u.id} className={styles.card}>
                <div className={styles.cardHead}>
                  <Avatar id={u.id} name={u.fullName} size={40} />
                  <div className={styles.cardText}>
                    <div className={styles.cardName}>
                      {u.fullName}
                      {isMe(u) && <span className={styles.you}>(you)</span>}
                      {u.role === 'Admin' && <span className={styles.rolePill}>Admin</span>}
                    </div>
                    <div className={styles.cardSub}>{u.email}</div>
                    <div className={styles.cardSub}>{u.jobTitle || 'No job title'}</div>
                  </div>
                </div>
                <div className={styles.cardNumbers}>
                  <span className={styles.cardGive}>
                    {u.numberOfPointsAvailable} <span className={styles.unit}>points to give</span>
                  </span>
                  <span className={styles.cardRecv}>
                    {u.numberOfPointsAcquired} <span className={styles.unit}>points received</span>
                  </span>
                </div>
                <div className={styles.cardActions}>
                  <button type="button" className={styles.cardAction} aria-label={`Edit ${u.fullName}`} onClick={() => setEditing(u)}>
                    <Icon name="edit" size={18} />
                    Edit
                  </button>
                  <button
                    type="button"
                    className={`${styles.cardAction} ${styles.dangerText}`}
                    aria-label={`Deactivate ${u.fullName}`}
                    disabled={isMe(u)}
                    onClick={() => setConfirming(u)}
                  >
                    <Icon name="person_remove" size={18} />
                    Deactivate
                  </button>
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <caption className="sr-only">Members, sortable by column</caption>
              <thead>
                <tr>
                  {head('fullName', 'Name')}
                  {head('email', 'Email')}
                  {head('jobTitle', 'Job title')}
                  {head('role', 'Role')}
                  {head('numberOfPointsAvailable', 'Points to give', styles.giveHead)}
                  {head('numberOfPointsAcquired', 'Points received', styles.recvHead)}
                  <th>
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div className={styles.person}>
                        <Avatar id={u.id} name={u.fullName} size={34} />
                        <span className={`${styles.strong} ${styles.nowrap}`}>{u.fullName}</span>
                        {isMe(u) && <span className={`${styles.you} ${styles.nowrap}`}>(you)</span>}
                      </div>
                    </td>
                    <td className={styles.muted}>
                      <BreakableEmail email={u.email} />
                    </td>
                    <td className={styles.muted}>{u.jobTitle || '—'}</td>
                    <td>
                      <span className={styles.rolePill}>
                        {u.role === 'Admin' && <Icon name="shield_person" size={14} />}
                        {u.role}
                      </span>
                    </td>
                    <td className={styles.num}>{u.numberOfPointsAvailable}</td>
                    <td className={`${styles.num} ${styles.strong}`}>{u.numberOfPointsAcquired}</td>
                    <td className={styles.actionsCell}>
                      <button type="button" className={styles.iconButton} aria-label={`Edit ${u.fullName}`} title="Edit" onClick={() => setEditing(u)}>
                        <Icon name="edit" />
                      </button>
                      <button
                        type="button"
                        className={`${styles.iconButton} ${styles.dangerIcon}`}
                        aria-label={`Deactivate ${u.fullName}`}
                        title={isMe(u) ? 'You can’t deactivate yourself' : 'Deactivate'}
                        disabled={isMe(u)}
                        onClick={() => setConfirming(u)}
                      >
                        <Icon name="person_remove" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {(deactivated.data?.length ?? 0) > 0 && (
          <section aria-labelledby="members-deactivated" className={styles.section}>
            <div className={styles.sectionHead}>
              <h2 id="members-deactivated" className={styles.sectionTitle}>
                Deactivated
              </h2>
              <span className={styles.sectionNote}>Can’t sign in. Their recognitions stay in the feed.</span>
            </div>
            <ul className={styles.cards}>
              {deactivated.data!.map((u) => (
                <li key={u.id} className={styles.card}>
                  <div className={styles.cardHead}>
                    <Avatar id={u.id} name={u.fullName} size={40} />
                    <div className={styles.cardText}>
                      <div className={styles.cardName}>{u.fullName}</div>
                      <div className={styles.cardSub}>
                        {u.email}
                        {u.deactivatedAt ? ` · deactivated ${shortDate(u.deactivatedAt)}` : ''}
                      </div>
                    </div>
                    <button
                      type="button"
                      className={styles.textButton}
                      aria-label={`Reactivate ${u.fullName}`}
                      disabled={reactivate.isPending}
                      onClick={() => reactivate.mutate(u)}
                    >
                      <Icon name="how_to_reg" size={18} style={{ verticalAlign: 'middle', marginRight: 4 }} />
                      Reactivate
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>

      {editing && <EditMemberDialog member={editing} isSelf={isMe(editing)} onClose={() => setEditing(null)} />}
      {confirming && (
        <ConfirmDialog
          title={`Deactivate ${confirming.fullName}?`}
          body={`${confirming.fullName} will lose access to Gratia straight away. Their recognitions stay in the feed, and you can reactivate them later.`}
          confirmLabel="Deactivate"
          busy={deactivate.isPending}
          onConfirm={() => deactivate.mutate(confirming)}
          onCancel={() => setConfirming(null)}
        />
      )}
    </>
  );
}
