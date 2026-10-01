using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Kundenprojekte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kalkulation");

            migrationBuilder.EnsureSchema(
                name: "projekte");

            migrationBuilder.CreateTable(
                name: "Kunden",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Firma = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Strasse = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Postleitzahl = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ansprechpartner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NavisionKundennummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kunden", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kundenprojekte",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KundeId = table.Column<int>(type: "int", nullable: false),
                    Verantwortlich = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Bestandskunde = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Verlustgrund = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Wahrscheinlichkeit = table.Column<int>(type: "int", nullable: true),
                    ErwarteterAbschlussmonat = table.Column<DateOnly>(type: "date", nullable: true),
                    AngelegtAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AngelegtVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Zeilenversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kundenprojekte", x => x.Id);
                    table.CheckConstraint("CK_Kundenprojekte_Wahrscheinlichkeit", "[Wahrscheinlichkeit] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Kundenprojekte_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalSchema: "projekte",
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kalkulationen",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FuerForecast = table.Column<bool>(type: "bit", nullable: false),
                    Vertragsbeginn = table.Column<DateOnly>(type: "date", nullable: true),
                    Eingabe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LetzteVersionsnummer = table.Column<int>(type: "int", nullable: false),
                    Zeilenversion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kalkulationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kalkulationen_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatusEreignisse",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Neu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Zeitpunkt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Benutzer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Verlustgrund = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Kommentar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusEreignisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusEreignisse_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Kalkulationsversionen",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KalkulationId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<int>(type: "int", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    Eingabe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vertragsbeginn = table.Column<DateOnly>(type: "date", nullable: true),
                    SummeMonatlich = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SummeEinmalig = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kalkulationsversionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kalkulationsversionen_Kalkulationen_KalkulationId",
                        column: x => x.KalkulationId,
                        principalSchema: "kalkulation",
                        principalTable: "Kalkulationen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kalkulationsversionen_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sonderpositionen",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KalkulationId = table.Column<int>(type: "int", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Einheit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Menge = table.Column<int>(type: "int", nullable: false),
                    Preis = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Begruendung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Einmalig = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntschiedenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntschiedenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Kommentar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sonderpositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sonderpositionen_Kalkulationen_KalkulationId",
                        column: x => x.KalkulationId,
                        principalSchema: "kalkulation",
                        principalTable: "Kalkulationen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VersionsPositionen",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KalkulationsversionId = table.Column<int>(type: "int", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Menge = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BerechneteMenge = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Einzelpreis = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Betrag = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Abrechnungsart = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Herkunft = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Hinweis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionsPositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionsPositionen_Kalkulationsversionen_KalkulationsversionId",
                        column: x => x.KalkulationsversionId,
                        principalSchema: "kalkulation",
                        principalTable: "Kalkulationsversionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PositionsKosten",
                schema: "intern",
                columns: table => new
                {
                    VersionsPositionId = table.Column<int>(type: "int", nullable: false),
                    Kosten = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionsKosten", x => x.VersionsPositionId);
                    table.ForeignKey(
                        name: "FK_PositionsKosten_VersionsPositionen_VersionsPositionId",
                        column: x => x.VersionsPositionId,
                        principalSchema: "kalkulation",
                        principalTable: "VersionsPositionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kalkulationen_Forecast",
                schema: "kalkulation",
                table: "Kalkulationen",
                column: "KundenprojektId",
                unique: true,
                filter: "[FuerForecast] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Kalkulationsversionen_KalkulationId_Nummer",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                columns: new[] { "KalkulationId", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kalkulationsversionen_PreislisteId",
                schema: "kalkulation",
                table: "Kalkulationsversionen",
                column: "PreislisteId");

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_NavisionKundennummer",
                schema: "projekte",
                table: "Kunden",
                column: "NavisionKundennummer",
                unique: true,
                filter: "[NavisionKundennummer] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Kundenprojekte_KundeId",
                schema: "projekte",
                table: "Kundenprojekte",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Kundenprojekte_Status",
                schema: "projekte",
                table: "Kundenprojekte",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Sonderpositionen_KalkulationId",
                schema: "kalkulation",
                table: "Sonderpositionen",
                column: "KalkulationId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusEreignisse_KundenprojektId_Zeitpunkt",
                schema: "projekte",
                table: "StatusEreignisse",
                columns: new[] { "KundenprojektId", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_VersionsPositionen_KalkulationsversionId_Reihenfolge",
                schema: "kalkulation",
                table: "VersionsPositionen",
                columns: new[] { "KalkulationsversionId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionsPositionen_ServiceCode",
                schema: "kalkulation",
                table: "VersionsPositionen",
                column: "ServiceCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionsKosten",
                schema: "intern");

            migrationBuilder.DropTable(
                name: "Sonderpositionen",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "StatusEreignisse",
                schema: "projekte");

            migrationBuilder.DropTable(
                name: "VersionsPositionen",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "Kalkulationsversionen",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "Kalkulationen",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "Kundenprojekte",
                schema: "projekte");

            migrationBuilder.DropTable(
                name: "Kunden",
                schema: "projekte");
        }
    }
}
