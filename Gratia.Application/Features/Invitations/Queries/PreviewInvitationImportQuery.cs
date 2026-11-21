using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;

namespace Gratia.Application.Features.Invitations.Queries;

// Reads an uploaded spreadsheet and says what importing it would do with each row (#54). Nothing is saved: the admin
// checks the list, then sends the rows to ImportInvitationsCommand. The controller limits the file to
// MaxFileBytes (1 MB).
public sealed record PreviewInvitationImportQuery(Guid CompanyId, Stream File) : IQuery<ImportPreviewDto>
{
    public const long MaxFileBytes = InvitationImport.MaxFileBytes;
}

public sealed class PreviewInvitationImportQueryHandler(
    IInvitationSpreadsheet spreadsheet,
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IInvitationRepository invitationRepository)
    : IQueryHandler<PreviewInvitationImportQuery, ImportPreviewDto>
{
    public async ValueTask<ImportPreviewDto> Handle(PreviewInvitationImportQuery query, CancellationToken cancellationToken)
    {
        var company = await companyRepository.GetByIdAsync(query.CompanyId)
            ?? throw new NotFoundException("Company not found");

        var rows = spreadsheet.Read(query.File, InvitationImport.MaxRows);
        var checkedRows = await InvitationImport.CheckAsync(rows, company, userRepository, invitationRepository, cancellationToken);

        return new ImportPreviewDto
        {
            Rows = checkedRows.Select(r => r.ToDto()).ToList(),
            Ready = checkedRows.Count(r => r.Status == ImportRowStatus.Ready)
        };
    }
}
