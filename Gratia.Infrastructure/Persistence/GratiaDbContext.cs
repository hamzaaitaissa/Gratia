using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;


namespace Gratia.Infrastructure.Data
{
    // DbContext already provides SaveChangesAsync, so it is the unit of work itself
    public class GratiaDbContext : DbContext, IUnitOfWork
    {
        public GratiaDbContext(DbContextOptions<GratiaDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<CompanyInvitation> CompanyInvitations { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            new DatabaseTransaction(await Database.BeginTransactionAsync(cancellationToken));

        private sealed class DatabaseTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

            // Rolls back when CommitAsync wasn't called
            public ValueTask DisposeAsync() => transaction.DisposeAsync();
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

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

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

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Sender)
                .WithMany(u => u.SentTransactions)
                .HasForeignKey(t=> t.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t=> t.Receiver)
                .WithMany(u=> u.ReceivedTransactions)
                .HasForeignKey(t=>t.ReceiverId)
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

            modelBuilder.Entity<CompanyInvitation>()
                .HasOne(ci => ci.InvitedByUser)
                .WithMany()
                .HasForeignKey(ci => ci.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(ci => ci.InvitationToken)
                .IsUnique();

            modelBuilder.Entity<CompanyInvitation>()
                .HasIndex(ci => new { ci.Email, ci.CompanyId });

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
