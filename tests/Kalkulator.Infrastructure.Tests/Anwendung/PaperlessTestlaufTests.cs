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
        VorlageId = 50379,
        Rollen = new() { ["Nösse"] = new PaperlessRolle { AusVorlage = true } },
    };

    [Fact]
    public void Muster_PDF_hat_je_Rolle_ein_Feld_am_Anfang_der_Linie()
    {
        var felder = Unterschriftsfelder.Finde(PaperlessTestlauf.MusterPdf("Testlauf", "Zusatz", 180, 56));

        Assert.Equal(["Kunde", "Nösse"], felder.Select(f => f.Rolle));
        Assert.All(felder, f => Assert.Equal((1, 72d), (f.Seite, Math.Round(f.X))));
        Assert.Equal([430d, 250d], felder.Select(f => Math.Round(f.Grundlinie)));
    }

    private static (HttpStatusCode, string)[] Variante(string blob, (HttpStatusCode, string) dokument) =>
    [
        (HttpStatusCode.OK, "{\"signed_id\":\"" + blob + "\",\"direct_upload\":{\"url\":\"https://speicher.test/" + blob + "\"}}"),
        (HttpStatusCode.OK, ""),
        dokument,
    ];

    [Fact]
    public async Task Legt_drei_Entwuerfe_an_oben_und_unten_mit_Vorlage_und_einmal_ohne()
    {
        var server = new TestServer([
            .. Variante("blob-a", (HttpStatusCode.Created, """{"id":101}""")),
            .. Variante("blob-b", (HttpStatusCode.Created, """{"id":102}""")),
            .. Variante("blob-c", (HttpStatusCode.Created, """{"id":103}""")),
        ]);
        var e = Einstellungen();
        e.Versenden = true;
        var ausgabe = new StringWriter();

        var kennungen = await PaperlessTestlauf.AusfuehrenAsync(
            e, " dennis@noesse.de ", x => new PaperlessUebergabe(new HttpClient(server), Options.Create(x)), ausgabe);

        Assert.Equal(["101", "102", "103"], kennungen);
        var dokumente = server.Anfragen.Where(a => a.Anfrage.RequestUri!.AbsolutePath.EndsWith("/documents", StringComparison.Ordinal))
            .Select(a => JsonNode.Parse(a.Inhalt)!.AsObject()).ToList();
        Assert.Equal(3, dokumente.Count);
        Assert.All(server.Anfragen.Where(a => a.Anfrage.Method == HttpMethod.Put), a => Assert.StartsWith("%PDF", a.Inhalt, StringComparison.Ordinal));
        Assert.Equal([true, true, false], dokumente.Select(d => d.ContainsKey("template_id")));

        foreach (var d in dokumente)
        {
            Assert.Equal(15114L, d["workspace_id"]!.GetValue<long>());
            Assert.Equal(50379L, d["template_id"]?.GetValue<long>() ?? 50379L);
            // Kein Versand trotz Versenden = true: Der Testlauf legt nur Entwürfe an.
            Assert.False(d.ContainsKey("state"));

            // Nur der Kunde ist Teilnehmer (mit der angegebenen Adresse); „Nösse“ legt die Vorlage fest.
            var teilnehmer = d["participants"]!.AsObject();
            Assert.Equal(["Kunde"], teilnehmer.Select(t => t.Key));
            Assert.Equal("dennis@noesse.de", teilnehmer["Kunde"]!["email"]!.GetValue<string>());
            Assert.Equal(2, d["blocks"]!.AsObject().Count);
        }

        // Feld der Kundenlinie (y = 430 vom unteren Rand, Feldhöhe 56): A rechnet ab oben, B ab unten.
        static double Y(JsonObject d) => d["blocks"]!["unterschrift_1"]!["settings"]!["absolutePosition"]!["y"]!.GetValue<double>();
        Assert.Equal(842 - 430 - 56, Y(dokumente[0]), 0);
        Assert.Equal(430, Y(dokumente[1]), 0);
        Assert.Contains("Testlauf A", dokumente[0]["name"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("Dokument 103 angelegt", ausgabe.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_gescheiterte_Variante_haelt_die_anderen_nicht_auf()
    {
        var server = new TestServer([
            .. Variante("blob-a", (HttpStatusCode.Created, """{"id":101}""")),
            .. Variante("blob-b", (HttpStatusCode.Created, """{"id":102}""")),
            .. Variante("blob-c", (HttpStatusCode.UnprocessableEntity, """{"error":"slot unknown"}""")),
        ]);
        var ausgabe = new StringWriter();

        var kennungen = await PaperlessTestlauf.AusfuehrenAsync(
            Einstellungen(), "dennis@noesse.de", x => new PaperlessUebergabe(new HttpClient(server), Options.Create(x)), ausgabe);

        Assert.Equal(["101", "102"], kennungen);
        Assert.Contains("C: fehlgeschlagen", ausgabe.ToString(), StringComparison.Ordinal);
        Assert.Contains("slot unknown", ausgabe.ToString(), StringComparison.Ordinal);
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
