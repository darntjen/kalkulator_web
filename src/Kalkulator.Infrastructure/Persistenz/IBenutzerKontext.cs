namespace Kalkulator.Infrastructure.Persistenz;

/// <summary>Liefert den angemeldeten Benutzer für das Änderungsprotokoll (ab #4 aus Entra ID).</summary>
public interface IBenutzerKontext
{
    string Name { get; }
}

/// <summary>Platzhalter, solange es keine Anmeldung gibt, sowie für Hintergrundprozesse wie die Erstbefüllung.</summary>
public sealed class SystemBenutzer : IBenutzerKontext
{
    public string Name => "System";
}
