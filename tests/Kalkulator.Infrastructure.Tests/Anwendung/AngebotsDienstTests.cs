using DocumentFormat.OpenXml.Packaging;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Tests.Dokumente;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Angebot erzeugen, archivieren und Versand vermerken gegen eine Datenbank mit freigegebener Preisliste.</summary>
[Collection(DatenbankSammlung.Name)]
public class AngebotsDienstTests(SqlServerFixture db)
{
    private const string Datenbank = "Angebote";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _befuellt;

    private static readonly KalkulationsEingabe Rk01 = new()
    {
        Positionen = [new("S01-STD", 1), new("B01", 15), new("B03", 2), new("B05", 1)],
        AnzahlUser = 15,
    };

    private sealed record Dienste(KundenprojektDienst Projekte, KalkulationsDienst Kalkulationen, AngebotsDienst Angebote);

    private async Task<Dienste> DiensteAsync(TestBenutzer benutzer, bool preislisteFreigeben = true)
    {
        await Sperre.WaitAsync();
        try
        {
            if (!_befuellt)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                await KatalogErstbefuellung.AusfuehrenAsync(kontext);
                (await kontext.Preislisten.SingleAsync()).Freigeben("produktmanagement", DateTimeOffset.UtcNow);
                await kontext.SaveChangesAsync();
                _befuellt = true;
            }
        }
        finally
        {
            Sperre.Release();
        }

        var fabrik = new Fabrik(() => db.NeuerKontextAufDatenbank(Datenbank, benutzer));
        var einstellungen = Options.Create(new AngebotsEinstellungen { Vorlagenordner = AngebotsdokumentTests.Vorlagenordner() });
        return new Dienste(
            new KundenprojektDienst(fabrik, benutzer, TimeProvider.System),
            new KalkulationsDienst(fabrik, benutzer, TimeProvider.System),
            new AngebotsDienst(fabrik, benutzer, TimeProvider.System, einstellungen));
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private async Task<(int Projekt, int Kalkulation, Dienste Dienste)> KalkulationAsync(KalkulationsEingabe eingabe, bool freigeben = true)
    {
        var dienste = await DiensteAsync(Neu(Rollen.Vertrieb));
        var projekt = await dienste.Projekte.AnlegenAsync(new NeuesKundenprojekt("Angebots-Test GmbH & Co. KG", "Am Hafen 3", "26122", "Oldenburg",
            "Herr Max Kunde, Geschäftsführer", null, "MS", false, null, null));
        var id = await dienste.Projekte.NeueKalkulationAsync(projekt, "Variante A");
        var geladen = await dienste.Kalkulationen.LadeAsync(id);
        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A", new DateOnly(2027, 1, 1), eingabe, geladen!.Kalkulation.Zeilenversion);
        if (freigeben)
        {
            await FreigebenAsync(id);
        }

        return (projekt, id, dienste);
    }

    /// <summary>Vertriebsfreigabe durch Vertriebsleitung und Solution Consultant (#26).</summary>
    private async Task FreigebenAsync(int kalkulationId)
    {
        foreach (var (rolle, freigabe) in new[] { (Rollen.Vertriebsleitung, FreigabeRolle.Vertriebsleitung), (Rollen.Consultant, FreigabeRolle.SolutionConsultant) })
        {
            var dienste = await DiensteAsync(Neu(rolle));
            var stand = (await dienste.Kalkulationen.LadeAsync(kalkulationId))!.Stand;
            await dienste.Kalkulationen.VertriebFreigebenAsync(kalkulationId, freigabe, stand, null);
        }
    }

    private static string Text(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        return dokument.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Fact]
    public async Task Angebot_friert_ein_vergibt_Nummer_und_archiviert_das_Dokument()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);

        var erstes = await dienste.Angebote.ErzeugenAsync(id, "Ausgangssituation des Kunden", null);
        var zweites = await dienste.Angebote.ErzeugenAsync(id, null, dienste.Angebote.Heute.AddDays(10));

        var liste = await dienste.Angebote.ListeAsync(id);
        Assert.Equal(["V2", "V1"], liste.Select(a => a.Version));
        Assert.Single(liste.Select(a => a.Nummer).Distinct());
        Assert.Matches(@"^MS-A-\d{4}-\d{4}$", liste[0].Nummer);
        Assert.Equal(dienste.Angebote.Heute.AddDays(30), liste[1].GueltigBis);
        Assert.Equal(dienste.Angebote.Heute.AddDays(10), liste[0].GueltigBis);

        var (name, inhalt) = await dienste.Angebote.DateiAsync(erstes);
        Assert.Equal($"Angebot_{liste[1].Nummer}_V1_Angebots-Test-GmbH-Co-KG.docx", name);
        var text = Text(inhalt);
        Assert.Contains($"{liste[1].Nummer} (V1)", text, StringComparison.Ordinal);
        Assert.Contains("Sehr geehrter Herr Max Kunde,", text, StringComparison.Ordinal);
        Assert.Contains("Ausgangssituation des Kunden", text, StringComparison.Ordinal);
        Assert.Contains("1.027,20 € netto monatlich", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ausgangssituation des Kunden", Text((await dienste.Angebote.DateiAsync(zweites)).Inhalt), StringComparison.Ordinal);

        // Die Version ist eingefroren: weitere Änderungen am Arbeitsstand ändern das Angebot nicht.
        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        var version = await kontext.Kalkulationsversionen.Include(v => v.Positionen).SingleAsync(v => v.KalkulationId == id && v.Nummer == 1);
        Assert.Equal(1027.20m, version.SummeMonatlich);
        Assert.StartsWith("vertriebsleitung-", version.FreigabeVertriebsleitungVon, StringComparison.Ordinal);
        Assert.StartsWith("consultant-", version.FreigabeSolutionConsultantVon, StringComparison.Ordinal);
        Assert.NotNull(version.FreigabeSolutionConsultantAm);
    }

    [Fact]
    public async Task Ohne_beide_Vertriebsfreigaben_gibt_es_kein_Angebot()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01, freigeben: false);
        var leitung = await DiensteAsync(Neu(Rollen.Vertriebsleitung));
        await leitung.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, (await leitung.Kalkulationen.LadeAsync(id))!.Stand, "passt");

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.ErzeugenAsync(id, null, null));

        Assert.Contains("Solution Consultant", fehler.Message, StringComparison.Ordinal);
        Assert.Empty(await dienste.Angebote.ListeAsync(id));
    }

    [Fact]
    public async Task Vertriebsfreigabe_nur_durch_die_passende_Rolle_und_nach_dem_Vier_Augen_Prinzip()
    {
        var (_, id, vertrieb) = await KalkulationAsync(Rk01, freigeben: false);
        var leitungsBenutzer = Neu(Rollen.Vertriebsleitung);
        var leitung = await DiensteAsync(leitungsBenutzer);
        var consultant = await DiensteAsync(Neu(Rollen.Consultant));
        var stand = (await vertrieb.Kalkulationen.LadeAsync(id))!.Stand;

        Assert.Empty((await vertrieb.Kalkulationen.LadeAsync(id))!.DarfVertriebFreigeben);
        Assert.Equal([FreigabeRolle.Vertriebsleitung], (await leitung.Kalkulationen.LadeAsync(id))!.DarfVertriebFreigeben);
        Assert.Equal([FreigabeRolle.SolutionConsultant], (await consultant.Kalkulationen.LadeAsync(id))!.DarfVertriebFreigeben);
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, stand, null));
        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, stand, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => leitung.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, "0000000000000000", null));

        await leitung.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, stand, " passt ");
        await Assert.ThrowsAsync<InvalidOperationException>(() => leitung.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, stand, null));

        // Wer Vertriebsleitung und Consultant zugleich ist, gibt trotzdem nur einmal frei.
        var beides = await DiensteAsync(new TestBenutzer(leitungsBenutzer.Name, Rollen.Vertriebsleitung, Rollen.Consultant));
        var vierAugen = await Assert.ThrowsAsync<InvalidOperationException>(() => beides.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.SolutionConsultant, stand, null));
        Assert.Contains("verschiedenen Personen", vierAugen.Message, StringComparison.Ordinal);

        await consultant.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.SolutionConsultant, stand, null);
        var freigegeben = (await vertrieb.Kalkulationen.LadeAsync(id))!.Kalkulation;
        Assert.True(freigegeben.IstVertriebsfreigegeben);
        Assert.Equal("passt", freigegeben.AktiveFreigabe(FreigabeRolle.Vertriebsleitung)!.Kommentar);

        // Nur wer freigegeben hat, zieht zurück.
        var andererConsultant = await DiensteAsync(Neu(Rollen.Consultant));
        await Assert.ThrowsAsync<InvalidOperationException>(() => andererConsultant.Kalkulationen.VertriebsfreigabeZurueckziehenAsync(id, FreigabeRolle.SolutionConsultant));
        await consultant.Kalkulationen.VertriebsfreigabeZurueckziehenAsync(id, FreigabeRolle.SolutionConsultant);
        var zurueck = (await vertrieb.Kalkulationen.LadeAsync(id))!.Kalkulation;
        Assert.False(zurueck.IstVertriebsfreigegeben);
        Assert.Equal("zurückgezogen", zurueck.Vertriebsfreigaben.Single(f => !f.IstAktiv).Aufhebungsgrund);
    }

    [Fact]
    public async Task Verantwortliche_geben_ihre_eigene_Kalkulation_nicht_frei()
    {
        var leitungsBenutzer = Neu(Rollen.Vertriebsleitung);
        var leitung = await DiensteAsync(leitungsBenutzer);
        var projekt = await leitung.Projekte.AnlegenAsync(new NeuesKundenprojekt("Eigenes Projekt GmbH", null, null, null, null, null, "MS", false, null, null));
        var id = await leitung.Projekte.NeueKalkulationAsync(projekt, "Variante A");
        var geladen = await leitung.Kalkulationen.LadeAsync(id);
        await leitung.Kalkulationen.SpeichernAsync(id, "Variante A", null, Rk01, geladen!.Kalkulation.Zeilenversion);

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await leitung.Kalkulationen.VertriebFreigebenAsync(id, FreigabeRolle.Vertriebsleitung, (await leitung.Kalkulationen.LadeAsync(id))!.Stand, null));

        Assert.Contains("verantwortet", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aenderungen_am_Arbeitsstand_heben_die_Freigaben_auf()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);
        var geladen = (await dienste.Kalkulationen.LadeAsync(id))!;

        // Speichern ohne inhaltliche Änderung (nur Titel) lässt die Freigaben stehen.
        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A (final)", new DateOnly(2027, 1, 1), Rk01, geladen.Kalkulation.Zeilenversion);
        geladen = (await dienste.Kalkulationen.LadeAsync(id))!;
        Assert.True(geladen.Kalkulation.IstVertriebsfreigegeben);

        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A (final)", new DateOnly(2027, 1, 1), Rk01 with { AnzahlUser = 16 }, geladen.Kalkulation.Zeilenversion);
        geladen = (await dienste.Kalkulationen.LadeAsync(id))!;
        Assert.False(geladen.Kalkulation.IstVertriebsfreigegeben);
        Assert.All(geladen.Kalkulation.Vertriebsfreigaben, f => Assert.Equal("Arbeitsstand geändert", f.Aufhebungsgrund));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.ErzeugenAsync(id, null, null));

        await FreigebenAsync(id);
        await dienste.Kalkulationen.SonderpositionHinzufuegenAsync(id, new SonderpositionsDaten("Sonderreport", "Monat", 1, 50m, "Kundenwunsch", false));
        geladen = (await dienste.Kalkulationen.LadeAsync(id))!;
        Assert.False(geladen.Kalkulation.IstVertriebsfreigegeben);
        Assert.Equal(2, geladen.Kalkulation.Vertriebsfreigaben.Count(f => f.Aufhebungsgrund == "Sonderposition geändert"));

        // Mit offener Sonderposition ist die Kalkulation nicht angebotsfähig und lässt sich nicht freigeben.
        await Assert.ThrowsAsync<KalkulationNichtAngebotsfaehigException>(() => FreigebenAsync(id));
    }

    [Fact]
    public async Task Angebotsnummern_sind_fortlaufend_und_eindeutig()
    {
        var a = await KalkulationAsync(Rk01);
        var b = await KalkulationAsync(Rk01);

        await Task.WhenAll(a.Dienste.Angebote.ErzeugenAsync(a.Kalkulation, null, null), b.Dienste.Angebote.ErzeugenAsync(b.Kalkulation, null, null));

        var nummerA = (await a.Dienste.Angebote.ListeAsync(a.Kalkulation)).Single().Nummer;
        var nummerB = (await b.Dienste.Angebote.ListeAsync(b.Kalkulation)).Single().Nummer;
        Assert.NotEqual(nummerA, nummerB);
        Assert.Equal(1, Math.Abs(int.Parse(nummerA[^4..], System.Globalization.CultureInfo.InvariantCulture) - int.Parse(nummerB[^4..], System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Fehlerhafte_Kalkulation_erzeugt_kein_Angebot_und_verbraucht_keine_Nummer()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01 with { Positionen = [new("B01", 10)] }, freigeben: false);

        await Assert.ThrowsAsync<KalkulationNichtAngebotsfaehigException>(() => FreigebenAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.ErzeugenAsync(id, null, null));

        Assert.Empty(await dienste.Angebote.ListeAsync(id));
        Assert.Null((await dienste.Kalkulationen.LadeAsync(id))!.Kalkulation.Angebotsnummer);
    }

    [Fact]
    public async Task Versandvermerk_setzt_den_Projektstatus()
    {
        var (projekt, id, dienste) = await KalkulationAsync(Rk01);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);

        await dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute);

        Assert.Equal(dienste.Angebote.Heute, (await dienste.Angebote.ListeAsync(id)).Single().VersendetAm);
        var geladen = await dienste.Projekte.LadeAsync(projekt);
        Assert.Equal(ProjektStatus.AngebotVersendet, geladen!.Status);
        Assert.Contains("versendet am", geladen.StatusEreignisse.OrderBy(e => e.Zeitpunkt).Last().Kommentar, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute));
    }

    [Fact]
    public async Task Gewonnen_nur_mit_einem_versendeten_Angebot_des_Projekts()
    {
        var (projekt, id, dienste) = await KalkulationAsync(Rk01);
        var entwurf = await dienste.Angebote.ErzeugenAsync(id, null, null);
        var versendet = await dienste.Angebote.ErzeugenAsync(id, null, null);
        var fremd = await KalkulationAsync(Rk01);
        var fremdesAngebot = await fremd.Dienste.Angebote.ErzeugenAsync(fremd.Kalkulation, null, null);
        await fremd.Dienste.Angebote.AlsVersendetMarkierenAsync(fremdesAngebot, fremd.Dienste.Angebote.Heute);
        await dienste.Angebote.AlsVersendetMarkierenAsync(versendet, dienste.Angebote.Heute);

        var auswahl = Assert.Single(await dienste.Projekte.AngeboteZurAnnahmeAsync(projekt));
        Assert.Equal((versendet, 2), (auswahl.Id, auswahl.Version));
        Assert.Equal(1027.20m, auswahl.SummeMonatlich);

        await Assert.ThrowsAsync<ArgumentException>(() => dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, null, null, entwurf));
        await Assert.ThrowsAsync<ArgumentException>(() => dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, null, null, fremdesAngebot));

        await dienste.Projekte.SetzeStatusAsync(projekt, ProjektStatus.Gewonnen, "Zusage per Mail", null, versendet);

        var geladen = await dienste.Projekte.LadeAsync(projekt);
        Assert.Equal((ProjektStatus.Gewonnen, versendet), (geladen!.Status, geladen.AngenommenesAngebotId));
    }

    [Fact]
    public async Task Nur_Bearbeiter_erzeugen_Angebote_Lesende_duerfen_herunterladen()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);

        var consultant = await DiensteAsync(Neu(Rollen.Consultant));
        var fremd = await DiensteAsync(Neu(Rollen.Vertrieb));

        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Angebote.ErzeugenAsync(id, null, null));
        Assert.NotEmpty((await consultant.Angebote.DateiAsync(angebot)).Inhalt);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.Angebote.DateiAsync(angebot));
        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Angebote.AlsVersendetMarkierenAsync(angebot, consultant.Angebote.Heute));
    }

    [Fact]
    public async Task Archivierte_Angebote_sind_unveraenderlich()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);
        var angebotId = await dienste.Angebote.ErzeugenAsync(id, null, null);

        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        var datei = await kontext.Set<AngebotsDatei>().SingleAsync(d => d.AngebotId == angebotId);
        kontext.Entry(datei).Property(d => d.Inhalt).CurrentValue = [1, 2, 3];
        await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());

        await using var loeschen = db.NeuerKontextAufDatenbank(Datenbank);
        loeschen.Remove(await loeschen.Angebote.SingleAsync(a => a.Id == angebotId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loeschen.SaveChangesAsync());
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
