using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Vertriebsfreigaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FreigabeSolutionConsultantAm",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreigabeSolutionConsultantVon",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FreigabeVertriebsleitungAm",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreigabeVertriebsleitungVon",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vertriebsfreigaben",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KalkulationId = table.Column<int>(type: "int", nullable: false),
                    Rolle = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Benutzer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Zeitpunkt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kommentar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AufgehobenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AufgehobenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Aufhebungsgrund = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vertriebsfreigaben", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vertriebsfreigaben_Kalkulationen_KalkulationId",
                        column: x => x.KalkulationId,
                        principalSchema: "kalkulation",
                        principalTable: "Kalkulationen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kundenprojekte_AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte",
                column: "AngenommenesAngebotId");

            migrationBuilder.CreateIndex(
                name: "IX_Vertriebsfreigaben_Aktiv",
                schema: "kalkulation",
                table: "Vertriebsfreigaben",
                columns: new[] { "KalkulationId", "Rolle" },
                unique: true,
                filter: "[AufgehobenAm] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Kundenprojekte_Angebote_AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte",
                column: "AngenommenesAngebotId",
                principalSchema: "kalkulation",
                principalTable: "Angebote",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kundenprojekte_Angebote_AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte");

            migrationBuilder.DropTable(
                name: "Vertriebsfreigaben",
                schema: "kalkulation");

            migrationBuilder.DropIndex(
                name: "IX_Kundenprojekte_AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte");

            migrationBuilder.DropColumn(
                name: "AngenommenesAngebotId",
                schema: "projekte",
                table: "Kundenprojekte");

            migrationBuilder.DropColumn(
                name: "FreigabeSolutionConsultantAm",
                schema: "kalkulation",
                table: "Kalkulationsversionen");

            migrationBuilder.DropColumn(
                name: "FreigabeSolutionConsultantVon",
                schema: "kalkulation",
                table: "Kalkulationsversionen");

            migrationBuilder.DropColumn(
                name: "FreigabeVertriebsleitungAm",
                schema: "kalkulation",
                table: "Kalkulationsversionen");

            migrationBuilder.DropColumn(
                name: "FreigabeVertriebsleitungVon",
                schema: "kalkulation",
                table: "Kalkulationsversionen");
        }
    }
}
