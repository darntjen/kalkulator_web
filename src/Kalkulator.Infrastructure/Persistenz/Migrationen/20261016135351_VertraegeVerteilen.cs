using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VertraegeVerteilen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AbgelegtAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AblageLink",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GemeldetAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbgelegtAm",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "AblageLink",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "GemeldetAm",
                schema: "kalkulation",
                table: "Vertragswerke");
        }
    }
}
