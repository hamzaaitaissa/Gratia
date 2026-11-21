namespace Gratia.Application.Interfaces
{
    // One filled-in row of an import spreadsheet (#54), as typed: trimmed, otherwise unchecked. Row is its number in the
    // sheet, so the admin can find it.
    public sealed record SpreadsheetRow(int Row, string Email, string FullName, string JobTitle, string Role);

    // The .xlsx side of bulk invitations (#54), implemented with ClosedXML in Infrastructure
    public interface IInvitationSpreadsheet
    {
        // A workbook with the columns Email, Full name, Job title and Role (a dropdown), and a sheet that explains them
        byte[] CreateTemplate();

        // The filled-in rows of the first sheet, found by their column names in its first row; empty rows are left out.
        // Stops after maxRows + 1, so the caller can tell there are too many without reading them all.
        // Throws BusinessRuleException when the file isn't a workbook this can read, is too large once unpacked, or has
        // no Email column.
        IReadOnlyList<SpreadsheetRow> Read(Stream file, int maxRows);
    }
}
