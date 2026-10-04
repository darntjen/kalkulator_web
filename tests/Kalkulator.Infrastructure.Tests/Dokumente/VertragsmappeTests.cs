using System.IO.Compression;
using Kalkulator.Documents;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Infrastructure.Vorlagen;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>Gesamt-PDF, ZIP und Deckblatt des Vertragswerks (#26, Teil C).</summary>
public class VertragsmappeTests
{
    internal static byte[] LeeresPdf(int seiten)
    {
        using var dokument = new PdfDocument();
        for (var i = 0; i < seiten; i++)
        {
            dokument.AddPage();
        }

        using var strom = new MemoryStream();
        dokument.Save(strom);
        return strom.ToArray();
    }

    private static int Seiten(byte[] pdf)
    {
        using var strom = new MemoryStream(pdf);
        using var dokument = PdfReader.Open(strom, PdfDocumentOpenMode.Import);
        return dokument.PageCount;
    }

    [Fact]
    public void Gesamt_PDF_haengt_alle_Seiten_in_Reihenfolge_an()
    {
        Assert.Equal(6, Seiten(Vertragsmappe.Zusammenfuehren([LeeresPdf(1), LeeresPdf(3), LeeresPdf(2)])));
    }

    [Fact]
    public void Zip_enthaelt_Gesamtdatei_und_nummerierte_Anlagen()
    {
        MappenDokument[] dokumente =
        [
            new(1, "AVV", "Auftragsverarbeitungsvereinbarung", "V1", LeeresPdf(1)),
            new(2, "GRUNDVERTRAG", "Managed-Services-Vertrag (Grundvertrag)", "V2", LeeresPdf(2)),
            new(3, "S14", "Server Backup / Sonderfall", "V1", LeeresPdf(1)),
        ];

        var zip = Vertragsmappe.Zip(dokumente, "Vertrag_MS-A-2026-0001.pdf", LeeresPdf(5));

        using var archiv = new ZipArchive(new MemoryStream(zip));
        Assert.Equal(
            ["Vertrag_MS-A-2026-0001.pdf", "Einzeldokumente/01 Auftragsverarbeitungsvereinbarung.pdf", "Einzeldokumente/02 Managed-Services-Vertrag-Grundvertrag.pdf", "Einzeldokumente/03 Server-Backup-Sonderfall.pdf"],
            archiv.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void Deckblatt_enthaelt_Vertragsdaten_und_Verzeichnis()
    {
        var vorlage = File.ReadAllBytes(Path.Combine(AngebotsdokumentTests.Vorlagenordner(), "..", "vertrag", "Deckblatt.docx"));
        var gemeinsam = new Datensatz
        {
            ["kunde.anschrift"] = "Muster Spedition GmbH\nHafenstraße 12\n26135 Oldenburg",
            ["kunde.firma"] = "Muster Spedition GmbH",
            ["vertrag.nummer"] = "MS-A-2026-0001",
            ["vertrag.angebot"] = "MS-A-2026-0001 V2",
            ["vertrag.datum"] = "04.10.2026",
        };

        var datei = WordVorlage.Befuellen(vorlage, Vertragsmappe.Deckblatt(gemeinsam, [new(1, "AVV", "Auftragsverarbeitungsvereinbarung", "V1", [])]));

        using var strom = new MemoryStream(datei);
        using var dokument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(strom, false);
        var text = dokument.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("MS-A-2026-0001", text, StringComparison.Ordinal);
        Assert.Contains("1Auftragsverarbeitungsvereinbarung", text, StringComparison.Ordinal);
        Assert.Contains(dokument.MainDocumentPart.HeaderParts, h => h.Header!.InnerText.Contains("Vertrag MS-A-2026-0001 · Muster Spedition GmbH", StringComparison.Ordinal));
    }

    [LibreOfficeFact]
    public async Task LibreOffice_wandelt_Word_in_PDF()
    {
        var pdf = await new LibreOfficeWandler("soffice").InPdfAsync(VorlagenpruefungTests.Dokument("Hallo Vertrag"), CancellationToken.None);

        Assert.True(Seiten(pdf) >= 1);
    }

    /// <summary>Läuft nur, wo LibreOffice installiert ist (lokal); sonst als übersprungen gemeldet.</summary>
    private sealed class LibreOfficeFactAttribute : FactAttribute
    {
        public LibreOfficeFactAttribute()
        {
            var pfade = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            if (!pfade.Any(p => File.Exists(Path.Combine(p, "soffice")) || File.Exists(Path.Combine(p, "soffice.exe"))))
            {
                Skip = "LibreOffice (soffice) ist nicht installiert.";
            }
        }
    }
}
