namespace Kalkulator.Domain.Vertrag;

/// <summary>
/// Angaben für das Vertragswerk, die der Kalkulator nicht selbst kennt, z. B. Vertreter oder Serverliste in S14 (#26, Teil C).
/// Welche gebraucht werden, legen die Vorlagen über <c>{{eingabe.…}}</c> fest (<see cref="EingabeDefinition"/>).
/// Namen gelten über alle Vorlagen hinweg: Fragen zwei Vorlagen nach „Vertreter“, ist es dieselbe Angabe.
/// Gepflegt werden sie mit der Kalkulation und mit dem Angebot eingefroren.
/// </summary>
public sealed record Vertragsangaben
{
    /// <summary>Text- und Auswahlangaben; bei einer Auswahl ist der Wert die gewählte Option.</summary>
    public IReadOnlyDictionary<string, string> Werte { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Listen, je Zeile die Werte der Spalten.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Listen { get; init; } =
        new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);

    public string? Wert(string name) => Werte.TryGetValue(name, out var wert) && !string.IsNullOrWhiteSpace(wert) ? wert.Trim() : null;

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Liste(string name) =>
        Listen.TryGetValue(name, out var zeilen)
            ? [.. zeilen.Where(z => z.Values.Any(v => !string.IsNullOrWhiteSpace(v)))]
            : [];

    /// <summary>
    /// Namen der Angaben, die für <paramref name="definitionen"/> fehlen: leerer Text, keine gültige Option oder eine Liste
    /// ohne ausgefüllte Zeile.
    /// </summary>
    public IReadOnlyList<string> Fehlend(IEnumerable<EingabeDefinition> definitionen) =>
        [.. definitionen.Where(d => d.Art switch
        {
            EingabeArt.Text => Wert(d.Name) is null,
            EingabeArt.Auswahl => Wert(d.Name) is not { } wahl || !d.Optionen.Contains(wahl, StringComparer.Ordinal),
            _ => Liste(d.Name).Count == 0,
        }).Select(d => d.Name)];

    /// <summary>
    /// Fasst die Eingaben mehrerer Vorlagen zusammen (Reihenfolge des ersten Auftretens). Verlangen zwei Vorlagen unter
    /// demselben Namen Unterschiedliches, gilt die erste Definition; bei Auswahl und Liste werden Optionen bzw. Spalten vereinigt.
    /// </summary>
    public static IReadOnlyList<EingabeDefinition> Vereinige(IEnumerable<EingabeDefinition> definitionen)
    {
        var ergebnis = new List<EingabeDefinition>();
        foreach (var d in definitionen)
        {
            var i = ergebnis.FindIndex(e => e.Name == d.Name);
            if (i < 0)
            {
                ergebnis.Add(d);
            }
            else if (ergebnis[i].Art == d.Art)
            {
                ergebnis[i] = ergebnis[i] with { Optionen = [.. ergebnis[i].Optionen.Union(d.Optionen, StringComparer.Ordinal)] };
            }
        }

        return ergebnis;
    }
}
