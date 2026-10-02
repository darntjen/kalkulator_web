using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Erstbefuellung;

namespace Kalkulator.Infrastructure.Tests.Referenzkalkulationen;

/// <summary>RK-08 aus docs/07_referenzkalkulationen.md: Auflösung des Vertragspakets mit dem echten Katalog.</summary>
public class VertragspaketTests
{
    private static readonly IReadOnlyList<Domain.Katalog.Service> Katalog = KatalogErstbefuellung.ErzeugeKatalog().Services;

    private static IEnumerable<string> Codes(params string[] gebucht) =>
        Vertragspaket.Aufloesen(gebucht, Katalog).Select(d => d.Code);

    private static readonly string[] Rahmen = ["AVV", "Grundvertrag", "AVB", "SLA"];

    [Fact]
    public void RK08_Premium_mit_B02_B05_und_S31()
    {
        var paket = Vertragspaket.Aufloesen(["S01-PRM", "B02", "B05", "S31"], Katalog);

        Assert.Equal([.. Rahmen, "S01", "B02", "S02", "S03", "S04", "S05", "S06", "S07", "B05", "S21", "S22", "S23", "S31"], paket.Select(d => d.Code));
        Assert.Equal("Service Level Agreement – Stufe Premium", paket[3].Bezeichnung);
        Assert.Equal("Nösse Connect Premium", paket[4].Bezeichnung);
        Assert.Equal("B02", paket.Single(d => d.Code == "S05").Bundle);
        Assert.Null(paket.Single(d => d.Code == "S31").Bundle);
    }

    [Fact]
    public void RK08_Standard_mit_B06_legt_S25_hinter_das_Bundle() =>
        Assert.Equal([.. Rahmen, "S01", "B06", "S21", "S22", "S23", "S24", "S25"], Codes("S01-STD", "B06", "S25"));

    [Fact]
    public void RK08_Einzelservice_im_Bundle_wird_nicht_doppelt_beigelegt() =>
        Assert.Equal([.. Rahmen, "S01", "B01", "S02", "S03", "S04"], Codes("S01-STD", "B01", "S03"));

    [Fact]
    public void RK08_Enterprise_mit_Cloud_Server_und_Server_Premium() =>
        Assert.Equal([.. Rahmen, "S01", "B04", "S11", "S12", "S13", "S14", "S21", "S61"], Codes("S01-ENT", "S61", "S21", "B04", "S14"));

    [Fact]
    public void Verschachteltes_Bundle_bekommt_keinen_eigenen_Schein() =>
        Assert.Equal([.. Rahmen, "S01", "B02", "S02", "S03", "S04", "S05", "S06", "S07"], Codes("S01-PRM", "B02", "B01"));

    [Fact]
    public void Reine_S41_Kalkulation_bekommt_Rahmen_mit_SLA_Standard_aber_kein_S01()
    {
        var paket = Vertragspaket.Aufloesen(["S41"], Katalog);

        Assert.Equal([.. Rahmen, "S41"], paket.Select(d => d.Code));
        Assert.EndsWith("Stufe Standard", paket[3].Bezeichnung, StringComparison.Ordinal);
    }
}
