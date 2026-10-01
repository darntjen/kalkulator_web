namespace Kalkulator.Domain.Berechnung;

/// <summary>
/// Alles, was der Vertrieb für eine Managed-Services-Kalkulation erfasst. Die Eingabe ist unabhängig von
/// Oberfläche und Datenbank; der <see cref="Rechenkern"/> macht daraus ein <see cref="KalkulationsErgebnis"/>.
/// </summary>
public sealed record KalkulationsEingabe
{
    /// <summary>Gebuchte Preiskomponenten mit Menge, z. B. „S01-PRM“ × 1, „B02“ × 40, „S25-CLIENT“ × 30.</summary>
    public IReadOnlyList<PositionsEingabe> Positionen { get; init; } = [];

    /// <summary>Anzahl User; bestimmt die Onboarding-Pauschale (Staffel nach Connect-Stufe).</summary>
    public int AnzahlUser { get; init; }

    /// <summary>Anzahl Mitarbeitende; bestimmt den Preis von S41.</summary>
    public int? AnzahlMitarbeitende { get; init; }

    public SupportkontingentEingabe? Supportkontingent { get; init; }

    public ServerBackupEingabe? ServerBackup { get; init; }

    public CloudServerEingabe? CloudServer { get; init; }

    public IReadOnlyList<SonderpositionEingabe> Sonderpositionen { get; init; } = [];

    /// <summary>Bisheriger Monatspreis eines Bestandskunden für den Vorher/Nachher-Vergleich (B-16).</summary>
    public decimal? BisherigerMonatspreis { get; init; }
}

public sealed record PositionsEingabe(string KomponentenCode, int Menge);

/// <summary>S60: Der Bedarf wird aus Anfragen je Monat und durchschnittlichen AE je Anfrage geschätzt.</summary>
public sealed record SupportkontingentEingabe(int AnfragenProMonat, decimal AeJeAnfrage);

public enum BackupVariante
{
    /// <summary>Cloud-Backup in Paketen nach nativ geschützter Datenmenge.</summary>
    Cloud = 1,

    /// <summary>Objektspeicher (Object Lock) nach belegtem Volumen.</summary>
    Objektspeicher = 2,
}

/// <summary>
/// S14 nach Baukasten V5.7. <see cref="Lizenzinstanzen"/> zählt nur bei Objektspeicher und nur,
/// wenn die Backup-Software-Lizenz über uns läuft; 0 bedeutet Kundenlizenz.
/// </summary>
public sealed record ServerBackupEingabe(
    BackupVariante Variante,
    int Server,
    int NativGeschuetzteGb = 0,
    decimal BelegteTb = 0,
    int Lizenzinstanzen = 0);

public enum CloudBackup
{
    Offen = 0,

    /// <summary>Datensicherung über S14 (zwingend Variante Cloud-Backup).</summary>
    ServerBackup = 1,

    /// <summary>Der Kunde sichert selbst (Ankreuzfeld in LS S61, Ziffer 4.4).</summary>
    Kunde = 2,
}

/// <summary>S61: Der Vertrieb erfasst den EK aus dem TERRA-Kalkulator; der VK ergibt sich über den Margenteiler.</summary>
public sealed record CloudServerEingabe(decimal EkTerraKalkulator, CloudBackup Backup);

/// <summary>Freie Sonderposition (B-21); sie braucht die Freigabe der Vertriebsleitung (B-22).</summary>
public sealed record SonderpositionEingabe(
    string Bezeichnung,
    string Einheit,
    int Menge,
    decimal Preis,
    string Begruendung,
    bool Einmalig = false,
    bool Freigegeben = false);
