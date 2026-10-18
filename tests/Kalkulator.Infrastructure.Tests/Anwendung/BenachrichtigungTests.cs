using Kalkulator.Infrastructure.Benachrichtigung;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.Extensions.DependencyInjection;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

public class BenachrichtigungTests
{
    private static IBenachrichtigung Aufloesen(string? website, SharePointEinstellungen sharePoint)
    {
        var dienste = new ServiceCollection();
        dienste.AddLogging();
        dienste.AddKalkulatorInfrastruktur("Server=nicht-verwendet");
        dienste.Configure<BenachrichtigungsEinstellungen>(e => e.Website = website);
        dienste.Configure<VorlagenEinstellungen>(e => e.SharePoint = sharePoint);
        using var anbieter = dienste.BuildServiceProvider();
        return anbieter.GetRequiredService<IBenachrichtigung>();
    }

    [Fact]
    public void Ohne_Website_gehen_Meldungen_nur_ins_Protokoll()
    {
        var meldung = Assert.IsType<ProtokollBenachrichtigung>(Aufloesen("", new SharePointEinstellungen()));
        Assert.False(meldung.Eingerichtet);
        Assert.Null(meldung.Grund);
    }

    [Fact]
    public void Website_ohne_Anmeldung_an_Graph_bricht_nicht_ab_sondern_meldet_nur_ins_Protokoll()
    {
        // So steht es im Codespace: Website eingetragen, Tenant und App noch leer.
        var meldung = Assert.IsType<ProtokollBenachrichtigung>(
            Aufloesen("https://noessedatentechnik.sharepoint.com/sites/Kundenprojekte", new SharePointEinstellungen()));
        Assert.False(meldung.Eingerichtet);
        Assert.Contains("TenantId", meldung.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void Mit_Website_und_Anmeldung_schreibt_der_Kalkulator_in_die_Liste()
    {
        var meldung = Aufloesen("https://noessedatentechnik.sharepoint.com/sites/Kundenprojekte",
            new SharePointEinstellungen { TenantId = "tenant", ClientId = "app", ClientSecret = "geheim-nur-im-test" });
        Assert.IsType<SharePointBenachrichtigung>(meldung);
        Assert.True(meldung.Eingerichtet);
    }
}
