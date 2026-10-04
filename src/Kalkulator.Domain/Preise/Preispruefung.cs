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
/// Schwellen der Margen-Ampel als Parameter der Preisliste. Für Managed Services gilt (Entscheidung 04.10.2026):
/// grün ab 45 %, gelb von 38 % bis unter 45 %, rot unter 38 %. Eine Obergrenze für Grün ist optional; ohne sie bleibt
/// auch eine sehr hohe Marge grün.
/// </summary>
public sealed record Margenschwellen(decimal GruenAb, decimal? GruenBis, decimal RotUnter)
{
    public static readonly Margenschwellen Standard = new(0.45m, null, 0.38m);

    public static Margenschwellen Aus(Preisliste preisliste) => new(
        preisliste.ParameterWertOder(ParameterSchluessel.MargeGruenAb, Standard.GruenAb),
        preisliste.Parameter.SingleOrDefault(p => p.Schluessel == ParameterSchluessel.MargeGruenBis)?.Wert ?? Standard.GruenBis,
        preisliste.ParameterWertOder(ParameterSchluessel.MargeRotUnter, Standard.RotUnter));

    /// <summary>Grün ab der Grün-Schwelle (bis zur Obergrenze, falls gesetzt), rot unter der Rot-Schwelle, sonst gelb.</summary>
    public Ampel Bewerte(decimal? marge) => marge switch
    {
        null => Ampel.Grau,
        var m when m < RotUnter => Ampel.Rot,
        var m when m >= GruenAb && (GruenBis is null || m <= GruenBis) => Ampel.Gruen,
        _ => Ampel.Gelb,
    };

    /// <summary>Lesbare Fassung, z. B. „grün ab 45 %, rot unter 38 %, sonst gelb“.</summary>
    public string Text(Func<decimal, string> prozent) =>
        $"grün ab {prozent(GruenAb)}{(GruenBis is { } bis ? $" bis {prozent(bis)}" : "")}, rot unter {prozent(RotUnter)}, sonst gelb";
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
