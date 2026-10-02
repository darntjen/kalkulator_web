using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>
/// Kalkulationseditor gegen eine Datenbank mit dem Katalog der Erstbefüllung. Die Preisliste bleibt im Entwurf,
/// so wie nach der Erstbefüllung; der Editor rechnet dann mit dem Entwurf.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class KalkulationsDienstTests(SqlServerFixture db)
{
    private const string Datenbank = "Editor";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _befuellt;

    private async Task<(KundenprojektDienst Projekte, KalkulationsDienst Kalkulationen)> DiensteAsync(TestBenutzer benutzer)
    {
        await Sperre.WaitAsync();
        try
        {
            if (!_befuellt)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                await KatalogErstbefuellung.AusfuehrenAsync(kontext);
                _befuellt = true;
            }
        }
        finally
        {
            Sperre.Release();
        }

        var fabrik = new Fabrik(() => db.NeuerKontextAufDatenbank(Datenbank, benutzer));
        return (new KundenprojektDienst(fabrik, benutzer, TimeProvider.System), new KalkulationsDienst(fabrik, benutzer, TimeProvider.System));
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private async Task<(int KalkulationId, TestBenutzer Vertrieb)> NeueKalkulationAsync()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var (projekte, _) = await DiensteAsync(vertrieb);
        var projekt = await projekte.AnlegenAsync(new NeuesKundenprojekt("Editor GmbH", null, null, null, null, null, "MS", false, null, null));
        return (await projekte.NeueKalkulationAsync(projekt, "Variante A"), vertrieb);
    }

    private static readonly KalkulationsEingabe Rk01 = new()
    {
        Positionen = [new("S01-STD", 1), new("B01", 15), new("B03", 2), new("B05", 1)],
        AnzahlUser = 15,
    };

    [Fact]
    public async Task Katalog_nutzt_ohne_Freigabe_den_Entwurf_und_EK_nur_fuer_die_Fuehrung()
    {
        var (_, vertrieb) = await DiensteAsync(Neu(Rollen.Vertrieb));
        var (_, fuehrung) = await DiensteAsync(Neu(Rollen.Fuehrung));
        var (_, pm) = await DiensteAsync(Neu(Rollen.Produktmanagement));

        var ohneEk = await vertrieb.KatalogAsync();
        var mitEk = await fuehrung.KatalogAsync();

        Assert.False(ohneEk.PreislisteFreigegeben);
        Assert.Equal(PreislistenStatus.Entwurf, ohneEk.Preisliste.Status);
        Assert.Empty(ohneEk.Preisliste.EkPositionen);
        Assert.NotEmpty(mitEk.Preisliste.EkPositionen);
        Assert.All(ohneEk.Services, s => Assert.NotNull(s.Kategorie));

        var ergebnisVertrieb = ohneEk.ErzeugeRechenkern().Berechne(Rk01);
        var ergebnisFuehrung = mitEk.ErzeugeRechenkern().Berechne(Rk01);
        Assert.Equal(1027.20m, ergebnisVertrieb.SummeMonatlich);
        Assert.False(ergebnisVertrieb.KostenVollstaendig);
        Assert.Equal(397.44m, ergebnisFuehrung.KostenMonatlich);
        await Assert.ThrowsAsync<KeinZugriffException>(() => pm.KatalogAsync());
    }

    [Fact]
    public async Task Arbeitsstand_speichern_und_parallele_Aenderung_erkennen()
    {
        var (id, vertrieb) = await NeueKalkulationAsync();
        var (_, dienst) = await DiensteAsync(vertrieb);
        var geladen = await dienst.LadeAsync(id);
        Assert.True(geladen!.DarfBearbeiten);
        Assert.False(geladen.DarfEinkaufSehen);

        var neueVersion = await dienst.SpeichernAsync(id, "Variante A (15 User)", new DateOnly(2027, 1, 1), Rk01, geladen.Kalkulation.Zeilenversion);

        var nachher = await dienst.LadeAsync(id);
        Assert.Equal("Variante A (15 User)", nachher!.Kalkulation.Titel);
        Assert.Equal(new DateOnly(2027, 1, 1), nachher.Kalkulation.Vertragsbeginn);
        Assert.Equal(Rk01.Positionen, nachher.Kalkulation.Eingabe.Positionen);
        Assert.Equal(neueVersion, nachher.Kalkulation.Zeilenversion);

        // Wer noch auf dem alten Stand arbeitet, überschreibt nichts.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            dienst.SpeichernAsync(id, "veraltet", null, Rk01 with { AnzahlUser = 99 }, geladen.Kalkulation.Zeilenversion));
        Assert.Equal(15, (await dienst.LadeAsync(id))!.Kalkulation.Eingabe.AnzahlUser);
    }

    [Theory]
    [InlineData(Rollen.Consultant)]
    [InlineData(Rollen.Fuehrung)]
    public async Task Lesende_Rollen_sehen_die_Kalkulation_speichern_aber_nicht(string rolle)
    {
        var (id, _) = await NeueKalkulationAsync();
        var (_, dienst) = await DiensteAsync(Neu(rolle));

        var geladen = await dienst.LadeAsync(id);

        Assert.False(geladen!.DarfBearbeiten);
        Assert.Equal(rolle == Rollen.Fuehrung, geladen.DarfEinkaufSehen);
        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.SpeichernAsync(id, "x", null, Rk01, geladen.Kalkulation.Zeilenversion));
    }

    [Fact]
    public async Task Fremder_Vertrieb_sieht_die_Kalkulation_nicht()
    {
        var (id, _) = await NeueKalkulationAsync();
        var (_, fremd) = await DiensteAsync(Neu(Rollen.Vertrieb));

        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.LadeAsync(id));
    }

    [Fact]
    public async Task Sonderpositionen_pflegt_der_Vertrieb_freigeben_darf_nur_die_Vertriebsleitung()
    {
        var (id, vertrieb) = await NeueKalkulationAsync();
        var (_, dienst) = await DiensteAsync(vertrieb);
        var (_, leitung) = await DiensteAsync(Neu(Rollen.Vertriebsleitung));
        var daten = new SonderpositionsDaten("Sonderreport", "Monat", 1, 50m, "Kundenwunsch", false);

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.SonderpositionHinzufuegenAsync(id, daten with { Begruendung = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.SonderpositionHinzufuegenAsync(id, daten with { Preis = 0m }));
        var liste = await dienst.SonderpositionHinzufuegenAsync(id, daten);
        var position = Assert.Single(liste);
        await dienst.SonderpositionHinzufuegenAsync(id, daten with { Bezeichnung = "Schulung", Einmalig = true, Preis = 400m });

        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.SonderpositionFreigebenAsync(id, position.Id, null));
        liste = await leitung.SonderpositionFreigebenAsync(id, position.Id, "passt");
        Assert.Equal(Freigabestatus.Freigegeben, liste[0].Status);
        Assert.Equal("passt", liste[0].Kommentar);

        // Ändert der Vertrieb den Preis, ist die Freigabe wieder offen.
        liste = await dienst.SonderpositionAendernAsync(id, position.Id, daten with { Preis = 45m });
        Assert.Equal(Freigabestatus.Offen, liste[0].Status);

        await Assert.ThrowsAsync<ArgumentException>(() => leitung.SonderpositionAblehnenAsync(id, position.Id, " "));
        liste = await leitung.SonderpositionAblehnenAsync(id, position.Id, "Preis zu niedrig");
        Assert.Equal(Freigabestatus.Abgelehnt, liste[0].Status);

        liste = await dienst.SonderpositionEntfernenAsync(id, liste[1].Id);
        Assert.Equal("Sonderreport", Assert.Single(liste).Bezeichnung);
        Assert.Single((await dienst.LadeAsync(id))!.Kalkulation.Sonderpositionen);
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
