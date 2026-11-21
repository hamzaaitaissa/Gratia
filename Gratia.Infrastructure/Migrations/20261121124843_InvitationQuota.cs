using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gratia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvitationQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationWindowStart",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitationsInWindow",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitationWindowStart",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "InvitationsInWindow",
                table: "Companies");
        }
    }
}
