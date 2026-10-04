using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>Bewertung der Marge wie im Blatt „Preisprüfung“ der EK-Kalkulation.</summary>
public enum Ampel
{
    /// <summary>EK noch offen, Marge nicht berechenbar.</summary>
    Grau = 0,
    Gruen = 1,

    /// <summary>Beobachten oder justieren.</summary>
    Gelb = 2,

    /// <summary>Entscheidung nötig.</summary>
    Rot = 3,
}

/// <summary>
/// Schwellen der Margen-Ampel. Die Excel-Vorlage nennt nur „grün = gesund (55–72 %)“ und setzt Gelb und Rot von Hand;
/// hier ist die Ampel regelbasiert und die Schwellen sind Parameter der Preisliste.
/// </summary>
public sealed record Margenschwellen(decimal GruenAb, decimal GruenBis, decimal RotUnter)
{
    public static readonly Margenschwellen Standard = new(0.55m, 0.72m, 0.45m);

    public static Margenschwellen Aus(Preisliste preisliste) => new(
        preisliste.ParameterWertOder(ParameterSchluessel.MargeGruenAb, Standard.GruenAb),
        preisliste.ParameterWertOder(ParameterSchluessel.MargeGruenBis, Standard.GruenBis),
        preisliste.ParameterWertOder(ParameterSchluessel.MargeRotUnter, Standard.RotUnter));

    /// <summary>Grün im Zielkorridor, rot unter der Rot-Schwelle, sonst gelb (auch oberhalb des Korridors: Preis prüfen).</summary>
    public Ampel Bewerte(decimal? marge) => marge switch
    {
        null => Ampel.Grau,
        var m when m < RotUnter => Ampel.Rot,
        var m when m >= GruenAb && m <= GruenBis => Ampel.Gruen,
        _ => Ampel.Gelb,
    };
}

/// <summary>Kosten, Verkaufspreis, Deckungsbeitrag und Marge einer Preiskomponente in einer Preisliste.</summary>
public sealed record KomponentenPruefung(Preiskomponente Komponente, decimal? Vk, bool VkAusStaffel, decimal? Kosten, Ampel Ampel)
{
    public decimal? Deckungsbeitrag => Vk is { } vk && Kosten is { } k ? vk - k : null;

    public decimal? Marge => Vk is > 0 && Deckungsbeitrag is { } db ? Math.Round(db / Vk.Value, 4, MidpointRounding.AwayFromZero) : null;

    /// <summary>
    /// Prüft eine Komponente. Bei gestaffelten Preisen zählt die erste Stufe. Dafür müssen Preise, Staffeln, Parameter
    /// und EK-Werte der Preisliste sowie bei Bundles die Bestandteile geladen sein.
    /// </summary>
    public static KomponentenPruefung Fuer(Preisliste preisliste, Preiskomponente komponente, Margenschwellen schwellen)
    {
        var vk = preisliste.PreisFuer(komponente);
        var ausStaffel = false;
        if (vk is null && komponente.StaffelBezug != StaffelBezug.Keine)
        {
            vk = preisliste.Staffeln.Where(s => GehoertZu(s, komponente)).MinBy(s => s.AbMenge)?.VkNetto;
            ausStaffel = vk is not null;
        }

        var kosten = preisliste.KostenFuer(komponente);
        var pruefung = new KomponentenPruefung(komponente, vk, ausStaffel, kosten, Ampel.Grau);
        return pruefung with { Ampel = schwellen.Bewerte(pruefung.Marge) };
    }

    private static bool GehoertZu(Preisstaffel stufe, Preiskomponente komponente) =>
        ReferenceEquals(stufe.Preiskomponente, komponente) || (komponente.Id != 0 && stufe.PreiskomponenteId == komponente.Id);
}
