using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gratia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyPointsCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing companies keep the cycle that applied to everyone until now (25 points, the 21st), instead of
            // the generated 0, which the check constraints below would refuse. New rows get them from the entity.
            migrationBuilder.AddColumn<int>(
                name: "MonthlyPointsAllowance",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<DateTime>(
                name: "PointsPeriodStartedAt",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsResetDay",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 21);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Companies_MonthlyPointsAllowance",
                table: "Companies",
                sql: "[MonthlyPointsAllowance] BETWEEN 1 AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Companies_PointsResetDay",
                table: "Companies",
                sql: "[PointsResetDay] BETWEEN 1 AND 28");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Companies_MonthlyPointsAllowance",
                table: "Companies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Companies_PointsResetDay",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MonthlyPointsAllowance",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PointsPeriodStartedAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PointsResetDay",
                table: "Companies");
        }
    }
}
