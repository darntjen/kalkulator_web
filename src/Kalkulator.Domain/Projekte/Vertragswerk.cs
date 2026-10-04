namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Erzeugtes Vertragswerk eines gewonnenen Kundenprojekts (#26, Teil C): alle Dokumente als PDF, eine Gesamtdatei für
/// den Kunden und ein ZIP. Grundlage sind die eingefrorene Version des angenommenen Angebots und die bei der Erzeugung
/// aktiven Vorlagenfassungen, die in <see cref="Dokumente"/> festgehalten werden. Unveränderlich; wird es neu
/// erzeugt (z. B. nach einer korrigierten Vorlage), entsteht eine weitere Ausfertigung.
/// </summary>
public class Vertragswerk
{
    public int Id { get; set; }
    public int KundenprojektId { get; init; }
    public int AngebotId { get; init; }

    /// <summary>Vertragsnummer = Angebotsnummer (Entscheidung 04.10.2026).</summary>
    public required string Nummer { get; init; }

    /// <summary>Laufende Ausfertigung je Kundenprojekt (1, 2 …).</summary>
    public int Ausfertigung { get; init; }

    public required string ErstelltVon { get; init; }
    public DateTimeOffset ErstelltAm { get; init; }
    public required string GesamtDateiname { get; init; }
    public required string ZipDateiname { get; init; }

    public List<VertragswerkEintrag> Dokumente { get; } = [];

    public VertragswerkDatei? Datei { get; init; }
}

/// <summary>Ein Dokument des Vertragswerks in der Rangfolge, mit der verwendeten Vorlagenfassung.</summary>
public class VertragswerkEintrag
{
    public int Id { get; set; }
    public int VertragswerkId { get; set; }
    public int Reihenfolge { get; init; }
    public required string Code { get; init; }
    public required string Bezeichnung { get; init; }
    public int VorlagenversionId { get; init; }

    /// <summary>Lesbare Fassung, z. B. „S14 V2 (5.7)“.</summary>
    public required string Fassung { get; init; }
}

/// <summary>Gesamt-PDF und ZIP; getrennt gespeichert, damit Übersichten sie nicht mitladen.</summary>
public class VertragswerkDatei
{
    public int VertragswerkId { get; set; }
    public required byte[] GesamtPdf { get; init; }
    public required byte[] Zip { get; init; }
}
