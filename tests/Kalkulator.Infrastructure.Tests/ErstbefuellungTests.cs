using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Tests;

/// <summary>
/// Prüft die Erstbefüllung gegen die Quellen (docs/06_ist-analyse.md, EK-Kalkulation V1.0).
/// Alle Tests teilen sich eine eigene, einmal befüllte Datenbank.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class ErstbefuellungTests(SqlServerFixture db)
{
    private const string Datenbank = "Erstbefuellung";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static ErstbefuellungsErgebnis? _ersterLauf;

    private async Task<KalkulatorDbContext> BefuellteDatenbankAsync()
    {
        await Sperre.WaitAsync();
        try
        {
            if (_ersterLauf is null)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                _ersterLauf = await KatalogErstbefuellung.AusfuehrenAsync(kontext);
            }
        }
        finally
        {
            Sperre.Release();
        }

        return db.NeuerKontextAufDatenbank(Datenbank);
    }

    private static async Task<(Dictionary<string, Service> Services, Preisliste Preisliste)> LadeAsync(KalkulatorDbContext kontext)
    {
        // Alle Services mit Komponenten und Bestandteilen laden; EF verknüpft verschachtelte Bundles dann selbst.
        var services = await kontext.Services
            .Include(s => s.Preiskomponenten)
            .Include(s => s.Bestandteile)
            .Include(s => s.Regeln).ThenInclude(r => r.Ziele)
            .Include(s => s.Leistungsschein)
            .AsSplitQuery()
            .ToDictionaryAsync(s => s.Code);
        var preisliste = await kontext.Preislisten
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .AsSplitQuery() // sonst ein Kreuzprodukt aller Unterlisten in einer Abfrage
            .SingleAsync();
        return (services, preisliste);
    }

    private static Preiskomponente Komponente(Dictionary<string, Service> services, string code) =>
        services.Values.SelectMany(s => s.Preiskomponenten).Single(p => p.Code == code);

    [Fact]
    public async Task Erstbefuellung_legt_den_vollstaendigen_Katalog_an()
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.True(_ersterLauf!.Ausgefuehrt);
        Assert.Equal(40, services.Count);
        Assert.Equal(5, services.Values.Count(s => s.Vertriebsstatus == Vertriebsstatus.Zukuenftig));
        Assert.All(services.Values.Where(s => s.Vertriebsstatus == Vertriebsstatus.Zukuenftig), s => Assert.False(s.DarfAngebotenWerden));
        Assert.Equal(3, services.Values.Count(s => s.Typ == ServiceTyp.Connect));
        Assert.Equal(8, await kontext.Kategorien.CountAsync());
        Assert.Equal("Preisstand 15.07.2026", preisliste.Bezeichnung);
        Assert.Equal(PreislistenStatus.Entwurf, preisliste.Status);
    }

    [Theory]
    [InlineData("S01-STD", 249.00)]
    [InlineData("S01-PRM", 699.00)]
    [InlineData("S01-ENT", 1790.00)]
    [InlineData("B01", 31.90)]
    [InlineData("B02", 54.90)]
    [InlineData("B03", 49.90)]
    [InlineData("B04", 49.90)]
    [InlineData("B05", 199.90)]
    [InlineData("B06", 1590.00)]
    [InlineData("B07-SWI", 49.90)]
    [InlineData("B07-WIF", 41.90)]
    [InlineData("S04", 7.90)]
    [InlineData("S21", 129.90)]
    [InlineData("S24", 1190.00)]
    [InlineData("S25", 499.00)]
    [InlineData("S25-CLIENT", 12.00)]
    [InlineData("S25-SERVER", 69.00)]
    [InlineData("S31-USER", 49.00)]
    [InlineData("S31-SERVER", 79.00)]
    [InlineData("S35", 49.90)]
    [InlineData("S41-ROADMAP", 2400.00)]
    [InlineData("S60", 30.38)]
    [InlineData("S14-GRUND", 99.00)]
    [InlineData("S14-SERVER", 15.00)]
    [InlineData("S14-PAKET", 69.00)]
    [InlineData("S14-OBJEKT", 17.90)]
    [InlineData("S14-LIZENZ", 19.60)]
    public async Task Verkaufspreise_entsprechen_dem_Preisstand_vom_15_07_2026(string code, decimal vk)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.Equal(vk, preisliste.PreisFuer(Komponente(services, code).Id));
    }

    [Theory]
    [InlineData("S01-STD-ONB", 30, 900.00)]
    [InlineData("S01-STD-ONB", 31, 1400.00)]
    [InlineData("S01-PRM-ONB", 100, 2000.00)]
    [InlineData("S01-PRM-ONB", 250, 2800.00)]
    [InlineData("S01-ENT-ONB", 500, 5500.00)]
    [InlineData("S41", 50, 350.00)]
    [InlineData("S41", 51, 550.00)]
    public async Task Staffelpreise_fuer_Onboarding_und_S41(string code, int menge, decimal vk)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.Equal(vk, preisliste.StaffelpreisFuer(Komponente(services, code).Id, menge));
    }

    [Fact]
    public async Task Onboarding_ab_501_User_ist_individuell_und_Sonderrechner_haben_keinen_Festpreis()
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.Null(preisliste.StaffelpreisFuer(Komponente(services, "S01-STD-ONB").Id, 501));
        Assert.Null(preisliste.PreisFuer(Komponente(services, "S61").Id));
        Assert.Null(preisliste.PreisFuer(Komponente(services, "S14-IMPORT").Id));
    }

    [Theory]
    [InlineData(ParameterSchluessel.AeSatzEbene1, 23.75)]
    [InlineData(ParameterSchluessel.AeSatzEbene2, 33.75)]
    [InlineData(ParameterSchluessel.AeSatzEbene3, 42.50)]
    [InlineData(ParameterSchluessel.SupportkontingentBlockgroesseAe, 2)]
    [InlineData(ParameterSchluessel.SupportkontingentAeProAnfrage, 2)]
    [InlineData(ParameterSchluessel.BackupPaketgroesseGb, 500)]
    [InlineData(ParameterSchluessel.BackupPreisuntergrenze, 187)]
    [InlineData(ParameterSchluessel.CloudServerMargenteiler, 0.55)]
    [InlineData(ParameterSchluessel.VkVerrechnungssatzProStunde, 135)]
    [InlineData(ParameterSchluessel.EkKostensatzProStunde, 60.75)]
    [InlineData(ParameterSchluessel.MargeGruenAb, 0.55)]
    [InlineData(ParameterSchluessel.MargeGruenBis, 0.72)]
    [InlineData(ParameterSchluessel.MargeRotUnter, 0.45)]
    public async Task Parameter_sind_gepflegt(string schluessel, decimal wert)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (_, preisliste) = await LadeAsync(kontext);

        Assert.Equal(wert, preisliste.ParameterWert(schluessel));
    }

    [Theory]
    [InlineData("B01", new[] { "S02", "S03", "S04" })]
    [InlineData("B02", new[] { "B01", "S05", "S06", "S07" })]
    [InlineData("B03", new[] { "S11", "S12", "S13" })]
    [InlineData("B04", new[] { "B03", "S14" })]
    [InlineData("B05", new[] { "S21", "S22", "S23" })]
    [InlineData("B06", new[] { "B05", "S24", "S25" })]
    [InlineData("B07", new[] { "S51", "S52", "S53" })]
    public async Task Bundles_enthalten_die_Services_aus_ihrem_Leistungsschein(string bundle, string[] bestandteile)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, _) = await LadeAsync(kontext);

        Assert.Equal(bestandteile, services[bundle].Bestandteile.Select(b => b.Bestandteil!.Code).Order());
    }

    /// <summary>Erwartete Gesamtkosten aus „Kalkulation EK und Deckungsbeitrag V1.0“, Spalte „Gesamt“.</summary>
    [Theory]
    [InlineData("S01-STD", 75.94)]
    [InlineData("S01-PRM", 248.00)]
    [InlineData("S01-ENT", 485.88)]
    [InlineData("S02", 6.10)]
    [InlineData("S03", 5.03)]
    [InlineData("S04", 4.01)]
    [InlineData("S05", 4.75)]
    [InlineData("S06", 5.87)]
    [InlineData("S07", 2.84)]
    [InlineData("S11", 9.52)]
    [InlineData("S12", 6.03)]
    [InlineData("S13", 6.03)]
    [InlineData("S21", 40.44)]
    [InlineData("S22", 18.15)]
    [InlineData("S23", 18.15)]
    [InlineData("S24", 353.75)]
    [InlineData("S25", 211.88)]
    [InlineData("S51", 15.65)]
    [InlineData("S52", 12.61)]
    [InlineData("S53", 4.03)]
    [InlineData("S31-USER", 31.66)]
    [InlineData("S31-SERVER", 48.77)]
    [InlineData("S32", 3.20)]
    [InlineData("S33", 4.88)]
    [InlineData("B01", 13.64)]
    [InlineData("B02", 27.10)]
    [InlineData("B03", 20.08)]
    [InlineData("B05", 76.74)]
    [InlineData("B06", 642.37)]
    [InlineData("B07-SWI", 18.18)]
    [InlineData("B07-WIF", 15.14)]
    public async Task Kosten_stimmen_mit_der_EK_Kalkulation_ueberein(string code, decimal kosten)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.Equal(kosten, preisliste.KostenFuer(Komponente(services, code)));
    }

    [Theory]
    [InlineData("B04")]
    [InlineData("S14-SERVER")]
    [InlineData("S35")]
    [InlineData("S60")]
    public async Task Ohne_EK_sind_die_Kosten_unbekannt(string code)
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.Null(preisliste.KostenFuer(Komponente(services, code)));
    }

    [Fact]
    public async Task Regeln_Connect_Ausschluss_S25_und_S61_sind_angelegt()
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, _) = await LadeAsync(kontext);
        string[] Ziele(ServiceRegel r) => r.Ziele.Select(z => services.Values.Single(s => s.Id == z.ZielServiceId).Code).Order().ToArray();

        var connect = Assert.Single(services["S01-STD"].Regeln);
        Assert.Equal(RegelTyp.SchliesstAus, connect.Typ);
        Assert.Equal(["S01-ENT", "S01-PRM"], Ziele(connect));

        var s25 = Assert.Single(services["S25"].Regeln);
        Assert.Equal(RegelTyp.ErfordertEinenVon, s25.Typ);
        Assert.Equal(["B01", "B02", "B03", "B04"], Ziele(s25));

        var s61 = Assert.Single(services["S61"].Regeln);
        Assert.Equal(RegelTyp.ErfordertAlle, s61.Typ);
        Assert.Equal(["S21"], Ziele(s61));
    }

    [Fact]
    public async Task Leistungsscheine_und_Navision_Artikel_sind_zugeordnet()
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var (services, preisliste) = await LadeAsync(kontext);

        Assert.All(services.Values.Where(s => s.DarfAngebotenWerden), s => Assert.NotNull(s.Leistungsschein));
        Assert.Equal("S01", services["S01-PRM"].Leistungsschein!.Code);
        Assert.Equal("5.7", services["S14"].Leistungsschein!.Version);
        Assert.Equal("98252949", Komponente(services, "S01-STD").NavisionArtikelnummer);
        Assert.Equal("98252954", Komponente(services, "B02").NavisionArtikelnummer);

        var onboardingXs = preisliste.Staffeln.Single(s => s.PreiskomponenteId == Komponente(services, "S01-STD-ONB").Id && s.AbMenge == 1);
        Assert.Equal("98010691", onboardingXs.NavisionArtikelnummer);
        Assert.Equal("XS (bis 30 User)", onboardingXs.Bezeichnung);
    }

    [Fact]
    public async Task Ein_zweiter_Lauf_aendert_nichts()
    {
        await using var kontext = await BefuellteDatenbankAsync();
        var vorher = await kontext.Preiskomponenten.CountAsync();

        var zweiterLauf = await KatalogErstbefuellung.AusfuehrenAsync(kontext);

        Assert.False(zweiterLauf.Ausgefuehrt);
        Assert.Equal(40, await kontext.Services.CountAsync());
        Assert.Equal(vorher, await kontext.Preiskomponenten.CountAsync());
        Assert.Equal(1, await kontext.Preislisten.CountAsync());
    }
}
