using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Exceptions;
using Gratia.Application.Features.Transactions.Commands;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Moq;

namespace GratiaTests
{
    // The race from #11 can't be staged over HTTP: the balance check passes, then another send spends the points
    // before this one's conditional update runs. Nothing may then be saved or committed.
    public class CreateTransactionCommandHandlerTests
    {
        private static readonly Guid Lower = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid Higher = Guid.Parse("00000000-0000-0000-0000-000000000002");

        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<ITransactionRepository> _transactions = new();
        private readonly Mock<ICompanyRepository> _companies = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IDatabaseTransaction> _dbTransaction = new();
        private readonly List<string> _calls = [];

        [Theory]
        [InlineData(true)]  // the sender's row is locked first
        [InlineData(false)] // the receiver is credited first, so the rollback has to undo that
        public async Task When_another_send_spent_the_points_first_nothing_is_saved(bool senderIdIsLower)
        {
            var command = Arrange(senderIdIsLower, spendSucceeds: false);

            await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(command, CancellationToken.None).AsTask());

            _transactions.Verify(t => t.Add(It.IsAny<Transaction>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _dbTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            _dbTransaction.Verify(t => t.DisposeAsync(), Times.Once); // disposing without committing rolls back
        }

        [Theory]
        [InlineData(true, new[] { "spend", "add", "save", "commit" })]
        [InlineData(false, new[] { "add", "spend", "save", "commit" })]
        public async Task Rows_are_updated_in_id_order_and_committed_once(bool senderIdIsLower, string[] expectedCalls)
        {
            var command = Arrange(senderIdIsLower, spendSucceeds: true);

            await CreateHandler().Handle(command, CancellationToken.None);

            Assert.Equal(expectedCalls, _calls);
        }

        private CreateTransactionCommandHandler CreateHandler() => new(_transactions.Object, _users.Object, _companies.Object, _unitOfWork.Object);

        private CreateTransactionCommand Arrange(bool senderIdIsLower, bool spendSucceeds)
        {
            var companyId = Guid.NewGuid();
            var sender = new User("Sender", "sender@acme.test", "hash", "Dev", UserRole.Employee, 0, 25, companyId)
            {
                Id = senderIdIsLower ? Lower : Higher
            };
            var receiver = new User("Receiver", "receiver@acme.test", "hash", "Dev", UserRole.Employee, 0, 25, companyId)
            {
                Id = senderIdIsLower ? Higher : Lower
            };

            _companies.Setup(c => c.GetByIdAsync(companyId)).ReturnsAsync(new Company("Acme", "", "#000000", "#FFFFFF") { Id = companyId });
            _users.Setup(u => u.GetByIdAsync(sender.Id)).ReturnsAsync(sender);
            _users.Setup(u => u.GetByIdInCompanyAsync(receiver.Id, companyId)).ReturnsAsync(receiver);
            _users.Setup(u => u.TrySpendAvailablePointsAsync(sender.Id, 5, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("spend")).ReturnsAsync(spendSucceeds);
            _users.Setup(u => u.AddAcquiredPointsAsync(receiver.Id, 5, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("add")).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_dbTransaction.Object);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("save")).ReturnsAsync(1);
            _dbTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => _calls.Add("commit")).Returns(Task.CompletedTask);

            return new CreateTransactionCommand(sender.Id, new CreateTransactionDto
            {
                ReceiverId = receiver.Id,
                Amount = 5,
                Message = "Thanks!",
                TypeOfDonation = "Teamwork"
            });
        }
    }
}
