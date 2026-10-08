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

    public static string Einheit(Domain.Katalog.Einheit einheit) => Domain.Katalog.KatalogTexte.Einheit(einheit);

    public static string Freigabe(Freigabestatus status) => status switch
    {
        Freigabestatus.Offen => "Freigabe offen",
        Freigabestatus.Freigegeben => "Freigegeben",
        Freigabestatus.Abgelehnt => "Abgelehnt",
        _ => status.ToString(),
    };

    public static string Freigaberolle(Domain.Projekte.FreigabeRolle rolle) => rolle switch
    {
        Domain.Projekte.FreigabeRolle.Vertriebsleitung => "Vertriebsleitung",
        Domain.Projekte.FreigabeRolle.SolutionConsultant => "Solution Consultant",
        _ => rolle.ToString(),
    };

    public static string Menge(decimal menge) => menge.ToString("#,##0.##", Deutsch);

    public static string Prozent(decimal anteil) => (anteil * 100).ToString("0.0", Deutsch) + " %";

    public static string Monat(DateOnly? monat) => monat?.ToString("MMMM yyyy", Deutsch) ?? "–";

    public static string Zeitpunkt(DateTimeOffset zeitpunkt) =>
        TimeZoneInfo.ConvertTime(zeitpunkt, Zeitzone).ToString("dd.MM.yyyy HH:mm", Deutsch);

    public static string Dimension(Dimension dimension) => dimension switch
    {
        Domain.Projekte.Dimension.Kaufmaennisch => "Kaufmännisch",
        Domain.Projekte.Dimension.Organisatorisch => "Organisatorisch",
        _ => "Technisch",
    };

    public static string Prioritaet(Prioritaet prioritaet) => prioritaet switch
    {
        Domain.Projekte.Prioritaet.Hoch => "hoch",
        Domain.Projekte.Prioritaet.Mittel => "mittel",
        _ => "niedrig",
    };

    /// <summary>Meldung nach einer Freigabe, die noch nicht die letzte war, z. B. „Es fehlen noch: AVV, Technik.“</summary>
    public static string FehlendeFreigaben(IReadOnlyList<VertragsfreigabeArt> fehlend) =>
        fehlend.Count == 1 ? $"Es fehlt noch: {Vertragsfreigabe(fehlend[0])}." : $"Es fehlen noch: {string.Join(", ", fehlend.Select(Vertragsfreigabe))}.";

    public static string Vertragsfreigabe(VertragsfreigabeArt art) => art switch
    {
        VertragsfreigabeArt.Vertriebsleitung => "Vertriebsleitung",
        VertragsfreigabeArt.Avv => "AVV",
        _ => "Technik",
    };

    public static string Angebotsstatus(AngebotsStatus status) => status switch
    {
        AngebotsStatus.Erzeugt => "erzeugt",
        AngebotsStatus.Versendet => "versendet",
        AngebotsStatus.Angenommen => "angenommen",
        AngebotsStatus.NichtAngenommen => "nicht angenommen",
        AngebotsStatus.Ersetzt => "ersetzt",
        _ => "abgelaufen",
    };

    public static string AnalyseStatus(AnalyseAngebotsStatus status) => status switch
    {
        AnalyseAngebotsStatus.Versendet => "versendet",
        AnalyseAngebotsStatus.Beauftragt => "beauftragt",
        _ => "abgelehnt",
    };

    public static string Unterlagenart(UnterlagenArt art) => art switch
    {
        UnterlagenArt.Recherche => "Recherche",
        UnterlagenArt.Standortgespraech => "Standortgespräch",
        UnterlagenArt.Analyse => "Analyse",
        UnterlagenArt.Workshop => "Workshop und Roadmap",
        UnterlagenArt.Angebot => "Angebot",
        UnterlagenArt.Protokoll => "Protokoll oder Transkript",
        _ => "Sonstiges",
    };

    /// <summary>Lesbarer Name eines Bereichs der Kundenablage, z. B. „30_Analyse“ → „Analyse“.</summary>
    public static string Ablagebereich(string bereich) => bereich switch
    {
        "00_Kundenakte" => "Kundenakte",
        "10_Recherche" => "Recherche",
        "20_Standortgespraech" => "Standortgespräch",
        "30_Analyse" => "Analyse",
        "40_Workshop_Roadmap" => "Workshop und Roadmap",
        "60_Angebote" => "Angebote",
        "80_Protokolle" => "Protokolle",
        _ => bereich,
    };

    public static string Groesse(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024d).ToString("0", Deutsch) + " KB",
        _ => (bytes / 1024d / 1024d).ToString("0.0", Deutsch) + " MB",
    };

    public static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", Deutsch);

    public static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);

    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
}
