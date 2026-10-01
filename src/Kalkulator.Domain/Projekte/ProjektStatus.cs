namespace Kalkulator.Domain.Projekte;

/// <summary>Projektstatus eines Kundenprojekts (docs/03_fachmodell.md, bestätigt 25.09.2026).</summary>
public enum ProjektStatus
{
    Entwurf = 1,
    AngebotVersendet = 2,
    VertragErstellt = 3,
    Gewonnen = 4,
    Verloren = 5,
    Zurueckgestellt = 6,
}

/// <summary>
/// Auswahlliste für „Verloren“ (B-09, bestätigt 02.10.2026); Details stehen im Freitext.
/// </summary>
public enum Verlustgrund
{
    Preis = 1,
    Wettbewerber = 2,
    KeinBedarf = 3,
    FalscherZeitpunkt = 4,
    InterneLoesung = 5,
    KeineRueckmeldung = 6,
    Sonstiges = 99,
}

/// <summary>Ein Eintrag in der Statushistorie; Grundlage für Pipeline- und Trendstatistiken.</summary>
public class StatusEreignis
{
    public long Id { get; set; }
    public int KundenprojektId { get; set; }

    /// <summary><c>null</c> beim Anlegen des Kundenprojekts.</summary>
    public ProjektStatus? Alt { get; init; }

    public ProjektStatus Neu { get; init; }
    public DateTimeOffset Zeitpunkt { get; init; }
    public required string Benutzer { get; init; }
    public Verlustgrund? Verlustgrund { get; init; }
    public string? Kommentar { get; init; }
}

public class UngueltigerStatuswechselException(ProjektStatus alt, ProjektStatus neu)
    : InvalidOperationException($"Der Status kann nicht von „{alt}“ auf „{neu}“ wechseln.");
