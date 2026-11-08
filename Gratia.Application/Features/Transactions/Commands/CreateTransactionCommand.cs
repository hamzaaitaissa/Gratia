using Gratia.Application.DTOs.Transaction;
using Gratia.Application.Exceptions;
using Gratia.Application.Mappings;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Transactions.Commands;

public sealed record CreateTransactionCommand(Guid SenderId, CreateTransactionDto Transaction) : ICommand<ReadTransactionDto>;

public sealed class CreateTransactionCommandHandler(
    ITransactionRepository transactionRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTransactionCommand, ReadTransactionDto>
{
    public async ValueTask<ReadTransactionDto> Handle(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Transaction;

        var sender = await userRepository.GetByIdAsync(command.SenderId)
            ?? throw new NotFoundException("Sender not found");
        var receiver = await userRepository.GetByIdAsync(dto.ReceiverId)
            ?? throw new NotFoundException("Receiver not found");

        if (dto.Amount <= 0) throw new BusinessRuleException("You cant send negative points or 0 points");
        if (sender.CompanyId != receiver.CompanyId) throw new BusinessRuleException("you are only allowed to exchange points with people within your company");
        if (sender.Id == dto.ReceiverId) throw new BusinessRuleException("You cant send points to yourself?!");

        var transaction = new Transaction(
            sender.Id,
            receiver.Id,
            dto.Amount,
            dto.Message,
            dto.TypeOfDonation,
            sender.CompanyId)
        {
            // Already-tracked entities, so EF links them without inserting them again; ToReadDto reads their names
            Sender = sender,
            Receiver = receiver,
            Company = sender.Company
        };

        sender.NumberOfPointsAvailable -= dto.Amount;
        receiver.NumberOfPointsAcquired += dto.Amount;

        transactionRepository.Add(transaction);

        // One SaveChanges is one database transaction: the transfer and both balances are stored together or not at all
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return transaction.ToReadDto();
    }
}
