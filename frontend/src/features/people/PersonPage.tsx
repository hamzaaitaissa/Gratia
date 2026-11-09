import { Navigate, useParams } from 'react-router';
import { sameId, useMe } from '../../api/queries';
import { Avatar } from '../../components/Avatar';
import { BreakableEmail } from '../../components/BreakableEmail';
import { Icon } from '../../components/Icon';
import { NotFound, PageError } from '../../components/Page';
import { BackLink, CardsSkeleton } from '../../components/States';
import { useGive } from '../give/GiveContext';
import styles from './People.module.css';

/**
 * A colleague's profile (design 6.8). There's no recognition history here: the API doesn't expose another
 * person's recognitions, only the sender and receiver can read one.
 */
export function PersonPage() {
  const { id = '' } = useParams();
  const users = useMe();
  const { openGive } = useGive();
  const { me } = users;

  if (users.isError) return <PageError error={users.error} onRetry={() => void users.refetch()} />;
  if (!users.data || !me) return <CardsSkeleton cards={1} />;
  if (sameId(id, me.id)) return <Navigate to="/me" replace />;

  // The directory is the company's: someone from another company is simply not found here
  const person = users.data.find((u) => sameId(u.id, id));
  if (!person) return <NotFound />;

  return (
    <div className={styles.profile}>
      <BackLink to="/people">People</BackLink>
      <section aria-labelledby="person-name" className={styles.profileCard}>
        <div className={styles.profileHead}>
          <Avatar id={person.id} name={person.fullName} size={88} />
          <div className={styles.profileText}>
            <h1 id="person-name" className={styles.profileName}>
              {person.fullName}
            </h1>
            <p className={styles.profileJob}>{person.jobTitle || 'No job title'}</p>
          </div>
          <button type="button" className={`btn btn-primary ${styles.profileAction}`} onClick={() => openGive(person.id)}>
            <Icon name="volunteer_activism" />
            Recognize
          </button>
        </div>
        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt>Email</dt>
            <dd>
              <a href={`mailto:${person.email}`}>
                <BreakableEmail email={person.email} />
              </a>
            </dd>
          </div>
          <div className={styles.fact}>
            <dt>Role</dt>
            <dd className={styles.role}>
              <Icon name="shield_person" size={18} className={styles.roleIcon} />
              {person.role}
            </dd>
          </div>
          <div className={styles.fact}>
            <dt className={styles.recvLabel}>Points received</dt>
            <dd className={styles.recvValue}>
              <Icon name="stars" size={22} filled className={styles.recvIcon} />
              {person.numberOfPointsAcquired}
            </dd>
          </div>
        </dl>
      </section>
    </div>
  );
}
