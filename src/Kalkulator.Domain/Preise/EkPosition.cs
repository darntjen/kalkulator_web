using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>
/// Interne Kostenkalkulation einer Preiskomponente (Quelle: „Kalkulation EK und Deckungsbeitrag“).
/// Nur für Produktmanagement und Führung sichtbar.
/// </summary>
public class EkPosition
{
    public int Id { get; set; }
    public int PreislisteId { get; set; }
    public int PreiskomponenteId { get; set; }
    public Preiskomponente? Preiskomponente { get; set; }

    /// <summary>Einkauf bzw. Lizenzkosten je Einheit und Monat.</summary>
    public decimal EkLizenz { get; set; }

    /// <summary>Betriebsaufwand in Minuten je Einheit und Monat.</summary>
    public decimal? AufwandMinuten { get; set; }

    /// <summary>Fester Betriebsaufwand in Euro, falls nicht in Minuten kalkuliert (z. B. MDR).</summary>
    public decimal? BetriebFix { get; set; }

    public decimal Overhead { get; set; }

    /// <summary>
    /// Bei Bundles: Kosten ergeben sich aus den Bestandteilen plus <see cref="KostenKorrektur"/>
    /// (z. B. −1,50 €, weil die RMM-Lizenz nur einmal je Gerät anfällt).
    /// </summary>
    public bool AusBestandteilen { get; set; }

    public decimal KostenKorrektur { get; set; }
    public string? Anmerkung { get; set; }

    /// <summary>
    /// Betriebskosten = Aufwand in Minuten × EK-Kostensatz ÷ 60, kaufmännisch auf Cent gerundet wie im Excel.
    /// Erst multiplizieren, dann teilen: sonst entstehen Rundungsreste (2 Min. ergäben 2,02 statt 2,03 €).
    /// </summary>
    public decimal Betrieb(decimal ekKostensatzProStunde) =>
        BetriebFix ?? Math.Round((AufwandMinuten ?? 0m) * ekKostensatzProStunde / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Gesamtkosten einer Einzelposition; für Bundles siehe <see cref="AusBestandteilen"/>.</summary>
    public decimal Kosten(decimal ekKostensatzProStunde) =>
        Math.Round(EkLizenz + Betrieb(ekKostensatzProStunde) + Overhead + KostenKorrektur, 2, MidpointRounding.AwayFromZero);

    internal EkPosition Kopie() => new()
    {
        PreiskomponenteId = PreiskomponenteId,
        EkLizenz = EkLizenz,
        AufwandMinuten = AufwandMinuten,
        BetriebFix = BetriebFix,
        Overhead = Overhead,
        AusBestandteilen = AusBestandteilen,
        KostenKorrektur = KostenKorrektur,
        Anmerkung = Anmerkung,
    };
}
