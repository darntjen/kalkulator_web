using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class NavisionArtikelnummern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bezeichnung",
                schema: "preise",
                table: "Preisstaffeln",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NavisionArtikelnummer",
                schema: "preise",
                table: "Preisstaffeln",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NavisionArtikelnummer",
                schema: "katalog",
                table: "Preiskomponenten",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bezeichnung",
                schema: "preise",
                table: "Preisstaffeln");

            migrationBuilder.DropColumn(
                name: "NavisionArtikelnummer",
                schema: "preise",
                table: "Preisstaffeln");

            migrationBuilder.DropColumn(
                name: "NavisionArtikelnummer",
                schema: "katalog",
                table: "Preiskomponenten");
        }
    }
}
