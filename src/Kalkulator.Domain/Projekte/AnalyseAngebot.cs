namespace Kalkulator.Domain.Projekte;

public enum AnalyseAngebotsStatus
{
    Versendet = 1,
    Beauftragt = 2,
    Abgelehnt = 3,
}

/// <summary>
/// Angebot „Infrastruktur-Analyse und Strategieworkshop“ (J-01). Das Dokument entsteht weiter per Claude-Skill; im
/// Kalkulator stehen nur die Eckdaten für Pipeline und Statistik. Die Nummer ist die Navision-Angebotsnummer
/// (Entscheidung 04.10.2026).
/// </summary>
public class AnalyseAngebot
{
    public int Id { get; set; }
    public int KundenprojektId { get; init; }
    public string Nummer { get; private set; } = "";
    public DateOnly Datum { get; private set; }
    public decimal Paketpreis { get; private set; }
    public AnalyseAngebotsStatus Status { get; private set; } = AnalyseAngebotsStatus.Versendet;
    public DateTimeOffset StatusSeit { get; private set; }
    public string? Bemerkung { get; private set; }

    /// <summary>Optional das Angebotsdokument aus den Uploads des Projekts.</summary>
    public int? UnterlageId { get; private set; }

    public required string ErfasstVon { get; init; }
    public DateTimeOffset ErfasstAm { get; init; }

    public static AnalyseAngebot Neu(int projektId, AnalyseAngebotsDaten daten, string benutzer, DateTimeOffset zeitpunkt)
    {
        var a = new AnalyseAngebot { KundenprojektId = projektId, ErfasstVon = benutzer, ErfasstAm = zeitpunkt, StatusSeit = zeitpunkt };
        a.Setze(daten, zeitpunkt);
        return a;
    }

    public void Aendern(AnalyseAngebotsDaten daten, DateTimeOffset zeitpunkt) => Setze(daten, zeitpunkt);

    /// <summary>Navision-Nummer in Großbuchstaben ohne Leerzeichen, z. B. „AN949714“.</summary>
    public static string Normalisiere(string nummer) => string.Concat(nummer.Where(z => !char.IsWhiteSpace(z))).ToUpperInvariant();

    private void Setze(AnalyseAngebotsDaten daten, DateTimeOffset zeitpunkt)
    {
        var nummer = Normalisiere(daten.Nummer ?? "");
        if (nummer.Length is 0 or > 30)
        {
            throw new ArgumentException("Bitte die Angebotsnummer aus Navision angeben (z. B. AN949714).");
        }

        if (daten.Paketpreis is < 0 or > 1_000_000)
        {
            throw new ArgumentException("Der Paketpreis muss zwischen 0 und 1.000.000 € liegen.");
        }

        if (!Enum.IsDefined(daten.Status))
        {
            throw new ArgumentException("Bitte einen Status wählen.");
        }

        if (daten.Status != Status || StatusSeit == default)
        {
            StatusSeit = zeitpunkt;
        }

        Nummer = nummer;
        Datum = daten.Datum;
        Paketpreis = Math.Round(daten.Paketpreis, 2);
        Status = daten.Status;
        Bemerkung = string.IsNullOrWhiteSpace(daten.Bemerkung) ? null : daten.Bemerkung.Trim()[..Math.Min(daten.Bemerkung.Trim().Length, 500)];
        UnterlageId = daten.UnterlageId;
    }
}

public sealed record AnalyseAngebotsDaten(string? Nummer, DateOnly Datum, decimal Paketpreis, AnalyseAngebotsStatus Status, string? Bemerkung, int? UnterlageId);
