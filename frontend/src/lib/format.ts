/** API timestamps are UTC but come back without a `Z` (TASKS.md #40), so treat zone-less strings as UTC. */
export function parseUtc(value: string | Date): Date {
  if (value instanceof Date) return value;
  const s = /[zZ]|[+-]\d\d:?\d\d$/.test(value) ? value : value + 'Z';
  return new Date(s);
}

export const shortDate = (d: string | Date) => parseUtc(d).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

export const exactDate = (d: string | Date) =>
  parseUtc(d).toLocaleString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });

export function relativeTime(d: string | Date, now: number = Date.now()): string {
  const minutes = Math.round((now - parseUtc(d).getTime()) / 6e4);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return minutes + ' min ago';
  const hours = Math.round(minutes / 60);
  if (hours < 24) return hours + ' h ago';
  const days = Math.floor(hours / 24);
  if (days < 2) return 'yesterday';
  if (days < 7) return days + ' days ago';
  return shortDate(d);
}

export function initials(name: string | null | undefined): string {
  const parts = String(name || '?').trim().split(/\s+/);
  const first = parts[0]?.[0] ?? '?';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return (first + last).toUpperCase();
}

/** Company monogram for the logo tile when there's no logo: "Fernhill Labs" → "FL". */
export function monogram(name: string | null | undefined): string {
  const parts = String(name || 'G')
    .replace(/[^\p{L}\p{N}\s&]/gu, '')
    .trim()
    .split(/\s+/)
    .filter((w) => w && w !== '&');
  return ((parts[0]?.[0] ?? 'G') + (parts[1]?.[0] ?? '')).toUpperCase();
}

const AVATARS: ReadonlyArray<readonly [string, string]> = [
  ['#E9DEF6', '#4A2B72'], ['#DAE7F6', '#1D4470'], ['#D6EEE2', '#1D5A3E'], ['#F6E2D0', '#7A3D10'],
  ['#F5D8DE', '#7A2236'], ['#E5E9D0', '#495419'], ['#D4ECEE', '#18535A'], ['#F0DFEE', '#6B2666'],
];

/** A stable avatar colour pair per id, so a person keeps their colour everywhere. */
export function avatarColors(id: string): { bg: string; fg: string } {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  const pair = AVATARS[h % AVATARS.length] ?? AVATARS[0]!;
  return { bg: pair[0], fg: pair[1] };
}

export const plural = (n: number, one: string, many = one + 's') => `${n} ${n === 1 ? one : many}`;
