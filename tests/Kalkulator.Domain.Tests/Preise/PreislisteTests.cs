using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Tests.Preise;

public class PreislisteTests
{
    private static readonly DateTimeOffset Zeitpunkt = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private static Preisliste Beispiel()
    {
        var liste = new Preisliste { Id = 1, Bezeichnung = "Preisstand 2026-07-15", GueltigAb = new DateOnly(2026, 7, 15) };
        liste.Preise.Add(new Preis { PreiskomponenteId = 10, VkNetto = 31.90m });
        liste.Staffeln.Add(new Preisstaffel { PreiskomponenteId = 20, AbMenge = 1, VkNetto = 350m });
        liste.Staffeln.Add(new Preisstaffel { PreiskomponenteId = 20, AbMenge = 51, VkNetto = 550m });
        liste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.SupportkontingentAeSatz, Wert = 30.38m });
        liste.EkPositionen.Add(new EkPosition { PreiskomponenteId = 10, EkLizenz = 3.59m, AufwandMinuten = 1, Overhead = 1.50m });
        return liste;
    }

    [Fact]
    public void Neue_Preisliste_ist_ein_aenderbarer_Entwurf()
    {
        var liste = Beispiel();

        Assert.Equal(PreislistenStatus.Entwurf, liste.Status);
        Assert.True(liste.IstAenderbar);
    }

    [Fact]
    public void Freigeben_sperrt_die_Preisliste_und_merkt_sich_wer_und_wann()
    {
        var liste = Beispiel();

        liste.Freigeben("Dennis Arntjen", Zeitpunkt);

        Assert.Equal(PreislistenStatus.Freigegeben, liste.Status);
        Assert.False(liste.IstAenderbar);
        Assert.Equal("Dennis Arntjen", liste.FreigegebenVon);
        Assert.Equal(Zeitpunkt, liste.FreigegebenAm);
    }

    [Fact]
    public void Eine_freigegebene_Preisliste_kann_nicht_erneut_freigegeben_werden()
    {
        var liste = Beispiel();
        liste.Freigeben("A", Zeitpunkt);

        Assert.Throws<PreislisteGesperrtException>(() => liste.Freigeben("B", Zeitpunkt));
    }

    [Fact]
    public void Neuer_Entwurf_kopiert_alle_Werte_und_laesst_das_Original_unveraendert()
    {
        var original = Beispiel();
        original.Freigeben("A", Zeitpunkt);

        var entwurf = original.ErzeugeEntwurf("Preisstand 2027-01-01", new DateOnly(2027, 1, 1));
        entwurf.Preise[0].VkNetto = 33.90m;

        Assert.Equal(PreislistenStatus.Entwurf, entwurf.Status);
        Assert.Equal(original.Id, entwurf.VorgaengerId);
        Assert.Equal(33.90m, entwurf.PreisFuer(10));
        Assert.Equal(31.90m, original.PreisFuer(10));
        Assert.Equal(2, entwurf.Staffeln.Count);
        Assert.Equal(30.38m, entwurf.ParameterWert(ParameterSchluessel.SupportkontingentAeSatz));
        Assert.Single(entwurf.EkPositionen);
        Assert.NotSame(original.EkPositionen[0], entwurf.EkPositionen[0]);
    }

    [Theory]
    [InlineData(1, 350)]
    [InlineData(50, 350)]
    [InlineData(51, 550)]
    [InlineData(500, 550)]
    public void Staffelpreis_S41_nach_Mitarbeitenden(int mitarbeitende, decimal erwartet)
    {
        Assert.Equal(erwartet, Beispiel().StaffelpreisFuer(20, mitarbeitende));
    }

    [Theory]
    [InlineData(30, 900)]
    [InlineData(31, 1400)]
    [InlineData(500, 2800)]
    public void Staffelpreis_Onboarding_Standard_nach_Usern(int user, decimal erwartet)
    {
        Assert.Equal(erwartet, OnboardingStandard().StaffelpreisFuer(30, user));
    }

    [Fact]
    public void Onboarding_ab_501_User_ist_individuell()
    {
        Assert.Null(OnboardingStandard().StaffelpreisFuer(30, 501));
    }

    [Fact]
    public void Fehlender_Parameter_wird_klar_gemeldet()
    {
        var fehler = Assert.Throws<KeyNotFoundException>(() => Beispiel().ParameterWert("GIBT_ES_NICHT"));
        Assert.Contains("GIBT_ES_NICHT", fehler.Message);
    }

    // Onboarding-Staffel Connect Standard laut Vertriebskalkulator V1.1
    private static Preisliste OnboardingStandard()
    {
        var liste = new Preisliste { Bezeichnung = "Test", GueltigAb = new DateOnly(2026, 7, 15) };
        liste.Staffeln.AddRange(
        [
            new Preisstaffel { PreiskomponenteId = 30, AbMenge = 1, VkNetto = 900m },
            new Preisstaffel { PreiskomponenteId = 30, AbMenge = 31, VkNetto = 1400m },
            new Preisstaffel { PreiskomponenteId = 30, AbMenge = 101, VkNetto = 2000m },
            new Preisstaffel { PreiskomponenteId = 30, AbMenge = 251, VkNetto = 2800m },
            new Preisstaffel { PreiskomponenteId = 30, AbMenge = 501, VkNetto = null },
        ]);
        return liste;
    }
}
