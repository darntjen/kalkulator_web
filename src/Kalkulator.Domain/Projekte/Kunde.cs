namespace Kalkulator.Domain.Projekte;

/// <summary>Minimaler Kundendatensatz. In v1 manuell gepflegt, später aus HubSpot (B-12).</summary>
public class Kunde
{
    public int Id { get; set; }
    public required string Firma { get; set; }
    public string? Strasse { get; set; }
    public string? Postleitzahl { get; set; }
    public string? Ort { get; set; }
    public string? Ansprechpartner { get; set; }

    /// <summary>Navision-Kundennummer; gegen sie wird später der Navision-Import geprüft (H-02).</summary>
    public string? NavisionKundennummer { get; set; }
}
