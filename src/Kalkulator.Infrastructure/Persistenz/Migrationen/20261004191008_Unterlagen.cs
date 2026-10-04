using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Unterlagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kanalordner",
                schema: "projekte",
                table: "Kundenprojekte",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KanalordnerId",
                schema: "projekte",
                table: "Kundenprojekte",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Unterlagen",
                schema: "projekte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KundenprojektId = table.Column<int>(type: "int", nullable: false),
                    Art = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Dateiname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Inhaltstyp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Groesse = table.Column<long>(type: "bigint", nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HochgeladenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HochgeladenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unterlagen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Unterlagen_Kundenprojekte_KundenprojektId",
                        column: x => x.KundenprojektId,
                        principalSchema: "projekte",
                        principalTable: "Kundenprojekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnterlagenDateien",
                schema: "projekte",
                columns: table => new
                {
                    UnterlageId = table.Column<int>(type: "int", nullable: false),
                    Inhalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnterlagenDateien", x => x.UnterlageId);
                    table.ForeignKey(
                        name: "FK_UnterlagenDateien_Unterlagen_UnterlageId",
                        column: x => x.UnterlageId,
                        principalSchema: "projekte",
                        principalTable: "Unterlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Unterlagen_KundenprojektId",
                schema: "projekte",
                table: "Unterlagen",
                column: "KundenprojektId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnterlagenDateien",
                schema: "projekte");

            migrationBuilder.DropTable(
                name: "Unterlagen",
                schema: "projekte");

            migrationBuilder.DropColumn(
                name: "Kanalordner",
                schema: "projekte",
                table: "Kundenprojekte");

            migrationBuilder.DropColumn(
                name: "KanalordnerId",
                schema: "projekte",
                table: "Kundenprojekte");
        }
    }
}
