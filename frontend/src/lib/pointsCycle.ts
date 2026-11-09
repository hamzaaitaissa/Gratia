import type { ReadCompany } from '../api/types';
import { parseUtc, shortDate } from './format';

/**
 * The company's points cycle (#19): points to give are refilled to the allowance on the reset day at 00:00 UTC.
 * The API sends the last and next refill dates; this keeps them right even when the cached company is older than the
 * last refill (the page stayed open across the reset day).
 */
export interface PointsCycle {
  allowance: number;
  resetDay: number;
  /** Start of the current period: what "this month" means for points given and received. */
  lastRefill: Date;
  nextRefill: Date;
  /** "Nov 21" */
  refillDate: string;
  /** "Refills in 12 days · Nov 21" */
  refillText: string;
}

type CycleSource = Pick<ReadCompany, 'monthlyPointsAllowance' | 'pointsResetDay' | 'lastPointsResetAt' | 'nextPointsResetAt'>;

const DAY = 864e5;

export const daysUntil = (d: Date, now: Date = new Date()) => Math.max(0, Math.ceil((d.getTime() - now.getTime()) / DAY));

/** The reset day of the month after `d`, at 00:00 UTC. */
function nextMonthOn(d: Date, day: number): Date {
  return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, day));
}

export function pointsCycle(company: CycleSource, now: Date = new Date()): PointsCycle {
  let lastRefill = parseUtc(company.lastPointsResetAt);
  let nextRefill = parseUtc(company.nextPointsResetAt);
  // A refill has happened since the company was loaded: move on by whole periods
  for (let i = 0; i < 24 && now >= nextRefill; i++) {
    lastRefill = nextRefill;
    nextRefill = nextMonthOn(nextRefill, company.pointsResetDay);
  }
  const refillDate = shortDate(nextRefill);
  const days = daysUntil(nextRefill, now);
  return {
    allowance: company.monthlyPointsAllowance,
    resetDay: company.pointsResetDay,
    lastRefill,
    nextRefill,
    refillDate,
    refillText: days <= 1 ? `Refills tomorrow · ${refillDate}` : `Refills in ${days} days · ${refillDate}`,
  };
}
