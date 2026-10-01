using Kalkulator.Domain.Berechnung;

namespace Kalkulator.Domain.Projekte;

public enum Freigabestatus
{
    Offen = 1,
    Freigegeben = 2,
    Abgelehnt = 3,
}

/// <summary>
/// Freie Sonderposition im Arbeitsstand einer Kalkulation (B-21). Sie braucht die Freigabe der Vertriebsleitung (B-22);
/// jede inhaltliche Änderung setzt die Freigabe zurück.
/// </summary>
public class Sonderposition
{
    public int Id { get; set; }
    public int KalkulationId { get; set; }
    public int Reihenfolge { get; init; }

    public string Bezeichnung { get; private set; } = "";
    public string Einheit { get; private set; } = "";
    public int Menge { get; private set; }
    public decimal Preis { get; private set; }
    public string Begruendung { get; private set; } = "";
    public bool Einmalig { get; private set; }

    public Freigabestatus Status { get; private set; } = Freigabestatus.Offen;
    public string? EntschiedenVon { get; private set; }
    public DateTimeOffset? EntschiedenAm { get; private set; }
    public string? Kommentar { get; private set; }

    public void Aendern(string bezeichnung, string einheit, int menge, decimal preis, string begruendung, bool einmalig)
    {
        var geaendert = bezeichnung != Bezeichnung || einheit != Einheit || menge != Menge || preis != Preis
            || begruendung != Begruendung || einmalig != Einmalig;
        Bezeichnung = bezeichnung;
        Einheit = einheit;
        Menge = menge;
        Preis = preis;
        Begruendung = begruendung;
        Einmalig = einmalig;
        if (geaendert)
        {
            Status = Freigabestatus.Offen;
            EntschiedenVon = null;
            EntschiedenAm = null;
            Kommentar = null;
        }
    }

    public void Freigeben(string benutzer, DateTimeOffset zeitpunkt, string? kommentar = null) =>
        Entscheiden(Freigabestatus.Freigegeben, benutzer, zeitpunkt, kommentar);

    /// <summary>Lehnt die Sonderposition ab; die Begründung ist Pflicht, damit der Vertrieb nacharbeiten kann.</summary>
    public void Ablehnen(string benutzer, DateTimeOffset zeitpunkt, string kommentar)
    {
        if (string.IsNullOrWhiteSpace(kommentar))
        {
            throw new ArgumentException("Bitte die Ablehnung begründen.", nameof(kommentar));
        }

        Entscheiden(Freigabestatus.Abgelehnt, benutzer, zeitpunkt, kommentar);
    }

    private void Entscheiden(Freigabestatus status, string benutzer, DateTimeOffset zeitpunkt, string? kommentar)
    {
        Status = status;
        EntschiedenVon = benutzer;
        EntschiedenAm = zeitpunkt;
        Kommentar = string.IsNullOrWhiteSpace(kommentar) ? null : kommentar.Trim();
    }

    public SonderpositionEingabe AlsEingabe() =>
        new(Bezeichnung, Einheit, Menge, Preis, Begruendung, Einmalig, Status == Freigabestatus.Freigegeben);

    internal Sonderposition Kopie()
    {
        var kopie = new Sonderposition { Reihenfolge = Reihenfolge };
        kopie.Aendern(Bezeichnung, Einheit, Menge, Preis, Begruendung, Einmalig);
        kopie.Status = Status;
        kopie.EntschiedenVon = EntschiedenVon;
        kopie.EntschiedenAm = EntschiedenAm;
        kopie.Kommentar = Kommentar;
        return kopie;
    }
}
