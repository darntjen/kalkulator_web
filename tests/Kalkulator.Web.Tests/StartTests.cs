using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kalkulator.Web.Tests;

public class StartTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Check_meldet_Healthy()
    {
        var antwort = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("Healthy", await antwort.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/", "Managed-Services-Kalkulator")]
    [InlineData("/kalkulationen", "Kalkulationen")]
    [InlineData("/katalog", "Katalog")]
    [InlineData("/statistik", "Statistik")]
    public async Task Seite_wird_mit_Layout_ausgeliefert(string pfad, string ueberschrift)
    {
        var html = await _client.GetStringAsync(pfad);

        Assert.Contains($"<h1>{ueberschrift}</h1>", html);
        Assert.Contains("lang=\"de\"", html);
        Assert.Contains("img/noesse-logo.jpg", html);
        Assert.Contains("Hauptnavigation", html);
    }

    [Fact]
    public async Task Unbekannte_Seite_liefert_404_mit_deutscher_Meldung()
    {
        var antwort = await _client.GetAsync("/gibt-es-nicht");

        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);
        Assert.Contains("Seite nicht gefunden", await antwort.Content.ReadAsStringAsync());
    }
}
