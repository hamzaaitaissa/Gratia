using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Features.Auth.Commands;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Moq;

namespace GratiaTests
{
    // #72: an attempt is saved before the email's earlier ones are counted, so attempts sent at the same moment (from
    // many IPs) each count the others, and at most LoginAttempt.MaxFailures get their password checked. SQLite runs one
    // request at a time, so this order can't be staged over HTTP; LoginLimitTests covers the behaviour.
    public class LoginCommandHandlerTests
    {
        private const string Email = "ada@acme.test";

        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
        private readonly Mock<ILoginAttemptRepository> _attempts = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<ITokenService> _tokens = new();
        private readonly Mock<IPasswordHashingService> _hasher = new();
        private readonly List<string> _calls = [];
        private LoginAttempt? _added;

        public LoginCommandHandlerTests()
        {
            _attempts.Setup(a => a.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("clean")).Returns(Task.CompletedTask);
            _attempts.Setup(a => a.Add(It.IsAny<LoginAttempt>())).Callback((LoginAttempt attempt) =>
            {
                _calls.Add("add");
                _added = attempt;
            });
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("save")).ReturnsAsync(1);
            _attempts.Setup(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("delete own")).Returns(Task.CompletedTask);
            _users.Setup(u => u.GetByEmailAsync(Email)).Callback(() => _calls.Add("find user")).ReturnsAsync((User?)null);
            _hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string?>())).Callback(() => _calls.Add("check password")).Returns(false);
        }

        [Fact]
        public async Task The_attempt_is_saved_before_the_earlier_ones_are_counted_and_the_password_checked()
        {
            EarlierAttempts(DateTime.UtcNow.AddMinutes(-1));

            Assert.Null(await HandleAsync());

            Assert.Equal(new[] { "clean", "add", "save", "count", "find user", "check password" }, _calls);
            Assert.Equal(Email, _added!.Email);
        }

        [Fact]
        public async Task An_email_without_an_account_still_has_its_password_checked()
        {
            // With no hash, so the hasher checks a dummy one: as slow as a wrong password, so timing doesn't tell (#82)
            EarlierAttempts();

            Assert.Null(await HandleAsync());

            _hasher.Verify(h => h.VerifyPassword("Wrong-password-1", null), Times.Once);
        }

        [Fact]
        public async Task A_refused_attempt_checks_no_password_and_takes_itself_back()
        {
            // The oldest of the five is ten minutes old, so there's room for another in five minutes
            var now = DateTime.UtcNow;
            EarlierAttempts([.. Enumerable.Range(0, LoginAttempt.MaxFailures).Select(i => now.AddMinutes(-10 + i))]);

            var refused = await Assert.ThrowsAsync<TooManyAttemptsException>(HandleAsync);

            Assert.Equal(new[] { "clean", "add", "save", "count", "delete own" }, _calls);
            _attempts.Verify(a => a.DeleteAsync(_added!.Id, It.IsAny<CancellationToken>()));
            _hasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.InRange(refused.RetryAfter, TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));
            Assert.EndsWith("Try again in 5 minutes.", refused.Message);
        }

        private void EarlierAttempts(params DateTime[] times) =>
            _attempts.Setup(a => a.GetTimesSinceAsync(Email, It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("count")).ReturnsAsync(times);

        private async Task<object?> HandleAsync()
        {
            var handler = new LoginCommandHandler(_users.Object, _refreshTokens.Object, _attempts.Object, _unitOfWork.Object,
                _tokens.Object, _hasher.Object);
            return await handler.Handle(new LoginCommand(new LoginDto { Email = $" {Email.ToUpperInvariant()}", HashedPassword = "Wrong-password-1" }),
                CancellationToken.None);
        }
    }
}
