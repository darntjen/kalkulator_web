using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Infrastructure.Paperless;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Anfragen an die Paperless-API (#26, Teil D) gegen einen nachgebauten Server.</summary>
public class PaperlessUebergabeTests
{
    /// <summary>Antwortet je Anfrage mit der nächsten vorbereiteten Antwort und merkt sich Anfrage und Inhalt.</summary>
    internal sealed class TestServer(params (HttpStatusCode Status, string Inhalt)[] antworten) : HttpMessageHandler
    {
        private int _naechste;

        public List<(HttpRequestMessage Anfrage, string Inhalt)> Anfragen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var inhalt = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Anfragen.Add((request, inhalt));
            var (status, text) = antworten[_naechste++];
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly PaperlessEinstellungen Einstellungen = new()
    {
        Adresse = "https://paperless.test/api/v1",
        ApiSchluessel = "test-schluessel",
        ArbeitsbereichId = 42,
    };

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 Test");

    private static PaperlessAuftrag Auftrag() => new(
        "Vertrag MS-A-2026-0001 – Muster Spedition GmbH",
        "Vertrag_MS-A-2026-0001.pdf",
        Pdf,
        [new PaperlessTeilnehmer("Kunde", "Erika Beispiel", "erika@example.org")],
        [
            new PaperlessFeld("Kunde", new Unterschriftsfeld("Kunde", 3, 72, 200, 595, 842)),
            new PaperlessFeld("Nösse", new Unterschriftsfeld("Nösse", 3, 320, 200, 595, 842)),
        ]);

    [Fact]
    public async Task Laedt_die_Datei_hoch_und_legt_das_Dokument_mit_Teilnehmern_und_Feldern_an()
    {
        var server = new TestServer(
            (HttpStatusCode.OK, """{"signed_id":"blob-123","direct_upload":{"url":"https://speicher.test/upload/abc","headers":{"Content-Type":"application/pdf","Content-MD5":"xyz"}}}"""),
            (HttpStatusCode.NoContent, ""),
            (HttpStatusCode.Created, """{"id":4711,"name":"Vertrag"}"""));

        var id = await new PaperlessUebergabe(new HttpClient(server), Options.Create(Einstellungen)).UebergebenAsync(Auftrag(), CancellationToken.None);

        Assert.Equal("4711", id);
        Assert.Equal(3, server.Anfragen.Count);

        var (blob, blobInhalt) = server.Anfragen[0];
        Assert.Equal((HttpMethod.Post, "https://paperless.test/api/v1/blobs"), (blob.Method, blob.RequestUri!.ToString()));
        Assert.Equal("Bearer test-schluessel", blob.Headers.Authorization!.ToString());
        var b = JsonNode.Parse(blobInhalt)!["blob"]!;
        Assert.Equal(("Vertrag_MS-A-2026-0001.pdf", "application/pdf", Pdf.Length), (b["filename"]!.GetValue<string>(), b["content_type"]!.GetValue<string>(), b["byte_size"]!.GetValue<int>()));
        Assert.Equal(Convert.ToBase64String(System.Security.Cryptography.MD5.HashData(Pdf)), b["checksum"]!.GetValue<string>());

        var (hoch, hochInhalt) = server.Anfragen[1];
        Assert.Equal((HttpMethod.Put, "https://speicher.test/upload/abc"), (hoch.Method, hoch.RequestUri!.ToString()));
        Assert.Null(hoch.Headers.Authorization);
        Assert.Equal("xyz", hoch.Content!.Headers.GetValues("Content-MD5").Single());
        Assert.Equal("%PDF-1.7 Test", hochInhalt);

        Assert.All(server.Anfragen.Where(a => a.Anfrage.Method == HttpMethod.Post), a => Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(a.Inhalt), a.Anfrage.Content!.Headers.ContentLength));

        var (dokument, dokumentInhalt) = server.Anfragen[2];
        Assert.Equal("https://paperless.test/api/v1/documents", dokument.RequestUri!.ToString());
        var d = JsonNode.Parse(dokumentInhalt)!.AsObject();
        Assert.Equal(42, d["workspace_id"]!.GetValue<long>());
        Assert.Equal("blob-123", d["pdf"]!.GetValue<string>());
        Assert.Equal("Vertrag MS-A-2026-0001 – Muster Spedition GmbH", d["name"]!.GetValue<string>());
        Assert.False(d.ContainsKey("state"), "Ohne „Versenden“ bleibt das Dokument ein Entwurf für die technische Freigabe.");
        Assert.False(d.ContainsKey("template_id"));
        Assert.Equal("erika@example.org", d["participants"]!["Kunde"]!["email"]!.GetValue<string>());

        var feld = d["blocks"]!["unterschrift_2"]!;
        Assert.Equal("Block::Input::SignatureInput", feld["type"]!.GetValue<string>());
        Assert.Equal("Nösse", feld["owner_participants_slot_names"]![0]!.GetValue<string>());
        Assert.Equal(3, feld["pdf_page_number"]!.GetValue<int>());
        // Ursprung oben links: 842 − 200 − 56 = 586.
        Assert.Equal((320d, 586d), (feld["settings"]!["absolutePosition"]!["x"]!.GetValue<double>(), feld["settings"]!["absolutePosition"]!["y"]!.GetValue<double>()));
        Assert.Equal((180d, 56d), (feld["settings"]!["absoluteSize"]!["width"]!.GetValue<double>(), feld["settings"]!["absoluteSize"]!["height"]!.GetValue<double>()));
    }

    [Fact]
    public void Mit_Vorlage_Versand_und_PDF_Koordinaten()
    {
        var e = new PaperlessEinstellungen { ApiSchluessel = "x", ArbeitsbereichId = 1, VorlageId = 77, Versenden = true, YVonOben = false, FeldBreite = 150, FeldHoehe = 40 };

        var d = PaperlessUebergabe.Dokument(e, Auftrag(), "blob");

        Assert.Equal("dispatched", d["state"]!.GetValue<string>());
        Assert.Equal(77, d["template_id"]!.GetValue<long>());
        Assert.Equal(200d, d["blocks"]!["unterschrift_1"]!["settings"]!["absolutePosition"]!["y"]!.GetValue<double>());
        Assert.Equal(150d, d["blocks"]!["unterschrift_1"]!["settings"]!["absoluteSize"]!["width"]!.GetValue<double>());
    }

    [Fact]
    public async Task Abgelehnte_Anfrage_und_fehlende_Einrichtung_werden_verstaendlich_gemeldet()
    {
        var server = new TestServer(
            (HttpStatusCode.OK, """{"signed_id":"blob-1"}"""),
            (HttpStatusCode.UnprocessableEntity, """{"errors":{"participants":["ist ung\u00fcltig"]}}"""));

        var fehler = await Assert.ThrowsAsync<PaperlessFehler>(() =>
            new PaperlessUebergabe(new HttpClient(server), Options.Create(Einstellungen)).UebergebenAsync(Auftrag(), CancellationToken.None));
        Assert.Contains("documents", fehler.Message, StringComparison.Ordinal);
        Assert.Contains("422", fehler.Message, StringComparison.Ordinal);
        Assert.Contains("\"participants\":[\"ist ungültig\"]", fehler.Message, StringComparison.Ordinal);
        Assert.Equal(2, server.Anfragen.Count);

        var aus = await Assert.ThrowsAsync<PaperlessFehler>(() =>
            new PaperlessUebergabe(new HttpClient(new TestServer()), Options.Create(new PaperlessEinstellungen())).UebergebenAsync(Auftrag(), CancellationToken.None));
        Assert.Contains("nicht eingerichtet", aus.Message, StringComparison.Ordinal);
    }
}
