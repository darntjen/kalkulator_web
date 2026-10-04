using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

namespace Kalkulator.Infrastructure.Vorlagen;

/// <summary>
/// Zugang zu Microsoft Graph mit der App-Registrierung des Kalkulators (Anwendungsberechtigung, kein Benutzer).
/// Wird für den Vorlagenabgleich und später für die PDF-Umwandlung verwendet (#26).
/// </summary>
public sealed class GraphZugang(HttpClient http, TokenCredential anmeldung)
{
    private static readonly string[] Bereich = ["https://graph.microsoft.com/.default"];
    private const string Basis = "https://graph.microsoft.com/v1.0/";

    public static GraphZugang Aus(SharePointEinstellungen e, HttpClient http)
    {
        var tenant = Pflicht(e.TenantId, "Vorlagen:SharePoint:TenantId");
        var client = Pflicht(e.ClientId, "Vorlagen:SharePoint:ClientId");
        TokenCredential anmeldung = !string.IsNullOrWhiteSpace(e.ZertifikatPfad)
            ? new ClientCertificateCredential(tenant, client, X509CertificateLoader.LoadPkcs12FromFile(e.ZertifikatPfad, e.ZertifikatKennwort))
            : new ClientSecretCredential(tenant, client, Pflicht(e.ClientSecret, "Vorlagen:SharePoint:ClientSecret oder ZertifikatPfad"));
        return new GraphZugang(http, anmeldung);
    }

    public async Task<T> HoleAsync<T>(string pfad, CancellationToken abbruch)
    {
        using var antwort = await SendeAsync(new HttpRequestMessage(HttpMethod.Get, Adresse(pfad)), abbruch);
        return await antwort.Content.ReadFromJsonAsync<T>(abbruch) ?? throw new InvalidOperationException($"Leere Antwort von Microsoft Graph ({pfad}).");
    }

    public async Task<byte[]> LadeAsync(string pfad, CancellationToken abbruch)
    {
        using var antwort = await SendeAsync(new HttpRequestMessage(HttpMethod.Get, Adresse(pfad)), abbruch);
        return await antwort.Content.ReadAsByteArrayAsync(abbruch);
    }

    public async Task<HttpResponseMessage> SendeAsync(HttpRequestMessage anfrage, CancellationToken abbruch)
    {
        var token = await anmeldung.GetTokenAsync(new TokenRequestContext(Bereich), abbruch);
        anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var antwort = await http.SendAsync(anfrage, abbruch);
        if (!antwort.IsSuccessStatusCode)
        {
            var text = await antwort.Content.ReadAsStringAsync(abbruch);
            antwort.Dispose();
            throw new GraphFehler((int)antwort.StatusCode, $"Microsoft Graph antwortet mit {(int)antwort.StatusCode} auf {anfrage.RequestUri?.AbsolutePath}: {Kuerze(text)}");
        }

        return antwort;
    }

    /// <summary>Laufwerk (Dokumentbibliothek) einer Website; leere Bibliothek heißt Standardbibliothek.</summary>
    public async Task<string> LaufwerkAsync(string? website, string? bibliothek, CancellationToken abbruch)
    {
        var adresse = new Uri(website ?? throw new InvalidOperationException("Für SharePoint fehlt die Einstellung Vorlagen:SharePoint:Website."));
        var site = await HoleAsync<GraphElement>($"sites/{adresse.Host}:{adresse.AbsolutePath.TrimEnd('/')}", abbruch);
        if (string.IsNullOrWhiteSpace(bibliothek))
        {
            return (await HoleAsync<GraphElement>($"sites/{site.Id}/drive", abbruch)).Id;
        }

        var laufwerke = await HoleAsync<GraphListe>($"sites/{site.Id}/drives?$select=id,name", abbruch);
        return laufwerke.Werte.FirstOrDefault(l => l.Name.Equals(bibliothek, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new InvalidOperationException($"Die Bibliothek „{bibliothek}“ gibt es auf {website} nicht.");
    }

    /// <summary>Pfad für <c>root:/…:</c>-Adressen; Schrägstriche bleiben erhalten.</summary>
    public static string Pfad(string pfad) => Uri.EscapeDataString(pfad.Trim('/')).Replace("%2F", "/", StringComparison.Ordinal);

    private sealed record GraphElement([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name);

    private sealed record GraphListe([property: JsonPropertyName("value")] List<GraphElement> Werte);

    private static Uri Adresse(string pfad) => pfad.StartsWith("https://", StringComparison.Ordinal) ? new Uri(pfad) : new Uri(Basis + pfad);

    private static string Kuerze(string text) => text.Length > 300 ? text[..300] + " …" : text;

    private static string Pflicht(string? wert, string name) =>
        string.IsNullOrWhiteSpace(wert) ? throw new InvalidOperationException($"Für SharePoint fehlt die Einstellung {name}.") : wert;
}

public sealed class GraphFehler(int status, string meldung) : InvalidOperationException(meldung)
{
    public int Status { get; } = status;
}

/// <summary>
/// Vorlagenordner in SharePoint (Website „Service-Katalog“, Ordner 03_Vertragswerk), gelesen über Microsoft Graph.
/// Die App braucht Lesezugriff auf die Website (Sites.Selected mit Rolle „read“).
/// </summary>
public sealed class SharePointQuelle(GraphZugang graph, SharePointEinstellungen einstellungen) : IVorlagenQuelle
{
    private string? _laufwerk;

    public string Name => "SharePoint";

    public string Ort => $"{einstellungen.Website} › {einstellungen.Bibliothek ?? "Standardbibliothek"} › {einstellungen.Ordnerpfad}";

    public async Task<IReadOnlyList<QuellDatei>> ListeAsync(CancellationToken abbruch)
    {
        var laufwerk = await LaufwerkAsync(abbruch);
        var ordner = (einstellungen.Ordnerpfad ?? "").Trim('/');
        var start = ordner.Length == 0
            ? $"drives/{laufwerk}/root/children"
            : $"drives/{laufwerk}/root:/{GraphZugang.Pfad(ordner)}:/children";
        var ergebnis = new List<QuellDatei>();
        await SammleAsync(start, "", ergebnis, abbruch);
        return [.. ergebnis.OrderBy(d => d.Pfad, StringComparer.Ordinal)];
    }

    public async Task<byte[]> LadeAsync(QuellDatei datei, CancellationToken abbruch) =>
        await graph.LadeAsync($"drives/{await LaufwerkAsync(abbruch)}/items/{datei.Id}/content", abbruch);

    private async Task SammleAsync(string adresse, string pfad, List<QuellDatei> ergebnis, CancellationToken abbruch)
    {
        string? weiter = adresse + "?$select=id,name,eTag,lastModifiedDateTime,lastModifiedBy,file,folder&$top=200";
        while (weiter is not null)
        {
            var seite = await graph.HoleAsync<Seite>(weiter, abbruch);
            foreach (var element in seite.Werte)
            {
                var elementPfad = pfad.Length == 0 ? element.Name : $"{pfad}/{element.Name}";
                if (element.Ordner is not null)
                {
                    // Archiv-Ordner gar nicht erst lesen.
                    if (!element.Name.Equals("Archiv", StringComparison.OrdinalIgnoreCase))
                    {
                        await SammleAsync($"drives/{_laufwerk}/items/{element.Id}/children", elementPfad, ergebnis, abbruch);
                    }
                }
                else if (element.Datei is not null && element.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    ergebnis.Add(new QuellDatei(element.Id, elementPfad, element.Name, element.ETag, element.Geaendert, element.GeaendertVon?.Benutzer?.Name));
                }
            }

            weiter = seite.Weiter;
        }
    }

    /// <summary>Laufwerk (Dokumentbibliothek) der Website; einmal je Quelle ermittelt.</summary>
    private async Task<string> LaufwerkAsync(CancellationToken abbruch) =>
        _laufwerk ??= await graph.LaufwerkAsync(einstellungen.Website, einstellungen.Bibliothek, abbruch);

    private sealed record Seite(
        [property: JsonPropertyName("value")] List<Element> Werte,
        [property: JsonPropertyName("@odata.nextLink")] string? Weiter);

    private sealed record Element(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("eTag")] string? ETag,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? Geaendert,
        [property: JsonPropertyName("lastModifiedBy")] Identitaeten? GeaendertVon,
        [property: JsonPropertyName("file")] object? Datei,
        [property: JsonPropertyName("folder")] object? Ordner);

    private sealed record Identitaeten([property: JsonPropertyName("user")] Identitaet? Benutzer);

    private sealed record Identitaet([property: JsonPropertyName("displayName")] string? Name);
}
