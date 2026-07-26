using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeOs.Modules.Reminders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "Reminders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Reminders",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_HouseholdId_SourceKey_SourceId",
                table: "Reminders",
                columns: new[] { "HouseholdId", "SourceKey", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reminders_HouseholdId_SourceKey_SourceId",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Reminders");
        }
    }
}
