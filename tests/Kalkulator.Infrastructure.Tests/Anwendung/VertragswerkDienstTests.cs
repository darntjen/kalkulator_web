using System.Security.Cryptography;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Tests.Dokumente;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>
/// Vertragsangaben und Vertragswerk (#26, Teil C) gegen eine eigene Datenbank je Test mit freigegebener Preisliste.
/// Vorlagenfassungen werden direkt angelegt und aktiviert, so wie es sonst Abgleich und Produktmanagement tun.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class VertragswerkDienstTests(SqlServerFixture db)
{
    internal static readonly KalkulationsEingabe Rk01 = new()
    {
        Positionen = [new("S01-STD", 1), new("B01", 15), new("B03", 2), new("B05", 1)],
        AnzahlUser = 15,
    };

    private static readonly TestBenutzer Vertrieb = new("vertrieb@noesse.de", Rollen.Vertrieb);

    internal sealed record Dienste(KundenprojektDienst Projekte, KalkulationsDienst Kalkulationen, AngebotsDienst Angebote, VertragswerkDienst Vertragswerk);

    private async Task<string> NeueDatenbankAsync()
    {
        var name = "Vertragswerk_" + Guid.NewGuid().ToString("N")[..12];
        await using var kontext = db.NeuerKontextAufDatenbank(name);
        await kontext.Database.MigrateAsync();
        await KatalogErstbefuellung.AusfuehrenAsync(kontext);
        (await kontext.Preislisten.SingleAsync()).Freigeben("produktmanagement", DateTimeOffset.UtcNow);
        await kontext.SaveChangesAsync();
        return name;
    }

    /// <summary>PDF-Umwandlung für Tests: merkt sich die Word-Dateien und liefert je Dokument eine leere PDF-Seite.</summary>
    internal sealed class TestWandler : Kalkulator.Infrastructure.Vorlagen.IPdfWandler
    {
        public List<byte[]> Dokumente { get; } = [];

        public string Name => "Test";

        public Task<byte[]> InPdfAsync(byte[] docx, CancellationToken abbruch)
        {
            Dokumente.Add(docx);
            return Task.FromResult(VertragsmappeTests.LeeresPdf(1));
        }
    }

    private readonly TestWandler _wandler = new();

    private Dienste DiensteFuer(string datenbank, TestBenutzer benutzer, Kalkulator.Infrastructure.Vorlagen.IPdfWandler? wandler = null)
    {
        var fabrik = new Fabrik(() => db.NeuerKontextAufDatenbank(datenbank, benutzer));
        var einstellungen = Options.Create(new AngebotsEinstellungen { Vorlagenordner = AngebotsdokumentTests.Vorlagenordner() });
        return new Dienste(
            new KundenprojektDienst(fabrik, benutzer, TimeProvider.System),
            new KalkulationsDienst(fabrik, benutzer, TimeProvider.System),
            new AngebotsDienst(fabrik, benutzer, TimeProvider.System, einstellungen),
            new VertragswerkDienst(fabrik, benutzer, TimeProvider.System, wandler ?? _wandler,
                Options.Create(new VertragswerkEinstellungen { Deckblatt = Path.Combine(AngebotsdokumentTests.Vorlagenordner(), "..", "vertrag", "Deckblatt.docx") })));
    }

    /// <summary>Legt eine aktive Fassung für die Vorlage <paramref name="code"/> an, geprüft wie beim Abgleich.</summary>
    internal async Task AktiviereVorlageAsync(string datenbank, string code, byte[] inhalt)
    {
        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        var vorlage = await kontext.DokumentVorlagen.Include(d => d.Versionen).SingleOrDefaultAsync(d => d.Code == code)
            ?? kontext.DokumentVorlagen.Add(new DokumentVorlage { Typ = DokumentTyp.Avv, Code = code, Bezeichnung = code, Version = "" }).Entity;
        var komponenten = (await kontext.Preiskomponenten.Select(k => k.Code).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var analyse = Vorlagenpruefung.Pruefe(inhalt, vorlage.Typ == DokumentTyp.Grundvertrag ? VorlagenRolle.Grundvertrag : VorlagenRolle.Sonstige, komponenten);
        Assert.False(analyse.HatFehler, string.Join("; ", analyse.Hinweise.Select(h => h.Text)));
        var fassung = vorlage.NimmAuf(new Vorlagenversion
        {
            Dateiname = $"{code}.docx",
            Quelle = "Test",
            Pfad = $"{code}.docx",
            QuellId = code,
            Sha256 = Convert.ToHexString(SHA256.HashData(inhalt)),
            AbgerufenVon = "Test",
            AbgerufenAm = DateTimeOffset.UtcNow,
            VersionLaut = "1.0",
            Hinweise = analyse.Hinweise,
            Eingaben = analyse.Eingaben,
            Komponenten = analyse.Komponenten,
            Datei = new VorlagenDatei { Inhalt = inhalt },
        }, DateTimeOffset.UtcNow)!;
        vorlage.Aktiviere(fassung, "produktmanagement@noesse.de", DateTimeOffset.UtcNow, null);
        await kontext.SaveChangesAsync();
    }

    /// <summary>Kundenprojekt mit gespeicherter Kalkulation RK-01 des Vertriebs.</summary>
    internal static async Task<(int Projekt, int Kalkulation)> KalkulationAsync(Dienste dienste, Vertragsangaben? angaben = null)
    {
        var projekt = await dienste.Projekte.AnlegenAsync(new NeuesKundenprojekt("Muster Spedition GmbH", "Hafenstraße 12", "26135", "Oldenburg",
            "Frau Beispiel", null, "Managed Services 2027", false, null, null));
        var id = await dienste.Projekte.NeueKalkulationAsync(projekt, "Variante A");
        var geladen = await dienste.Kalkulationen.LadeAsync(id);
        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A", new DateOnly(2027, 1, 1), Rk01, geladen!.Kalkulation.Zeilenversion, angaben);
        return (projekt, id);
    }

    internal async Task FreigebenAsync(string datenbank, int kalkulationId)
    {
        foreach (var (benutzer, rolle) in new[] { (new TestBenutzer("vertriebsleitung@noesse.de", Rollen.Vertriebsleitung), FreigabeRolle.Vertriebsleitung), (new TestBenutzer("consultant@noesse.de", Rollen.Consultant), FreigabeRolle.SolutionConsultant) })
        {
            var dienste = DiensteFuer(datenbank, benutzer);
            await dienste.Kalkulationen.VertriebFreigebenAsync(kalkulationId, rolle, (await dienste.Kalkulationen.LadeAsync(kalkulationId))!.Stand, null);
        }
    }

    private static Vertragsangaben Angaben(string vertreter) => new() { Werte = new Dictionary<string, string> { ["Vertreter"] = vertreter } };

    [Fact]
    public async Task Vorschau_nennt_Dokumente_in_Rangfolge_aktive_Fassungen_und_verlangte_Angaben()
    {
        var datenbank = await NeueDatenbankAsync();
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("Vertreter: {{eingabe.Vertreter}}", "{{eingabe.Stufe=Standard}} Standard"));
        await AktiviereVorlageAsync(datenbank, "B05", VorlagenpruefungTests.Dokument("Vertreter: {{eingabe.Vertreter}}"));
        var dienste = DiensteFuer(datenbank, Vertrieb);
        var (_, id) = await KalkulationAsync(dienste);

        var vorschau = await dienste.Vertragswerk.VorschauAsync(id, ["S01-STD", "B01", "B03", "B05"]);

        Assert.Equal(["AVV", "Grundvertrag", "AVB", "SLA", "S01"], vorschau.Dokumente.Take(5).Select(d => d.Dokument.Code));
        Assert.Equal("S01", vorschau.Dokumente[4].Vorlage!.Code);
        Assert.NotNull(vorschau.Dokumente[4].Fassung);
        Assert.Contains(vorschau.OhneVorlage, d => d.Dokument.Code == "AVV");
        Assert.Equal(["Vertreter", "Stufe"], vorschau.Eingaben.Select(e => e.Name));

        var fremd = DiensteFuer(datenbank, new TestBenutzer("andere@noesse.de", Rollen.Vertrieb));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.Vertragswerk.VorschauAsync(id, ["S01-STD"]));
    }

    [Fact]
    public async Task Ohne_vollstaendige_Vertragsangaben_kein_Angebot_und_Aenderungen_heben_Freigaben_auf()
    {
        var datenbank = await NeueDatenbankAsync();
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("Vertreter: {{eingabe.Vertreter}}"));
        var dienste = DiensteFuer(datenbank, Vertrieb);
        var (_, id) = await KalkulationAsync(dienste);
        await FreigebenAsync(datenbank, id);

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.ErzeugenAsync(id, null, null));
        Assert.Contains("Vertragsangaben: Vertreter", fehler.Message, StringComparison.Ordinal);
        Assert.Empty(await dienste.Angebote.ListeAsync(id));

        var geladen = (await dienste.Kalkulationen.LadeAsync(id))!;
        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A", new DateOnly(2027, 1, 1), Rk01, geladen.Kalkulation.Zeilenversion, Angaben("Frau Beispiel"));
        geladen = (await dienste.Kalkulationen.LadeAsync(id))!;
        Assert.False(geladen.Kalkulation.IstVertriebsfreigegeben);
        Assert.Contains(geladen.Kalkulation.Vertriebsfreigaben, f => f.Aufhebungsgrund == "Arbeitsstand geändert");

        await FreigebenAsync(datenbank, id);
        await dienste.Angebote.ErzeugenAsync(id, null, null);

        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        var version = await kontext.Kalkulationsversionen.SingleAsync(v => v.KalkulationId == id);
        Assert.Equal("Frau Beispiel", version.Vertragsangaben.Wert("Vertreter"));

        // Die Kopie einer Kalkulation übernimmt die Angaben.
        var kopie = await dienste.Projekte.DuplizierenAsync(id, "Variante B");
        Assert.Equal("Frau Beispiel", (await dienste.Kalkulationen.LadeAsync(kopie))!.Kalkulation.Vertragsangaben.Wert("Vertreter"));
    }

    private static readonly byte[] Grundvertrag = VorlagenpruefungTests.Dokument(
        "Managed-Services-Vertrag mit {{kunde.anschrift}}, Vertragsnummer {{vertrag.nummer}}, Beginn {{vertrag.beginn}}",
        "{{#positionen}}",
        "{{position.code}} {{position.bezeichnung}} {{position.gesamtpreis}}",
        "{{/positionen}}",
        "Gesamt {{summe.monatlich}}",
        "{{#anlagen}}",
        "{{anlage.code}} — {{anlage.bezeichnung}}",
        "{{/anlagen}}");

    /// <summary>Aktiviert für jedes Dokument des Vertragswerks eine einfache Vorlage; S01 verlangt den Vertreter.</summary>
    private async Task AlleVorlagenAsync(string datenbank, int kalkulationId)
    {
        await AktiviereVorlageAsync(datenbank, "GRUNDVERTRAG", Grundvertrag);
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var vorschau = await DiensteFuer(datenbank, Vertrieb).Vertragswerk.VorschauAsync(kalkulationId, ["S01-STD", "B01", "B03", "B05"]);
        foreach (var d in vorschau.OhneVorlage)
        {
            await AktiviereVorlageAsync(datenbank, d.Vorlage?.Code ?? d.Dokument.Code.ToUpperInvariant(), VorlagenpruefungTests.Dokument($"{d.Dokument.Bezeichnung} für {{{{kunde.firma}}}}"));
        }
    }

    /// <summary>Kalkulation mit Angaben, freigegeben, Angebot versendet und angenommen: Projekt „Gewonnen“.</summary>
    private async Task<(int Projekt, int Kalkulation)> GewonnenAsync(string datenbank, Dienste dienste)
    {
        var (projekt, id) = await KalkulationAsync(dienste, Angaben("Frau Beispiel"));
        await FreigebenAsync(datenbank, id);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);
        await dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute);
        await dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, "Zusage", null, angebot);
        return (projekt, id);
    }

    [Fact]
    public async Task Vertragswerk_entsteht_erst_nach_Gewonnen_und_mit_allen_Vorlagen()
    {
        var datenbank = await NeueDatenbankAsync();
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var dienste = DiensteFuer(datenbank, Vertrieb);
        var (projekt, id) = await KalkulationAsync(dienste, Angaben("Frau Beispiel"));

        var vorher = await dienste.Vertragswerk.BereitschaftAsync(projekt);
        Assert.Contains("Gewonnen", vorher.Sperrgrund, StringComparison.Ordinal);

        await FreigebenAsync(datenbank, id);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);
        await dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute);
        await dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, "Zusage", null, angebot);

        var ohneVorlagen = await dienste.Vertragswerk.BereitschaftAsync(projekt);
        Assert.Contains("AVV", ohneVorlagen.Sperrgrund, StringComparison.Ordinal);
        Assert.Contains("keine freigegebene Vorlage", ohneVorlagen.Sperrgrund, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Vertragswerk.ErzeugenAsync(projekt));

        await AlleVorlagenAsync(datenbank, id);
        var bereit = await dienste.Vertragswerk.BereitschaftAsync(projekt);
        Assert.Null(bereit.Sperrgrund);
        Assert.True(bereit.DarfErzeugen);
    }

    [Fact]
    public async Task Vertragswerk_mit_Deckblatt_Gesamt_PDF_ZIP_und_festgehaltenen_Fassungen()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienste = DiensteFuer(datenbank, Vertrieb);
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var (projekt, id) = await GewonnenAsync(datenbank, dienste);
        await AlleVorlagenAsync(datenbank, id);
        var anzahl = (await dienste.Vertragswerk.BereitschaftAsync(projekt)).Vorschau!.Dokumente.Count;

        var werkId = await dienste.Vertragswerk.ErzeugenAsync(projekt);

        var werk = Assert.Single(await dienste.Vertragswerk.ListeAsync(projekt));
        Assert.Equal((werkId, 1), (werk.Id, werk.Ausfertigung));
        Assert.Matches(@"^MS-A-\d{4}-\d{4}$", werk.Nummer);
        Assert.Equal(anzahl, werk.Dokumente.Count);
        Assert.Equal(["AVV", "GRUNDVERTRAG", "AVB", "SLA", "S01"], werk.Dokumente.Take(5).Select(d => d.Code));
        Assert.StartsWith("S01 V", werk.Dokumente[4].Fassung, StringComparison.Ordinal);

        // Je Dokument eine Umwandlung plus Deckblatt; Gesamt-PDF hat entsprechend viele Seiten.
        Assert.Equal(anzahl + 1, _wandler.Dokumente.Count);
        var grundvertrag = Text(_wandler.Dokumente[1]);
        Assert.Contains("Muster Spedition GmbH", grundvertrag, StringComparison.Ordinal);
        Assert.Contains(werk.Nummer, grundvertrag, StringComparison.Ordinal);
        Assert.Contains("Anlage AVV — Auftragsverarbeitungsvereinbarung", grundvertrag, StringComparison.Ordinal);
        Assert.Contains("Vertreter Frau Beispiel", Text(_wandler.Dokumente[4]), StringComparison.Ordinal);
        using (var deckblatt = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(_wandler.Dokumente[^1]), false))
        {
            Assert.Contains("1Auftragsverarbeitungsvereinbarung", deckblatt.MainDocumentPart!.Document!.Body!.InnerText, StringComparison.Ordinal);
        }

        var (pdfName, pdf) = await dienste.Vertragswerk.DateiAsync(werkId, zip: false);
        Assert.Equal($"Vertrag_{werk.Nummer}_Muster-Spedition-GmbH.pdf", pdfName);
        using (var strom = new MemoryStream(pdf))
        using (var dokument = PdfSharp.Pdf.IO.PdfReader.Open(strom, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
        {
            Assert.Equal(anzahl + 1, dokument.PageCount);
        }

        var (_, zip) = await dienste.Vertragswerk.DateiAsync(werkId, zip: true);
        using (var archiv = new System.IO.Compression.ZipArchive(new MemoryStream(zip)))
        {
            Assert.Equal(anzahl + 1, archiv.Entries.Count);
            Assert.Contains(archiv.Entries, e => e.FullName == "Einzeldokumente/02 Managed-Services-Vertrag-Grundvertrag.pdf");
        }

        // Eine zweite Ausfertigung ist möglich; die erste bleibt unverändert.
        await dienste.Vertragswerk.ErzeugenAsync(projekt);
        Assert.Equal([2, 1], (await dienste.Vertragswerk.ListeAsync(projekt)).Select(w => w.Ausfertigung));

        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        var datei = await kontext.Set<VertragswerkDatei>().FirstAsync();
        kontext.Entry(datei).Property(d => d.Zip).CurrentValue = [1, 2, 3];
        await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
    }

    [Fact]
    public async Task Neue_Pflichtangabe_nach_dem_Angebot_verlangt_ein_neues_Angebot()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienste = DiensteFuer(datenbank, Vertrieb);
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var (projekt, id) = await GewonnenAsync(datenbank, dienste);
        await AlleVorlagenAsync(datenbank, id);

        // Das Produktmanagement aktiviert für S01 eine Fassung mit einer weiteren Eingabe.
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01: {{eingabe.Vertreter}}, Standort {{eingabe.Standort}}"));

        var bereitschaft = await dienste.Vertragswerk.BereitschaftAsync(projekt);
        Assert.Equal(["Standort"], bereitschaft.FehlendeAngaben);
        Assert.Contains("neues Angebot", bereitschaft.Sperrgrund, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erzeugen_darf_der_Vertrieb_lesen_auch_Consultant_Fremde_nichts()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienste = DiensteFuer(datenbank, Vertrieb);
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var (projekt, id) = await GewonnenAsync(datenbank, dienste);
        await AlleVorlagenAsync(datenbank, id);
        var werk = await dienste.Vertragswerk.ErzeugenAsync(projekt);

        var consultant = DiensteFuer(datenbank, new TestBenutzer("consultant@noesse.de", Rollen.Consultant));
        var fremd = DiensteFuer(datenbank, new TestBenutzer("andere@noesse.de", Rollen.Vertrieb));

        Assert.False((await consultant.Vertragswerk.BereitschaftAsync(projekt)).DarfErzeugen);
        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Vertragswerk.ErzeugenAsync(projekt));
        Assert.NotEmpty((await consultant.Vertragswerk.DateiAsync(werk, zip: true)).Inhalt);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.Vertragswerk.DateiAsync(werk, zip: false));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.Vertragswerk.ListeAsync(projekt));
    }

    [Fact]
    public async Task Ohne_PDF_Umwandlung_nennt_die_Bereitschaft_den_Grund()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienste = DiensteFuer(datenbank, Vertrieb, new Kalkulator.Infrastructure.Vorlagen.KeinPdfWandler());
        await AktiviereVorlageAsync(datenbank, "S01", VorlagenpruefungTests.Dokument("S01 für {{kunde.firma}}, Vertreter {{eingabe.Vertreter}}"));
        var (projekt, id) = await GewonnenAsync(datenbank, dienste);
        await AlleVorlagenAsync(datenbank, id);

        Assert.Contains("PDF-Umwandlung ist nicht eingerichtet", (await dienste.Vertragswerk.BereitschaftAsync(projekt)).Sperrgrund, StringComparison.Ordinal);
    }

    private static string Text(byte[] docx)
    {
        using var strom = new MemoryStream(docx);
        using var dokument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(strom, false);
        return string.Join("\n", dokument.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().Select(p => p.InnerText));
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
