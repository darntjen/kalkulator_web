using System.IO.Compression;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Kalkulator.Documents.Vertrag;

/// <summary>Ein fertiges Dokument des Vertragswerks: Nummer in der Rangfolge, Bezeichnung, Fassung der Vorlage und PDF.</summary>
public sealed record MappenDokument(int Nummer, string Code, string Bezeichnung, string Fassung, byte[] Pdf);

/// <summary>Deckblatt, Gesamt-PDF und ZIP des Vertragswerks (#26, Teil C).</summary>
public static partial class Vertragsmappe
{
    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex Unzulaessig();

    /// <summary>Werte für das Deckblatt (templates/vertrag/Deckblatt.docx): gemeinsame Vertragsdaten und das Verzeichnis.</summary>
    public static Datensatz Deckblatt(Datensatz gemeinsam, IEnumerable<MappenDokument> dokumente)
    {
        var daten = new Datensatz();
        foreach (var (schluessel, wert) in gemeinsam)
        {
            daten[schluessel] = wert;
        }

        daten["dokumente"] = dokumente.Select(d => new Datensatz
        {
            ["dokument.nummer"] = d.Nummer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["dokument.code"] = d.Code,
            ["dokument.bezeichnung"] = d.Bezeichnung,
            ["dokument.fassung"] = d.Fassung,
        }).ToList();
        return daten;
    }

    /// <summary>Hängt die PDFs in der gegebenen Reihenfolge aneinander.</summary>
    public static byte[] Zusammenfuehren(IEnumerable<byte[]> pdfs)
    {
        using var gesamt = new PdfDocument();
        foreach (var pdf in pdfs)
        {
            using var strom = new MemoryStream(pdf, writable: false);
            using var teil = PdfReader.Open(strom, PdfDocumentOpenMode.Import);
            foreach (var seite in teil.Pages)
            {
                gesamt.AddPage(seite);
            }
        }

        using var ausgabe = new MemoryStream();
        gesamt.Save(ausgabe);
        return ausgabe.ToArray();
    }

    /// <summary>ZIP mit allen Einzel-PDFs (nummeriert in Rangfolge) und der Gesamtdatei.</summary>
    public static byte[] Zip(IEnumerable<MappenDokument> dokumente, string gesamtName, byte[] gesamt)
    {
        using var strom = new MemoryStream();
        using (var zip = new ZipArchive(strom, ZipArchiveMode.Create, leaveOpen: true))
        {
            Eintrag(zip, gesamtName, gesamt);
            foreach (var d in dokumente)
            {
                Eintrag(zip, $"Einzeldokumente/{d.Nummer:00} {Dateiteil(d.Bezeichnung)}.pdf", d.Pdf);
            }
        }

        return strom.ToArray();
    }

    /// <summary>Für Dateinamen geeignet: Buchstaben und Ziffern, sonst Bindestriche.</summary>
    public static string Dateiteil(string text) => Unzulaessig().Replace(text, "-").Trim('-');

    private static void Eintrag(ZipArchive zip, string name, byte[] inhalt)
    {
        var eintrag = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var ziel = eintrag.Open();
        ziel.Write(inhalt);
    }
}
