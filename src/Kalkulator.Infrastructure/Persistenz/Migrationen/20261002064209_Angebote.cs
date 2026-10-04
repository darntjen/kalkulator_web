using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Angebote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Angebotsnummer",
                schema: "kalkulation",
                table: "Kalkulationen",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Angebote",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KalkulationsversionId = table.Column<int>(type: "int", nullable: false),
                    Nummer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: false),
                    Freitext = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Dateiname = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Vorlage = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VersendetAm = table.Column<DateOnly>(type: "date", nullable: true),
                    VersendetVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Angebote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Angebote_Kalkulationsversionen_KalkulationsversionId",
                        column: x => x.KalkulationsversionId,
                        principalSchema: "kalkulation",
                        principalTable: "Kalkulationsversionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Nummernkreise",
                schema: "projekte",
                columns: table => new
                {
                    Kreis = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Jahr = table.Column<int>(type: "int", nullable: false),
                    LetzteNummer = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nummernkreise", x => new { x.Kreis, x.Jahr });
                });

            migrationBuilder.CreateTable(
                name: "AngebotsDateien",
                schema: "kalkulation",
                columns: table => new
                {
                    AngebotId = table.Column<int>(type: "int", nullable: false),
                    Inhalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AngebotsDateien", x => x.AngebotId);
                    table.ForeignKey(
                        name: "FK_AngebotsDateien_Angebote_AngebotId",
                        column: x => x.AngebotId,
                        principalSchema: "kalkulation",
                        principalTable: "Angebote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kalkulationen_Angebotsnummer",
                schema: "kalkulation",
                table: "Kalkulationen",
                column: "Angebotsnummer",
                unique: true,
                filter: "[Angebotsnummer] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Angebote_KalkulationsversionId",
                schema: "kalkulation",
                table: "Angebote",
                column: "KalkulationsversionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Angebote_Nummer",
                schema: "kalkulation",
                table: "Angebote",
                column: "Nummer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AngebotsDateien",
                schema: "kalkulation");

            migrationBuilder.DropTable(
                name: "Nummernkreise",
                schema: "projekte");

            migrationBuilder.DropTable(
                name: "Angebote",
                schema: "kalkulation");

            migrationBuilder.DropIndex(
                name: "IX_Kalkulationen_Angebotsnummer",
                schema: "kalkulation",
                table: "Kalkulationen");

            migrationBuilder.DropColumn(
                name: "Angebotsnummer",
                schema: "kalkulation",
                table: "Kalkulationen");
        }
    }
}
