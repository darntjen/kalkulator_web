namespace Kalkulator.Domain.Projekte;

/// <summary>Art einer Unterlage im Kundenprojekt (G-02); entspricht den Ordnern der Kundenablage in Teams.</summary>
public enum UnterlagenArt
{
    Recherche = 1,
    Standortgespraech = 2,
    Analyse = 3,
    Workshop = 4,
    Angebot = 5,
    Protokoll = 6,
    Sonstiges = 9,
}

/// <summary>
/// Hochgeladene Unterlage eines Kundenprojekts, ergänzend zum Kanalordner in Teams (Entscheidung 04.10.2026). Der
/// Inhalt liegt getrennt in <see cref="UnterlageDatei"/>, damit Übersichten ihn nicht mitladen. Uploads werden drei
/// Jahre nach Abschluss des Projekts gelöscht (F-13), von Hand jederzeit.
/// </summary>
public class Unterlage
{
    /// <summary>Höchstgröße einer Datei in Byte (25 MB).</summary>
    public const long MaxGroesse = 25L * 1024 * 1024;

    /// <summary>Erlaubte Dateiendungen.</summary>
    public static readonly IReadOnlySet<string> Endungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".md", ".vtt", ".csv", ".png", ".jpg", ".jpeg", ".msg", ".eml",
    };

    public int Id { get; set; }
    public int KundenprojektId { get; init; }
    public UnterlagenArt Art { get; init; }
    public required string Dateiname { get; init; }
    public required string Inhaltstyp { get; init; }
    public long Groesse { get; init; }
    public string? Beschreibung { get; init; }
    public required string HochgeladenVon { get; init; }
    public DateTimeOffset HochgeladenAm { get; init; }

    public UnterlageDatei? Datei { get; init; }

    /// <summary>Prüft Name, Endung und Größe; die Meldung erscheint so in der Oberfläche.</summary>
    public static void Pruefe(string dateiname, long groesse)
    {
        if (string.IsNullOrWhiteSpace(dateiname) || dateiname.Length > 255)
        {
            throw new ArgumentException("Der Dateiname fehlt oder ist länger als 255 Zeichen.");
        }

        if (!Endungen.Contains(Path.GetExtension(dateiname)))
        {
            throw new ArgumentException($"Dateien vom Typ „{Path.GetExtension(dateiname)}“ werden nicht angenommen. Erlaubt sind {string.Join(", ", Endungen.Order())}.");
        }

        if (groesse <= 0 || groesse > MaxGroesse)
        {
            throw new ArgumentException($"Die Datei ist leer oder größer als {MaxGroesse / 1024 / 1024} MB.");
        }
    }
}

public class UnterlageDatei
{
    public int UnterlageId { get; set; }
    public required byte[] Inhalt { get; init; }
}
