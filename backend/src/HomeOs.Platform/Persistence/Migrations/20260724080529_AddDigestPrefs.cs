using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeOs.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDigestPrefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DigestFrequency",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DigestLastSentUtc",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigestFrequency",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DigestLastSentUtc",
                table: "AspNetUsers");
        }
    }
}
