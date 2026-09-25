namespace Kalkulator.Domain.Preise;

public enum PreislistenStatus
{
    Entwurf = 1,
    Freigegeben = 2,
}

/// <summary>
/// Versionierter Preisstand. Eine freigegebene Preisliste ist unveränderlich;
/// Preisänderungen erfolgen über einen neuen Entwurf (Anforderung A-05, ADR-0005).
/// </summary>
public class Preisliste
{
    public int Id { get; set; }
    public required string Bezeichnung { get; set; }
    public DateOnly GueltigAb { get; set; }
    public PreislistenStatus Status { get; private set; } = PreislistenStatus.Entwurf;
    public DateTimeOffset? FreigegebenAm { get; private set; }
    public string? FreigegebenVon { get; private set; }

    /// <summary>Preisliste, aus der dieser Entwurf kopiert wurde.</summary>
    public int? VorgaengerId { get; private set; }

    public List<Preis> Preise { get; } = [];
    public List<Preisstaffel> Staffeln { get; } = [];
    public List<Parameter> Parameter { get; } = [];
    public List<EkPosition> EkPositionen { get; } = [];

    public bool IstAenderbar => Status == PreislistenStatus.Entwurf;

    public void Freigeben(string benutzer, DateTimeOffset zeitpunkt)
    {
        if (!IstAenderbar)
        {
            throw new PreislisteGesperrtException(Bezeichnung);
        }

        Status = PreislistenStatus.Freigegeben;
        FreigegebenVon = benutzer;
        FreigegebenAm = zeitpunkt;
    }

    /// <summary>Legt einen neuen Entwurf mit Kopien aller Preise, Staffeln, Parameter und EK-Werte an.</summary>
    public Preisliste ErzeugeEntwurf(string bezeichnung, DateOnly gueltigAb)
    {
        var entwurf = new Preisliste { Bezeichnung = bezeichnung, GueltigAb = gueltigAb, VorgaengerId = Id };
        entwurf.Preise.AddRange(Preise.Select(p => new Preis { PreiskomponenteId = p.PreiskomponenteId, VkNetto = p.VkNetto }));
        entwurf.Staffeln.AddRange(Staffeln.Select(s => new Preisstaffel { PreiskomponenteId = s.PreiskomponenteId, AbMenge = s.AbMenge, VkNetto = s.VkNetto }));
        entwurf.Parameter.AddRange(Parameter.Select(p => new Parameter { Schluessel = p.Schluessel, Wert = p.Wert, Beschreibung = p.Beschreibung }));
        entwurf.EkPositionen.AddRange(EkPositionen.Select(e => e.Kopie()));
        return entwurf;
    }

    /// <summary>Verkaufspreis einer Komponente ohne Staffel; <c>null</c> heißt „auf Anfrage“ oder nicht bepreist.</summary>
    public decimal? PreisFuer(int preiskomponenteId) =>
        Preise.SingleOrDefault(p => p.PreiskomponenteId == preiskomponenteId)?.VkNetto;

    /// <summary>
    /// Staffelpreis für eine Menge: gilt die Stufe mit der größten Untergrenze, die die Menge erreicht.
    /// <c>null</c> heißt „individuell kalkulieren“ (z. B. Onboarding ab 501 User).
    /// </summary>
    public decimal? StaffelpreisFuer(int preiskomponenteId, int menge)
    {
        var stufe = Staffeln
            .Where(s => s.PreiskomponenteId == preiskomponenteId && s.AbMenge <= menge)
            .MaxBy(s => s.AbMenge);

        return stufe?.VkNetto;
    }

    public decimal ParameterWert(string schluessel) =>
        Parameter.SingleOrDefault(p => p.Schluessel == schluessel)?.Wert
        ?? throw new KeyNotFoundException($"Parameter „{schluessel}“ ist in der Preisliste „{Bezeichnung}“ nicht gepflegt.");
}

public class PreislisteGesperrtException(string preisliste)
    : InvalidOperationException($"Die Preisliste „{preisliste}“ ist freigegeben und kann nicht mehr geändert werden. Bitte einen neuen Entwurf anlegen.");
