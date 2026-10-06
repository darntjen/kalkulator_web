using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Vertragsfreigaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vertragsfreigaben",
                schema: "kalkulation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VertragswerkId = table.Column<int>(type: "int", nullable: false),
                    Art = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Erteilt = table.Column<bool>(type: "bit", nullable: false),
                    Begruendung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Von = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Am = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vertragsfreigaben", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vertragsfreigaben_Vertragswerke_VertragswerkId",
                        column: x => x.VertragswerkId,
                        principalSchema: "kalkulation",
                        principalTable: "Vertragswerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vertragsfreigaben_VertragswerkId_Art",
                schema: "kalkulation",
                table: "Vertragsfreigaben",
                columns: new[] { "VertragswerkId", "Art" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vertragsfreigaben",
                schema: "kalkulation");
        }
    }
}
