namespace Gratia.Application.Settings
{
    public class PointsOptions
    {
        // Shares the existing "AppSettings" section so current appsettings files keep working
        public const string SectionName = "AppSettings";

        public int MonthlyAllowance { get; set; }
    }
}
