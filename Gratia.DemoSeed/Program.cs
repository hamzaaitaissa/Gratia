// Fills a dedicated demo database with two companies, their people and two months of recognitions, to try the app:
//
//   dotnet run --project Gratia.DemoSeed                 # creates GratiaDemo on LocalDB and seeds it once
//   dotnet run --project Gratia.DemoSeed -- --reset      # drops GratiaDemo and starts over (dates are relative to now)
//   dotnet run --project Gratia.DemoSeed -- --connection "<connection string>"
//
// Then run the API against it with `dotnet run --project Gratia.API --launch-profile demo`.
// It refuses any database whose name doesn't contain "Demo", so it can never touch the real Gratia database.
//
// The database is disposable, so its schema comes straight from the current model (EnsureCreated), not from the
// migrations: it matches the code you're running even while a model change has no migration yet. It has no
// migration history, so don't run `dotnet ef database update` on it; after a model change, run with --reset.
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Infrastructure.Authentication;
using Gratia.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string DefaultConnection = @"Server=(localdb)\MSSQLLocalDB;Database=GratiaDemo;Trusted_Connection=True;TrustServerCertificate=True";

var reset = args.Contains("--reset");
var connectionIndex = Array.IndexOf(args, "--connection");
var connection = connectionIndex >= 0 && connectionIndex + 1 < args.Length ? args[connectionIndex + 1] : DefaultConnection;

var database = new SqlConnectionStringBuilder(connection).InitialCatalog;
if (!database.Contains("demo", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Refusing to seed '{database}': only a database whose name contains \"Demo\" is seeded.");
    return 1;
}

var options = new DbContextOptionsBuilder<GratiaDbContext>().UseSqlServer(connection).Options;
await using var db = new GratiaDbContext(options);

if (reset)
{
    Console.WriteLine($"Dropping {database}…");
    await db.Database.EnsureDeletedAsync();
}
await db.Database.EnsureCreatedAsync();

if (await db.Companies.AnyAsync())
{
    Console.WriteLine($"{database} already has data. Use --reset to start over (also after a model change).");
    DemoData.PrintLogins(await db.Users.AsNoTracking().Include(u => u.Company).ToListAsync());
    return 0;
}

var seed = DemoData.Build(DateTime.UtcNow, new PasswordHashingService().HashPassword(DemoData.Password));
db.Companies.AddRange(seed.Companies);
db.Users.AddRange(seed.Users);
db.Transactions.AddRange(seed.Transactions);
await db.SaveChangesAsync();

Console.WriteLine($"Seeded {database}: {seed.Companies.Count} companies, {seed.Users.Count} people, {seed.Transactions.Count} recognitions.");
DemoData.PrintLogins(seed.Users);
return 0;

static class DemoData
{
    public const string Password = "Gratia-demo-1";

    // Demo companies keep the default points cycle (#19), which the balances below are worked out from
    private const int Allowance = Company.DefaultMonthlyPointsAllowance;
    private const int RefillDayUtc = Company.DefaultPointsResetDay;

    public sealed record Seed(List<Company> Companies, List<User> Users, List<Transaction> Transactions);

    // The design's mock team (gratia-core.js). Domains are .example (RFC 2606), so no email can reach anyone.
    private static readonly (string Name, string Job, UserRole Role)[] Fernhill =
    [
        ("Maya Okonkwo", "Product Designer", UserRole.Admin), ("Jonas Lindqvist", "Head of Operations", UserRole.Admin),
        ("Priya Raman", "Senior Backend Engineer", UserRole.Employee), ("Tomás Herrera", "Customer Success Lead", UserRole.Employee),
        ("Aiko Tanaka", "Data Analyst", UserRole.Employee), ("Samuel Mensah", "Frontend Engineer", UserRole.Employee),
        ("Léa Fontaine", "Marketing Manager", UserRole.Employee), ("Daniel Novak", "QA Engineer", UserRole.Employee),
        ("Fatima Al-Sayed", "People Partner", UserRole.Employee), ("Oliver Brennan", "Sales Executive", UserRole.Employee),
        ("Ingrid Solberg", "Engineering Manager", UserRole.Employee), ("Rafael Costa", "Support Specialist", UserRole.Employee),
        ("Hannah Weiss", "Finance Controller", UserRole.Employee), ("Kwame Asante", "DevOps Engineer", UserRole.Employee),
        ("Zoe Chen", "", UserRole.Employee),
    ];

    // (sender, receiver: 1-based into Fernhill), amount, category, hours ago, message
    private static readonly (int From, int To, int Amount, string Category, int HoursAgo, string Message)[] FernhillGifts =
    [
        (3, 1, 5, "Teamwork", 2, "Thanks for pairing on the onboarding flow today. You turned a messy spec into something the whole team understood in twenty minutes."),
        (4, 12, 3, "Customer focus", 5, "Saved the Ortega account this morning. Calm, clear, kind."),
        (11, 6, 5, "Going the extra mile", 9, "Samuel stayed late to get the accessibility fixes into the release. We shipped with zero blocker bugs, and two customers have already written in to say the new keyboard navigation makes their day easier. That doesn’t happen by accident. Thank you."),
        (1, 5, 3, "Innovation", 22, "The churn dashboard is brilliant. Already using it every morning."),
        (9, 2, 2, "Leadership", 27, "Thank you for running the offsite."),
        (7, 10, 5, "Teamwork", 30, "Oliver jumped into the launch webinar with an hour’s notice and carried the whole Q&A."),
        (14, 3, 10, "Helping out", 46, "Priya, you dropped everything to help me trace the queue outage at 2 a.m. You explained every step so I can handle it myself next time. I owe you more than points, but here are some anyway."),
        (1, 14, 2, "Helping out", 50, "Thanks for the new staging setup!"),
        (13, 9, 3, "Customer focus", 70, "Your patience with the payroll questions this week was amazing."),
        (6, 1, 3, "Teamwork", 75, "Best design review I have sat in. Clear decisions, no ego."),
        (2, 11, 5, "Leadership", 96, "Ingrid made the reorg feel humane. She met every person one-on-one before the announcement and answered the hard questions honestly."),
        (5, 8, 1, "Going the extra mile", 120, "Found the rounding bug nobody else could."),
        (12, 4, 3, "Helping out", 140, "Thanks for covering my shift on Friday."),
        (10, 7, 5, "Innovation", 170, "The referral campaign idea is already paying off: 31 new leads in a week."),
        (8, 6, 2, "Teamwork", 200, "Great test plan, made my life easier."),
        (15, 1, 3, "Helping out", 230, "Thank you for the portfolio feedback. I rewrote my case study and it reads so much better now."),
        (1, 15, 2, "Going the extra mile", 260, "Your icon audit was meticulous."),
        (3, 14, 5, "Innovation", 300, "The deploy pipeline is twice as fast. Wow."),
        (11, 3, 10, "Leadership", 340, "Priya mentored two new engineers this quarter while leading the payments migration. Both of them told me separately that she’s the reason they feel at home here. That’s leadership."),
        (4, 10, 2, "Customer focus", 400, "Great handover notes for the Halden renewal."),
        (9, 13, 3, "Teamwork", 450, "Thanks for pulling the headcount numbers so fast."),
        (6, 5, 3, "Helping out", 500, "Thank you for teaching me SQL window functions!"),
        (2, 1, 5, "Customer focus", 560, "Maya spent a full day on calls with our three biggest customers and came back with a redesign everyone agreed on."),
        (7, 15, 2, "Teamwork", 620, "Lovely work on the newsletter visuals."),
        (13, 2, 1, "Helping out", 700, "Thanks!"),
        (14, 8, 3, "Going the extra mile", 780, "Weekend regression run saved the release."),
        (12, 9, 5, "Customer focus", 860, "Fatima helped a customer through a really hard situation with compassion, and made sure our support team was looked after too."),
        (5, 11, 3, "Leadership", 940, "Clear priorities every single week. Thank you."),
        (1, 3, 5, "Teamwork", 1010, "Thank you for reviewing every API contract with me, even the boring ones."),
        (10, 4, 3, "Helping out", 1100, "Owe you one for the demo save."),
        (8, 14, 2, "Innovation", 1200, "Flaky-test detector = genius."),
        (3, 6, 3, "Teamwork", 1300, "Pairing with you is the best part of my week."),
        (15, 7, 1, "Going the extra mile", 1380, "Thank you for the late-night proofreading."),
        (11, 1, 5, "Leadership", 1420, "Maya led the design system kickoff and got six teams aligned in one afternoon."),
        (1, 9, 2, "Teamwork", 1440, "Thanks for organising the mentoring circle."),
    ];

    // Fatima gives the rest of her points this cycle, so there's someone with 0 points to give
    private static readonly (int To, string Category, string Message)[] FatimaSpendsTheRest =
    [
        (13, "Helping out", "Thank you for untangling the benefits renewal with me. Two days of spreadsheets, zero complaints."),
        (12, "Customer focus", "The way you handled the escalation on Tuesday was textbook."),
        (14, "Going the extra mile", "Thanks for staying on after hours to get the laptop rollout done before the new starters arrived."),
    ];

    // A second tenant with a bright yellow brand: it never sees Fernhill's people or feed, and shows the contrast adjustment
    private static readonly (string Name, string Job, UserRole Role)[] Kestrel =
    [
        ("Ada Mwangi", "Founder", UserRole.Admin), ("Leo Park", "Brand Designer", UserRole.Employee), ("Nia Okafor", "Producer", UserRole.Employee),
    ];

    private static readonly (int From, int To, int Amount, string Category, int HoursAgo, string Message)[] KestrelGifts =
    [
        (2, 3, 4, "Teamwork", 3, "Thanks for keeping the shoot on schedule when the rain came in."),
        (1, 2, 5, "Innovation", 26, "The new identity sketches are the best work we’ve shown a client this year."),
        (3, 1, 2, "Leadership", 120, "Thank you for backing the team in that client meeting."),
    ];

    public static Seed Build(DateTime now, string passwordHash)
    {
        var lastRefill = LastRefill(now);
        var companies = new List<Company>();
        var users = new List<User>();
        var transactions = new List<Transaction>();

        var fernhill = AddCompany("Fernhill Labs", "fernhill.example", "#1B2A4A", "#C8A15A");
        var fernhillPeople = Fernhill.Select(p => AddUser(fernhill, p)).ToList();
        foreach (var (From, To, Amount, Category, HoursAgo, Message) in FernhillGifts)
            AddGift(fernhill, fernhillPeople[From - 1], fernhillPeople[To - 1], Amount, Category, now.AddHours(-HoursAgo), Message);

        var fatima = fernhillPeople[8];
        var rest = Allowance - SpentSince(fatima, lastRefill);
        var sinceRefill = now - lastRefill;
        for (var i = 0; rest > 0; i++)
        {
            var (to, category, message) = FatimaSpendsTheRest[i % FatimaSpendsTheRest.Length];
            var amount = Math.Min(10, rest);
            // Spread over this cycle, however recently it started
            AddGift(fernhill, fatima, fernhillPeople[to - 1], amount, category, now - sinceRefill * (0.1 + 0.2 * i), message);
            rest -= amount;
        }

        var kestrel = AddCompany("Kestrel Studio", "kestrel.example", "#FFD60A", "");
        var kestrelPeople = Kestrel.Select(p => AddUser(kestrel, p)).ToList();
        foreach (var (From, To, Amount, Category, HoursAgo, Message) in KestrelGifts)
            AddGift(kestrel, kestrelPeople[From - 1], kestrelPeople[To - 1], Amount, Category, now.AddHours(-HoursAgo), Message);

        // The balances follow from the gifts: received is everything ever received, and points to give are what's
        // left of the allowance after this cycle's gifts
        foreach (var u in users)
        {
            u.NumberOfPointsAcquired = transactions.Where(t => t.ReceiverId == u.Id).Sum(t => t.Amount);
            u.NumberOfPointsAvailable = Math.Max(0, Allowance - SpentSince(u, lastRefill));
        }

        return new Seed(companies, users, transactions);

        Company AddCompany(string name, string domain, string primary, string secondary)
        {
            var c = new Company(name, "", primary, secondary) { Id = Guid.NewGuid(), Domain = domain, CreatedDate = now.AddDays(-75) };
            companies.Add(c);
            return c;
        }

        User AddUser(Company c, (string Name, string Job, UserRole Role) p)
        {
            var u = new User(p.Name, EmailFor(p.Name, c.Domain!), passwordHash, p.Job, p.Role, 0, Allowance, c.Id)
            {
                Id = Guid.NewGuid(),
                CreatedDate = now.AddDays(-70),
            };
            users.Add(u);
            return u;
        }

        void AddGift(Company c, User from, User to, int amount, string category, DateTime at, string message) =>
            transactions.Add(new Transaction(from.Id, to.Id, amount, message, category, c.Id) { Id = Guid.NewGuid(), CreatedDate = at });

        int SpentSince(User u, DateTime since) => transactions.Where(t => t.SenderId == u.Id && t.CreatedDate >= since).Sum(t => t.Amount);
    }

    /// <summary>The last reset: the refill day at 00:00 UTC, this month or the one before.</summary>
    private static DateTime LastRefill(DateTime now)
    {
        var refill = new DateTime(now.Year, now.Month, RefillDayUtc, 0, 0, 0, DateTimeKind.Utc);
        return now >= refill ? refill : refill.AddMonths(-1);
    }

    /// <summary>"Tomás Herrera" → tomas.herrera@domain</summary>
    private static string EmailFor(string name, string domain)
    {
        var plain = new string([.. name.Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)]);
        return string.Join('.', plain.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + "@" + domain;
    }

    public static void PrintLogins(IEnumerable<User> users)
    {
        var byEmail = users.ToDictionary(u => u.Email);
        (string Email, string Why)[] picks =
        [
            ("maya.okonkwo@fernhill.example", "Admin, a busy history"),
            ("priya.raman@fernhill.example", "Employee, the most recognized"),
            ("fatima.al-sayed@fernhill.example", "Employee, 0 points left to give"),
            ("zoe.chen@fernhill.example", "Employee, no job title"),
            ("ada.mwangi@kestrel.example", "Admin of another company (yellow brand)"),
        ];
        Console.WriteLine();
        Console.WriteLine($"Every account's password is {Password}. Some to try:");
        foreach (var (email, why) in picks)
            if (byEmail.TryGetValue(email, out var u))
                Console.WriteLine($"  {email,-36} {why}: {u.NumberOfPointsAvailable} to give, {u.NumberOfPointsAcquired} received");
        Console.WriteLine();
        Console.WriteLine("Run the API on this database with: dotnet run --project Gratia.API --launch-profile demo");
    }
}
