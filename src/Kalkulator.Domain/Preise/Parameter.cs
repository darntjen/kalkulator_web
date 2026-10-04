namespace Kalkulator.Domain.Preise;

/// <summary>Zentraler Kalkulationswert einer Preisliste, z. B. AE-Satz oder S14-Baustein.</summary>
public class Parameter
{
    public int Id { get; set; }
    public int PreislisteId { get; set; }
    public required string Schluessel { get; set; }
    public decimal Wert { get; set; }
    public string? Beschreibung { get; set; }
}

/// <summary>
/// Bekannte Parameter-Schlüssel (Werte siehe docs/06_ist-analyse.md).
/// Beträge, die als Position im Angebot erscheinen (z. B. S14-Bausteine, S60-Satz), sind Preise, keine Parameter.
/// </summary>
public static class ParameterSchluessel
{
    public const string AeSatzEbene1 = "AE_SATZ_EBENE_1";
    public const string AeSatzEbene2 = "AE_SATZ_EBENE_2";
    public const string AeSatzEbene3 = "AE_SATZ_EBENE_3";
    public const string SupportkontingentBlockgroesseAe = "S60_BLOCKGROESSE_AE";
    public const string SupportkontingentAeProAnfrage = "S60_AE_PRO_ANFRAGE";
    public const string BackupPaketgroesseGb = "S14_PAKETGROESSE_GB";
    public const string BackupPreisuntergrenze = "S14_PREISUNTERGRENZE";
    public const string CloudServerMargenteiler = "S61_MARGENTEILER";
    public const string VkVerrechnungssatzProStunde = "VK_VERRECHNUNGSSATZ_PRO_STUNDE";
    public const string EkKostensatzProStunde = "EK_KOSTENSATZ_PRO_STUNDE";

    /// <summary>Margen-Ampel der Preisprüfung: grün von … bis … (Anteil, z. B. 0,55), rot unterhalb der Schwelle.</summary>
    public const string MargeGruenAb = "MARGE_GRUEN_AB";
    public const string MargeGruenBis = "MARGE_GRUEN_BIS";
    public const string MargeRotUnter = "MARGE_ROT_UNTER";

    /// <summary>Ohne diese Parameter rechnet der Rechenkern nicht; eine Preisliste ohne sie lässt sich nicht freigeben.</summary>
    public static readonly IReadOnlyList<string> Pflicht =
    [
        AeSatzEbene1, AeSatzEbene2, AeSatzEbene3, SupportkontingentBlockgroesseAe, SupportkontingentAeProAnfrage,
        BackupPaketgroesseGb, BackupPreisuntergrenze, CloudServerMargenteiler, VkVerrechnungssatzProStunde, EkKostensatzProStunde,
    ];

    /// <summary>Alle bekannten Schlüssel mit Erklärung, z. B. für die Auswahl beim Ergänzen in der Pflegeoberfläche.</summary>
    public static readonly IReadOnlyDictionary<string, string> Bekannte = new Dictionary<string, string>
    {
        [AeSatzEbene1] = "Service-Request-Satz Ebene 1 je AE (15 Min.)",
        [AeSatzEbene2] = "Service-Request-Satz Ebene 2 (Standard) je AE",
        [AeSatzEbene3] = "Service-Request-Satz Ebene 3 je AE",
        [SupportkontingentBlockgroesseAe] = "S60: Blockgröße in AE",
        [SupportkontingentAeProAnfrage] = "S60: Standardwert AE je Anfrage",
        [BackupPaketgroesseGb] = "S14: Paketgröße Cloud-Backup in GB",
        [BackupPreisuntergrenze] = "S14: Preisuntergrenze je Monat",
        [CloudServerMargenteiler] = "S61: VK = EK ÷ Margenteiler",
        [VkVerrechnungssatzProStunde] = "Verrechnungssatz Dienstleistung (VK) je Stunde",
        [EkKostensatzProStunde] = "EK-Kostensatz Dienstleistung je Stunde (Basis der Betriebskosten)",
        [MargeGruenAb] = "Margen-Ampel: grün ab (Anteil, z. B. 0,55)",
        [MargeGruenBis] = "Margen-Ampel: grün bis (Anteil, z. B. 0,72)",
        [MargeRotUnter] = "Margen-Ampel: rot unter (Anteil, z. B. 0,45)",
    };
}
