using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kalkulator.Documents.Vertrag;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>Teilnehmer eines Paperless-Dokuments in einem Slot der Vorlage bzw. des Dokuments.</summary>
public sealed record PaperlessTeilnehmer(string Slot, string Name, string EMail);

/// <summary>Unterschriftsfeld für einen Slot; Lage wie im PDF gefunden (<see cref="Unterschriftsfeld"/>).</summary>
public sealed record PaperlessFeld(string Slot, Unterschriftsfeld Stelle);

/// <summary>Was an Paperless geht: das Gesamt-PDF mit Teilnehmern und Unterschriftsfeldern.</summary>
public sealed record PaperlessAuftrag(string Name, string Dateiname, byte[] Pdf, IReadOnlyList<PaperlessTeilnehmer> Teilnehmer, IReadOnlyList<PaperlessFeld> Felder);

public interface IPaperlessUebergabe
{
    /// <summary>Legt das Dokument in Paperless an und gibt seine Kennung zurück; Fehler als <see cref="PaperlessFehler"/>.</summary>
    Task<string> UebergebenAsync(PaperlessAuftrag auftrag, CancellationToken abbruch);
}

public sealed class PaperlessFehler(string meldung) : InvalidOperationException(meldung);

/// <summary>
/// Übergabe an die Paperless-API (#26, Teil D): erst die PDF-Datei als Blob hochladen, dann das Dokument mit Teilnehmern
/// und Unterschriftsfeldern anlegen. Paperless meldet nichts zurück (Entscheidung „keine Rückmeldung“).
/// </summary>
public sealed class PaperlessUebergabe(HttpClient http, IOptions<PaperlessEinstellungen> einstellungen) : IPaperlessUebergabe
{
    /// <summary>Umlaute im Klartext, auch in Fehlermeldungen aus Antworten von Paperless.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<string> UebergebenAsync(PaperlessAuftrag auftrag, CancellationToken abbruch)
    {
        var e = einstellungen.Value;
        if (!e.Aktiv)
        {
            throw new PaperlessFehler("Die Übergabe an Paperless ist nicht eingerichtet (Paperless:ApiSchluessel und Paperless:ArbeitsbereichId).");
        }

        try
        {
            var blob = await HochladenAsync(e, auftrag, abbruch);
            var antwort = await SendeAsync(e, HttpMethod.Post, "documents", Dokument(e, auftrag, blob), abbruch);
            return Kennung(antwort, "id") ?? throw new PaperlessFehler("Paperless hat das Dokument angelegt, aber keine Kennung (id) zurückgegeben.");
        }
        catch (HttpRequestException ex)
        {
            throw new PaperlessFehler($"Paperless ist nicht erreichbar: {ex.Message}");
        }
        catch (TaskCanceledException) when (!abbruch.IsCancellationRequested)
        {
            throw new PaperlessFehler("Paperless hat nicht rechtzeitig geantwortet.");
        }
    }

    /// <summary>
    /// Direkter Upload nach dem Muster von ActiveStorage: Paperless nimmt Name, Größe und MD5-Prüfsumme entgegen und
    /// liefert eine <c>signed_id</c>; enthält die Antwort ein Upload-Ziel, folgt dorthin der Inhalt per PUT.
    /// </summary>
    private async Task<string> HochladenAsync(PaperlessEinstellungen e, PaperlessAuftrag auftrag, CancellationToken abbruch)
    {
        var anfrage = new JsonObject
        {
            ["blob"] = new JsonObject
            {
                ["filename"] = auftrag.Dateiname,
                ["content_type"] = "application/pdf",
                ["byte_size"] = auftrag.Pdf.Length,
                ["checksum"] = Convert.ToBase64String(MD5.HashData(auftrag.Pdf)),
            },
        };
        var antwort = await SendeAsync(e, HttpMethod.Post, "blobs", anfrage, abbruch);
        var signiert = Kennung(antwort, "signed_id") ?? throw new PaperlessFehler("Paperless hat beim Hochladen keine signed_id zurückgegeben.");

        if (antwort["direct_upload"] is JsonObject ziel && ziel["url"]?.GetValue<string>() is { Length: > 0 } url)
        {
            using var hochladen = new HttpRequestMessage(HttpMethod.Put, url) { Content = new ByteArrayContent(auftrag.Pdf) };
            if (ziel["headers"] is JsonObject kopf)
            {
                foreach (var (name, wert) in kopf)
                {
                    var text = wert?.ToString() ?? "";
                    if (!hochladen.Headers.TryAddWithoutValidation(name, text))
                    {
                        hochladen.Content.Headers.Remove(name);
                        hochladen.Content.Headers.TryAddWithoutValidation(name, text);
                    }
                }
            }

            using var ergebnis = await http.SendAsync(hochladen, abbruch);
            if (!ergebnis.IsSuccessStatusCode)
            {
                throw new PaperlessFehler($"Das Hochladen der PDF-Datei ist fehlgeschlagen ({(int)ergebnis.StatusCode}).");
            }
        }

        return signiert;
    }

    /// <summary>
    /// Inhalt der Anfrage „Dokument anlegen“: Teilnehmer je Slot, je Unterschriftsfeld ein Block
    /// <c>Block::Input::SignatureInput</c> mit Seite, Lage und Größe. Ohne <see cref="PaperlessEinstellungen.Versenden"/>
    /// bleibt das Dokument ein Entwurf.
    /// </summary>
    internal static JsonObject Dokument(PaperlessEinstellungen e, PaperlessAuftrag auftrag, string blob)
    {
        var teilnehmer = new JsonObject();
        foreach (var t in auftrag.Teilnehmer)
        {
            teilnehmer[t.Slot] = new JsonObject { ["name"] = t.Name, ["email"] = t.EMail };
        }

        var bloecke = new JsonObject();
        foreach (var (feld, i) in auftrag.Felder.Select((f, i) => (f, i)))
        {
            var s = feld.Stelle;
            var y = e.YVonOben ? s.SeitenHoehe - s.Grundlinie - e.FeldHoehe : s.Grundlinie;
            bloecke[$"unterschrift_{i + 1}"] = new JsonObject
            {
                ["type"] = "Block::Input::SignatureInput",
                ["owner_participants_slot_names"] = new JsonArray(feld.Slot),
                ["pdf_page_number"] = s.Seite,
                ["settings"] = new JsonObject
                {
                    ["absolutePosition"] = new JsonObject { ["x"] = Runde(s.X), ["y"] = Runde(Math.Max(0, y)) },
                    ["absoluteSize"] = new JsonObject { ["width"] = Runde(e.FeldBreite), ["height"] = Runde(e.FeldHoehe) },
                },
            };
        }

        var dokument = new JsonObject
        {
            ["workspace_id"] = e.ArbeitsbereichId,
            ["name"] = auftrag.Name,
            ["pdf"] = blob,
            ["participants"] = teilnehmer,
            ["blocks"] = bloecke,
        };
        if (e.VorlageId is { } vorlage)
        {
            dokument["template_id"] = vorlage;
        }

        if (e.Versenden)
        {
            dokument["state"] = "dispatched";
        }

        return dokument;
    }

    private static double Runde(double wert) => Math.Round(wert, 1);

    private async Task<JsonObject> SendeAsync(PaperlessEinstellungen e, HttpMethod methode, string pfad, JsonObject inhalt, CancellationToken abbruch)
    {
        using var anfrage = new HttpRequestMessage(methode, new Uri(new Uri(e.Adresse.TrimEnd('/') + "/"), pfad))
        {
            // Mit fester Länge statt gestreamt, damit auch Server ohne „chunked“-Unterstützung den Inhalt erhalten.
            Content = new StringContent(inhalt.ToJsonString(Json), Encoding.UTF8, "application/json"),
        };
        anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", e.ApiSchluessel);
        anfrage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var antwort = await http.SendAsync(anfrage, abbruch);
        var text = await antwort.Content.ReadAsStringAsync(abbruch);
        if (!antwort.IsSuccessStatusCode)
        {
            throw new PaperlessFehler($"Paperless hat „{pfad}“ abgelehnt ({(int)antwort.StatusCode} {antwort.ReasonPhrase}): {Kurz(text)}");
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            throw new PaperlessFehler($"Paperless hat auf „{pfad}“ keine lesbare Antwort geschickt: {Kurz(text)}");
        }
    }

    /// <summary>Kennung als Text, gleich ob Paperless sie als Zahl oder Zeichenkette schickt.</summary>
    private static string? Kennung(JsonObject antwort, string feld) => antwort[feld] switch
    {
        JsonValue w when w.TryGetValue<string>(out var s) && s.Length > 0 => s,
        JsonValue w when w.TryGetValue<long>(out var n) => n.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string Kurz(string text)
    {
        try
        {
            text = JsonNode.Parse(text)?.ToJsonString(Json) ?? text;
        }
        catch (JsonException)
        {
            // Kein JSON, z. B. eine HTML-Fehlerseite: Text bleibt, wie er ist.
        }

        return text.Length > 300 ? text[..300] + " …" : text;
    }
}
