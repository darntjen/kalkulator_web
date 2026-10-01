using System.Globalization;
using Kalkulator.Domain.Projekte;

namespace Kalkulator.Web.Components.Bausteine;

/// <summary>Anzeigetexte für Aufzählungen und Formate, damit alle Seiten dieselben Begriffe verwenden.</summary>
public static class Texte
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static string Status(ProjektStatus status) => status switch
    {
        ProjektStatus.Entwurf => "Entwurf",
        ProjektStatus.AngebotVersendet => "Angebot versendet",
        ProjektStatus.VertragErstellt => "Vertrag erstellt",
        ProjektStatus.Gewonnen => "Gewonnen",
        ProjektStatus.Verloren => "Verloren",
        ProjektStatus.Zurueckgestellt => "Zurückgestellt",
        _ => status.ToString(),
    };

    public static string Verlustgrund(Verlustgrund grund) => grund switch
    {
        Domain.Projekte.Verlustgrund.Preis => "Preis",
        Domain.Projekte.Verlustgrund.Wettbewerber => "Wettbewerber",
        Domain.Projekte.Verlustgrund.KeinBedarf => "Kein Bedarf",
        Domain.Projekte.Verlustgrund.FalscherZeitpunkt => "Falscher Zeitpunkt",
        Domain.Projekte.Verlustgrund.InterneLoesung => "Interne Lösung",
        Domain.Projekte.Verlustgrund.KeineRueckmeldung => "Keine Rückmeldung",
        Domain.Projekte.Verlustgrund.Sonstiges => "Sonstiges",
        _ => grund.ToString(),
    };

    public static string Monat(DateOnly? monat) => monat?.ToString("MMMM yyyy", Deutsch) ?? "–";

    public static string Zeitpunkt(DateTimeOffset zeitpunkt) =>
        TimeZoneInfo.ConvertTime(zeitpunkt, Zeitzone).ToString("dd.MM.yyyy HH:mm", Deutsch);

    public static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);

    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
}
