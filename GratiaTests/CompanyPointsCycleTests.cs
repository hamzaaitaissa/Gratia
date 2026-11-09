using Gratia.Domain.Entities;

namespace GratiaTests
{
    // The dates of a company's points cycle (#19): the refill is on PointsResetDay at 00:00 UTC
    public class CompanyPointsCycleTests
    {
        [Theory]
        [InlineData(21, "2026-11-09T10:00:00Z", "2026-10-21", "2026-11-21")]
        [InlineData(21, "2026-11-20T23:59:59Z", "2026-10-21", "2026-11-21")]
        [InlineData(21, "2026-11-21T00:00:00Z", "2026-11-21", "2026-12-21")] // the refill moment itself starts the new period
        [InlineData(21, "2027-01-05T08:00:00Z", "2026-12-21", "2027-01-21")] // across the new year
        [InlineData(1, "2026-03-01T00:00:00Z", "2026-03-01", "2026-04-01")]
        [InlineData(28, "2027-02-28T12:00:00Z", "2027-02-28", "2027-03-28")] // February has a 28th
        [InlineData(28, "2027-02-27T12:00:00Z", "2027-01-28", "2027-02-28")]
        public void The_period_runs_from_the_last_reset_day_to_the_next(int resetDay, string now, string start, string next)
        {
            var company = new Company { PointsResetDay = resetDay };
            var at = Utc(now);

            Assert.Equal(Utc(start), company.PointsPeriodStart(at));
            Assert.Equal(Utc(next), company.NextPointsReset(at));
            Assert.Equal(DateTimeKind.Utc, company.PointsPeriodStart(at).Kind);
        }

        [Fact]
        public void A_new_allowance_keeps_the_current_period()
        {
            var company = new Company { PointsPeriodStartedAt = Utc("2026-10-21") };

            company.ChangePointsCycle(40, Company.DefaultPointsResetDay, Utc("2026-11-09"));

            Assert.Equal(40, company.MonthlyPointsAllowance);
            Assert.Equal(Utc("2026-10-21"), company.PointsPeriodStartedAt);
        }

        [Theory]
        [InlineData(5, "2026-11-05")]  // the new day has already passed this month: next refill on 5 December
        [InlineData(25, "2026-10-25")] // it's still ahead: next refill on 25 November
        public void A_new_day_does_not_refill_anyone_and_the_next_refill_is_on_that_day(int newDay, string recordedPeriod)
        {
            var company = new Company { PointsPeriodStartedAt = Utc("2026-10-21") };
            var now = Utc("2026-11-09");

            company.ChangePointsCycle(Company.DefaultMonthlyPointsAllowance, newDay, now);

            // The reset job refills only when the current period starts after the recorded one, which it now doesn't
            Assert.Equal(Utc(recordedPeriod), company.PointsPeriodStartedAt);
            Assert.Equal(company.PointsPeriodStart(now), company.PointsPeriodStartedAt);
        }

        [Fact]
        public void A_company_the_reset_job_has_not_seen_yet_stays_that_way()
        {
            var company = new Company();

            company.ChangePointsCycle(30, 5, Utc("2026-11-09"));

            Assert.Null(company.PointsPeriodStartedAt);
        }

        [Theory]
        [InlineData(0, 21)]
        [InlineData(1001, 21)]
        [InlineData(25, 0)]
        [InlineData(25, 29)]
        public void Values_outside_the_limits_are_refused(int allowance, int resetDay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Company().ChangePointsCycle(allowance, resetDay, Utc("2026-11-09")));
        }

        private static DateTime Utc(string value) =>
            DateTime.SpecifyKind(DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
    }
}
