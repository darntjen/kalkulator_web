using System.Net;
using System.Text.Json.Nodes;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Infrastructure.Paperless;
using Microsoft.Extensions.Options;
using TestServer = Kalkulator.Infrastructure.Tests.Anwendung.PaperlessUebergabeTests.TestServer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Testlauf gegen die echte Paperless-API (Frage 12.5), hier gegen den nachgebauten Server.</summary>
public class PaperlessTestlaufTests
{
    private static PaperlessEinstellungen Einstellungen() => new()
    {
        Adresse = "https://paperless.test/api/v1",
        ApiSchluessel = "test-schluessel",
        ArbeitsbereichId = 15114,
        AblaufVorlageId = 50379,
        Rollen = new() { ["Nösse"] = new PaperlessRolle { Name = "Sascha Manczak", EMail = "s.manczak@noesse.de" } },
    };

    [Fact]
    public void Muster_PDF_hat_je_Rolle_ein_Feld_am_Anfang_der_Linie()
    {
        var felder = Unterschriftsfelder.Finde(PaperlessTestlauf.MusterPdf("Testlauf", "Zusatz", 180, 56));

        Assert.Equal(["Kunde", "Nösse"], felder.Select(f => f.Rolle));
        Assert.All(felder, f => Assert.Equal((1, 72d), (f.Seite, Math.Round(f.X))));
        Assert.Equal([430d, 250d], felder.Select(f => Math.Round(f.Grundlinie)));
    }

    private static (HttpStatusCode, string) Blob(string blob) =>
        (HttpStatusCode.OK, "{\"signed_id\":\"" + blob + "\",\"direct_upload\":{\"url\":\"https://speicher.test/" + blob + "\"}}");

    [Fact]
    public async Task Legt_D_ueber_eine_Kopie_der_Ablauf_Vorlage_und_E_direkt_aus_dem_PDF_an()
    {
        var server = new TestServer(
            Blob("blob-d"), (HttpStatusCode.OK, ""), (HttpStatusCode.Created, """{"id":900}"""), (HttpStatusCode.Created, """{"id":101}"""), (HttpStatusCode.NoContent, ""),
            Blob("blob-e"), (HttpStatusCode.OK, ""), (HttpStatusCode.Created, """{"id":102}"""));
        var e = Einstellungen();
        e.Versenden = true;
        var ausgabe = new StringWriter();

        var kennungen = await PaperlessTestlauf.AusfuehrenAsync(
            e, " dennis@noesse.de ", x => new PaperlessUebergabe(new HttpClient(server), Options.Create(x)), ausgabe);

        Assert.Equal(["101", "102"], kennungen);
        Assert.All(server.Anfragen.Where(a => a.Anfrage.Method == HttpMethod.Put), a => Assert.StartsWith("%PDF", a.Inhalt, StringComparison.Ordinal));
        var dokumente = server.Anfragen.Where(a => a.Anfrage.RequestUri!.AbsolutePath.EndsWith("/documents", StringComparison.Ordinal))
            .Select(a => JsonNode.Parse(a.Inhalt)!.AsObject()).ToList();

        // D: Dokument aus der Kopie (900) der Ablauf-Vorlage, E: aus dem PDF ohne Vorlage.
        Assert.Equal(900L, dokumente[0]["template_id"]!.GetValue<long>());
        Assert.False(dokumente[1].ContainsKey("template_id"));
        Assert.Equal(15114L, JsonNode.Parse(server.Anfragen[2].Inhalt)!["workspace_id"]!.GetValue<long>());
        Assert.Equal(50379L, JsonNode.Parse(server.Anfragen[2].Inhalt)!["template_id"]!.GetValue<long>());

        foreach (var d in dokumente)
        {
            // Kein Versand trotz Versenden = true: Der Testlauf legt nur Entwürfe an.
            Assert.False(d.ContainsKey("state"));
            var teilnehmer = d["participants"]!.AsObject();
            Assert.Equal(["Kunde", "Nösse"], teilnehmer.Select(t => t.Key));
            Assert.Equal("dennis@noesse.de", teilnehmer["Kunde"]!["email"]!.GetValue<string>());
            Assert.Equal("s.manczak@noesse.de", teilnehmer["Nösse"]!["email"]!.GetValue<string>());
        }

        // Feld der Kundenlinie in Pixeln: (842 − 430 − 56) × 96/72.
        Assert.Equal(474.7, dokumente[1]["blocks"]!["unterschrift_1"]!["settings"]!["absolutePosition"]!["y"]!.GetValue<double>(), 1);
        Assert.Contains("Testlauf E", dokumente[1]["name"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("Dokument 102 angelegt", ausgabe.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scheitert_D_wird_E_trotzdem_angelegt()
    {
        var server = new TestServer(
            Blob("blob-d"), (HttpStatusCode.OK, ""), (HttpStatusCode.UnprocessableEntity, """{"error":"pdf not allowed"}"""),
            Blob("blob-e"), (HttpStatusCode.OK, ""), (HttpStatusCode.Created, """{"id":102}"""));
        var ausgabe = new StringWriter();

        var kennungen = await PaperlessTestlauf.AusfuehrenAsync(
            Einstellungen(), "dennis@noesse.de", x => new PaperlessUebergabe(new HttpClient(server), Options.Create(x)), ausgabe);

        Assert.Equal(["102"], kennungen);
        Assert.Contains("D: fehlgeschlagen", ausgabe.ToString(), StringComparison.Ordinal);
        Assert.Contains("pdf not allowed", ausgabe.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Schluessel_oder_Adresse_bricht_der_Testlauf_mit_Hinweis_ab()
    {
        var ohneSchluessel = Einstellungen();
        ohneSchluessel.ApiSchluessel = "";
        var fehler = await Assert.ThrowsAsync<PaperlessFehler>(() =>
            PaperlessTestlauf.AusfuehrenAsync(ohneSchluessel, "dennis@noesse.de", _ => throw new InvalidOperationException(), TextWriter.Null));
        Assert.Contains("Paperless__ApiSchluessel", fehler.Message, StringComparison.Ordinal);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            PaperlessTestlauf.AusfuehrenAsync(Einstellungen(), "", _ => throw new InvalidOperationException(), TextWriter.Null));
    }
}
