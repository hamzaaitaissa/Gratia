import { initials, monogram, parseUtc, relativeTime } from './format';
import { daysUntil, pointsCycle } from './pointsCycle';

describe('pointsCycle', () => {
  // A company that refills 40 points on the 5th, as GET /api/company/me sent it on Nov 9
  const company = {
    monthlyPointsAllowance: 40,
    pointsResetDay: 5,
    lastPointsResetAt: '2026-11-05T00:00:00Z',
    nextPointsResetAt: '2026-12-05T00:00:00Z',
  };

  it('uses the company’s allowance and refill dates', () => {
    const c = pointsCycle(company, new Date(Date.UTC(2026, 10, 9, 12)));
    expect(c.allowance).toBe(40);
    expect(c.lastRefill.toISOString()).toBe('2026-11-05T00:00:00.000Z');
    expect(c.nextRefill.toISOString()).toBe('2026-12-05T00:00:00.000Z');
    expect(c.refillText).toMatch(/^Refills in 26 days · /);
  });

  it('moves on when a refill happened since the company was loaded', () => {
    const c = pointsCycle(company, new Date(Date.UTC(2026, 11, 5, 0, 1)));
    expect(c.lastRefill.toISOString()).toBe('2026-12-05T00:00:00.000Z');
    expect(c.nextRefill.toISOString()).toBe('2027-01-05T00:00:00.000Z'); // rolls over the year
  });

  it('says “tomorrow” on the last day', () => {
    expect(pointsCycle(company, new Date(Date.UTC(2026, 11, 4, 12))).refillText).toMatch(/^Refills tomorrow · /);
  });

  it('counts whole days, rounding up', () => {
    expect(daysUntil(new Date(Date.UTC(2026, 10, 21)), new Date(Date.UTC(2026, 10, 20, 12)))).toBe(1);
  });
});

describe('format', () => {
  it('reads API timestamps without a Z as UTC', () => {
    expect(parseUtc('2026-11-09T10:00:00').toISOString()).toBe('2026-11-09T10:00:00.000Z');
    expect(parseUtc('2026-11-09T10:00:00Z').toISOString()).toBe('2026-11-09T10:00:00.000Z');
    expect(parseUtc('2026-11-09T11:00:00+01:00').toISOString()).toBe('2026-11-09T10:00:00.000Z');
  });

  it('describes recent times relatively', () => {
    const now = Date.UTC(2026, 10, 9, 12);
    expect(relativeTime('2026-11-09T11:59:40', now)).toBe('just now');
    expect(relativeTime('2026-11-09T11:30:00', now)).toBe('30 min ago');
    expect(relativeTime('2026-11-09T07:00:00', now)).toBe('5 h ago');
    expect(relativeTime('2026-11-08T10:00:00', now)).toBe('yesterday');
    expect(relativeTime('2026-11-06T10:00:00', now)).toBe('3 days ago');
  });

  it('makes initials and monograms', () => {
    expect(initials('Maya Okonkwo')).toBe('MO');
    expect(initials('Zoe')).toBe('Z');
    expect(initials('Fatima Al-Sayed')).toBe('FA');
    expect(monogram('Fernhill Labs')).toBe('FL');
    expect(monogram('Smith & Sons')).toBe('SS');
  });
});
