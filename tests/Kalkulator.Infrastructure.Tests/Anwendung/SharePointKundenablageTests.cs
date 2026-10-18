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

    /// <summary>Nimmt Uploads entgegen und antwortet mit dem angelegten Element; Laufwerk wie oben.</summary>
    private sealed class Hochladen : HttpMessageHandler
    {
        public List<(HttpMethod Methode, string Pfad, string Abfrage, byte[] Inhalt)> Anfragen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pfad = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath).Replace("/v1.0/", "", StringComparison.Ordinal);
            var inhalt = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Anfragen.Add((request.Method, pfad, Uri.UnescapeDataString(request.RequestUri.Query), inhalt));
            var json = pfad switch
            {
                "sites/noesse.sharepoint.com:/sites/Kundenprojekte" => """{"id":"site1","name":"Kundenprojekte"}""",
                "sites/site1/drive" => """{"id":"lw1","name":"Dokumente"}""",
                _ when request.Method == HttpMethod.Put => """{"id":"neu1","name":"Vertrag.pdf","size":3,"webUrl":"https://noesse.sharepoint.com/Vertrag.pdf"}""",
                _ => null,
            };
            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Ablegen_laedt_ueber_den_Pfad_hoch_und_ersetzt_gleichnamige_Dateien()
    {
        var server = new Hochladen();
        var ablage = new SharePointKundenablage(new GraphZugang(new HttpClient(server), new TestAnmeldung()),
            new KundenablageEinstellungen { Website = "https://noesse.sharepoint.com/sites/Kundenprojekte" });

        var datei = await ablage.AblegenAsync("k1", "70_Vertraege/Archiv Kalkulator", "Vertrag.pdf", [1, 2, 3], default);

        var hoch = Assert.Single(server.Anfragen, a => a.Methode == HttpMethod.Put);
        Assert.Equal("drives/lw1/items/k1:/70_Vertraege/Archiv Kalkulator/Vertrag.pdf:/content", hoch.Pfad);
        Assert.Contains("conflictBehavior=replace", hoch.Abfrage, StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], hoch.Inhalt);
        Assert.Equal(("70_Vertraege", "70_Vertraege/Archiv Kalkulator/Vertrag.pdf", "https://noesse.sharepoint.com/Vertrag.pdf"), (datei.Bereich, datei.Pfad, datei.Link));
    }

    [Theory]
    [InlineData("30_Analyse", "a.pdf")]
    [InlineData("70_Vertraege/../30_Analyse", "a.pdf")]
    [InlineData("70_Vertraege//x", "a.pdf")]
    [InlineData("70_Vertraege", "a:b.pdf")]
    [InlineData("70_Vertraege", "")]
    public async Task Ablegen_nur_in_Ablagebereichen_mit_gueltigen_Namen(string ordner, string name)
    {
        var verzeichnis = Directory.CreateTempSubdirectory("ablage-");
        try
        {
            Directory.CreateDirectory(Path.Combine(verzeichnis.FullName, "Muster GmbH"));
            var ablage = new OrdnerKundenablage(verzeichnis.FullName);

            await Assert.ThrowsAsync<ArgumentException>(() => ablage.AblegenAsync("Muster GmbH", ordner, name, [1], default));
        }
        finally
        {
            verzeichnis.Delete(true);
        }
    }

    [Fact]
    public async Task Ordnerablage_legt_Unterordner_an_und_ersetzt()
    {
        var verzeichnis = Directory.CreateTempSubdirectory("ablage-");
        try
        {
            Directory.CreateDirectory(Path.Combine(verzeichnis.FullName, "Muster GmbH"));
            var ablage = new OrdnerKundenablage(verzeichnis.FullName);

            await ablage.AblegenAsync("Muster GmbH", "60_Angebote/Archiv Kalkulator", "Angebot.docx", [1], default);
            var datei = await ablage.AblegenAsync("Muster GmbH", "60_Angebote/Archiv Kalkulator", "Angebot.docx", [2, 3], default);

            var voll = Path.Combine(verzeichnis.FullName, "Muster GmbH", "60_Angebote", "Archiv Kalkulator", "Angebot.docx");
            Assert.Equal([2, 3], await File.ReadAllBytesAsync(voll));
            Assert.Equal(("60_Angebote/Archiv Kalkulator/Angebot.docx", 2L), (datei.Pfad, datei.Groesse));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.AblegenAsync("../x", "60_Angebote", "a.docx", [1], default));
        }
        finally
        {
            verzeichnis.Delete(true);
        }
    }
}
