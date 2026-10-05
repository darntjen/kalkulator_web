using Kalkulator.Documents.Vertrag;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Testlauf gegen die echte Paperless-API (Frage 12.5), ohne Vertragsvorlagen und PDF-Umwandlung: Ein Muster-PDF mit
/// erfundenem Inhalt und je einem Unterschriftsfeld für „Kunde“ und „Nösse“ geht zweimal als Entwurf an Paperless,
/// einmal mit Koordinaten ab oberem Seitenrand (A) und einmal ab unterem (B). Ein grauer Rahmen im PDF zeigt, wo das
/// Feld liegen soll; in Paperless sieht man, welche Variante passt (<see cref="PaperlessEinstellungen.YVonOben"/>).
/// Aufruf: <c>dotnet run --project src/Kalkulator.Web -- --paperless-test name@firma.de</c>.
/// </summary>
public static class PaperlessTestlauf
{
    public const string Schalter = "--paperless-test";

    /// <summary>Rollen des Muster-PDFs in Unterschriftsreihenfolge.</summary>
    public static readonly IReadOnlyList<string> Rollen = ["Kunde", "Nösse"];

    /// <summary>Linker Rand und Höhe der Unterschriftslinien in Punkt vom unteren Seitenrand (A4: 595 × 842).</summary>
    private const double Rand = 72;

    private static readonly double[] Linien = [430, 250];

    /// <summary>
    /// Ein A4-Blatt mit Hinweistext, zwei Unterschriftslinien und den unsichtbaren Marken am Anfang jeder Linie, wie sie
    /// <see cref="Vertragsdaten"/> in die Word-Vorlagen schreibt. Die grauen Rahmen haben die Größe der Felder.
    /// </summary>
    public static byte[] MusterPdf(string titel, double feldBreite, double feldHoehe)
    {
        var builder = new PdfDocumentBuilder();
        var schrift = builder.AddStandard14Font(Standard14Font.Helvetica);
        var fett = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var seite = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);

        seite.AddText(Lesbar(titel), 14, new PdfPoint(Rand, 770), fett);
        string[] hinweis =
        [
            "Testdokument des Managed-Services-Kalkulators, erfundener Inhalt. Bitte nicht versenden und nach der",
            "Prüfung löschen.",
            "",
            "Prüfen: Liegt das Unterschriftsfeld von Paperless im grauen Rahmen über der jeweiligen Linie?",
            "Ist die Paperless-Vorlage übernommen (Reihenfolge, Freigaben)?",
            "Lässt sich für „Nösse“ eine Person zuordnen?",
        ];
        for (var i = 0; i < hinweis.Length; i++)
        {
            seite.AddText(Lesbar(hinweis[i]), 10, new PdfPoint(Rand, 735 - (i * 14)), schrift);
        }

        foreach (var (rolle, y) in Rollen.Zip(Linien))
        {
            seite.SetStrokeColor(170, 170, 170);
            seite.DrawRectangle(new PdfPoint(Rand, y), feldBreite, feldHoehe, 0.5);
            seite.SetStrokeColor(0, 0, 0);
            seite.DrawLine(new PdfPoint(Rand, y), new PdfPoint(Rand + 220, y), 0.8);
            seite.AddText(Lesbar($"Unterschrift {rolle}"), 9, new PdfPoint(Rand, y - 14), schrift);

            // Marke wie in den Vorlagen: weiß, 1 pt, an den Anfang der Linie.
            seite.SetTextAndFillColor(255, 255, 255);
            seite.AddText(Unterschriftsfelder.Marke(rolle), 1, new PdfPoint(Rand, y), schrift);
            seite.SetTextAndFillColor(0, 0, 0);
        }

        return builder.Build();
    }

    /// <summary>
    /// Lädt beide Varianten als Entwurf hoch und gibt die Kennungen der Paperless-Dokumente zurück. Der Kunde ist die
    /// angegebene Adresse; für Rollen mit <see cref="PaperlessRolle.AusVorlage"/> wird nur das Feld gesetzt.
    /// </summary>
    public static async Task<IReadOnlyList<string>> AusfuehrenAsync(
        PaperlessEinstellungen einstellungen,
        string empfaenger,
        Func<PaperlessEinstellungen, IPaperlessUebergabe> uebergabe,
        TextWriter ausgabe,
        CancellationToken abbruch = default)
    {
        if (!einstellungen.Aktiv)
        {
            throw new PaperlessFehler("Paperless ist nicht eingerichtet: Es fehlt der API-Schlüssel (Paperless__ApiSchluessel) oder der Arbeitsbereich.");
        }

        if (string.IsNullOrWhiteSpace(empfaenger) || !empfaenger.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Bitte die eigene E-Mail-Adresse als Empfänger angeben: {Schalter} name@noesse.de");
        }

        ausgabe.WriteLine($"Paperless-Testlauf: Arbeitsbereich {einstellungen.ArbeitsbereichId}, Vorlage {einstellungen.VorlageId?.ToString() ?? "keine"}, nur Entwürfe.");
        var kennungen = new List<string>();
        foreach (var (variante, vonOben) in new[] { ("A", true), ("B", false) })
        {
            var e = Kopie(einstellungen, vonOben);
            var titel = $"Kalkulator-Testlauf {variante} – Koordinaten ab {(vonOben ? "oberem" : "unterem")} Rand (bitte löschen)";
            var pdf = MusterPdf(titel, e.FeldBreite, e.FeldHoehe);
            var auftrag = Auftrag(e, titel, pdf, empfaenger.Trim());
            var kennung = await uebergabe(e).UebergebenAsync(auftrag, abbruch);
            kennungen.Add(kennung);
            ausgabe.WriteLine($"  {variante}: Dokument {kennung} angelegt ({string.Join(", ", auftrag.Felder.Select(f => $"{f.Slot} Seite {f.Stelle.Seite} x={f.Stelle.X:0} Linie={f.Stelle.Grundlinie:0}"))}).");
        }

        ausgabe.WriteLine("Fertig. In Paperless prüfen, bei welcher Variante die Felder in den grauen Rahmen liegen, und beide Entwürfe danach löschen.");
        return kennungen;
    }

    /// <summary>Die eingebaute Standardschrift kennt keine Umlaute und typografischen Zeichen; ersetzt sie lesbar.</summary>
    private static string Lesbar(string text) => text
        .Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal).Replace("ü", "ue", StringComparison.Ordinal)
        .Replace("Ä", "Ae", StringComparison.Ordinal).Replace("Ö", "Oe", StringComparison.Ordinal).Replace("Ü", "Ue", StringComparison.Ordinal)
        .Replace("ß", "ss", StringComparison.Ordinal).Replace("–", "-", StringComparison.Ordinal)
        .Replace("„", "\"", StringComparison.Ordinal).Replace("“", "\"", StringComparison.Ordinal);

    internal static PaperlessAuftrag Auftrag(PaperlessEinstellungen e, string titel, byte[] pdf, string empfaenger)
    {
        var stellen = Unterschriftsfelder.Finde(pdf);
        var teilnehmer = new List<PaperlessTeilnehmer>();
        var felder = new List<PaperlessFeld>();
        foreach (var stelle in stellen)
        {
            var rolle = e.Rolle(stelle.Rolle);
            var slot = string.IsNullOrWhiteSpace(rolle.Slot) ? stelle.Rolle : rolle.Slot.Trim();
            felder.Add(new PaperlessFeld(slot, stelle));
            if (rolle.AusVorlage || teilnehmer.Any(t => t.Slot == slot))
            {
                continue;
            }

            teilnehmer.Add(rolle.Fest
                ? new PaperlessTeilnehmer(slot, rolle.Name!.Trim(), rolle.EMail!.Trim())
                : new PaperlessTeilnehmer(slot, $"Test {stelle.Rolle}", empfaenger));
        }

        return new PaperlessAuftrag(titel, "Kalkulator-Testlauf.pdf", pdf, teilnehmer, felder);
    }

    private static PaperlessEinstellungen Kopie(PaperlessEinstellungen e, bool vonOben) => new()
    {
        Adresse = e.Adresse,
        ApiSchluessel = e.ApiSchluessel,
        ArbeitsbereichId = e.ArbeitsbereichId,
        VorlageId = e.VorlageId,
        Versenden = false,
        Rollen = e.Rollen,
        FeldBreite = e.FeldBreite,
        FeldHoehe = e.FeldHoehe,
        YVonOben = vonOben,
    };
}
