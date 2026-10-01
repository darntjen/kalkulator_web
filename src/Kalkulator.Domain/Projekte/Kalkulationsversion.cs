using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Eingefrorener Angebotsstand einer Kalkulation (V1, V2 …). Unveränderlich: Spätere Preis- oder Katalogänderungen
/// verändern alte Angebote nicht (docs/03_fachmodell.md, Designprinzip 2).
/// </summary>
public class Kalkulationsversion
{
    public int Id { get; set; }
    public int KalkulationId { get; set; }
    public int Nummer { get; init; }
    public DateTimeOffset ErstelltAm { get; init; }
    public required string ErstelltVon { get; init; }

    /// <summary>Preisliste, mit der berechnet wurde.</summary>
    public int PreislisteId { get; init; }

    /// <summary>Vollständige Eingabe inklusive Sonderpositionen, so wie sie berechnet wurde.</summary>
    public required KalkulationsEingabe Eingabe { get; init; }

    public DateOnly? Vertragsbeginn { get; init; }
    public decimal SummeMonatlich { get; init; }
    public decimal SummeEinmalig { get; init; }

    public List<VersionsPosition> Positionen { get; } = [];

    public string Bezeichnung => $"V{Nummer}";

    /// <summary>ARR: 12 × monatliche Summe.</summary>
    public decimal Jahreswert => 12 * SummeMonatlich;

    /// <summary>Wert der Erstlaufzeit von 12 Monaten inklusive Einmalkosten.</summary>
    public decimal WertErstlaufzeit => Jahreswert + SummeEinmalig;
}

/// <summary>Eine eingefrorene Ergebniszeile; Grundlage für Angebot, Vertrag und Statistik je Service (E-03).</summary>
public class VersionsPosition
{
    public int Id { get; set; }
    public int KalkulationsversionId { get; set; }
    public int Reihenfolge { get; init; }
    public required string Code { get; init; }
    public required string Bezeichnung { get; init; }
    public string? ServiceCode { get; init; }
    public decimal Menge { get; init; }
    public decimal BerechneteMenge { get; init; }
    public decimal? Einzelpreis { get; init; }
    public decimal Betrag { get; init; }
    public Abrechnungsart Abrechnungsart { get; init; }
    public PositionsHerkunft Herkunft { get; init; }
    public string? Hinweis { get; init; }

    /// <summary>Interne Kosten in eigener Tabelle; nur für berechtigte Rollen laden (Designprinzip 4).</summary>
    public PositionsKosten? Kosten { get; init; }
}

/// <summary>Interne Kosten einer eingefrorenen Zeile; <c>null</c>, wenn der EK fehlte.</summary>
public class PositionsKosten
{
    public int VersionsPositionId { get; set; }
    public decimal? Kosten { get; init; }
}
