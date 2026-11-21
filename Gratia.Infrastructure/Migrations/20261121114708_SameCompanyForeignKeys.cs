using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gratia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SameCompanyForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #79: rows that already cross companies would make the new keys fail with a bare constraint error. Say what's
            // wrong instead; nothing has changed at this point, and the migration's transaction rolls back.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Transactions t JOIN Users u ON u.Id IN (t.SenderId, t.ReceiverId) WHERE u.CompanyId <> t.CompanyId)
                    OR EXISTS (SELECT 1 FROM CompanyInvitations i JOIN Users u ON u.Id = i.InvitedByUserId WHERE u.CompanyId <> i.CompanyId)
                    THROW 50079, 'Some recognitions or invitations involve a user from another company. Fix or remove those rows, then apply SameCompanyForeignKeys again (TASKS.md #79).', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvitations_Users_InvitedByUserId",
                table: "CompanyInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_ReceiverId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_SenderId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReceiverId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SenderId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_CompanyInvitations_InvitedByUserId",
                table: "CompanyInvitations");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Users_Id_CompanyId",
                table: "Users",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReceiverId_CompanyId",
                table: "Transactions",
                columns: new[] { "ReceiverId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SenderId_CompanyId",
                table: "Transactions",
                columns: new[] { "SenderId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_InvitedByUserId_CompanyId",
                table: "CompanyInvitations",
                columns: new[] { "InvitedByUserId", "CompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvitations_Users_InvitedByUserId_CompanyId",
                table: "CompanyInvitations",
                columns: new[] { "InvitedByUserId", "CompanyId" },
                principalTable: "Users",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_ReceiverId_CompanyId",
                table: "Transactions",
                columns: new[] { "ReceiverId", "CompanyId" },
                principalTable: "Users",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_SenderId_CompanyId",
                table: "Transactions",
                columns: new[] { "SenderId", "CompanyId" },
                principalTable: "Users",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvitations_Users_InvitedByUserId_CompanyId",
                table: "CompanyInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_ReceiverId_CompanyId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_SenderId_CompanyId",
                table: "Transactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Users_Id_CompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReceiverId_CompanyId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SenderId_CompanyId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_CompanyInvitations_InvitedByUserId_CompanyId",
                table: "CompanyInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReceiverId",
                table: "Transactions",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SenderId",
                table: "Transactions",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_InvitedByUserId",
                table: "CompanyInvitations",
                column: "InvitedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvitations_Users_InvitedByUserId",
                table: "CompanyInvitations",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_ReceiverId",
                table: "Transactions",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_SenderId",
                table: "Transactions",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
