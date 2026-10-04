namespace Kalkulator.Infrastructure.Vorlagen;

/// <summary>
/// Abschnitt „Vorlagen“ der Konfiguration: woher die Vertragsvorlagen kommen (#26, Teil B). Geheimnisse wie das
/// Client-Secret gehören nicht in appsettings.json, sondern in Umgebungsvariablen oder User-Secrets.
/// </summary>
public sealed class VorlagenEinstellungen
{
    public const string Abschnitt = "Vorlagen";

    /// <summary>„SharePoint“, „Ordner“ oder leer (kein Abgleich).</summary>
    public string? Quelle { get; set; }

    /// <summary>Bei Quelle „Ordner“: lokaler Ordner mit derselben Struktur wie in SharePoint (Rahmen, Leistungsscheine, Bundles).</summary>
    public string? Ordner { get; set; }

    public SharePointEinstellungen SharePoint { get; set; } = new();

    /// <summary>Uhrzeit (Ortszeit) des nächtlichen Abgleichs, z. B. „02:30“; leer schaltet ihn ab.</summary>
    public string? AbgleichUm { get; set; } = "02:30";
}

/// <summary>Zugriff per Microsoft Graph mit einer App-Registrierung (Anwendungsberechtigung Sites.Selected).</summary>
public sealed class SharePointEinstellungen
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }

    /// <summary>Client-Secret; alternativ <see cref="ZertifikatPfad"/>.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>PFX-Datei des Zertifikats der App-Registrierung (empfohlen statt Secret).</summary>
    public string? ZertifikatPfad { get; set; }

    public string? ZertifikatKennwort { get; set; }

    /// <summary>Adresse der Website, z. B. https://noessedatentechnik.sharepoint.com/sites/Service-Katalog.</summary>
    public string? Website { get; set; }

    /// <summary>Name der Dokumentbibliothek; leer heißt Standardbibliothek („Freigegebene Dokumente“).</summary>
    public string? Bibliothek { get; set; }

    /// <summary>Ordner innerhalb der Bibliothek, z. B. „Allgemein/Nösse MSP Servicekatalog/03_Vertragswerk (EXTERN)“.</summary>
    public string? Ordnerpfad { get; set; }
}
