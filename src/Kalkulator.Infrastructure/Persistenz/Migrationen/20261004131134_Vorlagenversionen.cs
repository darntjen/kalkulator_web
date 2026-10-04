using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Vorlagenversionen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DokumentVorlagen_Code_Version",
                schema: "katalog",
                table: "DokumentVorlagen");

            migrationBuilder.CreateTable(
                name: "Vorlagenabgleiche",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Quelle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AusgeloestVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beginn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Ende = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Dateien = table.Column<int>(type: "int", nullable: false),
                    NeueFassungen = table.Column<int>(type: "int", nullable: false),
                    Bericht = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fehler = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vorlagenabgleiche", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vorlagenversionen",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DokumentVorlageId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<int>(type: "int", nullable: false),
                    VersionLaut = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Dateiname = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Quelle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Pfad = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    QuellId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuellStand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GeaendertInQuelleAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GeaendertInQuelleVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AbgerufenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AbgerufenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Hinweise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Eingaben = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Komponenten = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntschiedenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntschiedenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Kommentar = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vorlagenversionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vorlagenversionen_DokumentVorlagen_DokumentVorlageId",
                        column: x => x.DokumentVorlageId,
                        principalSchema: "katalog",
                        principalTable: "DokumentVorlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VorlagenDateien",
                schema: "katalog",
                columns: table => new
                {
                    VorlagenversionId = table.Column<int>(type: "int", nullable: false),
                    Inhalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenDateien", x => x.VorlagenversionId);
                    table.ForeignKey(
                        name: "FK_VorlagenDateien_Vorlagenversionen_VorlagenversionId",
                        column: x => x.VorlagenversionId,
                        principalSchema: "katalog",
                        principalTable: "Vorlagenversionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DokumentVorlagen_Code",
                schema: "katalog",
                table: "DokumentVorlagen",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vorlagenabgleiche_Beginn",
                schema: "katalog",
                table: "Vorlagenabgleiche",
                column: "Beginn");

            migrationBuilder.CreateIndex(
                name: "IX_Vorlagenversionen_DokumentVorlageId_Nummer",
                schema: "katalog",
                table: "Vorlagenversionen",
                columns: new[] { "DokumentVorlageId", "Nummer" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vorlagenabgleiche",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "VorlagenDateien",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "Vorlagenversionen",
                schema: "katalog");

            migrationBuilder.DropIndex(
                name: "IX_DokumentVorlagen_Code",
                schema: "katalog",
                table: "DokumentVorlagen");

            migrationBuilder.CreateIndex(
                name: "IX_DokumentVorlagen_Code_Version",
                schema: "katalog",
                table: "DokumentVorlagen",
                columns: new[] { "Code", "Version" },
                unique: true);
        }
    }
}
