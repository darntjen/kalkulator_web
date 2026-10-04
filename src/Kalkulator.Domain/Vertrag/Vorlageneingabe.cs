namespace Kalkulator.Domain.Vertrag;

/// <summary>Art einer Eingabe, die eine Vertragsvorlage im Kalkulator abfragt (#26, Teil B).</summary>
public enum EingabeArt
{
    /// <summary>Freitext, Platzhalter <c>{{eingabe.Name}}</c>.</summary>
    Text = 1,

    /// <summary>Genau eine Option einer Gruppe, Platzhalter <c>{{eingabe.Gruppe=Option}}</c> (☒ oder ☐).</summary>
    Auswahl = 2,

    /// <summary>Tabelle mit beliebig vielen Zeilen, Block <c>{{#eingabe.Name}}</c> mit Spalten <c>{{Name.Spalte}}</c>.</summary>
    Liste = 3,
}

/// <summary>
/// Eine Eingabe, die eine Vorlage braucht. <paramref name="Optionen"/> sind bei einer Auswahl die Optionen, bei einer
/// Liste die Spalten, jeweils in der Reihenfolge des ersten Vorkommens; bei Text leer.
/// </summary>
public sealed record EingabeDefinition(EingabeArt Art, string Name, IReadOnlyList<string> Optionen);

/// <summary>Ergebnis der Prüfung einer Vorlage; Fehler verhindern, dass die Vorlage verwendet wird.</summary>
public sealed record VorlagenHinweis(bool IstFehler, string Text);
