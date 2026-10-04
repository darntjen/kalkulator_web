using System.Text.Json.Serialization;
using Kalkulator.Infrastructure.Vorlagen;

namespace Kalkulator.Infrastructure.Ablage;

/// <summary>
/// Abschnitt „Kundenablage“ der Konfiguration (Phase 4): die Kanalordner der Kunden im Team „Kundenprojekte“. Die
/// Anmeldung an Microsoft Graph kommt aus „Vorlagen:SharePoint“ (dieselbe App-Registrierung, Rolle „read“ auf die
/// Website des Teams).
/// </summary>
public sealed class KundenablageEinstellungen
{
    public const string Abschnitt = "Kundenablage";

    /// <summary>„SharePoint“, „Ordner“ oder leer (keine Verknüpfung mit Teams; Upload geht trotzdem).</summary>
    public string? Quelle { get; set; }

    /// <summary>Bei Quelle „Ordner“: lokaler Ordner, dessen Unterordner die Kanalordner sind.</summary>
    public string? Ordner { get; set; }

    /// <summary>Website des Teams, z. B. https://noessedatentechnik.sharepoint.com/sites/Kundenprojekte.</summary>
    public string? Website { get; set; }

    /// <summary>Dokumentbibliothek; leer heißt Standardbibliothek („Dokumente“).</summary>
    public string? Bibliothek { get; set; }

    /// <summary>Wie viele Jahre nach Abschluss eines Projekts seine Uploads gelöscht werden (Entscheidung 04.10.2026).</summary>
    public int AufbewahrungJahre { get; set; } = 3;

    /// <summary>Uhrzeit (Ortszeit) der täglichen Löschung abgelaufener Uploads; leer schaltet sie ab.</summary>
    public string? LoeschungUm { get; set; } = "03:15";
}

/// <summary>Ein Kanalordner im Team „Kundenprojekte“, z. B. „Muster Spedition GmbH“.</summary>
public sealed record Kanalordner(string Id, string Name);

/// <summary>
/// Eine Datei im Kanalordner. <see cref="Bereich"/> ist der Ordner des Nösse-Standards (z. B. „30_Analyse“),
/// <see cref="Pfad"/> der Weg ab dem Kanalordner. <see cref="Link"/> öffnet die Datei in SharePoint, falls vorhanden.
/// </summary>
public sealed record AblageDatei(string Id, string Bereich, string Pfad, string Name, long Groesse, DateTimeOffset? GeaendertAm, string? GeaendertVon, string? Link);

/// <summary>Lesender Zugriff auf die Kanalordner der Kunden (Entscheidung 04.10.2026: Teams bleibt Leitablage).</summary>
public interface IKundenablage
{
    /// <summary>Kurzname für die Anzeige, z. B. „Teams“.</summary>
    string Name { get; }

    /// <summary>Ob die Ablage eingerichtet ist; sonst steht in <see cref="Grund"/>, was fehlt.</summary>
    bool Eingerichtet { get; }

    string? Grund { get; }

    Task<IReadOnlyList<Kanalordner>> KanaeleAsync(CancellationToken abbruch);

    /// <summary>Dateien der gelesenen Bereiche (<see cref="Kundenablage.Bereiche"/>) eines Kanalordners, mit Unterordnern.</summary>
    Task<IReadOnlyList<AblageDatei>> DateienAsync(string kanalId, CancellationToken abbruch);

    /// <summary>Inhalt einer Datei; nur aus den gelesenen Bereichen des angegebenen Kanalordners.</summary>
    Task<(string Name, byte[] Inhalt)> LadeAsync(string kanalId, string dateiId, CancellationToken abbruch);
}

public static class Kundenablage
{
    /// <summary>
    /// Ordner des Nösse-Standards (Skill „kundenprojekt-anlegen“), die der Kalkulator liest. 50_Umsetzung,
    /// 70_Vertraege und 99_Archiv bleiben außen vor.
    /// </summary>
    public static readonly IReadOnlyList<string> Bereiche =
        ["00_Kundenakte", "10_Recherche", "20_Standortgespraech", "30_Analyse", "40_Workshop_Roadmap", "60_Angebote", "80_Protokolle"];

    public static bool IstBereich(string ordnername) => Bereiche.Contains(ordnername, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Vorschlag eines Kanalordners für eine Firma: Kanalnamen sind in Teams auf 50 Zeichen begrenzt, deshalb gilt ein
    /// Ordner, dessen Name gleich der Firma ist oder mit dem die (lange) Firma beginnt. Nur ein eindeutiger Treffer.
    /// </summary>
    public static Kanalordner? Vorschlag(string firma, IEnumerable<Kanalordner> kanaele)
    {
        var name = firma.Trim();
        var treffer = kanaele.Where(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || (name.Length > 50 && k.Name.Length >= 40 && name.StartsWith(k.Name.TrimEnd(), StringComparison.OrdinalIgnoreCase))).ToList();
        return treffer.Count == 1 ? treffer[0] : null;
    }
}

/// <summary>Ohne Konfiguration: keine Verknüpfung mit Teams möglich.</summary>
public sealed class KeineKundenablage(string grund = "Die Kundenablage in Teams ist nicht eingerichtet (Kundenablage:Quelle).") : IKundenablage
{
    public string Name => "keine";

    public bool Eingerichtet => false;

    public string? Grund => grund;

    public Task<IReadOnlyList<Kanalordner>> KanaeleAsync(CancellationToken abbruch) => throw new InvalidOperationException(grund);

    public Task<IReadOnlyList<AblageDatei>> DateienAsync(string kanalId, CancellationToken abbruch) => throw new InvalidOperationException(grund);

    public Task<(string Name, byte[] Inhalt)> LadeAsync(string kanalId, string dateiId, CancellationToken abbruch) => throw new InvalidOperationException(grund);
}

/// <summary>Lokaler Ordner mit derselben Struktur wie das Team (für Entwicklung und Tests); Kennungen sind relative Pfade.</summary>
public sealed class OrdnerKundenablage(string ordner) : IKundenablage
{
    public string Name => "Ordner";

    public bool Eingerichtet => true;

    public string? Grund => null;

    public Task<IReadOnlyList<Kanalordner>> KanaeleAsync(CancellationToken abbruch)
    {
        if (!Directory.Exists(ordner))
        {
            throw new DirectoryNotFoundException($"Den Ordner der Kundenablage „{ordner}“ gibt es nicht.");
        }

        IReadOnlyList<Kanalordner> kanaele = [.. new DirectoryInfo(ordner).EnumerateDirectories()
            .Select(d => new Kanalordner(d.Name, d.Name))
            .OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)];
        return Task.FromResult(kanaele);
    }

    public Task<IReadOnlyList<AblageDatei>> DateienAsync(string kanalId, CancellationToken abbruch)
    {
        var kanal = Kanal(kanalId);
        var dateien = new List<AblageDatei>();
        foreach (var bereich in new DirectoryInfo(kanal).EnumerateDirectories().Where(d => Kundenablage.IstBereich(d.Name)))
        {
            foreach (var datei in bereich.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var pfad = Path.GetRelativePath(kanal, datei.FullName).Replace('\\', '/');
                dateien.Add(new AblageDatei(pfad, bereich.Name, pfad, datei.Name, datei.Length,
                    new DateTimeOffset(datei.LastWriteTimeUtc, TimeSpan.Zero), null, null));
            }
        }

        IReadOnlyList<AblageDatei> ergebnis = [.. dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal)];
        return Task.FromResult(ergebnis);
    }

    public async Task<(string Name, byte[] Inhalt)> LadeAsync(string kanalId, string dateiId, CancellationToken abbruch)
    {
        var kanal = Kanal(kanalId);
        var voll = Path.GetFullPath(Path.Combine(kanal, dateiId));
        var bereich = Path.GetRelativePath(kanal, voll).Replace('\\', '/').Split('/')[0];
        if (!voll.StartsWith(kanal + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Kundenablage.IstBereich(bereich) || !File.Exists(voll))
        {
            throw new KeyNotFoundException("Diese Datei gibt es im Kanalordner nicht.");
        }

        return (Path.GetFileName(voll), await File.ReadAllBytesAsync(voll, abbruch));
    }

    /// <summary>Vollständiger Pfad des Kanalordners; die Kennung darf nicht aus dem Ordner herausführen.</summary>
    private string Kanal(string kanalId)
    {
        var wurzel = Path.GetFullPath(ordner);
        var kanal = Path.GetFullPath(Path.Combine(wurzel, kanalId));
        if (Path.GetDirectoryName(kanal) != wurzel.TrimEnd(Path.DirectorySeparatorChar) || !Directory.Exists(kanal))
        {
            throw new KeyNotFoundException($"Den Kanalordner „{kanalId}“ gibt es nicht.");
        }

        return kanal;
    }
}

/// <summary>
/// Die Kanalordner im Team „Kundenprojekte“, gelesen über Microsoft Graph. Jeder Standardkanal ist ein Ordner in der
/// Dokumentbibliothek der Team-Website. Die App braucht Lesezugriff (Sites.Selected, Rolle „read“).
/// </summary>
public sealed class SharePointKundenablage(GraphZugang graph, KundenablageEinstellungen einstellungen) : IKundenablage
{
    private string? _laufwerk;

    public string Name => "Teams";

    public bool Eingerichtet => true;

    public string? Grund => null;

    public async Task<IReadOnlyList<Kanalordner>> KanaeleAsync(CancellationToken abbruch)
    {
        var laufwerk = await LaufwerkAsync(abbruch);
        var elemente = await AlleAsync($"drives/{laufwerk}/root/children", abbruch);
        return [.. elemente.Where(e => e.Ordner is not null)
            .Select(e => new Kanalordner(e.Id, e.Name))
            .OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<IReadOnlyList<AblageDatei>> DateienAsync(string kanalId, CancellationToken abbruch)
    {
        var laufwerk = await LaufwerkAsync(abbruch);
        var dateien = new List<AblageDatei>();
        foreach (var bereich in (await AlleAsync($"drives/{laufwerk}/items/{Kennung(kanalId)}/children", abbruch))
            .Where(e => e.Ordner is not null && Kundenablage.IstBereich(e.Name)))
        {
            await SammleAsync(laufwerk, bereich, bereich.Name, bereich.Name, dateien, abbruch);
        }

        return [.. dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal)];
    }

    public async Task<(string Name, byte[] Inhalt)> LadeAsync(string kanalId, string dateiId, CancellationToken abbruch)
    {
        var laufwerk = await LaufwerkAsync(abbruch);
        var kanal = await graph.HoleAsync<Element>($"drives/{laufwerk}/items/{Kennung(kanalId)}?$select=id,name", abbruch);
        var datei = await graph.HoleAsync<Element>($"drives/{laufwerk}/items/{Kennung(dateiId)}?$select=id,name,file,parentReference", abbruch);

        // Nur Dateien aus den gelesenen Bereichen dieses Kanalordners.
        var pfad = Uri.UnescapeDataString(datei.Eltern?.Pfad ?? "") + "/";
        var marke = $"/root:/{kanal.Name}/";
        var start = pfad.IndexOf(marke, StringComparison.OrdinalIgnoreCase);
        var bereich = start < 0 ? "" : pfad[(start + marke.Length)..].Split('/')[0];
        if (datei.Datei is null || start < 0 || !Kundenablage.IstBereich(bereich))
        {
            throw new KeyNotFoundException("Diese Datei gibt es im Kanalordner nicht.");
        }

        return (datei.Name, await graph.LadeAsync($"drives/{laufwerk}/items/{datei.Id}/content", abbruch));
    }

    private async Task SammleAsync(string laufwerk, Element ordner, string bereich, string pfad, List<AblageDatei> ergebnis, CancellationToken abbruch)
    {
        foreach (var element in await AlleAsync($"drives/{laufwerk}/items/{ordner.Id}/children", abbruch))
        {
            var elementPfad = $"{pfad}/{element.Name}";
            if (element.Ordner is not null)
            {
                await SammleAsync(laufwerk, element, bereich, elementPfad, ergebnis, abbruch);
            }
            else if (element.Datei is not null)
            {
                ergebnis.Add(new AblageDatei(element.Id, bereich, elementPfad, element.Name, element.Groesse ?? 0, element.Geaendert,
                    element.GeaendertVon?.Benutzer?.Name, element.Link));
            }
        }
    }

    private async Task<List<Element>> AlleAsync(string adresse, CancellationToken abbruch)
    {
        var alle = new List<Element>();
        string? weiter = adresse + "?$select=id,name,size,webUrl,lastModifiedDateTime,lastModifiedBy,file,folder&$top=200";
        while (weiter is not null)
        {
            var seite = await graph.HoleAsync<Seite>(weiter, abbruch);
            alle.AddRange(seite.Werte);
            weiter = seite.Weiter;
        }

        return alle;
    }

    /// <summary>Kennungen aus Graph bestehen aus Buchstaben, Ziffern und „!“; alles andere wird abgewiesen.</summary>
    private static string Kennung(string id) =>
        id.Length is > 0 and <= 200 && id.All(z => char.IsAsciiLetterOrDigit(z) || z is '!' or '-' or '_')
            ? id
            : throw new KeyNotFoundException("Ungültige Kennung.");

    private async Task<string> LaufwerkAsync(CancellationToken abbruch) =>
        _laufwerk ??= await graph.LaufwerkAsync(einstellungen.Website, einstellungen.Bibliothek, abbruch);

    private sealed record Seite(
        [property: JsonPropertyName("value")] List<Element> Werte,
        [property: JsonPropertyName("@odata.nextLink")] string? Weiter);

    private sealed record Element(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long? Groesse = null,
        [property: JsonPropertyName("webUrl")] string? Link = null,
        [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset? Geaendert = null,
        [property: JsonPropertyName("lastModifiedBy")] Identitaeten? GeaendertVon = null,
        [property: JsonPropertyName("file")] object? Datei = null,
        [property: JsonPropertyName("folder")] object? Ordner = null,
        [property: JsonPropertyName("parentReference")] Eltern? Eltern = null);

    private sealed record Eltern([property: JsonPropertyName("path")] string? Pfad);

    private sealed record Identitaeten([property: JsonPropertyName("user")] Identitaet? Benutzer);

    private sealed record Identitaet([property: JsonPropertyName("displayName")] string? Name);
}
