using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kalkulator.Web.Tests;

/// <summary>Übergangsanmeldung bis #4: Testrollen in der Entwicklung, gesperrt in allen anderen Umgebungen.</summary>
public class AnmeldungTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task In_der_Entwicklung_ist_man_als_Vertrieb_angemeldet()
    {
        var html = await Client().GetStringAsync("/");

        Assert.Contains("vertrieb@noesse.de", html);
        Assert.Contains("Rolle (Test)", html);
    }

    [Fact]
    public async Task Rollenwechsel_setzt_das_Cookie_und_fuehrt_zurueck()
    {
        var client = Client();
        var antwort = await client.GetAsync($"{Anmeldung.Anmeldung.RollenPfad}?rolle=Fuehrung&zurueck=/katalog");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Equal("/katalog", antwort.Headers.Location?.OriginalString);
        var cookie = Assert.Single(antwort.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{Anmeldung.Anmeldung.RollenCookie}=Fuehrung", cookie, StringComparison.Ordinal);

        // Startseite statt /katalog: Sie braucht keine Datenbank, und es geht hier nur um die Anmeldung mit der neuen Rolle.
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, "/");
        anfrage.Headers.Add("Cookie", $"{Anmeldung.Anmeldung.RollenCookie}=Fuehrung");
        var html = await (await client.SendAsync(anfrage)).Content.ReadAsStringAsync();
        Assert.Contains("fuehrung@noesse.de", html);
    }

    [Theory]
    [InlineData("Vertrieb", false)]
    [InlineData("Vertriebsleitung", false)]
    [InlineData("Consultant", false)]
    [InlineData("Admin", false)]
    [InlineData("Produktmanagement", true)]
    [InlineData("Fuehrung", true)]
    public async Task Den_Katalog_sehen_nur_Produktmanagement_und_Fuehrung(string rolle, bool sichtbar)
    {
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, "/");
        anfrage.Headers.Add("Cookie", $"{Anmeldung.Anmeldung.RollenCookie}={rolle}");
        var html = await (await Client().SendAsync(anfrage)).Content.ReadAsStringAsync();

        Assert.Equal(sichtbar, html.Contains("href=\"katalog\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("//example.org")]
    [InlineData("https://example.org")]
    public async Task Rollenwechsel_leitet_nicht_auf_fremde_Seiten_um(string ziel)
    {
        var antwort = await Client().GetAsync($"{Anmeldung.Anmeldung.RollenPfad}?rolle=Vertrieb&zurueck={Uri.EscapeDataString(ziel)}");

        Assert.Equal("/", antwort.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Unbekannte_Rolle_wird_abgelehnt()
    {
        var antwort = await Client().GetAsync($"{Anmeldung.Anmeldung.RollenPfad}?rolle=Chef");

        Assert.Equal(HttpStatusCode.BadRequest, antwort.StatusCode);
    }

    [Fact]
    public async Task Ausserhalb_der_Entwicklung_ist_ohne_Entra_alles_gesperrt_ausser_dem_Health_Check()
    {
        var produktion = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await produktion.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await produktion.GetAsync("/projekte")).StatusCode);
        var rollenwechsel = await produktion.GetAsync(Anmeldung.Anmeldung.RollenPfad + "?rolle=Admin");
        Assert.False(rollenwechsel.Headers.Contains("Set-Cookie"), "Den Rollenwechsel gibt es nur in der Entwicklung.");
        Assert.NotEqual(HttpStatusCode.Redirect, rollenwechsel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await produktion.GetAsync("/health")).StatusCode);
    }
}
