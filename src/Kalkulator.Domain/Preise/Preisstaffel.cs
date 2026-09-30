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

    /// <summary>Name der Stufe für Angebot und Vertrag, z. B. „XS (bis 30 User)“.</summary>
    public string? Bezeichnung { get; set; }

    /// <summary>Navision-Artikel dieser Stufe, z. B. je Onboarding-Größe und Connect-Stufe.</summary>
    public string? NavisionArtikelnummer { get; set; }
}
