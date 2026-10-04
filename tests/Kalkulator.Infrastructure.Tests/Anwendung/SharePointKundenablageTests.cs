using System.Net;
using System.Text;
using Azure.Core;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Vorlagen;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Kanalordner im Team „Kundenprojekte“ über Microsoft Graph, gegen nachgebaute Antworten.</summary>
public class SharePointKundenablageTests
{
    private sealed class TestAnmeldung : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("test", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }

    /// <summary>Antwortet je Pfad (ohne Abfrage) mit vorbereitetem JSON oder Inhalt.</summary>
    private sealed class Graph(Dictionary<string, string> antworten) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pfad = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath).Replace("/v1.0/", "", StringComparison.Ordinal);
            return Task.FromResult(antworten.TryGetValue(pfad, out var text)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        }
    }

    private static SharePointKundenablage Ablage() => new(
        new GraphZugang(new HttpClient(new Graph(new Dictionary<string, string>
        {
            ["sites/noesse.sharepoint.com:/sites/Kundenprojekte"] = """{"id":"site1","name":"Kundenprojekte"}""",
            ["sites/site1/drive"] = """{"id":"lw1","name":"Dokumente"}""",
            ["drives/lw1/root/children"] = """{"value":[{"id":"k1","name":"Muster Spedition GmbH","folder":{}},{"id":"k2","name":"Andere GmbH","folder":{}},{"id":"x","name":"Notiz.txt","file":{}}]}""",
            ["drives/lw1/items/k1/children"] = """{"value":[{"id":"b30","name":"30_Analyse","folder":{}},{"id":"b70","name":"70_Vertraege","folder":{}}]}""",
            ["drives/lw1/items/b30/children"] = """{"value":[{"id":"d1","name":"Analyse.pdf","size":2048,"webUrl":"https://noesse.sharepoint.com/a.pdf","file":{},"lastModifiedBy":{"user":{"displayName":"Christoph Schröder"}}},{"id":"u1","name":"Fotos","folder":{}}]}""",
            ["drives/lw1/items/u1/children"] = """{"value":[{"id":"d2","name":"Serverraum.jpg","size":10,"file":{}}]}""",
            ["drives/lw1/items/k1"] = """{"id":"k1","name":"Muster Spedition GmbH"}""",
            ["drives/lw1/items/d1"] = """{"id":"d1","name":"Analyse.pdf","file":{},"parentReference":{"path":"/drives/lw1/root:/Muster%20Spedition%20GmbH/30_Analyse"}}""",
            ["drives/lw1/items/d9"] = """{"id":"d9","name":"Vertrag.pdf","file":{},"parentReference":{"path":"/drives/lw1/root:/Muster Spedition GmbH/70_Vertraege"}}""",
            ["drives/lw1/items/d8"] = """{"id":"d8","name":"Fremd.pdf","file":{},"parentReference":{"path":"/drives/lw1/root:/Andere GmbH/30_Analyse"}}""",
            ["drives/lw1/items/d1/content"] = "PDF",
        })), new TestAnmeldung()),
        new KundenablageEinstellungen { Website = "https://noesse.sharepoint.com/sites/Kundenprojekte" });

    [Fact]
    public async Task Listet_Kanalordner_und_Dateien_der_Bereiche_mit_Unterordnern()
    {
        var ablage = Ablage();

        Assert.Equal(["Andere GmbH", "Muster Spedition GmbH"], (await ablage.KanaeleAsync(default)).Select(k => k.Name));
        var dateien = await ablage.DateienAsync("k1", default);
        Assert.Equal(["30_Analyse/Analyse.pdf", "30_Analyse/Fotos/Serverraum.jpg"], dateien.Select(d => d.Pfad));
        Assert.Equal(("30_Analyse", 2048L, "Christoph Schröder", "https://noesse.sharepoint.com/a.pdf"), (dateien[0].Bereich, dateien[0].Groesse, dateien[0].GeaendertVon, dateien[0].Link));
    }

    [Fact]
    public async Task Laedt_nur_Dateien_aus_den_Bereichen_des_eigenen_Kanals()
    {
        var ablage = Ablage();

        var (name, inhalt) = await ablage.LadeAsync("k1", "d1", default);
        Assert.Equal(("Analyse.pdf", "PDF"), (name, Encoding.UTF8.GetString(inhalt)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.LadeAsync("k1", "d9", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.LadeAsync("k1", "d8", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.LadeAsync("k1", "../root", default));
    }
}
