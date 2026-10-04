namespace Kalkulator.Domain.Katalog;

/// <summary>Protokoll eines Abgleichs mit der Vorlagenquelle (nächtlich oder auf Knopfdruck).</summary>
public class Vorlagenabgleich
{
    public int Id { get; set; }
    public required string Quelle { get; init; }
    public required string AusgeloestVon { get; init; }
    public DateTimeOffset Beginn { get; init; }
    public DateTimeOffset? Ende { get; set; }
    public int Dateien { get; set; }
    public int NeueFassungen { get; set; }

    /// <summary>Was gefunden wurde, eine Zeile je Datei bzw. Meldung.</summary>
    public string? Bericht { get; set; }

    /// <summary>Gesetzt, wenn der Abgleich abgebrochen ist (z. B. keine Verbindung zu SharePoint).</summary>
    public string? Fehler { get; set; }
}
