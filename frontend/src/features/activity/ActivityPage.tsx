import { useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { sameId, useMe, useMyHistory, usePointsCycle } from '../../api/queries';
import { Icon } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { RecognitionCard, type CardTransaction } from '../../components/RecognitionCard';
import { CardsSkeleton, EmptyState } from '../../components/States';
import { parseUtc, shortDate } from '../../lib/format';
import { useGive } from '../give/GiveContext';
import styles from './ActivityPage.module.css';

type Tab = 'all' | 'received' | 'sent';
const TABS: { key: Tab; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'received', label: 'Received' },
  { key: 'sent', label: 'Sent' },
];
const PAGE_SIZE = 8;
const ALL = 'All';

/** "My activity" (design 6.6): tabs, this cycle's summary, a category filter and client-side "Show more". */
export function ActivityPage() {
  const users = useMe();
  const history = useMyHistory();
  const cycle = usePointsCycle();
  const { openGive, highlightId } = useGive();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get('tab') === 'received' || params.get('tab') === 'sent' ? (params.get('tab') as Tab) : 'all';
  const [category, setCategory] = useState(ALL);
  const [shown, setShown] = useState(PAGE_SIZE);
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const { me } = users;

  const lists = useMemo(() => {
    const mine = history.data ?? [];
    return {
      all: mine,
      received: mine.filter((t) => sameId(t.receiverId, me?.id)),
      sent: mine.filter((t) => sameId(t.senderId, me?.id)),
    };
  }, [history.data, me?.id]);

  const header = <PageHeader title="My activity" sub="Recognition you’ve given and received." />;

  if (users.isError || history.isError || (users.isSuccess && !me))
    return (
      <>
        {header}
        <PageError
          error={users.error ?? history.error}
          onRetry={() => {
            void users.refetch();
            void history.refetch();
          }}
        />
      </>
    );
  if (!me || history.isPending || !cycle) return <CardsSkeleton cards={4} />;

  const selectTab = (next: Tab, focus = false) => {
    setParams(next === 'all' ? {} : { tab: next }, { replace: true });
    setCategory(ALL);
    setShown(PAGE_SIZE);
    if (focus) tabRefs.current[TABS.findIndex((t) => t.key === next)]?.focus();
  };
  const onTabKey = (e: KeyboardEvent<HTMLButtonElement>, i: number) => {
    const move = { ArrowRight: 1, ArrowLeft: -1, Home: -i, End: TABS.length - 1 - i }[e.key];
    if (move === undefined) return;
    e.preventDefault();
    selectTab(TABS[(i + move + TABS.length) % TABS.length]!.key, true);
  };

  const base = lists[tab];
  const categories = [ALL, ...new Set(base.map((t) => t.typeOfDonation))];
  const filtered = category === ALL ? base : base.filter((t) => t.typeOfDonation === category);
  const since = cycle.lastRefill;
  const sumSince = (list: CardTransaction[]) => list.filter((t) => parseUtc(t.createdAt) >= since).reduce((n, t) => n + t.amount, 0);
  const left = me.numberOfPointsAvailable;

  const emptyCopy: Record<Tab, [string, string]> = {
    all: ['Nothing here yet', 'Recognition you give and receive will show up here. Start by thanking someone who helped you this week.'],
    received: ['No recognition received yet', 'It’ll come. In the meantime, thank someone who made your week easier.'],
    sent: [
      'You haven’t given any recognition yet',
      left > 0 ? `You have ${left} points to give this month. Who made your week better?` : `Your points refill on ${cycle.refillDate}. Then you can thank a colleague.`,
    ],
  };
  const openTx = (tx: CardTransaction) => navigate(`/recognition/${tx.id}`, { state: { back: '/activity' + (tab === 'all' ? '' : `?tab=${tab}`), backLabel: 'My activity' } });

  return (
    <>
      {header}
      <div className={styles.page}>
        <div role="tablist" aria-label="Activity" className={styles.tabs}>
          {TABS.map((t, i) => {
            const on = t.key === tab;
            return (
              <button
                key={t.key}
                ref={(el) => {
                  tabRefs.current[i] = el;
                }}
                type="button"
                role="tab"
                id={`activity-tab-${t.key}`}
                aria-selected={on}
                aria-controls="activity-panel"
                tabIndex={on ? 0 : -1}
                className={styles.tab}
                onClick={() => selectTab(t.key)}
                onKeyDown={(e) => onTabKey(e, i)}
              >
                {t.label}
                <span className={styles.count}>{lists[t.key].length}</span>
              </button>
            );
          })}
        </div>

        <div role="tabpanel" id="activity-panel" aria-labelledby={`activity-tab-${tab}`} className={styles.panel}>
          <div className={styles.summary}>
            <div className={styles.stat}>
              <span aria-hidden="true" className={`${styles.statIcon} ${styles.recv}`}>
                <Icon name="stars" size={24} filled />
              </span>
              <div>
                <div className={`${styles.statLabel} ${styles.recvText}`}>Received this month</div>
                <div className={styles.statValue}>
                  {sumSince(lists.received)} <span className={styles.statNote}>points · Since the {shortDate(since)} refill</span>
                </div>
              </div>
            </div>
            <div className={styles.stat}>
              <span aria-hidden="true" className={`${styles.statIcon} ${styles.given}`}>
                <Icon name="volunteer_activism" size={24} />
              </span>
              <div>
                <div className={`${styles.statLabel} ${styles.givenText}`}>Given this month</div>
                <div className={styles.statValue}>
                  {sumSince(lists.sent)} <span className={styles.statNote}>of {cycle.allowance} points to give</span>
                </div>
              </div>
            </div>
          </div>

          {categories.length > 2 && (
            <div role="group" aria-label="Filter by category" className={styles.chips}>
              {categories.map((c) => (
                <button
                  key={c}
                  type="button"
                  className={styles.chip}
                  aria-pressed={c === category}
                  onClick={() => {
                    setCategory(c);
                    setShown(PAGE_SIZE);
                  }}
                >
                  {c === ALL ? 'All categories' : c}
                </button>
              ))}
            </div>
          )}

          {filtered.length > 0 ? (
            <>
              <div className={styles.cards}>
                {filtered.slice(0, shown).map((tx) => (
                  <RecognitionCard key={tx.id} tx={tx} meId={me.id} highlight={sameId(tx.id, highlightId)} onOpen={openTx} />
                ))}
              </div>
              <div className={styles.more}>
                <span className={styles.moreCount}>
                  Showing {Math.min(shown, filtered.length)} of {filtered.length}
                </span>
                {filtered.length > shown && (
                  <button type="button" className={`btn btn-secondary ${styles.moreButton}`} onClick={() => setShown((s) => s + PAGE_SIZE)}>
                    Show more
                  </button>
                )}
              </div>
            </>
          ) : base.length > 0 ? (
            <div className={styles.noMatch}>
              <p>No recognitions in {category} yet.</p>
              <button type="button" className="btn btn-secondary" onClick={() => setCategory(ALL)}>
                Show all categories
              </button>
            </div>
          ) : (
            <EmptyState
              icon="volunteer_activism"
              title={emptyCopy[tab][0]}
              action={
                left > 0 && (
                  <button type="button" className="btn btn-primary" onClick={() => openGive()}>
                    Give recognition
                  </button>
                )
              }
            >
              {emptyCopy[tab][1]}
            </EmptyState>
          )}
        </div>
      </div>
    </>
  );
}
