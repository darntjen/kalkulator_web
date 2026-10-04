using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>Befüllung der Vertragsvorlagen aus der eingefrorenen Version (#26, Teil C).</summary>
public class VertragsdatenTests
{
    private static readonly DokumentVorlage S01 = new() { Typ = DokumentTyp.Leistungsschein, Code = "S01", Bezeichnung = "S01", Version = "1.0" };
    private static readonly DokumentVorlage S14 = new() { Typ = DokumentTyp.Leistungsschein, Code = "S14", Bezeichnung = "S14", Version = "5.7" };

    private static readonly List<Service> Katalog =
    [
        new() { Code = "S01-STD", Bezeichnung = "Nösse Connect Standard", Typ = ServiceTyp.Connect, Leistungsschein = S01 },
        new() { Code = "S14", Bezeichnung = "Server Backup", Typ = ServiceTyp.Einzelservice, Leistungsschein = S14 },
    ];

    private static VertragsQuelle Quelle()
    {
        var version = new Kalkulationsversion
        {
            Nummer = 2,
            ErstelltVon = "vertrieb",
            Eingabe = new KalkulationsEingabe(),
            Vertragsbeginn = new DateOnly(2027, 1, 1),
            SummeMonatlich = 393m,
            SummeEinmalig = 900m,
            Vertragsangaben = new Vertragsangaben
            {
                Werte = new Dictionary<string, string> { ["Vertreter"] = "Frau Beispiel", ["Sicherungsvariante"] = "Objektspeicher" },
                Listen = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
                {
                    ["Server"] = [new Dictionary<string, string> { ["Servername"] = "SRV-DC01", ["Zweck"] = "Domäne" }, new Dictionary<string, string> { ["Servername"] = "SRV-FS01" }],
                },
            },
        };
        version.Positionen.AddRange(
        [
            new VersionsPosition { Reihenfolge = 1, Code = "S01-STD", Bezeichnung = "Nösse Connect Standard", ServiceCode = "S01-STD", Menge = 1, Einzelpreis = 249m, Betrag = 249m, Abrechnungsart = Abrechnungsart.Monatlich },
            new VersionsPosition { Reihenfolge = 2, Code = "S14-GRUND", Bezeichnung = "Grundpauschale Backup", ServiceCode = "S14", Menge = 1, Einzelpreis = 99m, Betrag = 99m, Abrechnungsart = Abrechnungsart.Monatlich },
            new VersionsPosition { Reihenfolge = 3, Code = "S14-SERVER", Bezeichnung = "Gesicherter Server", ServiceCode = "S14", Menge = 3, Einzelpreis = 15m, Betrag = 45m, Abrechnungsart = Abrechnungsart.Monatlich },
            new VersionsPosition { Reihenfolge = 4, Code = "S01-ONB-XS", Bezeichnung = "Onboarding XS", ServiceCode = "S01-STD", Menge = 1, Einzelpreis = 900m, Betrag = 900m, Abrechnungsart = Abrechnungsart.Einmalig, Herkunft = PositionsHerkunft.Onboarding },
        ]);

        IReadOnlyList<Vertragsdokument> dokumente =
        [
            new(VertragsdokumentArt.Avv, "AVV", "Auftragsverarbeitungsvereinbarung"),
            new(VertragsdokumentArt.Grundvertrag, "Grundvertrag", "Managed-Services-Vertrag (Grundvertrag)"),
            new(VertragsdokumentArt.Connect, "S01", "Nösse Connect Standard"),
            new(VertragsdokumentArt.Leistungsschein, "S14", "Server Backup"),
        ];
        IReadOnlyList<EingabeDefinition> eingaben =
        [
            new(EingabeArt.Text, "Vertreter", []),
            new(EingabeArt.Auswahl, "Sicherungsvariante", ["Cloud-Backup", "Objektspeicher"]),
            new(EingabeArt.Liste, "Server", ["Servername", "Zweck"]),
        ];
        return new VertragsQuelle("MS-A-2026-0001", "MS-A-2026-0001 V2", new DateOnly(2026, 10, 4), version,
            new Kunde { Firma = "Muster Spedition GmbH", Strasse = "Hafenstraße 12", Postleitzahl = "26135", Ort = "Oldenburg" },
            Katalog, dokumente, eingaben);
    }

    private static (List<string> Absaetze, List<List<string>> Tabellen) Lies(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        var body = dokument.MainDocumentPart!.Document!.Body!;
        var absaetze = body.Elements<Paragraph>().Select(p => string.Concat(p.Descendants().Select(d => d is Break ? "\n" : d is Text t ? t.Text : ""))).ToList();
        var tabellen = body.Elements<Table>()
            .Select(t => t.Elements<TableRow>().Select(r => string.Join(" | ", r.Elements<TableCell>().Select(c => c.InnerText))).ToList())
            .ToList();
        return (absaetze, tabellen);
    }

    [Fact]
    public void Grundvertrag_mit_Anschrift_Positionen_und_Anlagen()
    {
        var quelle = Quelle();
        var vorlage = VorlagenpruefungTests.Dokument(
            "zwischen Nösse und {{kunde.anschrift}}",
            "Vertragsnummer {{vertrag.nummer}}, Beginn {{vertrag.beginn}}, Stufe {{vertrag.connectstufe}}",
            (object)new[]
            {
                new[] { "{{#positionen}}{{position.code}}", "{{position.bezeichnung}}", "{{position.menge}}", "{{position.gesamtpreis}} {{position.abrechnung}}{{/positionen}}" },
                new[] { "Gesamt", "", "", "{{summe.monatlich}}" },
                new[] { "{{#einmalig}}{{position.code}}", "{{position.bezeichnung}}", "{{position.menge}}", "{{position.gesamtpreis}} {{position.abrechnung}}{{/einmalig}}" },
            },
            "{{#anlagen}}",
            "{{anlage.code}} — {{anlage.bezeichnung}}",
            "{{/anlagen}}");

        var (absaetze, tabellen) = Lies(Vertragsdaten.Erzeuge(vorlage, Vertragsdaten.Fuer(quelle, quelle.Dokumente[1]), []));

        Assert.Equal("zwischen Nösse und Muster Spedition GmbH\nHafenstraße 12\n26135 Oldenburg", absaetze[0]);
        Assert.Equal("Vertragsnummer MS-A-2026-0001, Beginn 01.01.2027, Stufe Standard", absaetze[1]);
        Assert.Equal(
            [
                "S01 | Nösse Connect Standard | 1 | 249,00 € monatlich",
                "S14 | Grundpauschale Backup | 1 | 99,00 € monatlich",
                "S14 | Gesicherter Server | 3 | 45,00 € monatlich",
                "Gesamt |  |  | 393,00 €",
                "S01 | Onboarding XS | 1 | 900,00 € einmalig",
            ],
            tabellen[0]);
        Assert.Equal(["Anlage AVV — Auftragsverarbeitungsvereinbarung", "S01 — Nösse Connect Standard", "S14 — Server Backup"], absaetze.Skip(2));
    }

    [Fact]
    public void Leistungsschein_mit_Eingaben_Ankreuzfeldern_Liste_und_Preisen()
    {
        var quelle = Quelle();
        var vorlage = VorlagenpruefungTests.Dokument(
            "{{schein.code}}: {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}",
            "{{eingabe.Sicherungsvariante=Cloud-Backup}} Cloud-Backup {{eingabe.Sicherungsvariante=Objektspeicher}} Objektspeicher ({{eingabe.Sicherungsvariante}})",
            "{{#eingabe.Sicherungsvariante=Cloud-Backup}}",
            "Nur bei Cloud-Backup",
            "{{/eingabe.Sicherungsvariante=Cloud-Backup}}",
            (object)new[]
            {
                new[] { "{{#eingabe.Server}}{{Server.Servername}}", "{{Server.Zweck}}{{/eingabe.Server}}" },
            },
            (object)new[]
            {
                new[] { "{{#preis.S14-SERVER}}Server", "{{preis.S14-SERVER.menge}}", "{{preis.S14-SERVER.summe}}{{/preis.S14-SERVER}}" },
                new[] { "{{#preis.S14-PAKET}}Cloud-Paket", "{{preis.S14-PAKET.menge}}", "{{preis.S14-PAKET.summe}}{{/preis.S14-PAKET}}" },
                new[] { "Summe", "", "{{schein.summe}}" },
            });

        var (absaetze, tabellen) = Lies(Vertragsdaten.Erzeuge(vorlage, Vertragsdaten.Fuer(quelle, quelle.Dokumente[3]), ["S14-SERVER", "S14-PAKET"]));

        Assert.Equal("S14: Muster Spedition GmbH, Vertreter Frau Beispiel", absaetze[0]);
        Assert.Equal("☐ Cloud-Backup ☒ Objektspeicher (Objektspeicher)", absaetze[1]);
        Assert.DoesNotContain(absaetze, a => a.Contains("Nur bei Cloud-Backup", StringComparison.Ordinal));
        Assert.Equal(["SRV-DC01 | Domäne", "SRV-FS01 | "], tabellen[0]);
        Assert.Equal(["Server | 3 | 45,00 €", "Summe |  | 144,00 €"], tabellen[1]);
    }
}
