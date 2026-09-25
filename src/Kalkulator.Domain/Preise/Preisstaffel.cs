using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>Stufe einer Preisstaffel, z. B. Onboarding „ab 31 User 1.400 €“ oder S41 „ab 51 Mitarbeitende 550 €“.</summary>
public class Preisstaffel
{
    public int Id { get; set; }
    public int PreislisteId { get; set; }
    public int PreiskomponenteId { get; set; }
    public Preiskomponente? Preiskomponente { get; set; }

    /// <summary>Untergrenze der Stufe (einschließlich).</summary>
    public int AbMenge { get; set; }

    /// <summary><c>null</c> = individuell kalkulieren (Projektkalkulation).</summary>
    public decimal? VkNetto { get; set; }
}
