using Gratia.Application.DTOs.UserDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Features.Users.Commands;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GratiaTests
{
    // #63: a company keeps an active admin. Two admins removing each other at the same moment can't be staged over HTTP
    // (SQLite runs one request at a time), so these check the order: the company is locked before the other admins are
    // counted, and nothing changes when none would remain.
    public class LastAdminTests
    {
        private static readonly Guid CompanyId = Guid.NewGuid();

        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<ICompanyRepository> _companies = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IDatabaseTransaction> _dbTransaction = new();
        private readonly List<string> _calls = [];

        public LastAdminTests()
        {
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_dbTransaction.Object);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("save")).ReturnsAsync(1);
            _dbTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("commit")).Returns(Task.CompletedTask);
            _companies.Setup(c => c.LockAsync(CompanyId, It.IsAny<CancellationToken>())).Callback(() => _calls.Add("lock")).Returns(Task.CompletedTask);
            _refreshTokens.Setup(r => r.DeleteForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Callback(() => _calls.Add("sign out"))
                .ReturnsAsync(0);
        }

        [Fact]
        public async Task An_admin_is_not_deactivated_when_no_other_admin_would_remain()
        {
            // As when another admin deactivated or demoted the caller a moment earlier
            var admin = NewUser(UserRole.Admin);
            OtherAdminRemains(admin, false);

            await Assert.ThrowsAsync<BusinessRuleException>(() => DeactivateAsync(admin));

            Assert.Null(admin.DeactivatedAt);
            Assert.Equal(new[] { "lock", "count" }, _calls);
        }

        [Fact]
        public async Task An_admin_is_deactivated_once_the_company_is_locked_and_another_admin_found()
        {
            var admin = NewUser(UserRole.Admin);
            OtherAdminRemains(admin, true);

            await DeactivateAsync(admin);

            Assert.NotNull(admin.DeactivatedAt);
            Assert.Equal(new[] { "lock", "count", "save", "sign out", "commit" }, _calls);
        }

        [Fact]
        public async Task Deactivating_an_employee_takes_no_lock()
        {
            await DeactivateAsync(NewUser(UserRole.Employee));

            Assert.Equal(new[] { "save", "sign out", "commit" }, _calls);
        }

        [Theory]
        [InlineData(true, new[] { "lock", "count", "save", "commit" })]
        [InlineData(false, new[] { "lock", "count" })]
        public async Task Demoting_an_admin_counts_the_others_after_locking_the_company(bool anotherAdminRemains, string[] expectedCalls)
        {
            var admin = NewUser(UserRole.Admin);
            OtherAdminRemains(admin, anotherAdminRemains);

            if (anotherAdminRemains)
                await UpdateAsync(admin, UserRole.Employee);
            else
                await Assert.ThrowsAsync<BusinessRuleException>(() => UpdateAsync(admin, UserRole.Employee));

            Assert.Equal(expectedCalls, _calls);
            Assert.Equal(anotherAdminRemains ? UserRole.Employee : UserRole.Admin, admin.Role);
        }

        [Theory]
        [InlineData(UserRole.Admin, UserRole.Admin)]
        [InlineData(UserRole.Employee, UserRole.Admin)]
        [InlineData(UserRole.Employee, UserRole.Employee)]
        public async Task Edits_that_keep_or_add_an_admin_take_no_lock(UserRole from, UserRole to)
        {
            await UpdateAsync(NewUser(from), to);

            Assert.Equal(new[] { "save", "commit" }, _calls);
        }

        private static User NewUser(UserRole role) =>
            new("Ada", $"{Guid.NewGuid():N}@acme.test", "hash", "Dev", role, 0, 25, CompanyId) { Id = Guid.NewGuid() };

        private void OtherAdminRemains(User admin, bool result) =>
            _users.Setup(u => u.HasOtherActiveAdminAsync(CompanyId, admin.Id, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("count")).ReturnsAsync(result);

        private async Task DeactivateAsync(User user)
        {
            _users.Setup(u => u.GetByIdInCompanyIncludingDeactivatedAsync(user.Id, CompanyId)).ReturnsAsync(user);
            var handler = new DeactivateUserCommandHandler(_users.Object, _companies.Object, _refreshTokens.Object, _unitOfWork.Object);
            await handler.Handle(new DeactivateUserCommand(user.Id, CallerUserId: Guid.NewGuid(), CompanyId), CancellationToken.None);
        }

        private async Task UpdateAsync(User user, UserRole role)
        {
            _users.Setup(u => u.GetByIdInCompanyAsync(user.Id, CompanyId)).ReturnsAsync(user);
            var handler = new UpdateUserCommandHandler(_users.Object, _companies.Object, _unitOfWork.Object, Mock.Of<IEmailService>(),
                NullLogger<UpdateUserCommandHandler>.Instance);
            // The same email, so the domain and duplicate checks don't run
            var dto = new UpdateUserDto { Id = user.Id, FullName = user.FullName, Email = user.Email, JobTitle = user.JobTitle, Role = role };
            await handler.Handle(new UpdateUserCommand(CompanyId, dto), CancellationToken.None);
        }
    }
}
