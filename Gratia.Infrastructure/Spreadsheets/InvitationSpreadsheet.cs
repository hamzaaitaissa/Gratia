using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using Gratia.Application.Exceptions;
using Gratia.Application.Interfaces;

namespace Gratia.Infrastructure.Spreadsheets
{
    // The .xlsx side of bulk invitations (#54), with ClosedXML (MIT). Its SixLabors.Fonts dependency is free to use only
    // as a transitive dependency: don't reference it directly.
    public sealed class InvitationSpreadsheet : IInvitationSpreadsheet
    {
        private static readonly string[] Columns = ["Email", "Full name", "Job title", "Role"];

        // An upload is at most 1 MB, but its parts are compressed: far more than 500 people's rows unpacked is refused
        // before ClosedXML reads it
        private const long MaxUnpackedBytes = 20 * 1024 * 1024;

        private const int TemplateRows = 500;

        public byte[] CreateTemplate()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Invitations");
            for (var i = 0; i < Columns.Length; i++)
                sheet.Cell(1, i + 1).Value = Columns[i];
            var header = sheet.Range(1, 1, 1, Columns.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            sheet.SheetView.FreezeRows(1);
            sheet.Column(1).Width = 36;
            sheet.Column(2).Width = 28;
            sheet.Column(3).Width = 28;
            sheet.Column(4).Width = 14;

            // Text, so a spreadsheet app doesn't turn an address into a link or a number
            sheet.Range(2, 1, TemplateRows + 1, 3).Style.NumberFormat.Format = "@";

            // Role as a dropdown
            var roles = sheet.Range(2, 4, TemplateRows + 1, 4).CreateDataValidation();
            roles.List("\"Employee,Admin\"", true);
            roles.ErrorTitle = "Role";
            roles.ErrorMessage = "Choose Employee or Admin.";

            var help = workbook.AddWorksheet("How to fill it in");
            string[] lines =
            [
                "One person per row, up to 500 in one import. Keep the first row: the import finds the columns by these names.",
                "Email: required. Until your company's domain is verified, only addresses on that domain can be invited.",
                "Full name, Job title: optional. The person starts from them when they accept, and can change them.",
                "Role: Employee or Admin. Empty means Employee.",
                "You'll see what happens to each row before anyone is invited."
            ];
            for (var i = 0; i < lines.Length; i++)
                help.Cell(i + 1, 1).Value = lines[i];
            help.Column(1).Width = 110;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public IReadOnlyList<SpreadsheetRow> Read(Stream file, int maxRows)
        {
            using var copy = new MemoryStream();
            file.CopyTo(copy);
            CheckUnpackedSize(copy);

            XLWorkbook workbook;
            try
            {
                copy.Position = 0;
                workbook = new XLWorkbook(copy);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw NotAWorkbook();
            }

            using (workbook)
            {
                var sheet = workbook.Worksheets.First();
                if (sheet.RangeUsed() is not { } used)
                    return [];

                // The columns by their names in the first row, in any order, so a sheet someone rearranged still works
                var headerRow = used.FirstRow().RowNumber();
                var columns = new Dictionary<string, int>();
                foreach (var cell in sheet.Row(headerRow).CellsUsed())
                    columns.TryAdd(Key(Text(cell)), cell.Address.ColumnNumber);
                if (!columns.ContainsKey("email"))
                    throw new BusinessRuleException(
                        "The first row must name the columns: Email, Full name, Job title and Role. Download the template and fill that in.");

                string Value(IXLRow row, string key) => columns.TryGetValue(key, out var column) ? Text(row.Cell(column)) : "";

                var rows = new List<SpreadsheetRow>();
                for (var number = headerRow + 1; number <= used.LastRow().RowNumber() && rows.Count <= maxRows; number++)
                {
                    var row = sheet.Row(number);
                    var values = new SpreadsheetRow(number, Value(row, "email"), Value(row, "fullname"), Value(row, "jobtitle"), Value(row, "role"));
                    if (values is { Email: "", FullName: "", JobTitle: "", Role: "" })
                        continue;
                    rows.Add(values);
                }

                return rows;
            }
        }

        // "Full name", "full_name", "FullName" and "E-mail" all name the same columns
        private static string Key(string header) =>
            new string(header.Where(char.IsLetter).ToArray()).ToLowerInvariant();

        // A formula's last calculated value, never a new calculation
        private static string Text(IXLCell cell)
        {
            var value = cell.HasFormula ? cell.CachedValue : cell.Value;
            return value.ToString(CultureInfo.InvariantCulture).Trim();
        }

        private static void CheckUnpackedSize(MemoryStream file)
        {
            long unpacked;
            try
            {
                file.Position = 0;
                using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
                unpacked = zip.Entries.Sum(e => e.Length);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
            {
                throw NotAWorkbook();
            }

            if (unpacked > MaxUnpackedBytes)
                throw new BusinessRuleException("This file holds far more than 500 people's rows. Download the template and fill that in.");
        }

        private static BusinessRuleException NotAWorkbook() =>
            new("This isn't an Excel workbook (.xlsx) that can be read. Download the template, fill it in and upload that.");
    }
}
