import { useState } from 'react';
import type { ReadTransaction } from '../api/types';
import { exactDate, parseUtc, relativeTime } from '../lib/format';
import { sameId } from '../api/queries';
import { Avatar } from './Avatar';
import { Icon } from './Icon';
import styles from './RecognitionCard.module.css';

export type CardTransaction = Pick<
  ReadTransaction,
  'id' | 'senderId' | 'senderName' | 'receiverId' | 'receiverName' | 'amount' | 'message' | 'typeOfDonation' | 'createdAt'
>;

interface RecognitionCardProps {
  tx: CardTransaction;
  meId: string | undefined;
  /** Outlines the card, e.g. the gift just sent. */
  highlight?: boolean;
  /** The Give drawer's live preview: placeholders stand in for what isn't filled in yet. */
  preview?: boolean;
  /** Only for the sender or receiver: the API answers 403 to anyone else. */
  onOpen?: (tx: CardTransaction) => void;
}

const LONG = 240;
const CUT = 200;

/** The large card of the recognition detail page: the whole message, quoted, with the exact date. */
export function LargeRecognitionCard({ tx, meId }: { tx: CardTransaction; meId: string | undefined }) {
  const senderLabel = sameId(tx.senderId, meId) ? 'You' : tx.senderName;
  const receiverLabel = sameId(tx.receiverId, meId) ? 'You' : tx.receiverName;
  const created = parseUtc(tx.createdAt);
  return (
    <article
      className={styles.large}
      aria-label={`${senderLabel} recognized ${receiverLabel === 'You' ? 'you' : receiverLabel} with ${tx.amount} points for ${tx.typeOfDonation}`}
    >
      <div aria-hidden="true" className={styles.brandBar} />
      <div className={styles.largeHead}>
        <div className={styles.party}>
          <Avatar id={tx.senderId} name={tx.senderName} size={52} />
          <div className={styles.partyText}>
            <div className={styles.partyLabel}>From</div>
            <div className={styles.partyName}>{senderLabel}</div>
          </div>
        </div>
        <Icon name="arrow_forward" size={24} className={styles.arrow} />
        <div className={styles.party}>
          <Avatar id={tx.receiverId} name={tx.receiverName} size={52} />
          <div className={styles.partyText}>
            <div className={styles.partyLabel}>To</div>
            <div className={styles.partyName}>{receiverLabel}</div>
          </div>
        </div>
        <span className={styles.largePoints}>
          <Icon name="stars" size={22} filled />+{tx.amount}
          <span className={styles.largePointsWord}>points</span>
        </span>
      </div>
      <blockquote className={styles.quote}>“{tx.message}”</blockquote>
      <div className={styles.largeFoot}>
        <span className={`${styles.category} ${styles.largeCategory}`}>{tx.typeOfDonation}</span>
        <time dateTime={created.toISOString()} className={styles.exact}>
          {exactDate(created)}
        </time>
      </div>
    </article>
  );
}

export function RecognitionCard({ tx, meId, highlight, preview, onOpen }: RecognitionCardProps) {
  const [expanded, setExpanded] = useState(false);

  const message = tx.message.trim() || (preview ? 'Your message will appear here.' : '');
  const isLong = message.length > LONG;
  let shown = message;
  if (isLong && !expanded) {
    shown = message.slice(0, CUT);
    shown = shown.slice(0, shown.lastIndexOf(' ')) + '…';
  }

  const senderName = tx.senderName || 'You';
  const receiverName = tx.receiverName || (preview ? 'Choose a colleague' : '');
  const senderLabel = sameId(tx.senderId, meId) ? 'You' : senderName;
  const receiverLabel = sameId(tx.receiverId, meId) ? 'you' : receiverName;
  const category = tx.typeOfDonation || (preview ? 'Category' : '');
  const created = parseUtc(tx.createdAt);
  const rel = preview ? 'just now' : relativeTime(created);
  const exact = exactDate(created);

  return (
    <article
      className={styles.card}
      aria-label={`${senderLabel} recognized ${receiverLabel} with ${tx.amount} points for ${tx.typeOfDonation}`}
    >
      {highlight && <div aria-hidden="true" className={styles.highlight} />}
      <div className={styles.head}>
        <div className={styles.avatars}>
          <Avatar id={tx.senderId || 'me'} name={senderName} size={32} className={styles.avatar} />
          <Avatar id={tx.receiverId || 'x'} name={receiverName} size={32} unknown={!tx.receiverName} className={styles.avatar} />
        </div>
        <div className={styles.who}>
          <strong>{senderLabel}</strong>
          <Icon name="arrow_forward" size={16} className={styles.arrow} />
          <span className="sr-only">recognized</span>
          <strong>{receiverLabel}</strong>
        </div>
        <span className={styles.points}>
          <Icon name="stars" size={16} filled />+{tx.amount}
          <span className="sr-only">points</span>
        </span>
      </div>
      <p className={styles.message}>
        {shown}
        {isLong && (
          <>
            {' '}
            <button type="button" className={styles.more} onClick={() => setExpanded((e) => !e)}>
              {expanded ? 'Show less' : 'Read more'}
            </button>
          </>
        )}
      </p>
      <div className={styles.foot}>
        <span className={styles.category}>{category}</span>
        {onOpen ? (
          <button type="button" className={styles.time} title={exact} onClick={() => onOpen(tx)}>
            <time dateTime={created.toISOString()}>{rel}</time>
          </button>
        ) : (
          <time dateTime={created.toISOString()} title={exact} className={styles.timeText}>
            {rel}
          </time>
        )}
      </div>
    </article>
  );
}
