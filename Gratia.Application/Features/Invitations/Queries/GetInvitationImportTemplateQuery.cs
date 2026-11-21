using Gratia.Application.Interfaces;
using Mediator;

namespace Gratia.Application.Features.Invitations.Queries;

// The spreadsheet to fill in for a bulk invitation (#54), as .xlsx bytes
public sealed record GetInvitationImportTemplateQuery : IQuery<byte[]>;

public sealed class GetInvitationImportTemplateQueryHandler(IInvitationSpreadsheet spreadsheet)
    : IQueryHandler<GetInvitationImportTemplateQuery, byte[]>
{
    public ValueTask<byte[]> Handle(GetInvitationImportTemplateQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(spreadsheet.CreateTemplate());
}
