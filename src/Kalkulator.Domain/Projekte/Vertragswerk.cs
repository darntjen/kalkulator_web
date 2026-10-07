namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Erzeugtes Vertragswerk eines gewonnenen Kundenprojekts (#26, Teil C): alle Dokumente als PDF, eine Gesamtdatei für
/// den Kunden und ein ZIP. Grundlage sind die eingefrorene Version des angenommenen Angebots und die bei der Erzeugung
/// aktiven Vorlagenfassungen, die in <see cref="Dokumente"/> festgehalten werden. Unveränderlich bis auf die
/// Vertragsfreigaben (Vertriebsleitung, dann AVV und Technik) und den Vermerk der Übergabe an Paperless (Teil D); wird es neu erzeugt (z. B.
/// nach einer Ablehnung oder einer korrigierten Vorlage), entsteht eine weitere Ausfertigung ohne Freigaben.
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

    /// <summary>Unterzeichner, die beim Erzeugen für Paperless erfasst wurden (ohne die fest eingestellten Rollen).</summary>
    public IReadOnlyList<Unterzeichner> Unterzeichner { get; init; } = [];

    /// <summary>Kennung des Dokuments in Paperless, sobald die Übergabe geklappt hat.</summary>
    public string? PaperlessDokumentId { get; private set; }

    public DateTimeOffset? UebergebenAm { get; private set; }

    /// <summary>Meldung der letzten fehlgeschlagenen Übergabe; leer nach Erfolg.</summary>
    public string? UebergabeFehler { get; private set; }

    /// <summary>Zeitpunkt des letzten Übergabeversuchs, gelungen oder nicht.</summary>
    public DateTimeOffset? UebergabeVersuchtAm { get; private set; }

    public bool IstUebergeben => PaperlessDokumentId is not null;

    /// <summary>
    /// Reihenfolge der Prüfungen: Zuerst gibt die Vertriebsleitung frei (Entscheidung 07.10.2026), danach prüfen AVV und
    /// Technik parallel (Entscheidung 05.10.2026).
    /// </summary>
    public static readonly IReadOnlyList<VertragsfreigabeArt> Pruefungen =
        [VertragsfreigabeArt.Vertriebsleitung, VertragsfreigabeArt.Avv, VertragsfreigabeArt.Technik];

    /// <summary>Vermerk an der Freigabe der Vertriebsleitung, wenn sie das Vertragswerk selbst erzeugt hat.</summary>
    public const string BeimErzeugen = "Von der Vertriebsleitung erzeugt";

    /// <summary>Prüfungen dieser Ausfertigung durch Vertriebsleitung, AVV und Technik.</summary>
    public List<Vertragsfreigabe> Freigaben { get; } = [];

    /// <summary>Eine Ablehnung sperrt die Ausfertigung; der Vertrieb korrigiert und erzeugt neu.</summary>
    public bool IstAbgelehnt => Freigaben.Any(f => !f.Erteilt);

    /// <summary>Alle Freigaben liegen vor; erst dann geht das Vertragswerk an Paperless.</summary>
    public bool IstFreigegeben => !IstAbgelehnt && Pruefungen.All(a => Freigaben.Any(f => f.Art == a && f.Erteilt));

    public Vertragsfreigabe? Freigabe(VertragsfreigabeArt art) => Freigaben.FirstOrDefault(f => f.Art == art);

    /// <summary>
    /// Ob die Prüfung <paramref name="art"/> schon vorgelegt wird: die Vertriebsleitung sofort, AVV und Technik erst nach
    /// ihrer Freigabe.
    /// </summary>
    public bool IstVorgelegt(VertragsfreigabeArt art) => IstVorgelegt(art, Freigabe);

    /// <summary>Gemeinsame Regel für das Vertragswerk und seine Listenansicht.</summary>
    public static bool IstVorgelegt(VertragsfreigabeArt art, Func<VertragsfreigabeArt, Vertragsfreigabe?> freigabe) =>
        art == VertragsfreigabeArt.Vertriebsleitung || freigabe(VertragsfreigabeArt.Vertriebsleitung)?.Erteilt == true;

    /// <summary>
    /// Vermerkt die Freigabe bzw. Ablehnung einer Prüfung. Eine Ablehnung braucht eine Begründung; AVV und Technik prüfen
    /// erst nach der Freigabe der Vertriebsleitung; nach einer Ablehnung oder der Übergabe an Paperless ist nichts mehr zu
    /// prüfen.
    /// </summary>
    public void Pruefe(VertragsfreigabeArt art, bool erteilt, string? begruendung, string von, DateTimeOffset zeitpunkt)
    {
        if (!Enum.IsDefined(art))
        {
            throw new ArgumentOutOfRangeException(nameof(art));
        }

        if (IstUebergeben)
        {
            throw new InvalidOperationException("Das Vertragswerk ist bereits an Paperless übergeben.");
        }

        if (IstAbgelehnt)
        {
            throw new InvalidOperationException("Diese Ausfertigung wurde abgelehnt. Der Vertrieb korrigiert sie und erzeugt das Vertragswerk neu.");
        }

        if (Freigabe(art) is not null)
        {
            throw new InvalidOperationException("Für diese Ausfertigung ist die Prüfung bereits erfolgt.");
        }

        if (!IstVorgelegt(art))
        {
            throw new InvalidOperationException("Zuerst gibt die Vertriebsleitung das Vertragswerk frei; danach prüfen AVV und Technik.");
        }

        begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim();
        if (!erteilt && begruendung is null)
        {
            throw new ArgumentException("Bitte begründen, warum das Vertragswerk abgelehnt wird.");
        }

        if (begruendung is { Length: > 1000 })
        {
            throw new ArgumentException("Die Begründung darf höchstens 1000 Zeichen lang sein.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(von);
        Freigaben.Add(new Vertragsfreigabe { Art = art, Erteilt = erteilt, Begruendung = begruendung, Von = von, Am = zeitpunkt });
    }

    /// <summary>
    /// Hat die Vertriebsleitung das Vertragswerk selbst erzeugt, entfällt ihre Prüfung (Entscheidung 07.10.2026): Die
    /// Freigabe wird beim Erzeugen vermerkt, und AVV und Technik sehen das Vertragswerk sofort.
    /// </summary>
    public void GibFreigabeBeimErzeugen(DateTimeOffset zeitpunkt) =>
        Pruefe(VertragsfreigabeArt.Vertriebsleitung, true, BeimErzeugen, ErstelltVon, zeitpunkt);

    /// <summary>Vermerkt die gelungene Übergabe; ein Vertragswerk geht nur einmal an Paperless.</summary>
    public void VermerkeUebergabe(string dokumentId, DateTimeOffset zeitpunkt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dokumentId);
        if (IstUebergeben)
        {
            throw new InvalidOperationException($"Das Vertragswerk ist bereits an Paperless übergeben (Dokument {PaperlessDokumentId}).");
        }

        PaperlessDokumentId = dokumentId;
        UebergebenAm = zeitpunkt;
        UebergabeVersuchtAm = zeitpunkt;
        UebergabeFehler = null;
    }

    public void VermerkeUebergabeFehler(string fehler, DateTimeOffset zeitpunkt)
    {
        if (IstUebergeben)
        {
            throw new InvalidOperationException($"Das Vertragswerk ist bereits an Paperless übergeben (Dokument {PaperlessDokumentId}).");
        }

        UebergabeFehler = fehler.Length > 1000 ? fehler[..1000] : fehler;
        UebergabeVersuchtAm = zeitpunkt;
    }
}

/// <summary>Interne Prüfungen eines Vertragswerks vor der Übergabe an Paperless.</summary>
public enum VertragsfreigabeArt
{
    /// <summary>Auftragsverarbeitung (Datenschutz).</summary>
    Avv = 1,

    /// <summary>Vertrag aus technischer Sicht.</summary>
    Technik = 2,

    /// <summary>Freigabe durch die Vertriebsleitung, bevor AVV und Technik prüfen (Entscheidung 07.10.2026).</summary>
    Vertriebsleitung = 3,
}

/// <summary>Freigabe oder Ablehnung einer Prüfung für genau eine Ausfertigung.</summary>
public class Vertragsfreigabe
{
    public int Id { get; set; }
    public int VertragswerkId { get; set; }
    public VertragsfreigabeArt Art { get; init; }
    public bool Erteilt { get; init; }
    public string? Begruendung { get; init; }
    public required string Von { get; init; }
    public DateTimeOffset Am { get; init; }
}

/// <summary>Wer für eine Rolle der Unterschriftsfelder unterschreibt, z. B. Rolle „Kunde“.</summary>
public sealed record Unterzeichner(string Rolle, string Name, string EMail);

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
