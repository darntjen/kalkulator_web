using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Kundensituation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Loesungsbezuege",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Zuordnungen",
                schema: "kalkulation",
                table: "Kalkulationen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "AnalyseAngebote",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Paketpreis = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusSeit = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Bemerkung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnterlageId = table.Column<int>(type: "int", nullable: true),
                    ErfasstVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErfasstAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyseAngebote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyseAngebote_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyseAngebote_Unterlagen_UnterlageId",
                        column: x => x.UnterlageId,
                        principalSchema: "projekte",
                        principalTable: "Unterlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Herausforderungen",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    Dimension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Auswirkung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Prioritaet = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quelle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AngelegtVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AngelegtAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeaendertVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Herausforderungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Herausforderungen_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseAngebote_KundenprojektId",
                schema: "projekte",
                table: "AnalyseAngebote",
                column: "KundenprojektId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseAngebote_Nummer",
                schema: "projekte",
                table: "AnalyseAngebote",
                column: "Nummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseAngebote_UnterlageId",
                schema: "projekte",
                table: "AnalyseAngebote",
                column: "UnterlageId");

            migrationBuilder.CreateIndex(
                name: "IX_Herausforderungen_KundenprojektId",
                schema: "projekte",
                table: "Herausforderungen",
                column: "KundenprojektId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyseAngebote",
                schema: "projekte");

            migrationBuilder.DropTable(
                name: "Herausforderungen",
                schema: "projekte");

            migrationBuilder.DropColumn(
                name: "Loesungsbezuege",
                schema: "kalkulation",
                table: "Kalkulationsversionen");

            migrationBuilder.DropColumn(
                name: "Zuordnungen",
                schema: "kalkulation",
                table: "Kalkulationen");
        }
    }
}
