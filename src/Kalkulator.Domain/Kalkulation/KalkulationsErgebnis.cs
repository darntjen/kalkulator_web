using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Kalkulation;

/// <summary>Woher eine Ergebniszeile stammt; wichtig für Angebot, Vertrag und Statistik.</summary>
public enum PositionsHerkunft
{
    Katalog = 1,
    Onboarding = 2,
    Supportkontingent = 3,
    ServerBackup = 4,
    CloudServer = 5,
    Sonderposition = 6,
}

/// <summary>
/// Eine berechnete Zeile. <see cref="Menge"/> ist die gebuchte Menge, <see cref="BerechneteMenge"/> die Menge,
/// die nach Abzug der in Bundles enthaltenen Einheiten tatsächlich berechnet wird (Regel R3).
/// </summary>
public sealed record ErgebnisPosition(
    string Code,
    string Bezeichnung,
    string? ServiceCode,
    decimal Menge,
    decimal BerechneteMenge,
    decimal? Einzelpreis,
    decimal Betrag,
    Abrechnungsart Abrechnungsart,
    PositionsHerkunft Herkunft,
    string? Hinweis = null)
{
    /// <summary>Interne Kosten dieser Zeile; <c>null</c>, wenn der EK fehlt. Nur für berechtigte Rollen anzeigen.</summary>
    public decimal? Kosten { get; init; }
}

public enum Schwere
{
    Hinweis = 1,

    /// <summary>Blockiert Angebot und Vertragspaket.</summary>
    Fehler = 2,
}

public sealed record Meldung(Schwere Schwere, string Text);

public sealed record SupportkontingentErgebnis(
    decimal BedarfAe,
    int KontingentAe,
    decimal Stunden,
    decimal Monatspreis,
    decimal AdHocPreis)
{
    public decimal Kundenvorteil => AdHocPreis - Monatspreis;
}

public sealed record ServerBackupErgebnis(decimal SummeBausteine, decimal Preisuntergrenze, decimal Monatspreis)
{
    public bool UntergrenzeGreift => Monatspreis > SummeBausteine;
}

public sealed record VorherNachher(decimal Bisher, decimal Neu)
{
    public decimal Differenz => Neu - Bisher;

    /// <summary>Veränderung in Prozent, auf zwei Stellen gerundet; <c>null</c>, wenn bisher 0 € gezahlt wurde.</summary>
    public decimal? DifferenzProzent =>
        Bisher == 0 ? null : Math.Round(Differenz / Bisher * 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed record KalkulationsErgebnis(
    IReadOnlyList<ErgebnisPosition> Positionen,
    IReadOnlyList<Meldung> Meldungen,
    SupportkontingentErgebnis? Supportkontingent,
    ServerBackupErgebnis? ServerBackup,
    VorherNachher? Vergleich)
{
    public decimal SummeMonatlich => Positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich).Sum(p => p.Betrag);

    public decimal SummeEinmalig => Positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Einmalig).Sum(p => p.Betrag);

    /// <summary>ARR: 12 × monatliche Summe.</summary>
    public decimal Jahreswert => 12 * SummeMonatlich;

    /// <summary>Wert der Erstlaufzeit von 12 Monaten inklusive Einmalkosten.</summary>
    public decimal WertErstlaufzeit => Jahreswert + SummeEinmalig;

    public bool HatFehler => Meldungen.Any(m => m.Schwere == Schwere.Fehler);

    /// <summary>Angebot und Vertragspaket sind nur ohne Fehler möglich; dazu zählen nicht freigegebene Sonderpositionen.</summary>
    public bool AngebotMoeglich => !HatFehler;

    /// <summary>Monatliche Kosten der Zeilen mit bekanntem EK. Nur für berechtigte Rollen.</summary>
    public decimal KostenMonatlich =>
        Positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich).Sum(p => p.Kosten ?? 0m);

    /// <summary>Ob für alle berechneten monatlichen Zeilen ein EK vorliegt.</summary>
    public bool KostenVollstaendig =>
        Positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich && p.Betrag != 0).All(p => p.Kosten is not null);

    public decimal DeckungsbeitragMonatlich => SummeMonatlich - KostenMonatlich;

    /// <summary>Marge als Anteil (0,57 = 57 %); <c>null</c> ohne monatlichen Umsatz.</summary>
    public decimal? Marge =>
        SummeMonatlich == 0 ? null : Math.Round(DeckungsbeitragMonatlich / SummeMonatlich, 4, MidpointRounding.AwayFromZero);
}
