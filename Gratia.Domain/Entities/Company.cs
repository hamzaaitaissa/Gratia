using Gratia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } 
        public string LogoUrl { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }

        // The founder's email domain (lowercase), e.g. "acme.com". Null for a workspace founded with a free email
        // address (only allowed when Onboarding:AllowFreeEmailDomains is on), since such a domain is shared by strangers.
        public string? Domain { get; set; }

        // Set once the company proves it controls Domain through DNS (TASKS.md #61). Until then the workspace is
        // provisional: its admins can only invite people on Domain.
        public DateTime? DomainVerifiedAt { get; set; }

        public bool IsDomainVerified => DomainVerifiedAt is not null;

        // Used until an admin sets the company's own branding
        public const string DefaultPrimaryColor = "#4F46E5";
        public const string DefaultSecondaryColor = "#F59E0B";

        // The points cycle (#19), set by the company's admins. Every month on PointsResetDay at 00:00 UTC, each member's
        // points to give are refilled to MonthlyPointsAllowance; unused points don't carry over. The day stops at 28,
        // so it exists in every month.
        public const int DefaultMonthlyPointsAllowance = 25;
        public const int DefaultPointsResetDay = 21;
        public const int MaxPointsResetDay = 28;
        public const int MaxMonthlyPointsAllowance = 1000;

        public int MonthlyPointsAllowance { get; set; } = DefaultMonthlyPointsAllowance;
        public int PointsResetDay { get; set; } = DefaultPointsResetDay;

        // The start of the period whose refill has been done. Null until the reset job first sees the company, which
        // then only records the current period: members already got the allowance when they joined.
        public DateTime? PointsPeriodStartedAt { get; set; }

        public ICollection<User> Users { get; set; }

        public Company()
        {
            
        }
        public Company(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }

        public void Update(string name, string logoUrl, string primaryColor, string secondaryColor)
        {
            Name = name;
            LogoUrl = logoUrl;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }

        // The latest reset (PointsResetDay at 00:00 UTC) at or before now: when the current period began
        public DateTime PointsPeriodStart(DateTime now)
        {
            var thisMonth = new DateTime(now.Year, now.Month, PointsResetDay, 0, 0, 0, DateTimeKind.Utc);
            return thisMonth <= now ? thisMonth : thisMonth.AddMonths(-1);
        }

        public DateTime NextPointsReset(DateTime now) => PointsPeriodStart(now).AddMonths(1);

        // A new allowance applies from the next refill. A new day doesn't refill anyone: the current period runs on
        // until the new day comes round, so nobody gets a surprise refill or loses one by moving the day.
        public void ChangePointsCycle(int monthlyAllowance, int resetDay, DateTime now)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(monthlyAllowance, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(monthlyAllowance, MaxMonthlyPointsAllowance);
            ArgumentOutOfRangeException.ThrowIfLessThan(resetDay, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(resetDay, MaxPointsResetDay);

            MonthlyPointsAllowance = monthlyAllowance;
            if (resetDay == PointsResetDay)
                return;

            PointsResetDay = resetDay;
            if (PointsPeriodStartedAt is not null)
                PointsPeriodStartedAt = PointsPeriodStart(now);
        }
    }
}
