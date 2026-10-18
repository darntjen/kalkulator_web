using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.Extensions.Logging;

namespace Kalkulator.Infrastructure.Benachrichtigung;

/// <summary>
/// Abschnitt „Benachrichtigung“ der Konfiguration (Entscheidung 16.10.2026): Der Kalkulator schreibt Ereignisse in eine
/// SharePoint-Liste, ein Power-Automate-Flow verschickt daraus die E-Mails. Die Anmeldung an Microsoft Graph kommt aus
/// „Vorlagen:SharePoint“; die App braucht Schreibrecht auf die Website der Liste (Sites.Selected, Rolle „write“).
/// </summary>
public sealed class BenachrichtigungsEinstellungen
{
    public const string Abschnitt = "Benachrichtigung";

    /// <summary>Website der Liste, z. B. https://noessedatentechnik.sharepoint.com/sites/Kundenprojekte; leer = aus.</summary>
    public string? Website { get; set; }

    /// <summary>Anzeigename oder Kennung der Liste.</summary>
    public string Liste { get; set; } = "Kalkulator-Benachrichtigungen";

    /// <summary>Feste Empfänger beim unterschriebenen Vertrag; dazu kommt der verantwortliche Vertrieb (15.3).</summary>
    public List<string> EmpfaengerVertrag { get; set; } = [];

    /// <summary>Adresse des Kalkulators für Links in den Nachrichten, z. B. https://kalkulator.noesse.de/; leer = ohne Link.</summary>
    public string? KalkulatorAdresse { get; set; }

    public bool Aktiv => !string.IsNullOrWhiteSpace(Website);
}

/// <summary>Ein Ereignis für den Flow: Art, Betreff, Text, Empfänger (E-Mail-Adressen) und Links.</summary>
public sealed record Nachricht(string Art, string Titel, string Text, IReadOnlyList<string> Empfaenger, string? Dokument, string? Kalkulator);

public interface IBenachrichtigung
{
    bool Eingerichtet { get; }

    Task MeldenAsync(Nachricht nachricht, CancellationToken abbruch);
}

/// <summary>Ohne Liste: Nachrichten gehen nur ins Protokoll des Servers (Entwicklung, Codespace).</summary>
public sealed class ProtokollBenachrichtigung(ILogger<ProtokollBenachrichtigung> protokoll) : IBenachrichtigung
{
    public bool Eingerichtet => false;

    public Task MeldenAsync(Nachricht nachricht, CancellationToken abbruch)
    {
        protokoll.LogInformation("Benachrichtigung (nicht eingerichtet) {Art} an {Empfaenger}: {Titel}", nachricht.Art, string.Join("; ", nachricht.Empfaenger), nachricht.Titel);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Schreibt die Nachricht als Eintrag in die SharePoint-Liste. Spalten (interne Namen): Title, Art, Empfaenger (mehrere
/// Zeilen, durch „;“ getrennt), Text (mehrere Zeilen), Dokument, Kalkulator. Einrichtung: docs/12_vertragsvorlagen.md.
/// </summary>
public sealed class SharePointBenachrichtigung(GraphZugang graph, BenachrichtigungsEinstellungen einstellungen) : IBenachrichtigung
{
    private string? _website;

    public bool Eingerichtet => true;

    public async Task MeldenAsync(Nachricht nachricht, CancellationToken abbruch)
    {
        _website ??= await WebsiteAsync(abbruch);
        var felder = new Dictionary<string, string?>
        {
            ["Title"] = Kurz(nachricht.Titel, 255),
            ["Art"] = nachricht.Art,
            ["Empfaenger"] = string.Join("; ", nachricht.Empfaenger),
            ["Text"] = nachricht.Text,
            ["Dokument"] = nachricht.Dokument,
            ["Kalkulator"] = nachricht.Kalkulator,
        };
        using var anfrage = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/sites/{_website}/lists/{Uri.EscapeDataString(einstellungen.Liste)}/items")
        {
            Content = JsonContent.Create(new { fields = felder.Where(f => f.Value is not null).ToDictionary() }),
        };
        using var antwort = await graph.SendeAsync(anfrage, abbruch);
    }

    private async Task<string> WebsiteAsync(CancellationToken abbruch)
    {
        var adresse = new Uri(einstellungen.Website!);
        return (await graph.HoleAsync<Element>($"sites/{adresse.Host}:{adresse.AbsolutePath.TrimEnd('/')}", abbruch)).Id;
    }

    private static string Kurz(string text, int laenge) => text.Length > laenge ? text[..laenge] : text;

    private sealed record Element([property: JsonPropertyName("id")] string Id);
}
