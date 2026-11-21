using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gratia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvitationEmailQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "CompanyInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailNextAttemptAt",
                table: "CompanyInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "CompanyInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailStatus",
                table: "CompanyInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingToken",
                table: "CompanyInvitations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_EmailStatus_EmailNextAttemptAt",
                table: "CompanyInvitations",
                columns: new[] { "EmailStatus", "EmailNextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvitations_EmailStatus_EmailNextAttemptAt",
                table: "CompanyInvitations");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "CompanyInvitations");

            migrationBuilder.DropColumn(
                name: "EmailNextAttemptAt",
                table: "CompanyInvitations");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "CompanyInvitations");

            migrationBuilder.DropColumn(
                name: "EmailStatus",
                table: "CompanyInvitations");

            migrationBuilder.DropColumn(
                name: "PendingToken",
                table: "CompanyInvitations");
        }
    }
}
