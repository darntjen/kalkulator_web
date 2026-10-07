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
        Assert.False(avv.DarfKatalogFreigeben);
        Assert.False(avv.DarfSonderpositionenFreigeben);
    }
}
