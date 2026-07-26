using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeOs.Modules.Reminders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Recurrence",
                table: "Reminders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recurrence",
                table: "Reminders");
        }
    }
}
