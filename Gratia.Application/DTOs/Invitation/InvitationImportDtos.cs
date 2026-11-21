using System.ComponentModel.DataAnnotations;

namespace Gratia.Application.DTOs.Invitation
{
    // What the import would do with a row (#54)
    public enum ImportRowStatus
    {
        // Will be invited
        Ready,
        // Can't be: Message says why (no email, not an email, an unknown role, another domain, too long, an account elsewhere)
        Error,
        // The same email as an earlier row
        Duplicate,
        // Already a member of the company (deactivated ones too: reactivate them instead)
        Member,
        // Already has an unused invitation that hasn't expired
        Invited
    }

    public class ImportPreviewRowDto
    {
        public int Row { get; set; }
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string Role { get; set; } = "";
        public ImportRowStatus Status { get; set; }
        // Why, when it isn't Ready
        public string? Message { get; set; }
    }

    // Each filled-in row and what the import would do with it. Nothing is saved.
    public class ImportPreviewDto
    {
        public List<ImportPreviewRowDto> Rows { get; set; } = [];
        public int Ready { get; set; }
    }

    // A row to import, as the preview showed it. Every row is checked again.
    public class ImportRowDto
    {
        public int Row { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? JobTitle { get; set; }
        // Employee or Admin; empty means Employee
        public string? Role { get; set; }
    }

    public class ImportInvitationsDto
    {
        [Required, MaxLength(500)]
        public List<ImportRowDto>? Rows { get; set; }
    }

    public class ImportSkippedRowDto
    {
        public int Row { get; set; }
        public string Email { get; set; } = "";
        public ImportRowStatus Status { get; set; }
        public string Reason { get; set; } = "";
    }

    public class ImportResultDto
    {
        public int Invited { get; set; }
        public List<ImportSkippedRowDto> Skipped { get; set; } = [];
    }
}
