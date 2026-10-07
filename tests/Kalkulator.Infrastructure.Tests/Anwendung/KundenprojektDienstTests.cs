using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Rechtematrix und Abläufe des <see cref="KundenprojektDienst"/> gegen eine echte Datenbank.</summary>
[Collection(DatenbankSammlung.Name)]
public class KundenprojektDienstTests(SqlServerFixture db)
{
    private const string Datenbank = "Projekte";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _migriert;

    private async Task<KundenprojektDienst> DienstAsync(TestBenutzer benutzer)
    {
        await Sperre.WaitAsync();
        try
        {
            if (!_migriert)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                _migriert = true;
            }
        }
        finally
        {
            Sperre.Release();
        }

        return new KundenprojektDienst(new Fabrik(() => db.NeuerKontextAufDatenbank(Datenbank, benutzer)), benutzer, TimeProvider.System);
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private static NeuesKundenprojekt Projekt(string firma, string? navision = null) =>
        new(firma, "Hauptstraße 1", "26122", "Oldenburg", "Frau Muster", navision, "Managed Services 2027", false, 50, new DateOnly(2026, 12, 15));

    [Fact]
    public async Task Vertrieb_legt_an_und_sieht_nur_eigene_Projekte()
    {
        var anna = Neu(Rollen.Vertrieb);
        var bernd = Neu(Rollen.Vertrieb);
        var annaId = await (await DienstAsync(anna)).AnlegenAsync(Projekt("Anna GmbH " + anna.Name));
        var berndId = await (await DienstAsync(bernd)).AnlegenAsync(Projekt("Bernd KG " + bernd.Name));

        var annasDienst = await DienstAsync(anna);
        var liste = await annasDienst.ListeAsync();

        Assert.Contains(liste, p => p.Id == annaId);
        Assert.DoesNotContain(liste, p => p.Id == berndId);
        var projekt = await annasDienst.LadeAsync(annaId);
        Assert.Equal(anna.Name, projekt!.Verantwortlich);
        Assert.Equal(new DateOnly(2026, 12, 1), projekt.ErwarteterAbschlussmonat);
        Assert.Equal(anna.Name, Assert.Single(projekt.StatusEreignisse).Benutzer);
        await Assert.ThrowsAsync<KeinZugriffException>(() => annasDienst.LadeAsync(berndId));
        await Assert.ThrowsAsync<KeinZugriffException>(() => annasDienst.SetzeForecastAsync(berndId, 90, null));
    }

    [Fact]
    public async Task Vertriebsleitung_sieht_und_bearbeitet_alle_Projekte()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var id = await (await DienstAsync(vertrieb)).AnlegenAsync(Projekt("Leitung GmbH"));
        var leitung = await DienstAsync(Neu(Rollen.Vertriebsleitung));

        Assert.Contains(await leitung.ListeAsync(), p => p.Id == id);
        await leitung.SetzeStatusAsync(id, ProjektStatus.AngebotVersendet, "an GF geschickt", null);

        var projekt = await leitung.LadeAsync(id);
        Assert.Equal(ProjektStatus.AngebotVersendet, projekt!.Status);
        Assert.Equal(vertrieb.Name, projekt.Verantwortlich);
    }

    [Theory]
    [InlineData(Rollen.Consultant)]
    [InlineData(Rollen.Fuehrung)]
    public async Task Consultant_und_Fuehrung_sehen_alles_aendern_aber_nichts(string rolle)
    {
        var id = await (await DienstAsync(Neu(Rollen.Vertrieb))).AnlegenAsync(Projekt("Lesen AG"));
        var dienst = await DienstAsync(Neu(rolle));

        Assert.Contains(await dienst.ListeAsync(), p => p.Id == id);
        Assert.NotNull(await dienst.LadeAsync(id));
        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.AnlegenAsync(Projekt("Neu AG")));
        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.SetzeForecastAsync(id, 10, null));
        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.NeueKalkulationAsync(id, "Variante"));
    }

    [Theory]
    [InlineData(Rollen.Produktmanagement)] // Der Admin sieht seit 07.10.2026 alles (BerechtigungTests).
    public async Task Ohne_passende_Rolle_gibt_es_keine_Kundenprojekte(string rolle)
    {
        var dienst = await DienstAsync(Neu(rolle));

        await Assert.ThrowsAsync<KeinZugriffException>(() => dienst.ListeAsync());
    }

    [Fact]
    public async Task Kunde_mit_gleicher_Navision_Nummer_wird_wiederverwendet()
    {
        var nummer = "K" + Random.Shared.Next(100000, 999999);
        var dienst = await DienstAsync(Neu(Rollen.Vertrieb));
        var erstes = await dienst.AnlegenAsync(Projekt("Wiederkehr GmbH", nummer));
        var zweites = await dienst.AnlegenAsync(Projekt("Wiederkehr GmbH (Tippfehler)", nummer) with { Titel = "Zweites Vorhaben" });

        var a = await dienst.LadeAsync(erstes);
        var b = await dienst.LadeAsync(zweites);
        Assert.Equal(a!.KundeId, b!.KundeId);
        Assert.Equal("Wiederkehr GmbH", b.Kunde!.Firma);
    }

    [Fact]
    public async Task Pflichtfelder_und_Statusregeln_werden_geprueft()
    {
        var dienst = await DienstAsync(Neu(Rollen.Vertrieb));

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.AnlegenAsync(Projekt(" ")));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.AnlegenAsync(Projekt("Firma") with { Titel = "" }));

        var id = await dienst.AnlegenAsync(Projekt("Status GmbH"));
        await Assert.ThrowsAsync<UngueltigerStatuswechselException>(() => dienst.SetzeStatusAsync(id, ProjektStatus.Gewonnen, null, null));
        await dienst.SetzeStatusAsync(id, ProjektStatus.AngebotVersendet, null, null);
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.SetzeStatusAsync(id, ProjektStatus.Verloren, null, null));
        await dienst.SetzeStatusAsync(id, ProjektStatus.Verloren, "zu teuer", Verlustgrund.Preis);

        var projekt = await dienst.LadeAsync(id);
        Assert.Equal(Verlustgrund.Preis, projekt!.Verlustgrund);
        Assert.Equal(3, projekt.StatusEreignisse.Count);
    }

    [Fact]
    public async Task Varianten_anlegen_duplizieren_und_fuer_Forecast_markieren()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var dienst = await DienstAsync(vertrieb);
        var id = await dienst.AnlegenAsync(Projekt("Varianten GmbH"));

        var a = await dienst.NeueKalkulationAsync(id, "Variante A");
        var b = await dienst.DuplizierenAsync(a, "Variante B");
        await dienst.FuerForecastMarkierenAsync(b);

        var projekt = await dienst.LadeAsync(id);
        Assert.Equal(["Variante A", "Variante B"], projekt!.Kalkulationen.OrderBy(k => k.Id).Select(k => k.Titel));
        Assert.Equal("Variante B", Assert.Single(projekt.Kalkulationen, k => k.FuerForecast).Titel);
        Assert.Equal(2, (await dienst.ListeAsync()).Single(p => p.Id == id).Kalkulationen);

        var fremd = await DienstAsync(Neu(Rollen.Vertrieb));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.DuplizierenAsync(a, "Kopie"));
    }

    [Fact]
    public async Task Suche_findet_Firma_Titel_und_Kundennummer()
    {
        var dienst = await DienstAsync(Neu(Rollen.Vertrieb));
        var nummer = "S" + Random.Shared.Next(100000, 999999);
        var id = await dienst.AnlegenAsync(Projekt("Suchbare Spedition GmbH", nummer));

        Assert.Contains(await dienst.ListeAsync("Spedition"), p => p.Id == id);
        Assert.Contains(await dienst.ListeAsync(nummer), p => p.Id == id);
        Assert.DoesNotContain(await dienst.ListeAsync("gibt-es-nicht"), p => p.Id == id);
    }

    internal sealed class TestBenutzer(string name, params string[] rollen) : IBenutzerKontext
    {
        public string Name => name;

        public bool IstInRolle(string rolle) => rollen.Contains(rolle);
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
