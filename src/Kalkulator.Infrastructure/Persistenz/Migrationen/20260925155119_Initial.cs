using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalkulator.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "protokoll");

            migrationBuilder.EnsureSchema(
                name: "katalog");

            migrationBuilder.EnsureSchema(
                name: "intern");

            migrationBuilder.EnsureSchema(
                name: "preise");

            migrationBuilder.CreateTable(
                name: "Aenderungsprotokoll",
                schema: "protokoll",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Zeitpunkt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Benutzer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Entitaet = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Schluessel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aktion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Aenderungen = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aenderungsprotokoll", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DokumentVorlagen",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Typ = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Dateiname = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokumentVorlagen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategorien",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategorien", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Preislisten",
                schema: "preise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GueltigAb = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FreigegebenAm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FreigegebenVon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VorgaengerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preislisten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Preislisten_Preislisten_VorgaengerId",
                        column: x => x.VorgaengerId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceNummer = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kurzbeschreibung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Typ = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Vertriebsstatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    KategorieId = table.Column<int>(type: "int", nullable: false),
                    LeistungsscheinId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_DokumentVorlagen_LeistungsscheinId",
                        column: x => x.LeistungsscheinId,
                        principalSchema: "katalog",
                        principalTable: "DokumentVorlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Services_Kategorien_KategorieId",
                        column: x => x.KategorieId,
                        principalSchema: "katalog",
                        principalTable: "Kategorien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parameter",
                schema: "preise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    Schluessel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Wert = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parameter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parameter_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BundleBestandteile",
                schema: "katalog",
                columns: table => new
                {
                    BundleId = table.Column<int>(type: "int", nullable: false),
                    BestandteilId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleBestandteile", x => new { x.BundleId, x.BestandteilId });
                    table.CheckConstraint("CK_BundleBestandteile_NichtSichSelbst", "[BundleId] <> [BestandteilId]");
                    table.ForeignKey(
                        name: "FK_BundleBestandteile_Services_BestandteilId",
                        column: x => x.BestandteilId,
                        principalSchema: "katalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BundleBestandteile_Services_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "katalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Preiskomponenten",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Einheit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Abrechnungsart = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StaffelBezug = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preiskomponenten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Preiskomponenten_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "katalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Regeln",
                schema: "katalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Meldung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regeln_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "katalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EkPositionen",
                schema: "intern",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    PreiskomponenteId = table.Column<int>(type: "int", nullable: false),
                    EkLizenz = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AufwandMinuten = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    BetriebFix = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Overhead = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AusBestandteilen = table.Column<bool>(type: "bit", nullable: false),
                    KostenKorrektur = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Anmerkung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkPositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkPositionen_Preiskomponenten_PreiskomponenteId",
                        column: x => x.PreiskomponenteId,
                        principalSchema: "katalog",
                        principalTable: "Preiskomponenten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkPositionen_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Preise",
                schema: "preise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    PreiskomponenteId = table.Column<int>(type: "int", nullable: false),
                    VkNetto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Preise_Preiskomponenten_PreiskomponenteId",
                        column: x => x.PreiskomponenteId,
                        principalSchema: "katalog",
                        principalTable: "Preiskomponenten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Preise_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Preisstaffeln",
                schema: "preise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreislisteId = table.Column<int>(type: "int", nullable: false),
                    PreiskomponenteId = table.Column<int>(type: "int", nullable: false),
                    AbMenge = table.Column<int>(type: "int", nullable: false),
                    VkNetto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preisstaffeln", x => x.Id);
                    table.CheckConstraint("CK_Preisstaffeln_AbMenge", "[AbMenge] >= 0");
                    table.ForeignKey(
                        name: "FK_Preisstaffeln_Preiskomponenten_PreiskomponenteId",
                        column: x => x.PreiskomponenteId,
                        principalSchema: "katalog",
                        principalTable: "Preiskomponenten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Preisstaffeln_Preislisten_PreislisteId",
                        column: x => x.PreislisteId,
                        principalSchema: "preise",
                        principalTable: "Preislisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegelZiele",
                schema: "katalog",
                columns: table => new
                {
                    RegelId = table.Column<int>(type: "int", nullable: false),
                    ZielServiceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegelZiele", x => new { x.RegelId, x.ZielServiceId });
                    table.ForeignKey(
                        name: "FK_RegelZiele_Regeln_RegelId",
                        column: x => x.RegelId,
                        principalSchema: "katalog",
                        principalTable: "Regeln",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegelZiele_Services_ZielServiceId",
                        column: x => x.ZielServiceId,
                        principalSchema: "katalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungsprotokoll_Entitaet_Schluessel",
                schema: "protokoll",
                table: "Aenderungsprotokoll",
                columns: new[] { "Entitaet", "Schluessel" });

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungsprotokoll_Zeitpunkt",
                schema: "protokoll",
                table: "Aenderungsprotokoll",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_BundleBestandteile_BestandteilId",
                schema: "katalog",
                table: "BundleBestandteile",
                column: "BestandteilId");

            migrationBuilder.CreateIndex(
                name: "IX_DokumentVorlagen_Code_Version",
                schema: "katalog",
                table: "DokumentVorlagen",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkPositionen_PreiskomponenteId",
                schema: "intern",
                table: "EkPositionen",
                column: "PreiskomponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_EkPositionen_PreislisteId_PreiskomponenteId",
                schema: "intern",
                table: "EkPositionen",
                columns: new[] { "PreislisteId", "PreiskomponenteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kategorien_Name",
                schema: "katalog",
                table: "Kategorien",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parameter_PreislisteId_Schluessel",
                schema: "preise",
                table: "Parameter",
                columns: new[] { "PreislisteId", "Schluessel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preise_PreiskomponenteId",
                schema: "preise",
                table: "Preise",
                column: "PreiskomponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Preise_PreislisteId_PreiskomponenteId",
                schema: "preise",
                table: "Preise",
                columns: new[] { "PreislisteId", "PreiskomponenteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preiskomponenten_Code",
                schema: "katalog",
                table: "Preiskomponenten",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preiskomponenten_ServiceId",
                schema: "katalog",
                table: "Preiskomponenten",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Preislisten_Bezeichnung",
                schema: "preise",
                table: "Preislisten",
                column: "Bezeichnung",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preislisten_VorgaengerId",
                schema: "preise",
                table: "Preislisten",
                column: "VorgaengerId");

            migrationBuilder.CreateIndex(
                name: "IX_Preisstaffeln_PreiskomponenteId",
                schema: "preise",
                table: "Preisstaffeln",
                column: "PreiskomponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Preisstaffeln_PreislisteId_PreiskomponenteId_AbMenge",
                schema: "preise",
                table: "Preisstaffeln",
                columns: new[] { "PreislisteId", "PreiskomponenteId", "AbMenge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regeln_ServiceId",
                schema: "katalog",
                table: "Regeln",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RegelZiele_ZielServiceId",
                schema: "katalog",
                table: "RegelZiele",
                column: "ZielServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Code",
                schema: "katalog",
                table: "Services",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_KategorieId",
                schema: "katalog",
                table: "Services",
                column: "KategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_LeistungsscheinId",
                schema: "katalog",
                table: "Services",
                column: "LeistungsscheinId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aenderungsprotokoll",
                schema: "protokoll");

            migrationBuilder.DropTable(
                name: "BundleBestandteile",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "EkPositionen",
                schema: "intern");

            migrationBuilder.DropTable(
                name: "Parameter",
                schema: "preise");

            migrationBuilder.DropTable(
                name: "Preise",
                schema: "preise");

            migrationBuilder.DropTable(
                name: "Preisstaffeln",
                schema: "preise");

            migrationBuilder.DropTable(
                name: "RegelZiele",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "Preiskomponenten",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "Preislisten",
                schema: "preise");

            migrationBuilder.DropTable(
                name: "Regeln",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "DokumentVorlagen",
                schema: "katalog");

            migrationBuilder.DropTable(
                name: "Kategorien",
                schema: "katalog");
        }
    }
}
