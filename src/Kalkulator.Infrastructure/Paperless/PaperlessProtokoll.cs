using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Protokoll für den Testlauf (<see cref="PaperlessTestlauf"/>): je Anfrage Methode, Adresse ohne Abfrageteil und
/// Status, bei JSON-Antworten nur ihr Aufbau (Feldnamen ohne Werte). Schlüssel, signierte Adressen und Inhalte
/// erscheinen deshalb nicht im Protokoll.
/// </summary>
public sealed class PaperlessProtokoll(TextWriter ausgabe, HttpMessageHandler? innen = null) : DelegatingHandler(innen ?? new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var antwort = await base.SendAsync(request, cancellationToken);
        var adresse = request.RequestUri is { } u ? u.Host + u.AbsolutePath : "?";
        var aufbau = "";
        if (antwort.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var text = await antwort.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                aufbau = " " + PaperlessUebergabe.Aufbau(JsonNode.Parse(text));
            }
            catch (JsonException)
            {
                aufbau = " (keine lesbare Antwort)";
            }

            var typ = antwort.Content.Headers.ContentType;
            antwort.Content = new StringContent(text);
            antwort.Content.Headers.ContentType = typ;
        }

        await ausgabe.WriteLineAsync($"    {request.Method} {adresse} → {(int)antwort.StatusCode}{aufbau}");
        return antwort;
    }
}
