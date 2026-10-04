using System.Text.RegularExpressions;
using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Vertrag;

/// <summary>Was ein Dateiname im SharePoint-Ordner des Vertragswerks über die Vorlage aussagt.</summary>
public sealed record VorlagenZuordnung(DokumentTyp Typ, string Code, string Bezeichnung, string? Version);

/// <summary>
/// Ordnet Dateien des Vertragswerks anhand ihres Namens einer Vorlage zu (Ablage in SharePoint, Stand 10/2026):
/// <list type="bullet">
/// <item><c>Leistungsschein S01 - Noesse Connect V1.0.docx</c> → S01</item>
/// <item><c>Bundle B01 - User as a Service Standard V1.0.docx</c> → B01</item>
/// <item><c>Vorlage Leistungsschein S14 V5.7.docx</c> → S14</item>
/// <item><c>Rahmenvertrag 01 - Grundvertrag V1.0.docx</c> → GRUNDVERTRAG; ebenso AVB, SLA und AVV am Titel erkannt.</item>
/// </list>
/// Alles andere (z. B. „S14 Baukasten und Preisbausteine“) wird nicht zugeordnet; Ordner „Archiv“ werden übersprungen.
/// </summary>
public static partial class Vorlagendateiname
{
    [GeneratedRegex(@"^(?:Vorlage\s+)?(?<art>Leistungsschein|Bundle|Rahmenvertrag)\s+(?<code>[A-Z]?\d+(?:-[A-Z0-9]+)?)(?:\s*-\s*|\s+)?(?<titel>.*?)\s*V(?<version>\d+(?:\.\d+)*)\.docx$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Muster();

    public static VorlagenZuordnung? Zuordnen(string dateiname)
    {
        var m = Muster().Match(dateiname.Trim());
        if (!m.Success)
        {
            return null;
        }

        var titel = m.Groups["titel"].Value.Trim();
        var version = m.Groups["version"].Value;
        var code = m.Groups["code"].Value.ToUpperInvariant();
        switch (m.Groups["art"].Value.ToUpperInvariant())
        {
            case "RAHMENVERTRAG":
                return Rahmen(titel) is { } rahmen ? rahmen with { Version = version } : null;
            case "BUNDLE":
                return code.StartsWith('B') ? new VorlagenZuordnung(DokumentTyp.Leistungsschein, code, $"Bundle {code} - {titel}".TrimEnd(' ', '-'), version) : null;
            default:
                return code.StartsWith('S') ? new VorlagenZuordnung(DokumentTyp.Leistungsschein, code, titel.Length > 0 ? titel : $"Leistungsschein {code}", version) : null;
        }
    }

    /// <summary>Ob eine Datei in diesem Pfad (relativ zum Vorlagenordner) übersprungen wird, z. B. im Archiv.</summary>
    public static bool Ueberspringen(string pfad) =>
        pfad.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).SkipLast(1)
            .Any(teil => teil.Equals("Archiv", StringComparison.OrdinalIgnoreCase))
        || Path.GetFileName(pfad).StartsWith("~$", StringComparison.Ordinal);

    private static VorlagenZuordnung? Rahmen(string titel)
    {
        bool Enthaelt(string wort) => titel.Contains(wort, StringComparison.OrdinalIgnoreCase);
        return Enthaelt("Grundvertrag") ? new(DokumentTyp.Grundvertrag, "GRUNDVERTRAG", "Grundvertrag", null)
            : Enthaelt("AVB") ? new(DokumentTyp.Avb, "AVB", titel, null)
            : Enthaelt("SLA") ? new(DokumentTyp.Sla, "SLA", titel, null)
            : Enthaelt("AVV") || Enthaelt("Auftragsverarbeitung") ? new(DokumentTyp.Avv, "AVV", titel, null)
            : null;
    }
}
