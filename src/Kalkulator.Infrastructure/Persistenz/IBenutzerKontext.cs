namespace Kalkulator.Infrastructure.Persistenz;

/// <summary>Liefert den angemeldeten Benutzer für Rechteprüfung und Änderungsprotokoll (ab #4 aus Entra ID).</summary>
public interface IBenutzerKontext
{
    string Name { get; }

    /// <summary>Ob der Benutzer die App-Rolle hat (Namen siehe <see cref="Anwendung.Rollen"/>).</summary>
    bool IstInRolle(string rolle);
}

/// <summary>Für Hintergrundprozesse wie die Erstbefüllung; hat keine App-Rollen.</summary>
public sealed class SystemBenutzer : IBenutzerKontext
{
    public string Name => "System";

    public bool IstInRolle(string rolle) => false;
}
