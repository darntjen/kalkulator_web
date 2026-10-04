namespace Kalkulator.Domain.Projekte;

/// <summary>Wer im Kalkulator freigibt, bevor ein Angebot an den Kunden geht (#26).</summary>
public enum FreigabeRolle
{
    Vertriebsleitung = 1,

    /// <summary>Solution Consultant (App-Rolle „Consultant“): fachliche Prüfung der Lösung.</summary>
    SolutionConsultant = 2,
}

/// <summary>
/// Vertriebsfreigabe einer Kalkulation durch Vertriebsleitung oder Solution Consultant. Eine Freigabe gilt für den
/// Arbeitsstand, auf dem sie erteilt wurde: Jede inhaltliche Änderung hebt sie auf. Aufgehobene Freigaben bleiben
/// als Verlauf erhalten.
/// </summary>
public class Vertriebsfreigabe
{
    public int Id { get; set; }
    public int KalkulationId { get; set; }
    public FreigabeRolle Rolle { get; init; }
    public required string Benutzer { get; init; }
    public DateTimeOffset Zeitpunkt { get; init; }
    public string? Kommentar { get; init; }

    public DateTimeOffset? AufgehobenAm { get; private set; }
    public string? AufgehobenVon { get; private set; }
    public string? Aufhebungsgrund { get; private set; }

    public bool IstAktiv => AufgehobenAm is null;

    internal void Aufheben(string benutzer, DateTimeOffset zeitpunkt, string grund)
    {
        AufgehobenAm = zeitpunkt;
        AufgehobenVon = benutzer;
        Aufhebungsgrund = grund;
    }
}
