using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>Verkaufspreis (netto) einer Preiskomponente in einer Preisliste.</summary>
public class Preis
{
    public int Id { get; set; }
    public int PreislisteId { get; set; }
    public int PreiskomponenteId { get; set; }
    public Preiskomponente? Preiskomponente { get; set; }

    /// <summary><c>null</c> = „auf Anfrage“ bzw. wird durch einen Spezialrechner ermittelt.</summary>
    public decimal? VkNetto { get; set; }
}
