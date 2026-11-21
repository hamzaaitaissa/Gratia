using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;


namespace Gratia.Infrastructure.Data
{
    // DbContext already provides SaveChangesAsync, so it is the unit of work itself
    public class GratiaDbContext : DbContext, IUnitOfWork
    {
        // The query filter that keeps every query to the caller's company (#78). A read that has to cross companies opts
        // out of it by name, with TenantFilterExtensions.AcrossCompanies.
        public const string TenantFilter = "Tenant";

        private readonly ICurrentUserService? _currentUser;

        // The API passes the caller (ICurrentUserService). A context made without one, like a tool's, sees no company's data.
        public GratiaDbContext(DbContextOptions<GratiaDbContext> options, ICurrentUserService? currentUser = null) : base(options)
        {
            _currentUser = currentUser;
        }

        // The company in the caller's access token. Null with no signed-in caller: a background job, an anonymous request,
        // or the access token check itself, which runs before the request has a caller. Read again by every query.
        private Guid? CurrentCompanyId => _currentUser?.CompanyId is { } id && id != Guid.Empty ? id : null;

        public DbSet<User> Users { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<CompanyInvitation> CompanyInvitations { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<LoginAttempt> LoginAttempts { get; set; }

        public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            new DatabaseTransaction(await Database.BeginTransactionAsync(cancellationToken));

        private sealed class DatabaseTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

            // Rolls back when CommitAsync wasn't called
            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // EF warns that RefreshToken has a required link to the filtered User but no tenant filter of its own (#78), so
            // a query can find a session whose user it can't see. That's intended: sessions hold no company data, and the
            // one read that loads their user opts out (RefreshTokenRepository.GetByHashAsync). TenantIsolationTests lists
            // the entities without the filter instead, so a new one is a decision rather than a warning.
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Every DateTime is UTC (#40). datetime2 doesn't keep the Kind, so it's marked again on the way out,
            // and the JSON then ends in "Z" instead of leaving clients to guess the time zone.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
        }

        private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
            toDatabase => toDatabase.Kind == DateTimeKind.Local ? toDatabase.ToUniversalTime() : toDatabase,
            fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // #78: a second line behind the CompanyId check every query already makes (#22). A query that forgets it sees
            // only the caller's company, and with no caller (CurrentCompanyId null) it sees nothing, so it fails closed.
            // It applies to ExecuteUpdate and ExecuteDelete too. Refresh tokens and sign-in attempts hold no company data.
            modelBuilder.Entity<Company>().HasQueryFilter(TenantFilter, c => c.Id == CurrentCompanyId);
            modelBuilder.Entity<User>().HasQueryFilter(TenantFilter, u => u.CompanyId == CurrentCompanyId);
            modelBuilder.Entity<Transaction>().HasQueryFilter(TenantFilter, t => t.CompanyId == CurrentCompanyId);
            modelBuilder.Entity<CompanyInvitation>().HasQueryFilter(TenantFilter, i => i.CompanyId == CurrentCompanyId);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // A forgotten-password link is found by the SHA-256 (hex) of its token (#50). Unique where set.
            modelBuilder.Entity<User>(user =>
            {
                user.Property(u => u.PasswordResetTokenHash).HasMaxLength(64);
                user.HasIndex(u => u.PasswordResetTokenHash).IsUnique();
            });

            // One row per signed-in device (#15). TokenHash is the SHA-256 of the token in hex; deleting a user signs
            // them out everywhere.
            modelBuilder.Entity<RefreshToken>(token =>
            {
                token.Property(t => t.TokenHash).HasMaxLength(64);
                token.HasIndex(t => t.TokenHash).IsUnique();
                token.HasOne(t => t.User)
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Sign-in attempts (#72), counted per email over a short window and deleted by age. No link to Users: an
            // email without an account counts the same. 254 is the longest email LoginDto accepts.
            modelBuilder.Entity<LoginAttempt>(attempt =>
            {
                attempt.Property(a => a.Email).HasMaxLength(254);
                attempt.HasIndex(a => new { a.Email, a.CreatedDate });
                attempt.HasIndex(a => a.CreatedDate);
            });

            // #79: the database keeps a recognition inside one company, not just CreateTransactionCommand. The sender and
            // the receiver are found by (Id, CompanyId) with the recognition's own CompanyId, so both must be in its company.
            // (Id, CompanyId) is an alternate key, so a user's company can't change.
            modelBuilder.Entity<User>().HasAlternateKey(u => new { u.Id, u.CompanyId });

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Sender)
                .WithMany(u => u.SentTransactions)
                .HasForeignKey(t => new { t.SenderId, t.CompanyId })
                .HasPrincipalKey(u => new { u.Id, u.CompanyId })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Receiver)
                .WithMany(u => u.ReceivedTransactions)
                .HasForeignKey(t => new { t.ReceiverId, t.CompanyId })
                .HasPrincipalKey(u => new { u.Id, u.CompanyId })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Company)
                .WithMany()
                .HasForeignKey(t => t.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(ci => ci.Company)
                .WithMany()
                .HasForeignKey(ci => ci.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // The inviter is in the invitation's company too (#79). A founding invitation has no inviter: with
            // InvitedByUserId NULL, the database doesn't check the key.
            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(ci => ci.InvitedByUser)
                .WithMany()
                .HasForeignKey(ci => new { ci.InvitedByUserId, ci.CompanyId })
                .HasPrincipalKey(u => new { u.Id, u.CompanyId })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(ci => ci.InvitationToken)
                .IsUnique();

            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(ci => new { ci.Email, ci.CompanyId });

            // The background sender looks for the emails that are due (#55)
            modelBuilder.Entity<CompanyInvitation>(invitation =>
            {
                invitation.Property(i => i.PendingToken).HasMaxLength(64);
                invitation.Property(i => i.FullName).HasMaxLength(CompanyInvitation.MaxNameLength);
                invitation.Property(i => i.JobTitle).HasMaxLength(CompanyInvitation.MaxNameLength);
                invitation.HasIndex(i => new { i.EmailStatus, i.EmailNextAttemptAt });
            });

            // One workspace per email domain. Companies without a domain (NULL) don't count.
            modelBuilder.Entity<Company>()
                .Property(c => c.Domain)
                .HasMaxLength(253);
            modelBuilder.Entity<Company>()
                .HasIndex(c => c.Domain)
                .IsUnique();

            // The points cycle (#19). A day past 28 doesn't exist in every month and would break the reset job.
            modelBuilder.Entity<Company>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_Companies_PointsResetDay", $"[PointsResetDay] BETWEEN 1 AND {Company.MaxPointsResetDay}");
                table.HasCheckConstraint("CK_Companies_MonthlyPointsAllowance",
                    $"[MonthlyPointsAllowance] BETWEEN 1 AND {Company.MaxMonthlyPointsAllowance}");
            });
        }
    }
}
