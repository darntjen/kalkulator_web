using System.Globalization;
using Kalkulator.Documents.Vertrag;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Testlauf gegen die echte Paperless-API (Frage 12.5), ohne Vertragsvorlagen und PDF-Umwandlung: Ein Muster-PDF mit
/// erfundenem Inhalt und je einem Unterschriftsfeld für „Kunde“ und „Nösse“ geht als Entwurf an Paperless, einmal über
/// eine Kopie der Ablauf-Vorlage (D, Reihenfolge und Freigaben) und einmal direkt aus dem PDF (E). Ein grauer Rahmen im
/// PDF zeigt, wo das Feld liegen soll. Aufruf: <c>dotnet run --project src/Kalkulator.Web -- --paperless-test name@firma.de</c>.
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
    public static byte[] MusterPdf(string titel, string zusatz, double feldBreite, double feldHoehe)
    {
        var builder = new PdfDocumentBuilder();
        var schrift = builder.AddStandard14Font(Standard14Font.Helvetica);
        var fett = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var seite = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);

        seite.AddText(Lesbar(titel), 14, new PdfPoint(Rand, 770), fett);
        seite.AddText(Lesbar(zusatz), 10, new PdfPoint(Rand, 754), schrift);
        string[] hinweis =
        [
            "Testdokument des Managed-Services-Kalkulators, erfundener Inhalt. Bitte nicht versenden und nach der",
            "Prüfung löschen.",
            "",
            "Prüfen: Liegt das Unterschriftsfeld von Paperless im grauen Rahmen über der jeweiligen Linie?",
            "Bei Variante D: Sind Reihenfolge (erst Kunde, dann Nösse) und Freigaben aus der Vorlage übernommen?",
        ];
        for (var i = 0; i < hinweis.Length; i++)
        {
            seite.AddText(Lesbar(hinweis[i]), 10, new PdfPoint(Rand, 725 - (i * 14)), schrift);
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
    /// Legt die Varianten D und E als Entwurf an und gibt die Kennungen der Paperless-Dokumente zurück. Der Kunde ist die
    /// angegebene Adresse; „Nösse“ kommt aus den Einstellungen (fest eingestellte Person).
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

        var e = Kopie(einstellungen);
        ausgabe.WriteLine($"Paperless-Testlauf: Arbeitsbereich {e.ArbeitsbereichId}, Ablauf-Vorlage {e.AblaufVorlageId?.ToString(CultureInfo.InvariantCulture) ?? "keine"}, Skalierung {e.Skalierung:0.###}, nur Entwürfe.");
        var kennungen = new List<string>();
        var varianten = new List<(string Name, string Zusatz, Func<PaperlessAuftrag, Task<string>> Anlegen)>();
        if (e.AblaufVorlageId is { } vorlage)
        {
            varianten.Add(("D", $"über eine Kopie der Ablauf-Vorlage {vorlage}", a => uebergabe(e).UebergebenUeberVorlageAsync(a, vorlage, abbruch)));
        }

        varianten.Add(("E", "direkt aus dem PDF, ohne Vorlage", a => uebergabe(e).UebergebenAsync(a, abbruch)));
        foreach (var (variante, zusatz, anlegen) in varianten)
        {
            var titel = $"Kalkulator-Testlauf {variante} (bitte löschen)";
            ausgabe.WriteLine($"  {variante}: {zusatz}");
            var pdf = MusterPdf(titel, zusatz, e.FeldBreite, e.FeldHoehe);
            var auftrag = Auftrag(e, titel, pdf, empfaenger.Trim());
            string kennung;
            try
            {
                kennung = await anlegen(auftrag);
            }
            catch (PaperlessFehler f)
            {
                // Eine Variante darf scheitern (z. B. D, wenn Paperless keine Vorlage mit eigenem PDF kopiert).
                ausgabe.WriteLine($"  {variante}: fehlgeschlagen: {f.Message}");
                continue;
            }

            kennungen.Add(kennung);
            ausgabe.WriteLine($"  {variante}: Dokument {kennung} angelegt, {pdf.Length} Byte PDF, Teilnehmer {string.Join(", ", auftrag.Teilnehmer.Select(t => $"{t.Slot} = {t.Name}"))}.");
        }

        ausgabe.WriteLine("Fertig. In Paperless prüfen: PDF zu sehen? Felder im grauen Rahmen? Bei D: Reihenfolge und Freigaben aus der Vorlage? Danach die Entwürfe löschen.");
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

    private static PaperlessEinstellungen Kopie(PaperlessEinstellungen e) => new()
    {
        Adresse = e.Adresse,
        ApiSchluessel = e.ApiSchluessel,
        ArbeitsbereichId = e.ArbeitsbereichId,
        AblaufVorlageId = e.AblaufVorlageId,
        Versenden = false,
        Rollen = e.Rollen,
        FeldBreite = e.FeldBreite,
        FeldHoehe = e.FeldHoehe,
        YVonOben = e.YVonOben,
        Skalierung = e.Skalierung,
    };
}
