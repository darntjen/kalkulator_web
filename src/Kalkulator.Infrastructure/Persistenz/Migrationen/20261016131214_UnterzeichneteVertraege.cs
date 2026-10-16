using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class UnterzeichneteVertraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaperlessStatus",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusGeprueftAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnterzeichnetAm",
                schema: "kalkulation",
                table: "Vertragswerke",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VertragswerkUnterschriften",
                schema: "kalkulation",
                columns: table => new
                {
                    VertragswerkId = table.Column<int>(type: "int", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Pruefprotokoll = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    AbgelegtAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VertragswerkUnterschriften", x => x.VertragswerkId);
                    table.ForeignKey(
                        name: "FK_VertragswerkUnterschriften_Vertragswerke_VertragswerkId",
                        column: x => x.VertragswerkId,
                        principalSchema: "kalkulation",
                        principalTable: "Vertragswerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VertragswerkUnterschriften",
                schema: "kalkulation");

            migrationBuilder.DropColumn(
                name: "PaperlessStatus",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "StatusGeprueftAm",
                schema: "kalkulation",
                table: "Vertragswerke");

            migrationBuilder.DropColumn(
                name: "UnterzeichnetAm",
                schema: "kalkulation",
                table: "Vertragswerke");
        }
    }
}
