import { useMemo, useState, type CSSProperties } from 'react';
import { Icon } from '../../components/Icon';
import { RecognitionCard } from '../../components/RecognitionCard';
import { monogram } from '../../lib/format';
import { buildTheme, type Mode } from '../../theme/theme';
import styles from './BrandPreview.module.css';

interface Props {
  name: string;
  logoUrl: string;
  primary: string;
  secondary: string;
  mode: Mode;
}

const SAMPLE = {
  id: 'preview',
  senderId: 'u3',
  senderName: 'Priya Raman',
  receiverId: 'u6',
  receiverName: 'Samuel Mensah',
  amount: 5,
  typeOfDonation: 'Teamwork',
  message: 'Thanks for staying late to get the accessibility fixes in. Customers already noticed.',
  createdAt: new Date(Date.now() - 2 * 36e5).toISOString(),
};

/** A small copy of the app in the draft colours. Its theme is scoped to this box, so the rest of the page keeps the saved one. */
export function BrandPreview({ name, logoUrl, primary, secondary, mode }: Props) {
  const vars = useMemo(() => buildTheme(primary, secondary, mode).vars, [primary, secondary, mode]);
  const [brokenUrl, setBrokenUrl] = useState('');
  const showLogo = /^https?:\/\//i.test(logoUrl) && brokenUrl !== logoUrl;
  const title = name.trim() || 'Your company';

  return (
    <div role="img" aria-label="Preview of the app in your colours" className={styles.preview} style={vars as CSSProperties}>
      <div className={styles.side}>
        <div className={styles.brand}>
          {showLogo ? (
            <img src={logoUrl} alt="" className={styles.logo} onError={() => setBrokenUrl(logoUrl)} />
          ) : (
            <span className={styles.mono}>{monogram(title)}</span>
          )}
          <span className={styles.name}>{title}</span>
        </div>
        <div className={styles.give}>
          <Icon name="volunteer_activism" size={16} />
          Give recognition
        </div>
        <div className={styles.nav}>
          <div className={`${styles.navItem} ${styles.navOn}`}>
            <Icon name="home" size={16} />
            Home
          </div>
          <div className={styles.navItem}>
            <Icon name="history" size={16} />
            My activity
          </div>
          <div className={styles.navItem}>
            <Icon name="group" size={16} />
            People
          </div>
        </div>
      </div>
      <div className={styles.main}>
        <div className={styles.balance}>
          <svg width="44" height="44" viewBox="0 0 44 44" aria-hidden="true">
            <circle cx="22" cy="22" r="18" fill="none" stroke="var(--g-primary-soft)" strokeWidth="5" />
            <circle cx="22" cy="22" r="18" fill="none" stroke="var(--g-primary)" strokeWidth="5" strokeLinecap="round" strokeDasharray="113.1" strokeDashoffset="31.7" transform="rotate(-90 22 22)" />
          </svg>
          <div className={styles.balanceText}>
            <div className={styles.balanceLabel}>Points to give</div>
            <div className={styles.balanceValue}>
              <span className={styles.balanceNumber}>18</span> of 25 left
            </div>
          </div>
          <div className={styles.balanceButton}>Give</div>
        </div>
        <RecognitionCard tx={SAMPLE} meId="nobody" />
      </div>
    </div>
  );
}
