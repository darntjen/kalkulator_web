using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Tests.Preise;

public class EkRechnerTests
{
    private readonly Preisliste _liste = new() { Bezeichnung = "Test", GueltigAb = new DateOnly(2026, 7, 15) };

    public EkRechnerTests() =>
        _liste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.EkKostensatzProStunde, Wert = 60.75m });

    private (Service Service, Preiskomponente Komponente) Einzel(string code, Einheit einheit, EkPosition? ek)
    {
        var service = new Service { Code = code, Bezeichnung = code, Typ = ServiceTyp.Einzelservice };
        var komponente = new Preiskomponente { Code = code, Bezeichnung = code, Einheit = einheit, Service = service };
        service.Preiskomponenten.Add(komponente);
        if (ek is not null)
        {
            ek.Preiskomponente = komponente;
            _liste.EkPositionen.Add(ek);
        }

        return (service, komponente);
    }

    private Preiskomponente Bundle(Einheit einheit, decimal korrektur, params Service[] bestandteile)
    {
        var (bundle, komponente) = Einzel("BUNDLE", einheit, new EkPosition { AusBestandteilen = true, KostenKorrektur = korrektur });
        bundle.Bestandteile.AddRange(bestandteile.Select(b => new BundleBestandteil { Bestandteil = b }));
        return komponente;
    }

    [Fact]
    public void Bundle_summiert_seine_Bestandteile_und_zieht_die_RMM_Korrektur_ab()
    {
        // B01 laut EK-Kalkulation: 6,10 + 5,03 + 4,01 − 1,50 = 13,64 €
        var s02 = Einzel("S02", Einheit.User, new EkPosition { EkLizenz = 3.59m, AufwandMinuten = 1, Overhead = 1.50m }).Service;
        var s03 = Einzel("S03", Einheit.User, new EkPosition { EkLizenz = 1.50m, AufwandMinuten = 2, Overhead = 1.50m }).Service;
        var s04 = Einzel("S04", Einheit.User, new EkPosition { EkLizenz = 1.50m, AufwandMinuten = 1, Overhead = 1.50m }).Service;

        Assert.Equal(13.64m, _liste.KostenFuer(Bundle(Einheit.User, -1.50m, s02, s03, s04)));
    }

    [Fact]
    public void Bestandteil_mit_anderer_Einheit_zaehlt_mit_einer_Einheit()
    {
        // B05 (pro Kunde) enthält S21 (pro Firewall): gezählt wird eine Firewall.
        var s21 = Einzel("S21", Einheit.Firewall, new EkPosition { AufwandMinuten = 35, Overhead = 5m }).Service;

        Assert.Equal(40.44m, _liste.KostenFuer(Bundle(Einheit.Kunde, 0m, s21)));
    }

    [Fact]
    public void Fehlender_EK_eines_Bestandteils_macht_die_Bundlekosten_unbekannt()
    {
        var s11 = Einzel("S11", Einheit.Server, new EkPosition { EkLizenz = 6.01m, AufwandMinuten = 1, Overhead = 2.50m }).Service;
        var s14 = Einzel("S14", Einheit.Server, ek: null).Service;

        Assert.Null(_liste.KostenFuer(Bundle(Einheit.Server, 0m, s11, s14)));
    }
}
