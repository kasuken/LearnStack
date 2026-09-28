using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnStack.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingEventOrderingAndCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastBillingEventAtUtc",
                table: "UserPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanCancelsAtUtc",
                table: "UserPlans",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastBillingEventAtUtc",
                table: "UserPlans");

            migrationBuilder.DropColumn(
                name: "PlanCancelsAtUtc",
                table: "UserPlans");
        }
    }
}
