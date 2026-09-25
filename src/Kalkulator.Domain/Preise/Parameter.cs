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

/// <summary>Bekannte Parameter-Schlüssel (Werte siehe docs/06_ist-analyse.md).</summary>
public static class ParameterSchluessel
{
    public const string AeSatzEbene1 = "AE_SATZ_EBENE_1";
    public const string AeSatzEbene2 = "AE_SATZ_EBENE_2";
    public const string AeSatzEbene3 = "AE_SATZ_EBENE_3";
    public const string SupportkontingentAeSatz = "S60_AE_SATZ";
    public const string SupportkontingentBlockgroesse = "S60_BLOCKGROESSE_AE";
    public const string BackupGrundpauschale = "S14_GRUNDPAUSCHALE";
    public const string BackupProServer = "S14_PRO_SERVER";
    public const string BackupPaketpreis = "S14_PAKETPREIS";
    public const string BackupPaketgroesseGb = "S14_PAKETGROESSE_GB";
    public const string BackupObjektspeicherProTb = "S14_OBJEKTSPEICHER_PRO_TB";
    public const string BackupLizenzProInstanz = "S14_LIZENZ_PRO_INSTANZ";
    public const string BackupPreisuntergrenze = "S14_PREISUNTERGRENZE";
    public const string CloudServerMargenteiler = "S61_MARGENTEILER";
    public const string EkKostensatzProStunde = "EK_KOSTENSATZ_PRO_STUNDE";
}
