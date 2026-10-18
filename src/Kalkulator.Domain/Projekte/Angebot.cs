namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Ein erzeugtes Angebotsdokument zu genau einer eingefrorenen Kalkulationsversion (C-06, C-07). Das Dokument selbst
/// liegt getrennt in <see cref="AngebotsDatei"/>, damit Übersichten es nicht mitladen. Änderbar sind nur der Versandvermerk
/// und der Archivvermerk: Nach dem Projektabschluss liegt das Dokument im Kanalordner, der Kalkulator behält den Link.
/// </summary>
public class Angebot
{
    public int Id { get; set; }
    public int KalkulationsversionId { get; init; }
    public Kalkulationsversion? Version { get; init; }

    /// <summary>Angebotsnummer der Kalkulation, z. B. „MS-A-2026-0001“; alle Versionen einer Kalkulation teilen sie.</summary>
    public required string Nummer { get; init; }

    public DateOnly Datum { get; init; }
    public DateOnly GueltigBis { get; init; }
    public string? Freitext { get; init; }
    public required string Dateiname { get; init; }
    public required string Vorlage { get; init; }
    public required string ErstelltVon { get; init; }
    public DateTimeOffset ErstelltAm { get; init; }

    public DateOnly? VersendetAm { get; private set; }
    public string? VersendetVon { get; private set; }

    public AngebotsDatei? Datei { get; init; }

    /// <summary>Höchstlänge eines Archivlinks; ein längerer Link wird nicht gekürzt, sondern abgewiesen.</summary>
    public const int LinkLaenge = 2000;

    /// <summary>Link auf das Dokument im Kanalordner (60_Angebote/Archiv Kalkulator), sobald es dorthin archiviert ist.</summary>
    public string? ArchivLink { get; private set; }

    public DateTimeOffset? ArchiviertAm { get; private set; }

    public bool IstArchiviert => ArchiviertAm is not null;

    /// <summary>
    /// Archivvermerk (Entscheidung 08.10.2026): Das Dokument liegt jetzt im Kanalordner; die <see cref="AngebotsDatei"/>
    /// darf danach gelöscht werden.
    /// </summary>
    public void Archiviere(string link, DateTimeOffset zeitpunkt)
    {
        PruefeLink(link);
        if (IstArchiviert)
        {
            throw new InvalidOperationException($"Angebot {Nummer} ist bereits archiviert.");
        }

        ArchivLink = link;
        ArchiviertAm = zeitpunkt;
    }

    /// <summary>Ein Archivlink muss vorhanden sein und in die Datenbank passen.</summary>
    public static void PruefeLink(string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        if (link.Length > LinkLaenge)
        {
            throw new ArgumentException($"Der Link ist länger als {LinkLaenge} Zeichen.", nameof(link));
        }
    }

    /// <summary>Versandvermerk (C-10): wann diese Version an den Kunden ging.</summary>
    public void AlsVersendetMarkieren(DateOnly datum, string benutzer)
    {
        if (VersendetAm is not null)
        {
            throw new InvalidOperationException($"Angebot {Nummer} ist bereits als versendet markiert ({VersendetAm:dd.MM.yyyy}).");
        }

        VersendetAm = datum;
        VersendetVon = benutzer;
    }
}

/// <summary>
/// Status eines Angebots für Übersicht und Filter (Entscheidung 08.10.2026). Er wird nicht gespeichert, sondern aus
/// Versandvermerk, Gültigkeit, neueren Versionen und dem Projektabschluss abgeleitet (<see cref="Angebotsstatus"/>).
/// </summary>
public enum AngebotsStatus
{
    /// <summary>Erzeugt, noch nicht als versendet markiert.</summary>
    Erzeugt = 1,

    Versendet = 2,

    /// <summary>Das Angebot, mit dem das Projekt auf „Gewonnen“ steht.</summary>
    Angenommen = 3,

    /// <summary>Das Projekt ist gewonnen oder verloren, dieses Angebot wurde nicht angenommen.</summary>
    NichtAngenommen = 4,

    /// <summary>Es gibt eine neuere Version derselben Angebotsnummer.</summary>
    Ersetzt = 5,

    /// <summary>Versendet, aber die Gültigkeit ist überschritten.</summary>
    Abgelaufen = 6,
}

public static class Angebotsstatus
{
    /// <summary>
    /// Leitet den Status ab. Vorrang: angenommen, nicht angenommen (Projekt abgeschlossen), ersetzt, abgelaufen,
    /// versendet, erzeugt.
    /// </summary>
    public static AngebotsStatus Bestimme(bool angenommen, bool projektAbgeschlossen, bool neuereVersion, DateOnly? versendetAm, DateOnly gueltigBis, DateOnly heute)
    {
        if (angenommen)
        {
            return AngebotsStatus.Angenommen;
        }

        if (projektAbgeschlossen)
        {
            return AngebotsStatus.NichtAngenommen;
        }

        if (neuereVersion)
        {
            return AngebotsStatus.Ersetzt;
        }

        if (versendetAm is null)
        {
            return AngebotsStatus.Erzeugt;
        }

        return gueltigBis < heute ? AngebotsStatus.Abgelaufen : AngebotsStatus.Versendet;
    }
}

/// <summary>Das erzeugte Word-Dokument eines Angebots (Archiv, C-07).</summary>
public class AngebotsDatei
{
    public int AngebotId { get; set; }
    public required byte[] Inhalt { get; init; }
}

/// <summary>Fortlaufender Nummernkreis je Art und Jahr, z. B. „MS-A“ 2026 → MS-A-2026-0001 (Frage 8.6).</summary>
public class Nummernkreis
{
    public required string Kreis { get; init; }
    public int Jahr { get; init; }
    public int LetzteNummer { get; set; }

    public static string Format(string kreis, int jahr, int nummer) => $"{kreis}-{jahr}-{nummer:0000}";
}
