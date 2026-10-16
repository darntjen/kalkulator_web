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

/// <summary>Status eines Dokuments in Paperless; <see cref="Unterzeichnet"/> laut <c>Paperless:StatusUnterzeichnet</c>.</summary>
public sealed record PaperlessStand(string Status, bool Unterzeichnet);

/// <summary>Unterschriebenes PDF und, falls eingerichtet, Prüfprotokoll eines abgeschlossenen Dokuments.</summary>
public sealed record PaperlessErgebnis(byte[] Pdf, byte[]? Pruefprotokoll);

/// <summary>Abfrage übergebener Dokumente (Entscheidung 16.10.2026: stündliche Abfrage statt Webhook).</summary>
public interface IPaperlessAbruf
{
    Task<PaperlessStand> StandAsync(string dokumentId, CancellationToken abbruch);

    /// <summary><c>null</c>, solange der Download nicht eingerichtet ist (<c>Paperless:PdfPfad</c>).</summary>
    Task<PaperlessErgebnis?> ErgebnisAsync(string dokumentId, CancellationToken abbruch);
}

public sealed class PaperlessFehler(string meldung) : InvalidOperationException(meldung);

/// <summary>
/// Übergabe an die Paperless-API (#26, Teil D): erst die PDF-Datei als Blob hochladen, dann das Dokument mit Teilnehmern
/// und Unterschriftsfeldern anlegen. Paperless meldet nichts zurück (Entscheidung „keine Rückmeldung“).
/// </summary>
public sealed class PaperlessUebergabe(HttpClient http, IOptions<PaperlessEinstellungen> einstellungen) : IPaperlessUebergabe, IPaperlessAbruf
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
        // Die Felder stehen auf oberster Ebene, nicht unter „blob“ (Antwort der API vom 05.10.2026).
        var anfrage = new JsonObject
        {
            ["filename"] = auftrag.Dateiname,
            ["content_type"] = "application/pdf",
            ["byte_size"] = auftrag.Pdf.Length,
            ["checksum"] = Convert.ToBase64String(MD5.HashData(auftrag.Pdf)),
        };
        var antwort = await SendeAsync(e, HttpMethod.Post, "blobs", anfrage, abbruch);
        var signiert = Kennung(antwort, "signed_id") ?? throw new PaperlessFehler("Paperless hat beim Hochladen keine signed_id zurückgegeben.");

        // Ohne Upload-Ziel bliebe das Dokument in Paperless leer; dann lieber abbrechen und den Aufbau der Antwort nennen.
        if (antwort["direct_upload"] is not JsonObject ziel || ziel["url"]?.GetValue<string>() is not { Length: > 0 } url)
        {
            throw new PaperlessFehler($"Paperless hat beim Hochladen kein Upload-Ziel (direct_upload.url) zurückgegeben. Aufbau der Antwort: {Aufbau(antwort)}");
        }

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

        return signiert;
    }

    /// <summary>
    /// Inhalt der Anfrage „Dokument anlegen“: Teilnehmer je Slot, je Unterschriftsfeld ein Block
    /// <c>Block::Input::SignatureInput</c> mit Seite, Lage und Größe. Ohne <see cref="PaperlessEinstellungen.Versenden"/>
    /// bleibt das Dokument ein Entwurf.
    /// </summary>
    internal static JsonObject Dokument(PaperlessEinstellungen e, PaperlessAuftrag auftrag, string blob)
    {
        var dokument = new JsonObject
        {
            ["workspace_id"] = e.ArbeitsbereichId,
            ["name"] = auftrag.Name,
            ["pdf"] = blob,
            ["participants"] = Teilnehmer(auftrag),
            ["blocks"] = Bloecke(e, auftrag),
        };

        // Kein template_id: Paperless legt das Dokument sonst aus der Vorlage an und lässt das PDF weg.
        MitSprache(e, dokument);
        if (e.Versenden)
        {
            dokument["state"] = "dispatched";
        }

        return dokument;
    }

    /// <summary>Sprache des Dokuments, in der Paperless es den Unterzeichnern zeigt (sonst Englisch).</summary>
    private static void MitSprache(PaperlessEinstellungen e, JsonObject dokument)
    {
        if (!string.IsNullOrWhiteSpace(e.Sprache))
        {
            dokument["original_content_locale"] = e.Sprache.Trim();
            dokument["rendering_locale"] = e.Sprache.Trim();
        }
    }

    private static JsonObject Teilnehmer(PaperlessAuftrag auftrag)
    {
        var teilnehmer = new JsonObject();
        foreach (var t in auftrag.Teilnehmer)
        {
            teilnehmer[t.Slot] = new JsonObject { ["name"] = t.Name, ["email"] = t.EMail };
        }

        return teilnehmer;
    }

    /// <summary>
    /// Je Unterschriftsfeld ein Block <c>Block::Input::SignatureInput</c>. Lage und Größe werden aus Punkt in die Einheit
    /// von Paperless umgerechnet (<see cref="PaperlessEinstellungen.Skalierung"/>).
    /// </summary>
    internal static JsonObject Bloecke(PaperlessEinstellungen e, PaperlessAuftrag auftrag)
    {
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
                    ["absolutePosition"] = new JsonObject { ["x"] = Runde(s.X * e.Skalierung), ["y"] = Runde(Math.Max(0, y) * e.Skalierung) },
                    ["absoluteSize"] = new JsonObject { ["width"] = Runde(e.FeldBreite * e.Skalierung), ["height"] = Runde(e.FeldHoehe * e.Skalierung) },
                },
            };
        }

        return bloecke;
    }

    public async Task<PaperlessStand> StandAsync(string dokumentId, CancellationToken abbruch)
    {
        var e = PruefeAktiv();
        var pfad = "documents/" + Uri.EscapeDataString(dokumentId);
        var antwort = await SendeAsync(e, HttpMethod.Get, pfad, null, abbruch);
        if (antwort["state"] is not JsonValue wert || !wert.TryGetValue<string>(out var status) || string.IsNullOrWhiteSpace(status))
        {
            throw new PaperlessFehler($"Paperless hat zu Dokument {dokumentId} keinen Status (state) geliefert. Aufbau: {Aufbau(antwort)}");
        }

        return new PaperlessStand(status, e.StatusUnterzeichnet.Contains(status, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<PaperlessErgebnis?> ErgebnisAsync(string dokumentId, CancellationToken abbruch)
    {
        var e = PruefeAktiv();
        if (string.IsNullOrWhiteSpace(e.PdfPfad))
        {
            return null;
        }

        var pdf = await LadeDateiAsync(e, e.PdfPfad, dokumentId, abbruch);
        var protokoll = string.IsNullOrWhiteSpace(e.ProtokollPfad) ? null : await LadeDateiAsync(e, e.ProtokollPfad, dokumentId, abbruch);
        return new PaperlessErgebnis(pdf, protokoll);
    }

    private PaperlessEinstellungen PruefeAktiv()
    {
        var e = einstellungen.Value;
        return e.Aktiv ? e : throw new PaperlessFehler("Paperless ist nicht eingerichtet (Paperless:ApiSchluessel und Paperless:ArbeitsbereichId).");
    }

    /// <summary>Lädt eine Datei; eine JSON-Antwort statt einer Datei gilt als Fehler und nennt ihren Aufbau.</summary>
    private async Task<byte[]> LadeDateiAsync(PaperlessEinstellungen e, string vorlage, string dokumentId, CancellationToken abbruch)
    {
        var pfad = vorlage.Replace("{id}", Uri.EscapeDataString(dokumentId), StringComparison.Ordinal).TrimStart('/');
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(e.Adresse.TrimEnd('/') + "/"), pfad));
        anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", e.ApiSchluessel);
        using var antwort = await http.SendAsync(anfrage, abbruch);
        var inhalt = await antwort.Content.ReadAsByteArrayAsync(abbruch);
        if (!antwort.IsSuccessStatusCode)
        {
            throw new PaperlessFehler($"Paperless hat „{pfad}“ abgelehnt ({(int)antwort.StatusCode} {antwort.ReasonPhrase}): {Kurz(Encoding.UTF8.GetString(inhalt))}");
        }

        if (antwort.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            string aufbau;
            try
            {
                aufbau = Aufbau(JsonNode.Parse(inhalt));
            }
            catch (JsonException)
            {
                aufbau = "nicht lesbar";
            }

            throw new PaperlessFehler($"Paperless liefert auf „{pfad}“ JSON statt einer Datei. Aufbau: {aufbau}");
        }

        return inhalt;
    }

    private static double Runde(double wert) => Math.Round(wert, 1);

    private async Task<JsonObject> SendeAsync(PaperlessEinstellungen e, HttpMethod methode, string pfad, JsonObject? inhalt, CancellationToken abbruch)
    {
        using var anfrage = new HttpRequestMessage(methode, new Uri(new Uri(e.Adresse.TrimEnd('/') + "/"), pfad))
        {
            // Mit fester Länge statt gestreamt, damit auch Server ohne „chunked“-Unterstützung den Inhalt erhalten.
            Content = inhalt is null ? null : new StringContent(inhalt.ToJsonString(Json), Encoding.UTF8, "application/json"),
        };
        anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", e.ApiSchluessel);
        anfrage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var antwort = await http.SendAsync(anfrage, abbruch);
        var text = await antwort.Content.ReadAsStringAsync(abbruch);
        if (!antwort.IsSuccessStatusCode)
        {
            throw new PaperlessFehler($"Paperless hat „{pfad}“ abgelehnt ({(int)antwort.StatusCode} {antwort.ReasonPhrase}): {Kurz(text)}");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonObject();
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

    /// <summary>Feldnamen einer JSON-Antwort ohne Werte, z. B. <c>{id, direct_upload{url, headers{…}}}</c>; für Fehlermeldungen und Protokolle.</summary>
    public static string Aufbau(JsonNode? knoten, int tiefe = 0) => knoten switch
    {
        JsonObject o when tiefe < 4 => "{" + string.Join(", ", o.Select(f => f.Value is JsonObject or JsonArray ? f.Key + Aufbau(f.Value, tiefe + 1) : f.Key)) + "}",
        JsonArray a when tiefe < 4 => "[" + (a.Count > 0 ? Aufbau(a[0], tiefe + 1) : "") + "]",
        JsonObject or JsonArray => "{…}",
        _ => "",
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
