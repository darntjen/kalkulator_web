using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalkulator.Documents;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Infrastructure.Vorlagen;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>Unterschriftsfelder: unsichtbare Marke in Word und ihre Lage im PDF (#26, Teil D).</summary>
public partial class UnterschriftsfelderTests
{
    [GeneratedRegex("PLSIG[0-9A-F]+Z")]
    private static partial Regex Marken();

    /// <summary>A4-Seiten mit Texten an festen Stellen (Punkt, vom unteren Rand); je Seite eine Liste.</summary>
    internal static byte[] PdfMitText(params (string Text, double X, double Y)[][] seiten)
    {
        var builder = new PdfDocumentBuilder();
        var schrift = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var texte in seiten)
        {
            var seite = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            foreach (var (text, x, y) in texte)
            {
                seite.AddText(text, 10, new PdfPoint(x, y), schrift);
            }
        }

        return builder.Build();
    }

    /// <summary>PDF, das die Marken eines Word-Dokuments untereinander enthält; Ersatz für die echte Umwandlung.</summary>
    internal static byte[] PdfMitMarken(byte[] docx)
    {
        using var strom = new MemoryStream(docx);
        using var dokument = WordprocessingDocument.Open(strom, false);
        var marken = Marken().Matches(dokument.MainDocumentPart!.Document!.Body!.InnerText).Select(m => m.Value).ToList();
        return PdfMitText([.. marken.Select((m, i) => (m, 72d, 700d - (i * 100)))]);
    }

    [Fact]
    public void Marke_kodiert_die_Rolle_auch_mit_Umlauten_und_wird_im_PDF_gefunden()
    {
        var kunde = Unterschriftsfelder.Marke("Kunde");
        var gf = Unterschriftsfelder.Marke("Geschäftsführung Nösse");
        Assert.Equal("PLSIG4B756E6465Z", kunde);

        var pdf = PdfMitText(
            [("Ort, Datum", 72, 300), ("Unterschrift " + kunde, 72, 200)],
            [(gf, 320.5, 150)]);

        var felder = Unterschriftsfelder.Finde(pdf);

        Assert.Equal(2, felder.Count);
        Assert.Equal("Kunde", felder[0].Rolle);
        Assert.Equal(1, felder[0].Seite);
        Assert.Equal(200, felder[0].Grundlinie, 1);
        Assert.True(felder[0].X > 72, "Die Marke steht hinter „Unterschrift “.");
        Assert.Equal(("Geschäftsführung Nösse", 2, 320.5, 150d), (felder[1].Rolle, felder[1].Seite, Math.Round(felder[1].X, 1), Math.Round(felder[1].Grundlinie, 1)));
        Assert.True(felder[1].SeitenHoehe > 840 && felder[1].SeitenBreite > 590);
    }

    [Fact]
    public void Ohne_Marken_keine_Felder()
    {
        Assert.Empty(Unterschriftsfelder.Finde(PdfMitText([("Vertrag ohne Unterschriftsfeld PLSIGZZ", 72, 700)])));
    }

    [Fact]
    public void Marke_steht_unsichtbar_im_eigenen_Lauf_und_der_Text_drumherum_bleibt()
    {
        var vorlage = VorlagenpruefungTests.Dokument(
            (object)new[] { new Run(new RunProperties(new Bold()), new Text("Für den Kunden: {{unterschrift.Kunde}} am {{vertrag.datum}}") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }, new TabChar(), new Text("Ende")) });

        var ergebnis = WordVorlage.Befuellen(vorlage, new Datensatz
        {
            ["unterschrift.Kunde"] = new UnsichtbareMarke(Unterschriftsfelder.Marke("Kunde")),
            ["vertrag.datum"] = "04.10.2026",
        });

        using var strom = new MemoryStream(ergebnis);
        using var dokument = WordprocessingDocument.Open(strom, false);
        var laeufe = dokument.MainDocumentPart!.Document!.Body!.Descendants<Run>().ToList();
        Assert.Equal(["Für den Kunden: ", "PLSIG4B756E6465Z", " am 04.10.2026Ende"], laeufe.Select(l => l.InnerText));
        Assert.All(laeufe, l => Assert.NotNull(l.RunProperties?.Bold));
        Assert.Equal(("FFFFFF", "2"), (laeufe[1].RunProperties!.Color!.Val!.Value, laeufe[1].RunProperties!.FontSize!.Val!.Value));
        Assert.Null(laeufe[0].RunProperties!.Color);
        Assert.Single(laeufe[2].Elements<TabChar>());
    }

    [VertragsmappeTests.LibreOfficeFact]
    public async Task Marke_uebersteht_die_PDF_Umwandlung_mit_LibreOffice()
    {
        var vorlage = VorlagenpruefungTests.Dokument(
            "Vertrag mit {{kunde.firma}}",
            "Oldenburg, den ____________",
            "{{unterschrift.Kunde}}______________________________",
            "Auftraggeber",
            "{{unterschrift.Nösse}}______________________________",
            "Auftragnehmer");
        var docx = Vertragsdaten.Erzeuge(vorlage, new Datensatz { ["kunde.firma"] = "Muster Spedition GmbH" }, [], ["Kunde", "Nösse"]);

        var pdf = await new LibreOfficeWandler("soffice").InPdfAsync(docx, CancellationToken.None);
        var felder = Unterschriftsfelder.Finde(pdf);

        Assert.Equal(["Kunde", "Nösse"], felder.Select(f => f.Rolle));
        Assert.All(felder, f => Assert.Equal(1, f.Seite));
        Assert.True(felder[0].Grundlinie > felder[1].Grundlinie, "Kunde steht über Nösse.");

        // Sichtbar bleibt nur der Vertragstext; die Marke ist weiß und 1 pt groß.
        using var dokument = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var marke = dokument.GetPage(1).Letters.First(l => l.Value == "P" && l.PointSize < 2);
        Assert.True(marke.PointSize < 2);
    }
}
