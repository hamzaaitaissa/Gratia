using Gratia.Application.Features.Points.Commands;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace GratiaTests
{
    // #77: one company's failed refill doesn't hold up the companies after it. PointsCycleTests fails a refill in the
    // database; here the order of the companies is fixed, so the failing one comes first.
    public class ResetDuePointsCommandHandlerTests
    {
        private static readonly DateTime Now = new(2026, 11, 21, 0, 5, 0, DateTimeKind.Utc);

        private readonly Company _failing = NewCompany();
        private readonly Company _next = NewCompany();
        private readonly Mock<ICompanyRepository> _companies = new();
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly CapturingLogger _logger = new();
        private readonly List<string> _calls = [];

        public ResetDuePointsCommandHandlerTests()
        {
            _companies.Setup(c => c.GetAllForPointsResetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_failing, _next]);
            _companies.Setup(c => c.TryStartPointsPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback((Guid id, DateTime _, CancellationToken _) => _calls.Add($"claim {Name(id)}")).ReturnsAsync(true);
            _users.Setup(u => u.ResetAvailablePointsAsync(_next.Id, _next.MonthlyPointsAllowance, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("refill next")).ReturnsAsync(4);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            {
                var dbTransaction = new Mock<IDatabaseTransaction>();
                dbTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("commit")).Returns(Task.CompletedTask);
                dbTransaction.Setup(t => t.DisposeAsync()).Callback(() => _calls.Add("end")).Returns(ValueTask.CompletedTask);
                return dbTransaction.Object;
            });
        }

        [Fact]
        public async Task A_failed_refill_is_rolled_back_and_logged_and_the_next_company_is_still_refilled()
        {
            var failure = new TimeoutException("Lock request time out period exceeded");
            FailRefill(failure);

            var refilled = await HandleAsync(CancellationToken.None);

            Assert.Equal(1, refilled);
            // The failing company's transaction ends without a commit, so its claim is undone with it
            Assert.Equal(new[] { "claim failing", "refill failing", "end", "claim next", "refill next", "commit", "end" }, _calls);
            var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Same(failure, error.Exception);
            Assert.Contains(_failing.Id.ToString(), error.Message);
        }

        [Fact]
        public async Task A_cancelled_run_stops_instead_of_moving_on()
        {
            using var cancellation = new CancellationTokenSource();
            _users.Setup(u => u.ResetAvailablePointsAsync(_failing.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback(() => { _calls.Add("refill failing"); cancellation.Cancel(); })
                .ThrowsAsync(new OperationCanceledException(cancellation.Token));

            await Assert.ThrowsAsync<OperationCanceledException>(() => HandleAsync(cancellation.Token));

            Assert.Equal(new[] { "claim failing", "refill failing", "end" }, _calls);
            Assert.Empty(_logger.Entries);
        }

        private void FailRefill(Exception failure) =>
            _users.Setup(u => u.ResetAvailablePointsAsync(_failing.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("refill failing")).ThrowsAsync(failure);

        private async Task<int> HandleAsync(CancellationToken cancellationToken)
        {
            var handler = new ResetDuePointsCommandHandler(_companies.Object, _users.Object, _unitOfWork.Object, _logger);
            return await handler.Handle(new ResetDuePointsCommand(Now), cancellationToken);
        }

        private string Name(Guid companyId) => companyId == _failing.Id ? "failing" : "next";

        // Refilled once before, so this run refills rather than only recording the period
        private static Company NewCompany() => new()
        {
            Id = Guid.NewGuid(),
            PointsResetDay = 21,
            PointsPeriodStartedAt = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
        };

        private sealed class CapturingLogger : ILogger<ResetDuePointsCommandHandler>
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // The successful refill's information line isn't what these tests are about
                if (logLevel >= LogLevel.Warning)
                    Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }
}
