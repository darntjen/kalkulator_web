using DocumentFormat.OpenXml.Packaging;
using Kalkulator.Documents.Angebot;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Erstbefuellung;

namespace Kalkulator.Infrastructure.Tests.Dokumente;

/// <summary>
/// Das Angebot aus der echten Vorlage in <c>templates/angebot</c>, gerechnet mit RK-02. Das ist dieselbe Kalkulation
/// wie im Musterangebot; die Beträge müssen mit dem Muster übereinstimmen.
/// </summary>
public class AngebotsdokumentTests
{
    internal static string Vorlagenordner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "Kalkulator.slnx")))
        {
            ordner = ordner.Parent;
        }

        return Path.Combine(ordner?.FullName ?? throw new DirectoryNotFoundException("Repository nicht gefunden."), "templates", "angebot");
    }

    private static readonly KalkulationsEingabe Rk02 = new()
    {
        Positionen = [new("S01-PRM", 1), new("B02", 40), new("B03", 4), new("B05", 1), new("S31-USER", 40), new("S32", 40)],
        Supportkontingent = new SupportkontingentEingabe(6, 2),
        AnzahlUser = 40,
    };

    /// <summary>Friert wie <see cref="Kalkulation.FriereEin"/> ein, aber ohne gespeicherte, freigegebene Preisliste.</summary>
    internal static Kalkulationsversion Version(KalkulationsErgebnis ergebnis, KalkulationsEingabe eingabe, DateOnly? beginn)
    {
        var version = new Kalkulationsversion
        {
            Nummer = 1,
            ErstelltVon = "test",
            Eingabe = eingabe,
            Vertragsbeginn = beginn,
            SummeMonatlich = ergebnis.SummeMonatlich,
            SummeEinmalig = ergebnis.SummeEinmalig,
        };
        version.Positionen.AddRange(ergebnis.Positionen.Select((p, i) => new VersionsPosition
        {
            Reihenfolge = i + 1,
            Code = p.Code,
            Bezeichnung = p.Bezeichnung,
            ServiceCode = p.ServiceCode,
            Menge = p.Menge,
            BerechneteMenge = p.BerechneteMenge,
            Einzelpreis = p.Einzelpreis,
            Betrag = p.Betrag,
            Abrechnungsart = p.Abrechnungsart,
            Herkunft = p.Herkunft,
            Hinweis = p.Hinweis,
        }));
        return version;
    }

    private static string Text(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        var haupt = dokument.MainDocumentPart!;
        return string.Join("\n", haupt.Document!.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Select(p => p.InnerText))
            + "\n" + string.Join("\n", haupt.HeaderParts.Select(h => h.Header!.InnerText));
    }

    private static (string Text, Documents.Datensatz Daten) Erzeuge(KalkulationsEingabe eingabe, Kunde kunde, string? freitext = null)
    {
        var katalog = KatalogErstbefuellung.ErzeugeKatalog();
        var ergebnis = new Rechenkern(katalog.Services, katalog.Preisliste).Berechne(eingabe);
        Assert.False(ergebnis.HatFehler, string.Join(" ", ergebnis.Meldungen.Select(m => m.Text)));
        var ordner = Vorlagenordner();
        using var texte = File.OpenRead(Path.Combine(ordner, "textbausteine.json"));
        var quelle = new AngebotsQuelle(
            "MS-A-2026-0001", Version(ergebnis, eingabe, new DateOnly(2026, 11, 1)), kunde,
            new DateOnly(2026, 10, 2), new DateOnly(2026, 11, 1), freitext,
            new AngebotsAbsender("Max Mustermann", "Vertrieb Managed Services", "+49 2171 7003-0", "m.mustermann@noesse.de"),
            katalog.Services, katalog.Preisliste, Textbausteine.Lies(texte));
        var datei = Angebotsdokument.Erzeuge(File.ReadAllBytes(Path.Combine(ordner, "Angebotsvorlage.docx")), quelle);
        return (Text(datei), Angebotsdokument.Daten(quelle));
    }

    private static readonly Kunde Muster = new()
    {
        Firma = "Muster Maschinenbau GmbH",
        Ansprechpartner = "Frau Erika Beispiel, Geschäftsführung",
        Strasse = "Industriestraße 12",
        Postleitzahl = "50667",
        Ort = "Köln",
    };

    [Fact]
    public void RK02_ergibt_das_Musterangebot()
    {
        var (text, _) = Erzeuge(Rk02, Muster, "Sie haben uns geschildert,\ndass Ausfälle Geld kosten.");

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        foreach (var erwartet in new[]
        {
            "Angebot MS-A-2026-0001 · Muster Maschinenbau GmbH",
            "MS-A-2026-0001 (V1)",
            "Sehr geehrte Frau Erika Beispiel,",
            "50667 Köln",
            "Sie haben uns geschildert,",
            "6.095,06 € netto monatlich",
            "2.000,00 € netto",
            "73.140,72 € netto",
            "75.140,72 € netto",
            "Ihr Bundle-Vorteil (B02, B03)\n583,20 € monatlich",
            "2. Ihre Servicebasis: Nösse Connect Premium",
            "Reaktionszeit P1 (kritisch)\n2 Stunden",
            "User as a Service Premium (B02) – 54,90 € je User / Monat, 40 User",
            "Endpoint Protection (XDR) & Richtlinienmanagement (S02)",
            "Security as a Service Standard (B05) – 199,90 € pauschal / Monat",
            "12 Abrechnungseinheiten (3 Stunden) pro Monat",
            "Bundle B02 mit Leistungsscheinen S02, S03, S04, S05, S06, S07",
            "Leistungsscheine S31, S32, S60",
            "Service Level Agreement (Anlage SLA) – Stufe Premium",
            "Ebene 2 (Standard): 33,75 €",
            "gültig bis zum 01.11.2026",
            "damit der Service zum 01.11.2026 starten kann",
        })
        {
            Assert.Contains(erwartet, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Notfallerreichbarkeit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Vergleich mit Ihrer bisherigen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Erzeugtes_Angebot_ist_gueltiges_Word_wie_die_Vorlage()
    {
        var ordner = Vorlagenordner();
        var vorlage = File.ReadAllBytes(Path.Combine(ordner, "Angebotsvorlage.docx"));
        var katalog = KatalogErstbefuellung.ErzeugeKatalog();
        var eingabe = Rk02 with { BisherigerMonatspreis = 5500m };
        var ergebnis = new Rechenkern(katalog.Services, katalog.Preisliste).Berechne(eingabe);
        using var texte = File.OpenRead(Path.Combine(ordner, "textbausteine.json"));
        var datei = Angebotsdokument.Erzeuge(vorlage, new AngebotsQuelle(
            "MS-A-2026-0001", Version(ergebnis, eingabe, null), Muster, new DateOnly(2026, 10, 2), new DateOnly(2026, 11, 1), "Zeile 1\nZeile 2",
            new AngebotsAbsender("A", "B", "C", "D"), katalog.Services, katalog.Preisliste, Textbausteine.Lies(texte)));

        Assert.Equal(Fehler(vorlage), Fehler(datei));
        Assert.Empty(Fehler(datei));
    }

    private static List<string> Fehler(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        return [.. new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(dokument).Select(f => $"{f.Part?.Uri}: {f.Path?.XPath}: {f.Description}")];
    }

    [Fact]
    public void Preistabelle_und_Sonderpositionen_sind_gekennzeichnet()
    {
        var eingabe = Rk02 with
        {
            BisherigerMonatspreis = 5500m,
            Sonderpositionen = [new("Monatlicher Sonderreport", "Monat", 1, 50m, "Kundenwunsch", Freigegeben: true)],
        };

        var (text, daten) = Erzeuge(eingabe, new Kunde { Firma = "Ohne Ansprechpartner AG" });
        var positionen = (List<Documents.Datensatz>)daten["positionen"]!;

        Assert.Equal(["S01", "B02", "B03", "B05", "S31", "S32", "S60", "SP"], positionen.Select(p => p["code"]));
        Assert.Equal("Monatlicher Sonderreport (Sonderposition)", positionen[^1]["bezeichnung"]);
        Assert.Equal("Monat", positionen[^1]["einheit"]);
        Assert.Contains("Sehr geehrte Damen und Herren,", text, StringComparison.Ordinal);
        Assert.Contains("Bisher 5.500,00 € monatlich, künftig 6.145,06 € monatlich: +645,06 € (+11,7 %).", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reine_S41_Kalkulation_hat_keine_Servicebasis()
    {
        var (text, _) = Erzeuge(new KalkulationsEingabe { Positionen = [new("S41", 1)], AnzahlMitarbeitende = 30 }, Muster);

        Assert.DoesNotContain("Ihre Servicebasis", text, StringComparison.Ordinal);
        Assert.Contains("Strategische IT-Begleitung (S41)", text, StringComparison.Ordinal);
        Assert.Contains("Stufe Standard", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Einmalige Kosten", text, StringComparison.Ordinal);
    }
}
