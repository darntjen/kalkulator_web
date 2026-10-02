using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalkulator.Documents;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>Die Platzhalter-Engine an kleinen, im Test gebauten Word-Dateien.</summary>
public class WordVorlageTests
{
    /// <summary>Baut ein Dokument; jeder Eintrag ist ein Absatz oder eine Tabelle (Zeilen als Zellentexte).</summary>
    private static byte[] Dokument(string kopfzeile, params object[] inhalt)
    {
        using var strom = new MemoryStream();
        using (var dokument = WordprocessingDocument.Create(strom, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var haupt = dokument.AddMainDocumentPart();
            var body = new Body();
            foreach (var element in inhalt)
            {
                body.AppendChild<DocumentFormat.OpenXml.OpenXmlElement>(element switch
                {
                    string text => Absatz(text),
                    Run[] laeufe => new Paragraph(laeufe),
                    string[][] zeilen => new Table(zeilen.Select(z => new TableRow(z.Select(zelle => new TableCell(Absatz(zelle)))))),
                    _ => throw new ArgumentException("unbekannt"),
                });
            }

            haupt.Document = new Document(body);
            var kopf = haupt.AddNewPart<HeaderPart>();
            kopf.Header = new Header(Absatz(kopfzeile));
        }

        return strom.ToArray();
    }

    private static Paragraph Absatz(string text) => new(new Run(new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));

    private static (List<string> Absaetze, string Kopf, List<List<string>> Tabellen) Lies(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        var body = dokument.MainDocumentPart!.Document!.Body!;
        var absaetze = body.Elements<Paragraph>().Select(p => string.Concat(p.Descendants().Select(d => d is Break ? "\n" : d is Text t ? t.Text : ""))).ToList();
        var tabellen = body.Elements<Table>()
            .Select(t => t.Elements<TableRow>().Select(r => string.Join(" | ", r.Elements<TableCell>().Select(c => c.InnerText))).ToList())
            .ToList();
        return (absaetze, dokument.MainDocumentPart.HeaderParts.Single().Header!.InnerText, tabellen);
    }

    [Fact]
    public void Platzhalter_im_Text_in_Kopfzeile_und_ueber_mehrere_Laeufe_werden_ersetzt()
    {
        var vorlage = Dokument("Angebot {{angebot.nummer}}",
            "Sehr geehrte {{kunde.anrede}},",
            new[] { new Run(new Text("Nummer {{ange")), new Run(new Text("bot.nummer}} vom ")), new Run(new Text("{{angebot.datum}}")) });

        var (absaetze, kopf, _) = Lies(WordVorlage.Befuellen(vorlage, new Datensatz
        {
            ["angebot.nummer"] = "MS-A-2026-0001",
            ["angebot.datum"] = "02.10.2026",
            ["kunde.anrede"] = "Frau Beispiel",
        }));

        Assert.Equal("Angebot MS-A-2026-0001", kopf);
        Assert.Equal(["Sehr geehrte Frau Beispiel,", "Nummer MS-A-2026-0001 vom 02.10.2026"], absaetze);
    }

    [Fact]
    public void Bloecke_werden_je_Eintrag_wiederholt_auch_verschachtelt()
    {
        var vorlage = Dokument("",
            "Vorher",
            "{{#leistungen}}",
            "{{titel}} für {{kunde}}",
            "{{#enthalten}}",
            "- {{name}}",
            "{{/enthalten}}",
            "{{/leistungen}}",
            "Nachher");

        var daten = new Datensatz
        {
            ["kunde"] = "Muster GmbH",
            ["leistungen"] = new List<Datensatz>
            {
                new() { ["titel"] = "B01", ["enthalten"] = new List<Datensatz> { new() { ["name"] = "S02" }, new() { ["name"] = "S03" } } },
                new() { ["titel"] = "S31", ["enthalten"] = new List<Datensatz>() },
            },
        };

        var (absaetze, _, _) = Lies(WordVorlage.Befuellen(vorlage, daten));

        Assert.Equal(["Vorher", "B01 für Muster GmbH", "- S02", "- S03", "S31 für Muster GmbH", "Nachher"], absaetze);
    }

    [Fact]
    public void Bedingte_Bloecke_erscheinen_nur_bei_Inhalt()
    {
        var vorlage = Dokument("", "{{#freitext}}", "{{freitext}}", "{{/freitext}}", "{{#vergleich}}", "Bisher {{bisher}}", "{{/vergleich}}", "Ende");

        var mit = Lies(WordVorlage.Befuellen(vorlage, new Datensatz { ["freitext"] = "Zeile 1\nZeile 2", ["vergleich"] = true, ["bisher"] = "900,00 €" })).Absaetze;
        var ohne = Lies(WordVorlage.Befuellen(vorlage, new Datensatz { ["freitext"] = " ", ["vergleich"] = false, ["bisher"] = "" })).Absaetze;

        Assert.Equal(["Zeile 1\nZeile 2", "Bisher 900,00 €", "Ende"], mit);
        Assert.Equal(["Ende"], ohne);
    }

    [Fact]
    public void Tabellenzeilen_werden_je_Eintrag_wiederholt()
    {
        // Als einzelnes Objekt übergeben, sonst würde das Feld selbst zur params-Liste.
        var vorlage = Dokument("", (object)new[]
        {
            new[] { "Nr.", "Leistung", "Betrag" },
            new[] { "{{#positionen}}{{code}}", "{{bezeichnung}}", "{{betrag}}{{/positionen}}" },
            new[] { "", "Summe", "{{summe}}" },
        });

        var daten = new Datensatz
        {
            ["summe"] = "548,90 €",
            ["positionen"] = new List<Datensatz>
            {
                new() { ["code"] = "S01", ["bezeichnung"] = "Nösse Connect Standard", ["betrag"] = "249,00 €" },
                new() { ["code"] = "B01", ["bezeichnung"] = "User as a Service Standard", ["betrag"] = "299,90 €" },
            },
        };

        var tabelle = Lies(WordVorlage.Befuellen(vorlage, daten)).Tabellen.Single();

        Assert.Equal(["Nr. | Leistung | Betrag", "S01 | Nösse Connect Standard | 249,00 €", "B01 | User as a Service Standard | 299,90 €", " | Summe | 548,90 €"], tabelle);
    }

    [Fact]
    public void Unbekannte_Platzhalter_und_offene_Bloecke_fallen_auf()
    {
        var unbekannt = Assert.Throws<VorlagenFehler>(() => WordVorlage.Befuellen(Dokument("{{gibt.es.nicht}}", "{{auch.nicht}}"), new Datensatz()));
        Assert.Contains("gibt.es.nicht", unbekannt.Message, StringComparison.Ordinal);
        Assert.Contains("auch.nicht", unbekannt.Message, StringComparison.Ordinal);

        Assert.Throws<VorlagenFehler>(() => WordVorlage.Befuellen(Dokument("", "{{#liste}}", "x"), new Datensatz { ["liste"] = new List<Datensatz>() }));
    }
}
