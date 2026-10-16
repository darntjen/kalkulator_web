using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Testlauf für die stündliche Abfrage (Entscheidung 16.10.2026): liest ein vorhandenes Dokument aus Paperless und gibt
/// seinen Aufbau aus, damit sich Statuswerte und Download-Adressen bestätigen lassen. Ausgegeben werden nur Feldnamen
/// sowie Werte von Status-, Zeit-, Typ- und Kennungsfeldern; Adressen ohne Abfrageteil. Namen, E-Mail-Adressen und
/// Tokens erscheinen nicht. Aufruf: <c>dotnet run --project src/Kalkulator.Web -- --paperless-status 12345</c>.
/// </summary>
public static partial class PaperlessStatuslauf
{
    public const string Schalter = "--paperless-status";

    public static async Task AusfuehrenAsync(PaperlessEinstellungen e, string dokumentId, HttpClient http, TextWriter ausgabe, CancellationToken abbruch = default)
    {
        if (!e.Aktiv)
        {
            throw new PaperlessFehler("Paperless ist nicht eingerichtet (Paperless:ApiSchluessel und Paperless:ArbeitsbereichId).");
        }

        if (string.IsNullOrWhiteSpace(dokumentId))
        {
            throw new ArgumentException("Bitte die Kennung des Dokuments angeben, z. B. --paperless-status 12345.");
        }

        foreach (var pfad in new[] { $"documents/{Uri.EscapeDataString(dokumentId.Trim())}", $"documents/{Uri.EscapeDataString(dokumentId.Trim())}?expand[]=participants" })
        {
            using var anfrage = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(e.Adresse.TrimEnd('/') + "/"), pfad));
            anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", e.ApiSchluessel);
            anfrage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var antwort = await http.SendAsync(anfrage, abbruch);
            await ausgabe.WriteLineAsync($"GET {pfad} → {(int)antwort.StatusCode}");
            var text = await antwort.Content.ReadAsStringAsync(abbruch);
            JsonNode? json;
            try
            {
                json = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                await ausgabe.WriteLineAsync("  (keine JSON-Antwort)");
                continue;
            }

            foreach (var zeile in Zeilen(json, ""))
            {
                await ausgabe.WriteLineAsync("  " + zeile);
            }
        }

        await ausgabe.WriteLineAsync($"Eingestellt: StatusUnterzeichnet = {string.Join(", ", e.StatusUnterzeichnet)}; PdfPfad = {e.PdfPfad ?? "(leer)"}; ProtokollPfad = {e.ProtokollPfad ?? "(leer)"}");
    }

    /// <summary>Je Feld eine Zeile „pfad: wert“ bzw. „pfad: (Typ)“; Listen nur mit ihrem ersten Eintrag.</summary>
    internal static IEnumerable<string> Zeilen(JsonNode? knoten, string pfad, int tiefe = 0)
    {
        switch (knoten)
        {
            case JsonObject o when tiefe < 6:
                foreach (var (name, wert) in o)
                {
                    foreach (var z in Zeilen(wert, pfad.Length == 0 ? name : $"{pfad}.{name}", tiefe + 1))
                    {
                        yield return z;
                    }
                }

                break;
            case JsonArray a when tiefe < 6:
                yield return $"{pfad}: Liste mit {a.Count} Einträgen";
                if (a.Count > 0)
                {
                    foreach (var z in Zeilen(a[0], pfad + "[0]", tiefe + 1))
                    {
                        yield return z;
                    }
                }

                break;
            case JsonValue v:
                yield return $"{pfad}: {Wert(pfad, v)}";
                break;
            case null:
                yield return $"{pfad}: null";
                break;
            default:
                yield return $"{pfad}: …";
                break;
        }
    }

    private static string Wert(string pfad, JsonValue v)
    {
        var name = pfad[(pfad.LastIndexOf('.') + 1)..];
        if (v.GetValueKind() is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number)
        {
            return Unkritisch().IsMatch(name) ? v.ToJsonString() : $"({v.GetValueKind()})";
        }

        var text = v.ToString();
        if (Adresse().IsMatch(name) && Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath} (ohne Abfrageteil)";
        }

        return Unkritisch().IsMatch(name) && !Geheim().IsMatch(name) && text.Length <= 80 ? $"\"{text}\"" : "(Text)";
    }

    [GeneratedRegex(@"^(id|.*_id|state|status|.*_state|.*_status|type|kind|.*_at|.*locale|content_type|filename|byte_size|turn|position|role|dispatch_strategy)$", RegexOptions.IgnoreCase)]
    private static partial Regex Unkritisch();

    [GeneratedRegex(@"(url|href|link)$", RegexOptions.IgnoreCase)]
    private static partial Regex Adresse();

    [GeneratedRegex(@"(token|secret|password|email)|(^|_)name$", RegexOptions.IgnoreCase)]
    private static partial Regex Geheim();
}
