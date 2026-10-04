using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class PaperlessUebergabe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unterschriften",
                schema: "katalog",
                table: "Vorlagenversionen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PaperlessDokumentId",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UebergabeFehler",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UebergabeVersuchtAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UebergebenAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unterzeichner",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unterschriften",
                schema: "katalog",
                table: "Vorlagenversionen");

            migrationBuilder.DropColumn(
                name: "PaperlessDokumentId",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "UebergabeFehler",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "UebergabeVersuchtAm",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "UebergebenAm",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "Unterzeichner",
                schema: "kalkulation",
                table: "Vertragswerke");
        }
    }
}
