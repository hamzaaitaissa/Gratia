import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { sameId, useCompany, useMe } from '../../api/queries';
import type { ReadUser } from '../../api/types';
import { useIsMobile } from '../../app/useIsMobile';
import { Avatar } from '../../components/Avatar';
import { Icon } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { EmptyState, RowsSkeleton } from '../../components/States';
import { useGive } from '../give/GiveContext';
import styles from './People.module.css';

/** The first letter a name is filed under: "Émile" goes with the Es. */
const letterOf = (name: string) => (name.normalize('NFD')[0] ?? '#').toUpperCase();

/** "People" (design 6.7): an A–Z directory with search by name or job title and a Recognize quick action. */
export function PeoplePage() {
  const users = useMe();
  const { data: company } = useCompany();
  const { openGive } = useGive();
  const isMobile = useIsMobile();
  const [query, setQuery] = useState('');
  const { me } = users;
  const q = query.trim().toLowerCase();

  const groups = useMemo(() => {
    const list = (users.data ?? [])
      .filter((u) => !q || u.fullName.toLowerCase().includes(q) || (u.jobTitle || '').toLowerCase().includes(q))
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
    const out: { letter: string; people: ReadUser[] }[] = [];
    for (const u of list) {
      const letter = letterOf(u.fullName);
      const last = out.at(-1);
      if (last?.letter === letter) last.people.push(u);
      else out.push({ letter, people: [u] });
    }
    return out;
  }, [users.data, q]);

  const header = <PageHeader title="People" sub={company ? `Everyone at ${company.name}.` : undefined} />;
  if (users.isError) return <>{header}<PageError error={users.error} onRetry={() => void users.refetch()} /></>;
  if (!users.data || !me) return <RowsSkeleton />;

  const count = groups.reduce((n, g) => n + g.people.length, 0);
  const onlyMe = !q && users.data.length === 1;

  return (
    <>
      {header}
      <div className={styles.directory}>
        <div className={styles.toolbar}>
          <div className={styles.search}>
            <label htmlFor="people-search" className="sr-only">
              Search by name or job title
            </label>
            <Icon name="search" className={styles.searchIcon} />
            <input
              id="people-search"
              type="search"
              className={`input ${styles.searchInput}`}
              placeholder="Search by name or job title"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </div>
          <span role="status" className={styles.count}>
            {count} {count === 1 ? 'person' : 'people'}
          </span>
        </div>

        {onlyMe ? (
          <EmptyState
            icon="group_add"
            title="It’s just you so far"
            action={
              me.role === 'Admin' && (
                <Link to="/admin/invitations" className="btn btn-primary">
                  Invite people
                </Link>
              )
            }
          >
            Colleagues appear here once they accept their invitation.
          </EmptyState>
        ) : count === 0 ? (
          <div className={styles.noMatch}>
            <Icon name="person_search" size={32} className={styles.noMatchIcon} />
            <p className={styles.noMatchTitle}>No one matches “{query.trim()}”</p>
            <p className={styles.noMatchText}>Check the spelling, or search by job title instead.</p>
            <button type="button" className="btn btn-secondary" onClick={() => setQuery('')}>
              Clear search
            </button>
          </div>
        ) : (
          groups.map((g) => (
            <section key={g.letter} aria-labelledby={`people-${g.letter}`} className={styles.group}>
              <h2 id={`people-${g.letter}`} className={styles.letter}>
                {g.letter}
              </h2>
              <ul className={styles.list}>
                {g.people.map((u) => {
                  const isMe = sameId(u.id, me.id);
                  return (
                    <li key={u.id} className={styles.row}>
                      <Link to={isMe ? '/me' : `/people/${u.id}`} className={styles.person}>
                        <Avatar id={u.id} name={u.fullName} size={42} />
                        <span className={styles.personText}>
                          <span className={styles.nameLine}>
                            <span className={styles.name}>{u.fullName}</span>
                            {isMe && <span className={styles.you}>(you)</span>}
                            {u.role === 'Admin' && (
                              <span className={styles.adminBadge}>
                                <Icon name="shield_person" size={14} />
                                Admin
                              </span>
                            )}
                          </span>
                          <span className={styles.job}>{u.jobTitle || 'No job title'}</span>
                        </span>
                      </Link>
                      {!isMe && (
                        <button type="button" className={styles.recognize} aria-label={`Recognize ${u.fullName}`} onClick={() => openGive(u.id)}>
                          <Icon name="volunteer_activism" size={18} />
                          {!isMobile && 'Recognize'}
                        </button>
                      )}
                    </li>
                  );
                })}
              </ul>
            </section>
          ))
        )}
      </div>
    </>
  );
}
