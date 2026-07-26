using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeOs.Modules.Calendar.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSharedWith : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharedWith",
                table: "CalendarEvents",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedWith",
                table: "CalendarEvents");
        }
    }
}
