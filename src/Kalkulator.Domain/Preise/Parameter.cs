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
}
