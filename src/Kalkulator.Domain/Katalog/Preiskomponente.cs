namespace Kalkulator.Domain.Katalog;

/// <summary>
/// Abrechenbare Einheit eines Service. Ein Service kann mehrere Komponenten haben,
/// z. B. MDR je User und je Server oder Schwachstellenmanagement mit Grundservice und Assetpreisen.
/// </summary>
public class Preiskomponente
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }

    /// <summary>Eindeutiger Code, z. B. „S31-USER“ oder „S01-STD-ONB“.</summary>
    public required string Code { get; set; }

    public required string Bezeichnung { get; set; }
    public Einheit Einheit { get; set; }
    public Abrechnungsart Abrechnungsart { get; set; } = Abrechnungsart.Monatlich;

    /// <summary>Gibt an, ob und wonach der Preis gestaffelt ist (siehe <see cref="Preise.Preisstaffel"/>).</summary>
    public StaffelBezug StaffelBezug { get; set; }

    public int Sortierung { get; set; }
}
