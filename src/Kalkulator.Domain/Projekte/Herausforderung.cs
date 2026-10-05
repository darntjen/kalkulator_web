namespace Kalkulator.Domain.Projekte;

/// <summary>Dimension der Kundensituation (G-03).</summary>
public enum Dimension
{
    Kaufmaennisch = 1,
    Organisatorisch = 2,
    Technisch = 3,
}

public enum Prioritaet
{
    Hoch = 1,
    Mittel = 2,
    Niedrig = 3,
}

/// <summary>
/// Baustein der Kundensituation (G-03): eine Herausforderung des Kunden in seiner Sprache, mit Auswirkung, Priorität und
/// Quelle. Gepflegt von Vertrieb, Vertriebsleitung und Consultants (Entscheidung 04.10.2026). Angebotsbausteine werden
/// mit den Herausforderungen verknüpft, die sie lösen (G-04).
/// </summary>
public class Herausforderung
{
    public int Id { get; set; }
    public int KundenprojektId { get; init; }
    public Dimension Dimension { get; private set; }
    public string Titel { get; private set; } = "";
    public string? Beschreibung { get; private set; }
    public string? Auswirkung { get; private set; }
    public Prioritaet Prioritaet { get; private set; } = Prioritaet.Mittel;

    /// <summary>Woher die Erkenntnis stammt, z. B. „Infrastruktur-Analyse vom 12.08.2026“.</summary>
    public string? Quelle { get; private set; }

    public required string AngelegtVon { get; init; }
    public DateTimeOffset AngelegtAm { get; init; }
    public string? GeaendertVon { get; private set; }
    public DateTimeOffset? GeaendertAm { get; private set; }

    public static Herausforderung Neu(int projektId, HerausforderungsDaten daten, string benutzer, DateTimeOffset zeitpunkt)
    {
        var h = new Herausforderung { KundenprojektId = projektId, AngelegtVon = benutzer, AngelegtAm = zeitpunkt };
        h.Setze(daten);
        return h;
    }

    public void Aendern(HerausforderungsDaten daten, string benutzer, DateTimeOffset zeitpunkt)
    {
        Setze(daten);
        GeaendertVon = benutzer;
        GeaendertAm = zeitpunkt;
    }

    private void Setze(HerausforderungsDaten daten)
    {
        if (!Enum.IsDefined(daten.Dimension) || !Enum.IsDefined(daten.Prioritaet))
        {
            throw new ArgumentException("Bitte Dimension und Priorität wählen.");
        }

        Titel = Text(daten.Titel, 200) ?? throw new ArgumentException("Bitte einen Titel angeben.");
        Dimension = daten.Dimension;
        Prioritaet = daten.Prioritaet;
        Beschreibung = Text(daten.Beschreibung, 2000);
        Auswirkung = Text(daten.Auswirkung, 1000);
        Quelle = Text(daten.Quelle, 300);
    }

    private static string? Text(string? wert, int laenge)
    {
        var text = wert?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= laenge ? text : throw new ArgumentException($"Höchstens {laenge} Zeichen: „{text[..Math.Min(text.Length, 30)]} …“.");
    }
}

public sealed record HerausforderungsDaten(Dimension Dimension, string Titel, string? Beschreibung, string? Auswirkung, Prioritaet Prioritaet, string? Quelle);

/// <summary>Ein Service der Kalkulation löst eine Herausforderung (G-04).</summary>
public sealed record Zuordnung(string ServiceCode, int HerausforderungId);

/// <summary>Eingefrorener Bezug im Angebot: welcher Service welche Herausforderung löst, mit dem Titel zum Zeitpunkt des Angebots.</summary>
public sealed record Loesungsbezug(string ServiceCode, Dimension Dimension, Prioritaet Prioritaet, string Titel);
