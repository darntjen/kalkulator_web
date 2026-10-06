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

    /// <summary>
    /// Paperless-Vorlage mit der Reihenfolge der Unterschriften (Ablauf-Vorlage). Ein Dokument entsteht in Paperless
    /// entweder aus einer Vorlage oder aus einem PDF; mit <c>template_id</c> bliebe das PDF unberücksichtigt (Testlauf
    /// 05.10.2026). Die Reihenfolge (erst Kunde, dann Nösse) lässt sich über die API nur über eine Vorlage vorgeben.
    /// </summary>
    public long? AblaufVorlageId { get; set; }

    /// <summary>
    /// <c>true</c>: Die Übergabe legt eine Kopie der Ablauf-Vorlage mit dem PDF und den Feldern an und erzeugt das
    /// Dokument daraus (Weg wie Testlauf D), damit die Reihenfolge der Vorlage gilt. <c>false</c> (Standard): Das
    /// Dokument entsteht direkt aus dem PDF; alle unterschreiben dann gleichzeitig.
    /// </summary>
    public bool UeberAblaufVorlage { get; set; }

    /// <summary>Sprache des Dokuments für die Unterzeichner (<c>original_content_locale</c>, <c>rendering_locale</c>); leer = Paperless-Standard.</summary>
    public string? Sprache { get; set; } = "de-DE";

    /// <summary>
    /// <c>true</c>: Paperless versendet sofort (state „dispatched“) und schickt den Teilnehmern die E-Mails selbst, mit
    /// Ablauf-Vorlage nacheinander. Standard ist <c>false</c>: Das Dokument liegt als Entwurf in Paperless und wird
    /// dort von Hand versendet. Die Prüfungen durch AVV und Technik liegen ohnehin vor der Übergabe im Kalkulator.
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

    /// <summary>
    /// Umrechnung von Punkt (1/72 Zoll, Einheit im PDF) in die Einheit von Paperless. Paperless rechnet in Pixeln
    /// (1/96 Zoll): Felder lagen ohne Umrechnung zu weit links und zu weit oben (Testlauf 05.10.2026).
    /// </summary>
    public double Skalierung { get; set; } = 96d / 72d;

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
