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
        Rollen = new() { ["Nösse"] = new PaperlessRolle { Name = "Sascha Manczak", EMail = "s.manczak@noesse.de" } },
    };

    [Fact]
    public void Muster_PDF_hat_je_Rolle_ein_Feld_am_Anfang_der_Linie()
    {
        var felder = Unterschriftsfelder.Finde(PaperlessTestlauf.MusterPdf("Testlauf", "Zusatz", 180, 56));

        Assert.Equal(["Nösse", "Kunde"], felder.Select(f => f.Rolle));
        Assert.All(felder, f => Assert.Equal((1, 72d), (f.Seite, Math.Round(f.X))));
        Assert.Equal([430d, 250d], felder.Select(f => Math.Round(f.Grundlinie)));
    }

    private static (HttpStatusCode, string) Blob(string blob) =>
        (HttpStatusCode.OK, "{\"signed_id\":\"" + blob + "\",\"direct_upload\":{\"url\":\"https://speicher.test/" + blob + "\"}}");

    [Fact]
    public async Task Legt_ein_Dokument_direkt_aus_dem_PDF_mit_Kunde_vor_Noesse_auf_Deutsch_an()
    {
        var server = new TestServer(Blob("blob-e"), (HttpStatusCode.OK, ""), (HttpStatusCode.Created, """{"id":102}"""));
        var e = Einstellungen();
        e.Versenden = true;
        var ausgabe = new StringWriter();

        var kennungen = await PaperlessTestlauf.AusfuehrenAsync(
            e, " dennis@noesse.de ", x => new PaperlessUebergabe(new HttpClient(server), Options.Create(x)), ausgabe);

        Assert.Equal(["102"], kennungen);
        Assert.StartsWith("%PDF", server.Anfragen[1].Inhalt, StringComparison.Ordinal);
        var d = JsonNode.Parse(server.Anfragen[2].Inhalt)!.AsObject();
        Assert.False(d.ContainsKey("template_id"));
        Assert.False(d.ContainsKey("state"), "Kein Versand trotz Versenden = true: Der Testlauf legt nur einen Entwurf an.");
        Assert.Equal("de-DE", d["rendering_locale"]!.GetValue<string>());

        // Im PDF steht Nösse zuerst, in der Anfrage der Kunde: in dieser Reihenfolge wird unterschrieben.
        var teilnehmer = d["participants"]!.AsObject();
        Assert.Equal(["Kunde", "Nösse"], teilnehmer.Select(t => t.Key));
        Assert.Equal("dennis@noesse.de", teilnehmer["Kunde"]!["email"]!.GetValue<string>());
        Assert.Equal("s.manczak@noesse.de", teilnehmer["Nösse"]!["email"]!.GetValue<string>());

        // Feld der Kundenlinie (unten, 250 pt) in Pixeln: (842 − 250 − 56) × 96/72.
        Assert.Equal(714.7, d["blocks"]!["unterschrift_2"]!["settings"]!["absolutePosition"]!["y"]!.GetValue<double>(), 1);
        Assert.Contains("Dokument 102 angelegt", ausgabe.ToString(), StringComparison.Ordinal);
        Assert.Contains("Kunde = Test Kunde, Nösse = Sascha Manczak", ausgabe.ToString(), StringComparison.Ordinal);
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
