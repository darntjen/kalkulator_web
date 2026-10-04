namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Abschnitt „Paperless“ der Konfiguration (#26, Teil D). Der API-Schlüssel gehört nicht ins Repository, sondern in
/// die Umgebungsvariable <c>Paperless__ApiSchluessel</c> bzw. die Benutzergeheimnisse. Ohne Schlüssel und Arbeitsbereich
/// ist die Übergabe ausgeschaltet; das Vertragswerk entsteht dann wie bisher nur im Kalkulator.
/// </summary>
public sealed class PaperlessEinstellungen
{
    public const string Abschnitt = "Paperless";

    public string Adresse { get; set; } = "https://api.paperless.io/api/v1/";

    public string? ApiSchluessel { get; set; }

    /// <summary>Arbeitsbereich (workspace_id), in dem das Dokument angelegt wird.</summary>
    public long? ArbeitsbereichId { get; set; }

    /// <summary>Optionale Paperless-Vorlage (template_id), die Ablauf, Freigabe und Teilnehmer regelt.</summary>
    public long? VorlageId { get; set; }

    /// <summary>
    /// <c>true</c>: Paperless versendet sofort (state „dispatched“). Standard ist <c>false</c>: Das Dokument liegt als
    /// Entwurf in Paperless, wird dort technisch geprüft und von dort an den Kunden geschickt.
    /// </summary>
    public bool Versenden { get; set; }

    /// <summary>
    /// Rollen der Unterschriftsfelder (<c>{{unterschrift.Rolle}}</c>). Nicht eingetragene Rollen fragt der Kalkulator
    /// beim Erzeugen ab (Name und E-Mail) und nutzt den Rollennamen als Paperless-Slot.
    /// </summary>
    public Dictionary<string, PaperlessRolle> Rollen { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Größe des Unterschriftsfelds in Punkt; die linke untere Ecke sitzt auf der Marke.</summary>
    public double FeldBreite { get; set; } = 180;

    public double FeldHoehe { get; set; } = 56;

    /// <summary>
    /// Ursprung der Koordinaten bei Paperless: <c>true</c> = linke obere Seitenecke, y wächst nach unten (Bildschirm);
    /// <c>false</c> = linke untere Ecke wie im PDF. Mit dem echten Schlüssel zu prüfen (docs/12_vertragsvorlagen.md).
    /// </summary>
    public bool YVonOben { get; set; } = true;

    public bool Aktiv => !string.IsNullOrWhiteSpace(ApiSchluessel) && ArbeitsbereichId is not null;

    /// <summary>Einstellung einer Rolle; ohne Eintrag gilt der Rollenname als Slot und die Person wird abgefragt.</summary>
    public PaperlessRolle Rolle(string rolle) => Rollen.TryGetValue(rolle, out var r) ? r : new PaperlessRolle();
}

/// <summary>
/// Einstellung einer Unterschriftsrolle. <see cref="AusVorlage"/>: Die Person legt die Paperless-Vorlage fest (z. B. die
/// Geschäftsführung), der Kalkulator setzt nur das Feld. Mit <see cref="Name"/> und <see cref="EMail"/> ist die Person
/// fest eingestellt. Sonst wird sie beim Erzeugen erfasst.
/// </summary>
public sealed class PaperlessRolle
{
    /// <summary>Slot-Name in Paperless; leer = Rollenname.</summary>
    public string? Slot { get; set; }

    public bool AusVorlage { get; set; }

    public string? Name { get; set; }

    public string? EMail { get; set; }

    public bool Fest => AusVorlage || (!string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(EMail));
}
