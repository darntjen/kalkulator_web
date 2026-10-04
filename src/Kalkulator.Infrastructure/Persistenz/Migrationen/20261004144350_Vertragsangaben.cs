using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Vertragsangaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bestehende Kalkulationen und Versionen haben noch keine Vertragsangaben: leeres JSON-Objekt.
            migrationBuilder.AddColumn<string>(
                name: "Vertragsangaben",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Vertragsangaben",
                schema: "kalkulation",
                table: "Kalkulationen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vertragsangaben",
                schema: "kalkulation",
                table: "Kalkulationsversionen");

            migrationBuilder.DropColumn(
                name: "Vertragsangaben",
                schema: "kalkulation",
                table: "Kalkulationen");
        }
    }
}
