namespace Kalkulator.Infrastructure.Persistenz;

/// <summary>Eintrag im Änderungsprotokoll: wer hat wann was geändert (Anforderungen A-08, F-09).</summary>
public class AenderungsEintrag
{
    public long Id { get; set; }
    public DateTimeOffset Zeitpunkt { get; set; }
    public required string Benutzer { get; set; }
    public required string Entitaet { get; set; }
    public required string Schluessel { get; set; }
    public required string Aktion { get; set; }

    /// <summary>Bei Änderungen: geänderte Felder mit altem und neuem Wert als JSON.</summary>
    public string? Aenderungen { get; set; }
}
