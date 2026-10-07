using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Rechtematrix (docs/00_projektueberblick.md, Abschnitt 5).</summary>
public class BerechtigungTests
{
    [Fact]
    public void Admin_sieht_alles_und_darf_jede_Freigabe_aber_nicht_pflegen_oder_kalkulieren()
    {
        var admin = new Berechtigung(new TestBenutzer("admin@noesse.de", Rollen.Admin));
        var fremd = new Kundenprojekt { Kunde = new Kunde { Firma = "Muster GmbH" }, Titel = "MS", Verantwortlich = "vertrieb@noesse.de" };

        Assert.True(admin.SiehtAlleProjekte);
        Assert.True(admin.DarfSehen(fremd));
        Assert.True(admin.DarfEinkaufSehen);
        Assert.True(admin.DarfKatalogSehen);
        Assert.True(admin.DarfSonderpositionenFreigeben);
        Assert.All(Enum.GetValues<FreigabeRolle>(), r => Assert.True(admin.DarfVertriebFreigeben(r)));
        Assert.All(Enum.GetValues<VertragsfreigabeArt>(), a => Assert.True(admin.DarfVertragFreigeben(a)));
        Assert.True(admin.DarfVertraegeFreigeben);
        Assert.True(admin.DarfKatalogFreigeben);

        Assert.False(admin.DarfKatalogPflegen);
        Assert.False(admin.DarfKalkulieren);
        Assert.False(admin.DarfBearbeiten(fremd));
    }

    [Fact]
    public void Pruefrollen_geben_nur_ihre_eigene_Vertragsfreigabe()
    {
        var avv = new Berechtigung(new TestBenutzer("avv@noesse.de", Rollen.FreigabeAvv));

        Assert.True(avv.DarfVertragFreigeben(VertragsfreigabeArt.Avv));
        Assert.False(avv.DarfVertragFreigeben(VertragsfreigabeArt.Technik));
        Assert.False(avv.DarfVertragFreigeben(VertragsfreigabeArt.Vertriebsleitung));
        Assert.False(avv.DarfKatalogFreigeben);
        Assert.False(avv.DarfSonderpositionenFreigeben);
    }

    [Fact]
    public void Vertriebsleitung_gibt_Vertraege_als_Erste_frei_aber_nicht_AVV_und_Technik()
    {
        var leitung = new Berechtigung(new TestBenutzer("vl@noesse.de", Rollen.Vertriebsleitung));
        var vertrieb = new Berechtigung(new TestBenutzer("vertrieb@noesse.de", Rollen.Vertrieb));

        Assert.True(leitung.IstVertriebsleitung);
        Assert.True(leitung.DarfVertraegeFreigeben);
        Assert.True(leitung.DarfVertragFreigeben(VertragsfreigabeArt.Vertriebsleitung));
        Assert.False(leitung.DarfVertragFreigeben(VertragsfreigabeArt.Avv));
        Assert.False(leitung.DarfVertragFreigeben(VertragsfreigabeArt.Technik));

        Assert.False(vertrieb.IstVertriebsleitung);
        Assert.False(vertrieb.DarfVertraegeFreigeben);
        Assert.All(Enum.GetValues<VertragsfreigabeArt>(), a => Assert.False(vertrieb.DarfVertragFreigeben(a)));
    }
}
