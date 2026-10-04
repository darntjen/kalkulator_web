using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Tests.Preise;

/// <summary>Margen-Ampel, Prüfung je Komponente und Prüfung vor der Freigabe einer Preisliste (#6).</summary>
public class PreispruefungTests
{
    private readonly Preisliste _liste = new() { Bezeichnung = "Test", GueltigAb = new DateOnly(2026, 7, 15) };
    private readonly List<Service> _services = [];

    public PreispruefungTests()
    {
        foreach (var schluessel in ParameterSchluessel.Pflicht)
        {
            _liste.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = schluessel == ParameterSchluessel.EkKostensatzProStunde ? 60.75m : 0.55m });
        }
    }

    private Preiskomponente Komponente(string code, decimal? vk, EkPosition? ek = null, Vertriebsstatus status = Vertriebsstatus.Verkaufsfaehig,
        StaffelBezug staffel = StaffelBezug.Keine, bool mitPreiseintrag = true, ServiceTyp typ = ServiceTyp.Einzelservice)
    {
        var service = new Service { Code = code, Bezeichnung = code, Typ = typ, Vertriebsstatus = status };
        var komponente = new Preiskomponente { Code = code, Bezeichnung = code, Einheit = Einheit.User, Service = service, StaffelBezug = staffel };
        service.Preiskomponenten.Add(komponente);
        _services.Add(service);
        if (mitPreiseintrag && staffel == StaffelBezug.Keine)
        {
            _liste.Preise.Add(new Preis { Preiskomponente = komponente, VkNetto = vk });
        }

        if (ek is not null)
        {
            ek.Preiskomponente = komponente;
            _liste.EkPositionen.Add(ek);
        }

        return komponente;
    }

    [Theory]
    [InlineData(null, Ampel.Grau)]
    [InlineData(0.30, Ampel.Rot)]
    [InlineData(0.3799, Ampel.Rot)]
    [InlineData(0.38, Ampel.Gelb)]
    [InlineData(0.449, Ampel.Gelb)]
    [InlineData(0.45, Ampel.Gruen)]
    [InlineData(0.72, Ampel.Gruen)]
    [InlineData(0.95, Ampel.Gruen)]
    public void Standard_Ampel_gruen_ab_45_gelb_ab_38_sonst_rot(double? marge, Ampel erwartet) =>
        Assert.Equal(erwartet, Margenschwellen.Standard.Bewerte(marge is null ? null : (decimal)marge.Value));

    [Fact]
    public void Schwellen_kommen_aus_den_Parametern_der_Preisliste()
    {
        _liste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.MargeGruenAb, Wert = 0.60m });
        _liste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.MargeGruenBis, Wert = 0.80m });
        _liste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.MargeRotUnter, Wert = 0.50m });

        var schwellen = Margenschwellen.Aus(_liste);

        Assert.Equal(new Margenschwellen(0.60m, 0.80m, 0.50m), schwellen);
        Assert.Equal(Ampel.Gelb, schwellen.Bewerte(0.55m));
        Assert.Equal(Ampel.Gruen, schwellen.Bewerte(0.75m));
        Assert.Equal(Ampel.Gelb, schwellen.Bewerte(0.85m));
        Assert.Equal("grün ab 60 %, rot unter 50 %, sonst gelb", (schwellen with { GruenBis = null }).Text(p => $"{p * 100:0} %"));
    }

    [Fact]
    public void Komponentenpruefung_rechnet_DB_und_Marge_wie_die_Excel_Kalkulation()
    {
        // S02 Endpoint Protection: Kosten 6,10 €, VK 14,90 € → DB 8,80 €, Marge 59,1 % (Excel: 59 %).
        var s02 = Komponente("S02", 14.90m, new EkPosition { EkLizenz = 3.59m, AufwandMinuten = 1, Overhead = 1.50m });

        var pruefung = KomponentenPruefung.Fuer(_liste, s02, Margenschwellen.Standard);

        Assert.Equal(6.10m, pruefung.Kosten);
        Assert.Equal(8.80m, pruefung.Deckungsbeitrag);
        Assert.Equal(0.5906m, pruefung.Marge);
        Assert.Equal(Ampel.Gruen, pruefung.Ampel);
    }

    [Fact]
    public void Ohne_EK_oder_ohne_VK_ist_die_Marge_offen()
    {
        var ohneEk = KomponentenPruefung.Fuer(_liste, Komponente("S35", 9.90m), Margenschwellen.Standard);
        var aufAnfrage = KomponentenPruefung.Fuer(_liste, Komponente("S61", null, new EkPosition { EkLizenz = 10 }), Margenschwellen.Standard);

        Assert.Equal((null, Ampel.Grau), (ohneEk.Marge, ohneEk.Ampel));
        Assert.Equal((null, Ampel.Grau), (aufAnfrage.Marge, aufAnfrage.Ampel));
    }

    [Fact]
    public void Bei_Staffeln_zaehlt_die_erste_Stufe()
    {
        var s41 = Komponente("S41", null, new EkPosition { AufwandMinuten = 120 }, staffel: StaffelBezug.Mitarbeitende);
        _liste.Staffeln.Add(new Preisstaffel { Preiskomponente = s41, AbMenge = 51, VkNetto = 550m });
        _liste.Staffeln.Add(new Preisstaffel { Preiskomponente = s41, AbMenge = 1, VkNetto = 350m });

        var pruefung = KomponentenPruefung.Fuer(_liste, s41, Margenschwellen.Standard);

        Assert.Equal((350m, true), (pruefung.Vk, pruefung.VkAusStaffel));
        Assert.Equal(121.50m, pruefung.Kosten);
    }

    [Fact]
    public void Vollstaendige_Preisliste_hat_keine_Fehler()
    {
        Komponente("S02", 14.90m, new EkPosition { EkLizenz = 3.59m, AufwandMinuten = 1, Overhead = 1.50m });

        Assert.Empty(Freigabepruefung.Pruefe(_liste, _services));
    }

    [Fact]
    public void Fehlender_Pflichtparameter_verhindert_die_Freigabe()
    {
        _liste.Parameter.RemoveAll(p => p.Schluessel == ParameterSchluessel.AeSatzEbene2);

        var hinweis = Assert.Single(Freigabepruefung.Pruefe(_liste, _services));

        Assert.True(hinweis.IstFehler);
        Assert.Contains(ParameterSchluessel.AeSatzEbene2, hinweis.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Anbietbare_Komponente_ohne_Preiseintrag_ist_ein_Fehler_zukuenftige_nicht()
    {
        Komponente("S34", null, mitPreiseintrag: false);
        Komponente("S36", null, status: Vertriebsstatus.Zukuenftig, mitPreiseintrag: false);
        Komponente("S37", null);

        var fehler = Freigabepruefung.Pruefe(_liste, _services).Where(h => h.IstFehler).ToList();

        var einzig = Assert.Single(fehler);
        Assert.Contains("S34", einzig.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("S36", einzig.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("S37", einzig.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Leere_Staffel_ist_ein_Fehler()
    {
        Komponente("S01-STD-ONB", null, staffel: StaffelBezug.User);

        Assert.Contains(Freigabepruefung.Pruefe(_liste, _services), h => h.IstFehler && h.Text.StartsWith("S01-STD-ONB", StringComparison.Ordinal));
    }

    [Fact]
    public void Fehlender_EK_und_rote_Marge_sind_nur_Hinweise()
    {
        Komponente("S35", 39m);
        Komponente("S31-USER", 49m, new EkPosition { EkLizenz = 11.66m, BetriebFix = 20m });

        var hinweise = Freigabepruefung.Pruefe(_liste, _services);

        Assert.All(hinweise, h => Assert.False(h.IstFehler));
        Assert.Contains(hinweise, h => h.Text.Contains("Ohne EK", StringComparison.Ordinal) && h.Text.Contains("S35", StringComparison.Ordinal));
        Assert.Contains(hinweise, h => h.Text.Contains("roten Bereich", StringComparison.Ordinal) && h.Text.Contains("S31-USER", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ParameterSchluessel.CloudServerMargenteiler, 1.2, "Margenteiler")]
    [InlineData(ParameterSchluessel.MargeRotUnter, 0.6, "Margen-Ampel")]
    public void Unplausible_Parameter_sind_Fehler(string schluessel, double wert, string erwartet)
    {
        _liste.Parameter.RemoveAll(p => p.Schluessel == schluessel);
        _liste.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = (decimal)wert });

        Assert.Contains(Freigabepruefung.Pruefe(_liste, _services), h => h.IstFehler && h.Text.Contains(erwartet, StringComparison.Ordinal));
    }

    [Fact]
    public void Enthaelt_findet_auch_verschachtelte_Bundles()
    {
        var s02 = new Service { Id = 2, Code = "S02", Bezeichnung = "S02", Typ = ServiceTyp.Einzelservice };
        var b01 = new Service { Id = 10, Code = "B01", Bezeichnung = "B01", Typ = ServiceTyp.Bundle };
        var b02 = new Service { Id = 11, Code = "B02", Bezeichnung = "B02", Typ = ServiceTyp.Bundle };
        b01.Bestandteile.Add(new BundleBestandteil { Bundle = b01, Bestandteil = s02, BestandteilId = 2 });
        b02.Bestandteile.Add(new BundleBestandteil { Bundle = b02, Bestandteil = b01, BestandteilId = 10 });

        Assert.True(b02.Enthaelt(s02));
        Assert.True(b02.Enthaelt(b02));
        Assert.False(b01.Enthaelt(b02));
        Assert.False(s02.Enthaelt(b01));
    }
}
