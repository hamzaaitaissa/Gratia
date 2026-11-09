using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gratia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "InvitedByUserId",
                table: "CompanyInvitations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "Companies",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DomainVerifiedAt",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Domain",
                table: "Companies",
                column: "Domain",
                unique: true,
                filter: "[Domain] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_Domain",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Domain",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DomainVerifiedAt",
                table: "Companies");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvitedByUserId",
                table: "CompanyInvitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
