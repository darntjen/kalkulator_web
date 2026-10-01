using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Persistenz;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>App-Rollen der Entra-App-Registrierung (ADR-0003).</summary>
public static class Rollen
{
    public const string Vertrieb = "Vertrieb";
    public const string Consultant = "Consultant";
    public const string Vertriebsleitung = "Vertriebsleitung";
    public const string Produktmanagement = "Produktmanagement";
    public const string Fuehrung = "Fuehrung";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> Alle = [Vertrieb, Consultant, Vertriebsleitung, Produktmanagement, Fuehrung, Admin];
}

/// <summary>
/// Rechtematrix aus docs/00_projektueberblick.md, Abschnitt 5. Die Dienste prüfen serverseitig;
/// die Oberfläche blendet nur zusätzlich aus.
/// </summary>
public sealed class Berechtigung(IBenutzerKontext benutzer)
{
    public string Name => benutzer.Name;

    private bool Vertrieb => benutzer.IstInRolle(Rollen.Vertrieb);
    private bool Leitung => benutzer.IstInRolle(Rollen.Vertriebsleitung);

    /// <summary>Vertriebsleitung, Consultant und Führung sehen alle Kundenprojekte; der Vertrieb nur seine eigenen.</summary>
    public bool SiehtAlleProjekte => Leitung || benutzer.IstInRolle(Rollen.Consultant) || benutzer.IstInRolle(Rollen.Fuehrung);

    public bool DarfProjekteSehen => SiehtAlleProjekte || Vertrieb;

    /// <summary>Kundenprojekte anlegen und kalkulieren: Vertrieb und Vertriebsleitung, nicht Consultant und Führung.</summary>
    public bool DarfKalkulieren => Vertrieb || Leitung;

    /// <summary>EK, Deckungsbeitrag und Marge der Managed Services sieht nur die Führungsebene.</summary>
    public bool DarfEinkaufSehen => benutzer.IstInRolle(Rollen.Fuehrung);

    public bool DarfSonderpositionenFreigeben => Leitung;

    public bool DarfSehen(Kundenprojekt projekt) => SiehtAlleProjekte || (Vertrieb && IstVerantwortlich(projekt));

    public bool DarfBearbeiten(Kundenprojekt projekt) => Leitung || (Vertrieb && IstVerantwortlich(projekt));

    private bool IstVerantwortlich(Kundenprojekt projekt) =>
        string.Equals(projekt.Verantwortlich, benutzer.Name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Der Benutzer darf diese Aktion nicht ausführen; die Oberfläche zeigt die Meldung an.</summary>
public class KeinZugriffException(string meldung) : InvalidOperationException(meldung);
