using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Vertragswerke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vertragswerke",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    AngebotId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ausfertigung = table.Column<int>(type: "int", nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GesamtDateiname = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ZipDateiname = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vertragswerke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vertragswerke_Angebote_AngebotId",
                        column: x => x.AngebotId,
                        principalSchema: "kalkulation",
                        principalTable: "Angebote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vertragswerke_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VertragswerkDateien",
                schema: "kalkulation",
                columns: table => new
                {
                    VertragswerkId = table.Column<int>(type: "int", nullable: false),
                    GesamtPdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Zip = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VertragswerkDateien", x => x.VertragswerkId);
                    table.ForeignKey(
                        name: "FK_VertragswerkDateien_Vertragswerke_VertragswerkId",
                        column: x => x.VertragswerkId,
                        principalSchema: "kalkulation",
                        principalTable: "Vertragswerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VertragswerkDokumente",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VertragswerkId = table.Column<int>(type: "int", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VorlagenversionId = table.Column<int>(type: "int", nullable: false),
                    Fassung = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VertragswerkDokumente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VertragswerkDokumente_Vertragswerke_VertragswerkId",
                        column: x => x.VertragswerkId,
                        principalSchema: "kalkulation",
                        principalTable: "Vertragswerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VertragswerkDokumente_Vorlagenversionen_VorlagenversionId",
                        column: x => x.VorlagenversionId,
                        principalSchema: "katalog",
                        principalTable: "Vorlagenversionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VertragswerkDokumente_VertragswerkId",
                schema: "kalkulation",
                table: "VertragswerkDokumente",
                column: "VertragswerkId");

            migrationBuilder.CreateIndex(
                name: "IX_VertragswerkDokumente_VorlagenversionId",
                schema: "kalkulation",
                table: "VertragswerkDokumente",
                column: "VorlagenversionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vertragswerke_AngebotId",
                schema: "kalkulation",
                table: "Vertragswerke",
                column: "AngebotId");

            migrationBuilder.CreateIndex(
                name: "IX_Vertragswerke_KundenprojektId_Ausfertigung",
                schema: "kalkulation",
                table: "Vertragswerke",
                columns: new[] { "KundenprojektId", "Ausfertigung" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VertragswerkDateien",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "VertragswerkDokumente",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "Vertragswerke",
                schema: "kalkulation");
        }
    }
}
