import { useMemo } from 'react';
import { useNavigate } from 'react-router';
import { sameId, useCompany, useCompanyFeed, useMe, usePointsCycle, useReceived } from '../../api/queries';
import type { ReadUser } from '../../api/types';
import { Avatar } from '../../components/Avatar';
import { Icon } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { RecognitionCard, type CardTransaction } from '../../components/RecognitionCard';
import { CardsSkeleton, EmptyState } from '../../components/States';
import { exactDate, relativeTime } from '../../lib/format';
import type { PointsCycle } from '../../lib/pointsCycle';
import { useGive } from '../give/GiveContext';
import styles from './HomePage.module.css';

const RING_R = 38;
const RING_C = 2 * Math.PI * RING_R;

function greeting(now = new Date()) {
  const h = now.getHours();
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
}

export function HomePage() {
  const users = useMe();
  const feed = useCompanyFeed();
  const received = useReceived();
  const companyQuery = useCompany();
  const company = companyQuery.data;
  const cycle = usePointsCycle();
  const { openGive, highlightId } = useGive();
  const navigate = useNavigate();
  const { me } = users;

  const most = useMemo(
    () =>
      [...(users.data ?? [])]
        .filter((u) => u.numberOfPointsAcquired > 0)
        .sort((a, b) => b.numberOfPointsAcquired - a.numberOfPointsAcquired || a.fullName.localeCompare(b.fullName))
        .slice(0, 5),
    [users.data],
  );

  // Signed in but missing from the company list (e.g. removed meanwhile) counts as an error too, not an endless skeleton
  if (users.isError || feed.isError || companyQuery.isError || (users.isSuccess && !me)) {
    return (
      <PageError
        error={users.error ?? feed.error ?? companyQuery.error}
        onRetry={() => {
          void users.refetch();
          void feed.refetch();
          void received.refetch();
          void companyQuery.refetch();
        }}
      />
    );
  }

  // Mirrors what's coming: the title, the two balance cards, then feed cards
  if (!me || feed.isPending || !cycle) return <CardsSkeleton blocks />;

  const openTx = (tx: CardTransaction) => navigate(`/recognition/${tx.id}`);
  const involvesMe = (tx: CardTransaction) => sameId(tx.senderId, me.id) || sameId(tx.receiverId, me.id);
  const openPerson = (u: ReadUser) => navigate(sameId(u.id, me.id) ? '/me' : `/people/${u.id}`);
  const latest = received.data?.[0];
  const showMost = (users.data?.length ?? 0) > 1 && most.length > 0;

  return (
    <>
      <PageHeader
        title={`${greeting()}, ${me.fullName.split(' ')[0]}`}
        sub={new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
      />

      <div className={styles.layout}>
        <div className={styles.mainColumn}>
          <div className={styles.balances}>
            <BalanceCard left={me.numberOfPointsAvailable} cycle={cycle} onGive={() => openGive()} />

            <section aria-labelledby="home-received" className={`${styles.card} ${styles.receivedCard}`}>
              <h2 id="home-received" className={styles.receivedHeading}>
                <Icon name="stars" filled />
                Points received
              </h2>
              <p className={styles.amountLine}>
                <span className={styles.bigNumber}>{me.numberOfPointsAcquired}</span>
                <span className={styles.amountNote}>in total</span>
              </p>
              {latest ? (
                <button type="button" className={styles.latest} title={exactDate(latest.createdAt)} onClick={() => openTx(latest)}>
                  <Avatar id={latest.senderId} name={latest.senderName} size={30} />
                  <span className={styles.latestText}>
                    <span className={styles.latestWhen}>Latest · {relativeTime(latest.createdAt)}</span>
                    <span className={styles.latestWhat}>
                      +{latest.amount} from {latest.senderName}
                    </span>
                  </span>
                  <Icon name="chevron_right" className={styles.latestChevron} />
                </button>
              ) : (
                received.isSuccess && <p className={styles.receivedEmpty}>When colleagues recognize you, it adds up here.</p>
              )}
            </section>
          </div>

          <section aria-labelledby="home-feed" className={styles.feed}>
            <div className={styles.feedHead}>
              <h2 id="home-feed" className={styles.feedTitle}>
                Company feed
              </h2>
              {feed.items.length > 0 && (
                <span className={styles.feedCount}>
                  Showing {feed.items.length} of {feed.total}
                </span>
              )}
            </div>

            {feed.items.length > 0 ? (
              <>
                <div className={styles.cards}>
                  {feed.items.map((tx) => (
                    <RecognitionCard
                      key={tx.id}
                      tx={tx}
                      meId={me.id}
                      highlight={sameId(tx.id, highlightId)}
                      onOpen={involvesMe(tx) ? openTx : undefined}
                    />
                  ))}
                </div>
                {feed.hasNextPage ? (
                  <button
                    type="button"
                    className={`btn btn-secondary ${styles.loadMore}`}
                    disabled={feed.isFetchingNextPage}
                    aria-busy={feed.isFetchingNextPage}
                    onClick={() => void feed.fetchNextPage()}
                  >
                    {feed.isFetchingNextPage ? 'Loading…' : 'Load more'}
                  </button>
                ) : (
                  <p className={styles.caughtUp}>You’re all caught up.</p>
                )}
              </>
            ) : (
              <EmptyState
                icon="forum"
                title="No recognitions yet"
                action={
                  me.numberOfPointsAvailable > 0 && (
                    <button type="button" className="btn btn-primary" onClick={() => openGive()}>
                      Give recognition
                    </button>
                  )
                }
              >
                When someone at {company?.name ?? 'your company'} says thank you, it shows up here for everyone to see. Why not be the first?
              </EmptyState>
            )}
          </section>
        </div>

        {showMost && (
          <aside aria-labelledby="home-most" className={styles.most}>
            <div className={styles.mostHead}>
              <h2 id="home-most" className={styles.mostTitle}>
                Most recognized
              </h2>
              <p className={styles.mostSub}>All time, by points received</p>
            </div>
            <ol className={styles.mostList}>
              {most.map((u, i) => (
                <li key={u.id}>
                  <button type="button" className={styles.mostItem} onClick={() => openPerson(u)}>
                    <span className={styles.rank}>{i + 1}</span>
                    <Avatar id={u.id} name={u.fullName} size={36} />
                    <span className={styles.mostText}>
                      <span className={styles.mostName}>{u.fullName}</span>
                      <span className={styles.mostJob}>{u.jobTitle || 'No job title'}</span>
                    </span>
                    <span className={styles.mostPts}>
                      <Icon name="stars" size={16} filled />
                      {u.numberOfPointsAcquired}
                      <span className="sr-only">points received</span>
                    </span>
                  </button>
                </li>
              ))}
            </ol>
          </aside>
        )}
      </div>
    </>
  );
}

function BalanceCard({ left, cycle, onGive }: { left: number; cycle: PointsCycle; onGive: () => void }) {
  const { allowance } = cycle;
  const ratio = Math.max(0, Math.min(1, left / allowance));
  return (
    <section aria-labelledby="home-balance" className={`${styles.card} ${styles.balanceCard}`}>
      <div className={styles.ring}>
        <svg width="96" height="96" viewBox="0 0 96 96" aria-hidden="true">
          <circle cx="48" cy="48" r={RING_R} fill="none" stroke="var(--g-primary-soft)" strokeWidth="9" />
          <circle
            cx="48"
            cy="48"
            r={RING_R}
            fill="none"
            stroke="var(--g-primary)"
            strokeWidth="9"
            strokeLinecap="round"
            strokeDasharray={RING_C.toFixed(1)}
            strokeDashoffset={(RING_C * (1 - ratio)).toFixed(1)}
            transform="rotate(-90 48 48)"
          />
        </svg>
        <Icon name="volunteer_activism" size={30} className={styles.ringIcon} />
      </div>
      <div className={styles.balanceText}>
        <h2 id="home-balance" className={styles.balanceHeading}>
          Points to give
        </h2>
        {left > 0 ? (
          <>
            <p className={styles.amountLine}>
              <span className={styles.bigNumber}>{left}</span>
              <span className={styles.ofLeft}>of {allowance} left</span>
            </p>
            <p className={styles.refill}>{cycle.refillText}</p>
            <button type="button" className={`btn btn-primary ${styles.balanceGive}`} onClick={onGive}>
              <Icon name="volunteer_activism" />
              Give recognition
            </button>
          </>
        ) : (
          <>
            <p className={styles.zeroTitle}>You’ve given all your points this month.</p>
            <p className={styles.zeroText}>They refill on {cycle.refillDate}.</p>
          </>
        )}
      </div>
    </section>
  );
}
