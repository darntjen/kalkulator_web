using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Tests;

/// <summary>
/// Kundenprojekte, Kalkulationen und eingefrorene Versionen gegen eine echte Datenbank mit dem Katalog der
/// Erstbefüllung. Die Preisliste wird einmal freigegeben; alle Tests teilen sich diese Datenbank.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class KalkulationsSpeicherTests(SqlServerFixture db)
{
    private const string Datenbank = "Kalkulationen";
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Stichtag = new(2026, 10, 1);
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _befuellt;

    /// <summary>RK-01: Kleinkunde mit Connect Standard, 1.027,20 € monatlich und 900,00 € Onboarding.</summary>
    private static readonly KalkulationsEingabe Rk01 = new()
    {
        Positionen = [new("S01-STD", 1), new("B01", 15), new("B03", 2), new("B05", 1)],
        AnzahlUser = 15,
        ServerBackup = new ServerBackupEingabe(BackupVariante.Cloud, 2, 400),
        Supportkontingent = new SupportkontingentEingabe(4, 1.5m),
        BisherigerMonatspreis = 900m,
    };

    private async Task<KalkulatorDbContext> KontextAsync()
    {
        await Sperre.WaitAsync();
        try
        {
            if (!_befuellt)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                await KatalogErstbefuellung.AusfuehrenAsync(kontext);
                var preisliste = await kontext.Preislisten.SingleAsync();
                preisliste.Freigeben("produktmanagement", Jetzt);
                await kontext.SaveChangesAsync();
                _befuellt = true;
            }
        }
        finally
        {
            Sperre.Release();
        }

        return db.NeuerKontextAufDatenbank(Datenbank);
    }

    private static Kundenprojekt NeuesProjekt(string titel) =>
        Kundenprojekt.Anlegen(
            new Kunde { Firma = "Muster GmbH " + titel, Ort = "Oldenburg" },
            titel, "vertrieb@noesse.de", bestandskunde: true, "vertrieb@noesse.de", Jetzt);

    [Fact]
    public async Task Kundenprojekt_mit_Arbeitsstand_wird_vollstaendig_gespeichert_und_geladen()
    {
        int id;
        await using (var kontext = await KontextAsync())
        {
            var projekt = NeuesProjekt("Speichern");
            projekt.SetzeForecast(70, new DateOnly(2026, 11, 20));
            var neu = projekt.NeueKalkulation("Variante A", "vertrieb@noesse.de", Jetzt);
            neu.Vertragsbeginn = new DateOnly(2027, 1, 1);
            neu.AendereEingabe(Rk01 with { CloudServer = new CloudServerEingabe(412.37m, CloudBackup.ServerBackup) });
            neu.SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch");
            projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "vertrieb@noesse.de", Jetzt.AddDays(1));
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            id = projekt.Id;
        }

        await using var lesen = await KontextAsync();
        var geladen = await lesen.Kundenprojekte
            .Include(p => p.Kunde)
            .Include(p => p.StatusEreignisse)
            .Include(p => p.Kalkulationen).ThenInclude(k => k.Sonderpositionen)
            .SingleAsync(p => p.Id == id);

        Assert.Equal("Oldenburg", geladen.Kunde!.Ort);
        Assert.Equal(ProjektStatus.AngebotVersendet, geladen.Status);
        Assert.Equal([ProjektStatus.Entwurf, ProjektStatus.AngebotVersendet], geladen.StatusEreignisse.OrderBy(e => e.Zeitpunkt).Select(e => e.Neu));
        Assert.Equal(new DateOnly(2026, 11, 1), geladen.ErwarteterAbschlussmonat);

        var kalkulation = Assert.Single(geladen.Kalkulationen);
        Assert.True(kalkulation.FuerForecast);
        Assert.Equal(new DateOnly(2027, 1, 1), kalkulation.Vertragsbeginn);
        Assert.Equal(Rk01.Positionen, kalkulation.Eingabe.Positionen);
        Assert.Equal(Rk01.ServerBackup, kalkulation.Eingabe.ServerBackup);
        Assert.Equal(Rk01.Supportkontingent, kalkulation.Eingabe.Supportkontingent);
        Assert.Equal(new CloudServerEingabe(412.37m, CloudBackup.ServerBackup), kalkulation.Eingabe.CloudServer);
        Assert.Equal(900m, kalkulation.Eingabe.BisherigerMonatspreis);
        Assert.Equal(Freigabestatus.Offen, Assert.Single(kalkulation.Sonderpositionen).Status);
    }

    [Fact]
    public async Task Geaenderter_Arbeitsstand_wird_erkannt_und_gespeichert()
    {
        int id;
        await using (var kontext = await KontextAsync())
        {
            var projekt = NeuesProjekt("Ändern");
            projekt.NeueKalkulation("Variante A", "v", Jetzt).AendereEingabe(Rk01);
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            id = projekt.Kalkulationen[0].Id;
        }

        await using (var kontext = await KontextAsync())
        {
            var kalkulation = await kontext.Kalkulationen.SingleAsync(k => k.Id == id);
            kalkulation.AendereEingabe(kalkulation.Eingabe with { AnzahlUser = 20, Positionen = [.. kalkulation.Eingabe.Positionen, new("S32", 20)] });
            await kontext.SaveChangesAsync();
        }

        await using var lesen = await KontextAsync();
        var geladen = await lesen.Kalkulationen.SingleAsync(k => k.Id == id);
        Assert.Equal(20, geladen.Eingabe.AnzahlUser);
        Assert.Equal(new PositionsEingabe("S32", 20), geladen.Eingabe.Positionen[^1]);
    }

    [Fact]
    public async Task Einfrieren_speichert_Positionen_Summen_und_Kosten_getrennt()
    {
        int kalkulationId;
        KalkulationsErgebnis erwartet;
        await using (var kontext = await KontextAsync())
        {
            var kern = await new RechenkernLader(kontext).LadeAsync(Stichtag, mitEinkauf: true);
            var projekt = NeuesProjekt("Einfrieren");
            var kalkulation = projekt.NeueKalkulation("Variante A", "v", Jetzt);
            kalkulation.AendereEingabe(Rk01);
            erwartet = kalkulation.Berechne(kern);

            var v1 = kalkulation.FriereEin(kern, "v", Jetzt);
            kalkulation.AendereEingabe(Rk01 with { AnzahlUser = 16, Positionen = [new("S01-STD", 1), new("B01", 16), new("B03", 2), new("B05", 1)] });
            var v2 = kalkulation.FriereEin(kern, "v", Jetzt.AddHours(1));
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();

            Assert.Equal(("V1", "V2"), (v1.Bezeichnung, v2.Bezeichnung));
            kalkulationId = kalkulation.Id;
        }

        await using var lesen = await KontextAsync();
        var versionen = await lesen.Kalkulationsversionen
            .Include(v => v.Positionen)
            .Where(v => v.KalkulationId == kalkulationId)
            .OrderBy(v => v.Nummer)
            .ToListAsync();

        Assert.Equal([1, 2], versionen.Select(v => v.Nummer));
        var v1Geladen = versionen[0];
        Assert.Equal(erwartet.SummeMonatlich, v1Geladen.SummeMonatlich);
        Assert.Equal(erwartet.SummeEinmalig, v1Geladen.SummeEinmalig);
        Assert.Equal(15, v1Geladen.Eingabe.AnzahlUser);
        Assert.Equal(16, versionen[1].Eingabe.AnzahlUser);
        Assert.Equal(erwartet.Positionen.Select(p => (p.Code, p.Menge, p.BerechneteMenge, p.Einzelpreis, p.Betrag, p.Herkunft)),
            v1Geladen.Positionen.OrderBy(p => p.Reihenfolge).Select(p => (p.Code, p.Menge, p.BerechneteMenge, p.Einzelpreis, p.Betrag, p.Herkunft)));

        // Kosten liegen im Schema „intern“ und kommen nur mit, wenn ausdrücklich geladen.
        Assert.All(v1Geladen.Positionen, p => Assert.Null(p.Kosten));
        var kosten = await lesen.Kalkulationsversionen
            .Where(v => v.Id == v1Geladen.Id)
            .SelectMany(v => v.Positionen)
            .SumAsync(p => p.Kosten!.Kosten ?? 0m);
        Assert.Equal(erwartet.Positionen.Sum(p => p.Kosten ?? 0m), kosten);
        Assert.True(kosten > 0);
    }

    [Fact]
    public async Task Eingefrorene_Versionen_sind_unveraenderlich()
    {
        int versionId;
        await using (var kontext = await KontextAsync())
        {
            var kern = await new RechenkernLader(kontext).LadeAsync(Stichtag, mitEinkauf: true);
            var projekt = NeuesProjekt("Unveränderlich");
            var kalkulation = projekt.NeueKalkulation("Variante A", "v", Jetzt);
            kalkulation.AendereEingabe(Rk01);
            var version = kalkulation.FriereEin(kern, "v", Jetzt);
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            versionId = version.Id;
        }

        await using (var kontext = await KontextAsync())
        {
            var position = await kontext.Kalkulationsversionen.Where(v => v.Id == versionId).SelectMany(v => v.Positionen).FirstAsync();
            kontext.Entry(position).Property(p => p.Betrag).CurrentValue = 1m;
            await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
        }

        await using (var kontext = await KontextAsync())
        {
            var version = await kontext.Kalkulationsversionen.SingleAsync(v => v.Id == versionId);
            kontext.Remove(version);
            await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Einfrieren_verlangt_freigegebene_Sonderpositionen()
    {
        await using var kontext = await KontextAsync();
        var kern = await new RechenkernLader(kontext).LadeAsync(Stichtag, mitEinkauf: true);
        var kalkulation = NeuesProjekt("Sonderposition").NeueKalkulation("Variante A", "v", Jetzt);
        kalkulation.AendereEingabe(Rk01);
        var sonder = kalkulation.SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch");

        var fehler = Assert.Throws<KalkulationNichtAngebotsfaehigException>(() => kalkulation.FriereEin(kern, "v", Jetzt));
        Assert.Contains("Freigabe", fehler.Message, StringComparison.Ordinal);
        Assert.Equal(0, kalkulation.LetzteVersionsnummer);

        sonder.Freigeben("vertriebsleitung", Jetzt);
        var version = kalkulation.FriereEin(kern, "v", Jetzt);

        Assert.Contains(version.Positionen, p => p.Herkunft == PositionsHerkunft.Sonderposition && p.Betrag == 50m);
    }

    [Fact]
    public async Task Einfrieren_geht_nur_mit_freigegebener_Preisliste()
    {
        var katalog = KatalogErstbefuellung.ErzeugeKatalog();
        var kern = new Rechenkern(katalog.Services, katalog.Preisliste);
        var kalkulation = NeuesProjekt("Entwurf").NeueKalkulation("Variante A", "v", Jetzt);
        kalkulation.AendereEingabe(Rk01);

        Assert.Equal(PreislistenStatus.Entwurf, katalog.Preisliste.Status);
        Assert.Throws<InvalidOperationException>(() => kalkulation.FriereEin(kern, "v", Jetzt));

        // Ohne freigegebene Preisliste zum Stichtag gibt es auch keinen Rechenkern aus der Datenbank.
        await using var kontext = await KontextAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RechenkernLader(kontext).LadeAsync(new DateOnly(2026, 1, 1), false));
    }

    [Fact]
    public async Task Ohne_Einkauf_geladen_bleiben_die_Kosten_leer()
    {
        await using var kontext = await KontextAsync();
        var ohne = await new RechenkernLader(kontext).LadeAsync(Stichtag, mitEinkauf: false);

        var ergebnis = ohne.Berechne(Rk01);

        Assert.False(ergebnis.HatFehler, string.Join(" ", ergebnis.Meldungen.Select(m => m.Text)));
        Assert.All(ergebnis.Positionen.Where(p => p.Betrag != 0), p => Assert.Null(p.Kosten));
        Assert.False(ergebnis.KostenVollstaendig);
    }

    [Fact]
    public async Task Forecast_Markierung_wechselt_zwischen_Varianten()
    {
        int projektId;
        await using (var kontext = await KontextAsync())
        {
            var projekt = NeuesProjekt("Varianten");
            var a = projekt.NeueKalkulation("Variante A", "v", Jetzt);
            a.AendereEingabe(Rk01);
            a.Duplizieren("Variante B", "v", Jetzt);
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            projektId = projekt.Id;
        }

        await using (var kontext = await KontextAsync())
        {
            var projekt = await kontext.Kundenprojekte.Include(p => p.Kalkulationen).SingleAsync(p => p.Id == projektId);
            projekt.FuerForecastMarkieren(projekt.Kalkulationen.Single(k => k.Titel == "Variante B"));
            await kontext.SaveChangesAsync();
        }

        await using var lesen = await KontextAsync();
        var markiert = await lesen.Kalkulationen.Where(k => k.KundenprojektId == projektId && k.FuerForecast).Select(k => k.Titel).ToListAsync();
        Assert.Equal(["Variante B"], markiert);
    }

    [Fact]
    public async Task Gleichzeitige_Aenderungen_an_einer_Kalkulation_werden_erkannt()
    {
        int id;
        await using (var kontext = await KontextAsync())
        {
            var projekt = NeuesProjekt("Parallel");
            projekt.NeueKalkulation("Variante A", "v", Jetzt).AendereEingabe(Rk01);
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            id = projekt.Kalkulationen[0].Id;
        }

        await using var erster = await KontextAsync();
        await using var zweiter = await KontextAsync();
        var k1 = await erster.Kalkulationen.SingleAsync(k => k.Id == id);
        var k2 = await zweiter.Kalkulationen.SingleAsync(k => k.Id == id);

        k1.AendereEingabe(k1.Eingabe with { AnzahlUser = 30 });
        await erster.SaveChangesAsync();
        k2.AendereEingabe(k2.Eingabe with { AnzahlUser = 40 });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => zweiter.SaveChangesAsync());
    }

    [Fact]
    public async Task Statuswechsel_landen_im_Aenderungsprotokoll()
    {
        int id;
        await using (var kontext = await KontextAsync())
        {
            var projekt = NeuesProjekt("Protokoll");
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
            id = projekt.Id;
        }

        await using (var kontext = await KontextAsync())
        {
            var projekt = await kontext.Kundenprojekte.SingleAsync(p => p.Id == id);
            projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "vertrieb@noesse.de", Jetzt);
            await kontext.SaveChangesAsync();
        }

        await using var lesen = await KontextAsync();
        var eintraege = await lesen.Aenderungsprotokoll
            .Where(e => e.Entitaet == nameof(Kundenprojekt) && e.Schluessel == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToListAsync();
        Assert.Contains(eintraege, e => e.Aktion == "Geändert" && e.Aenderungen!.Contains("AngebotVersendet", StringComparison.Ordinal));
    }
}
