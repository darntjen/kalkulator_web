namespace Kalkulator.Infrastructure.Vorlagen;

/// <summary>
/// Eine Datei in der Vorlagenquelle. <see cref="Pfad"/> ist relativ zum Vorlagenordner und mit „/“ getrennt;
/// <see cref="Stand"/> ändert sich mit jeder Änderung der Datei (ETag in SharePoint, sonst Zeitstempel und Größe).
/// </summary>
public sealed record QuellDatei(string Id, string Pfad, string Name, string? Stand, DateTimeOffset? GeaendertAm, string? GeaendertVon);

/// <summary>Woher die Vertragsvorlagen kommen: SharePoint oder (für Tests und den Übergang) ein lokaler Ordner.</summary>
public interface IVorlagenQuelle
{
    /// <summary>Kurzname für Protokoll und Anzeige, z. B. „SharePoint“.</summary>
    string Name { get; }

    /// <summary>Wo gesucht wird, z. B. Adresse und Ordner; für die Anzeige.</summary>
    string Ort { get; }

    /// <summary>Alle Word-Dateien (.docx) unterhalb des Vorlagenordners, einschließlich Unterordnern.</summary>
    Task<IReadOnlyList<QuellDatei>> ListeAsync(CancellationToken abbruch);

    Task<byte[]> LadeAsync(QuellDatei datei, CancellationToken abbruch);
}

/// <summary>Lokaler Ordner als Quelle; Struktur wie in SharePoint.</summary>
public sealed class OrdnerQuelle(string ordner) : IVorlagenQuelle
{
    public string Name => "Ordner";

    public string Ort => ordner;

    public Task<IReadOnlyList<QuellDatei>> ListeAsync(CancellationToken abbruch)
    {
        if (!Directory.Exists(ordner))
        {
            throw new DirectoryNotFoundException($"Den Vorlagenordner „{ordner}“ gibt es nicht.");
        }

        IReadOnlyList<QuellDatei> dateien = [.. Directory.EnumerateFiles(ordner, "*.docx", SearchOption.AllDirectories)
            .Select(pfad => new FileInfo(pfad))
            .Select(info => new QuellDatei(
                Path.GetRelativePath(ordner, info.FullName).Replace('\\', '/'),
                Path.GetRelativePath(ordner, info.FullName).Replace('\\', '/'),
                info.Name,
                $"{info.LastWriteTimeUtc.Ticks}-{info.Length}",
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                null))
            .OrderBy(d => d.Pfad, StringComparer.Ordinal)];
        return Task.FromResult(dateien);
    }

    public Task<byte[]> LadeAsync(QuellDatei datei, CancellationToken abbruch) =>
        File.ReadAllBytesAsync(Path.Combine(ordner, datei.Id), abbruch);
}
