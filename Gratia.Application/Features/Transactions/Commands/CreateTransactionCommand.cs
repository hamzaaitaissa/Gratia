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
    ICompanyRepository companyRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTransactionCommand, ReadTransactionDto>
{
    public async ValueTask<ReadTransactionDto> Handle(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Transaction;

        var sender = await userRepository.GetByIdAsync(command.SenderId)
            ?? throw new NotFoundException("Sender not found");
        // A deactivated user's access token works until it expires, but not for giving points (#16)
        if (sender.DeactivatedAt is not null)
            throw new ForbiddenException("Your account has been deactivated.");
        // Points stay within a company. A receiver in another company, or a deactivated one, gets the same 404 as one
        // that doesn't exist.
        var receiver = await userRepository.GetByIdInCompanyAsync(dto.ReceiverId, sender.CompanyId)
            ?? throw new NotFoundException("Receiver not found");

        if (dto.Amount <= 0) throw new BusinessRuleException("You cant send negative points or 0 points");
        if (sender.Id == dto.ReceiverId) throw new BusinessRuleException("You cant send points to yourself?!");

        // #9: the usual case gets a precise answer here. The conditional update below is what actually enforces it.
        if (dto.Amount > sender.NumberOfPointsAvailable)
            throw new BusinessRuleException($"You only have {sender.NumberOfPointsAvailable} points left to give this month.");

        // Loaded for the response's CompanyName, which was empty before (#14)
        var company = await companyRepository.GetByIdAsync(sender.CompanyId)
            ?? throw new NotFoundException("Company not found");

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
            Company = company
        };

        // #11: the balances change through atomic, conditional UPDATEs rather than the tracked entities (which may
        // already be stale), so two sends at once can't both spend the same points and two gifts to one person
        // can't overwrite each other. The updates and the new transaction commit together or not at all.
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The two rows are locked in Id order, so two people sending to each other at the same moment can't deadlock
        bool spent;
        if (sender.Id.CompareTo(receiver.Id) < 0)
        {
            spent = await userRepository.TrySpendAvailablePointsAsync(sender.Id, dto.Amount, cancellationToken);
            if (spent)
                await userRepository.AddAcquiredPointsAsync(receiver.Id, dto.Amount, cancellationToken);
        }
        else
        {
            await userRepository.AddAcquiredPointsAsync(receiver.Id, dto.Amount, cancellationToken);
            spent = await userRepository.TrySpendAvailablePointsAsync(sender.Id, dto.Amount, cancellationToken);
        }

        // Another send spent the points first. Leaving without committing rolls back the receiver's points too.
        if (!spent)
            throw new BusinessRuleException("You don't have enough points left to give this month.");

        transactionRepository.Add(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return transaction.ToReadDto();
    }
}
