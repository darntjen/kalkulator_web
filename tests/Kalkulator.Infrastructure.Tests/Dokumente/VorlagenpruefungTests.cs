using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>Prüfung der Vertragsvorlagen aus SharePoint (#26, Teil B) an kleinen, im Test gebauten Word-Dateien.</summary>
public class VorlagenpruefungTests
{
    private static readonly IReadOnlySet<string> Komponenten = new HashSet<string>(StringComparer.Ordinal) { "S14-BASIS", "S14-SRV", "S60-AE" };

    /// <summary>Jeder Eintrag ist ein Absatz oder eine Tabelle (Zeilen als Zellentexte); Kopfzeile optional.</summary>
    internal static byte[] Dokument(params object[] inhalt) => DokumentMitKopf(null, inhalt);

    internal static byte[] DokumentMitKopf(string? kopfzeile, params object[] inhalt)
    {
        using var strom = new MemoryStream();
        using (var dokument = WordprocessingDocument.Create(strom, WordprocessingDocumentType.Document))
        {
            var haupt = dokument.AddMainDocumentPart();
            var body = new Body();
            foreach (var element in inhalt)
            {
                body.AppendChild<OpenXmlElement>(element switch
                {
                    string text => Absatz(text),
                    Run[] laeufe => new Paragraph(laeufe),
                    string[][] zeilen => new Table(zeilen.Select(z => new TableRow(z.Select(zelle => new TableCell(Absatz(zelle)))))),
                    _ => throw new ArgumentException("unbekannt"),
                });
            }

            haupt.Document = new Document(body);
            if (kopfzeile is not null)
            {
                haupt.AddNewPart<HeaderPart>().Header = new Header(Absatz(kopfzeile));
            }
        }

        return strom.ToArray();
    }

    private static Paragraph Absatz(string text) => new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Vorlagenanalyse Pruefe(byte[] datei, VorlagenRolle rolle = VorlagenRolle.Sonstige) =>
        Vorlagenpruefung.Pruefe(datei, rolle, Komponenten);

    [Fact]
    public void Grundvertrag_mit_allen_Pflichtplatzhaltern_ist_fehlerfrei()
    {
        var analyse = Pruefe(DokumentMitKopf("Vertrag {{vertrag.nummer}}",
            "zwischen Nösse und {{kunde.anschrift}}",
            "Vertragsnummer {{vertrag.nummer}}, Beginn {{vertrag.beginn}}",
            new[]
            {
                new[] { "Code", "Bezeichnung", "Menge", "Preis" },
                new[] { "{{#positionen}}{{position.code}}", "{{position.bezeichnung}}", "{{position.menge}}", "{{position.gesamtpreis}}{{/positionen}}" },
                new[] { "Summe", "", "", "{{summe.monatlich}}" },
            },
            "{{#anlagen}}",
            "{{anlage.code}} – {{anlage.bezeichnung}}",
            "{{/anlagen}}"), VorlagenRolle.Grundvertrag);

        Assert.False(analyse.HatFehler, string.Join("; ", analyse.Hinweise.Select(h => h.Text)));
        Assert.Contains("positionen", analyse.Platzhalter);
        Assert.Contains("anlage.code", analyse.Platzhalter);
        Assert.Empty(analyse.Eingaben);
    }

    [Fact]
    public void Grundvertrag_ohne_Platzhalter_wie_heute_in_SharePoint_meldet_Pflichtfelder_und_Klammern()
    {
        var analyse = Pruefe(Dokument(
            "und [Firma und Anschrift des Kunden] („Auftraggeber“)",
            "mit der Vertragsnummer [Vertragsnummer] (im Folgenden „Vertrag“).",
            "Ort, Datum: _________________________"), VorlagenRolle.Grundvertrag);

        Assert.True(analyse.HatFehler);
        Assert.Contains(analyse.Hinweise, h => h.IstFehler && h.Text.Contains("{{vertrag.nummer}}", StringComparison.Ordinal));
        Assert.Contains(analyse.Hinweise, h => h.IstFehler && h.Text.Contains("{{#positionen}}", StringComparison.Ordinal));
        Assert.Contains(analyse.Hinweise, h => !h.IstFehler && h.Text.Contains("[Vertragsnummer]", StringComparison.Ordinal));
        Assert.Contains(analyse.Hinweise, h => !h.IstFehler && h.Text.StartsWith("1 Ausfüllstelle", StringComparison.Ordinal));
    }

    [Fact]
    public void Leistungsschein_ohne_Platzhalter_ist_fehlerfrei()
    {
        var analyse = Pruefe(Dokument("Leistungsschein S02 — Endpoint Protection", "1. Leistungsgegenstand"));

        Assert.False(analyse.HatFehler);
        Assert.Empty(analyse.Hinweise);
    }

    [Fact]
    public void Eingaben_Auswahl_und_Listen_werden_erkannt()
    {
        var analyse = Pruefe(Dokument(
            "Auftraggeber {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}",
            "{{eingabe.Sicherungsvariante=Cloud-Backup}} Cloud-Backup",
            "{{eingabe.Sicherungsvariante=Objektspeicher}} Objektspeicher",
            "Gewählt: {{eingabe.Sicherungsvariante}}",
            "{{#eingabe.Sicherungsvariante=Objektspeicher}}",
            "Erstes Sicherungsziel {{eingabe.Erstes Sicherungsziel}}",
            "{{/eingabe.Sicherungsvariante=Objektspeicher}}",
            new[]
            {
                new[] { "Servername", "Zweck", "Priorität" },
                new[] { "{{#eingabe.Server}}{{Server.Servername}}", "{{Server.Zweck}}", "{{Server.Priorität}}{{/eingabe.Server}}" },
            },
            "Vertreter nochmals: {{eingabe.Vertreter}}"));

        Assert.False(analyse.HatFehler, string.Join("; ", analyse.Hinweise.Select(h => h.Text)));
        Assert.Equal(
            [
                new EingabeDefinition(EingabeArt.Text, "Vertreter", []),
                new EingabeDefinition(EingabeArt.Auswahl, "Sicherungsvariante", ["Cloud-Backup", "Objektspeicher"]),
                new EingabeDefinition(EingabeArt.Text, "Erstes Sicherungsziel", []),
                new EingabeDefinition(EingabeArt.Liste, "Server", ["Servername", "Zweck", "Priorität"]),
            ],
            analyse.Eingaben,
            new EingabeVergleich());
    }

    [Fact]
    public void Preisfelder_brauchen_eine_Komponente_aus_dem_Katalog()
    {
        var analyse = Pruefe(Dokument((object)new[]
        {
            new[] { "{{#preis.S14-SRV}}Gesicherter Server", "{{preis.S14-SRV.einzelpreis}}", "{{preis.S14-SRV.menge}}", "{{preis.S14-SRV.summe}}{{/preis.S14-SRV}}" },
            new[] { "Unbekannt", "{{preis.S99-X.menge}}", "{{preis.S14-SRV.rabatt}}", "" },
        }));

        Assert.Equal(["S14-SRV"], analyse.Komponenten);
        Assert.Contains(analyse.Hinweise, h => h.IstFehler && h.Text.Contains("„S99-X“", StringComparison.Ordinal));
        Assert.Contains(analyse.Hinweise, h => h.IstFehler && h.Text.Contains("rabatt", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{{kunde.firmenname}}", "Unbekannter Platzhalter")]
    [InlineData("{{position.menge}}", "außerhalb der Liste")]
    [InlineData("{{positionen}}", "ist eine Liste")]
    [InlineData("{{eingabe.Ort.Datum}}", "ohne Punkt")]
    [InlineData("{{eingabe.Variante=}}", "fehlt die Option")]
    public void Falsche_Platzhalter_sind_Fehler(string text, string erwartet)
    {
        var analyse = Pruefe(Dokument($"Text {text} Text"));

        Assert.Contains(analyse.Hinweise, h => h.IstFehler && h.Text.Contains(erwartet, StringComparison.Ordinal));
    }

    [Fact]
    public void Bloecke_muessen_geschlossen_und_richtig_gestellt_sein()
    {
        var offen = Pruefe(Dokument("{{#anlagen}}", "{{anlage.code}}"));
        var falsch = Pruefe(Dokument("{{#anlagen}}", "{{/positionen}}", "{{/anlagen}}"));
        var mitten = Pruefe(Dokument("Text {{#anlagen}} mitten im Satz", "{{/anlagen}}"));
        var gemischt = Pruefe(Dokument("{{eingabe.Server}}", "{{#eingabe.Server}}", "{{Server.Name}}", "{{/eingabe.Server}}"));

        Assert.Contains(offen.Hinweise, h => h.IstFehler && h.Text.Contains("fehlt {{/anlagen}}", StringComparison.Ordinal));
        Assert.Contains(falsch.Hinweise, h => h.IstFehler && h.Text.Contains("passt nicht", StringComparison.Ordinal));
        Assert.Contains(mitten.Hinweise, h => h.IstFehler && h.Text.Contains("allein in einem Absatz", StringComparison.Ordinal));
        Assert.Contains(gemischt.Hinweise, h => h.IstFehler && h.Text.Contains("als Text und als Liste", StringComparison.Ordinal));
    }

    [Fact]
    public void Kaputte_Datei_ist_ein_Fehler()
    {
        var analyse = Pruefe([1, 2, 3, 4]);

        Assert.Contains("kein lesbares Word-Dokument", Assert.Single(analyse.Hinweise).Text, StringComparison.Ordinal);
    }

    /// <summary>Records mit Listen vergleichen die Listen sonst als Referenz.</summary>
    private sealed class EingabeVergleich : IEqualityComparer<EingabeDefinition>
    {
        public bool Equals(EingabeDefinition? x, EingabeDefinition? y) =>
            x is not null && y is not null && x.Art == y.Art && x.Name == y.Name && x.Optionen.SequenceEqual(y.Optionen);

        public int GetHashCode(EingabeDefinition obj) => HashCode.Combine(obj.Art, obj.Name);
    }
}
