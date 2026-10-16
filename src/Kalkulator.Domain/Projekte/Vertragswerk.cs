namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Erzeugtes Vertragswerk eines gewonnenen Kundenprojekts (#26, Teil C): alle Dokumente als PDF, eine Gesamtdatei für
/// den Kunden und ein ZIP. Grundlage sind die eingefrorene Version des angenommenen Angebots und die bei der Erzeugung
/// aktiven Vorlagenfassungen, die in <see cref="Dokumente"/> festgehalten werden. Unveränderlich bis auf die
/// Vertragsfreigaben (Vertriebsleitung, dann AVV und Technik), den Vermerk der Übergabe an Paperless (Teil D), den
/// Vermerk der Unterzeichnung und den Archivvermerk; wird es neu erzeugt (z. B.
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

    /// <summary>Zuletzt von Paperless gemeldeter Status des Dokuments (stündliche Abfrage, Entscheidung 16.10.2026).</summary>
    public string? PaperlessStatus { get; private set; }

    /// <summary>Zeitpunkt der letzten Statusabfrage bei Paperless.</summary>
    public DateTimeOffset? StatusGeprueftAm { get; private set; }

    /// <summary>Wann der Kalkulator die vollständige Unterzeichnung festgestellt hat; danach wird nicht mehr abgefragt.</summary>
    public DateTimeOffset? UnterzeichnetAm { get; private set; }

    public bool IstUnterzeichnet => UnterzeichnetAm is not null;

    /// <summary>Unterschriebenes PDF und Prüfprotokoll aus Paperless; getrennt gespeichert wie <see cref="Datei"/>.</summary>
    public VertragswerkUnterschrift? Unterschrift { get; private set; }

    /// <summary>Link auf den unterschriebenen Vertrag im Kanalordner (70_Vertraege), sobald er dort abgelegt ist.</summary>
    public string? AblageLink { get; private set; }

    public DateTimeOffset? AbgelegtAm { get; private set; }

    /// <summary>Wann die Benachrichtigung über den unterschriebenen Vertrag hinausging (Liste für Power Automate).</summary>
    public DateTimeOffset? GemeldetAm { get; private set; }

    /// <summary>Link auf das Gesamt-PDF im Kanalordner (70_Vertraege/Archiv Kalkulator), sobald es archiviert ist.</summary>
    public string? ArchivLink { get; private set; }

    /// <summary>Link auf das ZIP im Kanalordner (70_Vertraege/Archiv Kalkulator).</summary>
    public string? ArchivZipLink { get; private set; }

    public DateTimeOffset? ArchiviertAm { get; private set; }

    public bool IstArchiviert => ArchiviertAm is not null;

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

    /// <summary>
    /// Vermerkt das Ergebnis einer Statusabfrage. Ist das Dokument laut Paperless vollständig unterzeichnet, wird die
    /// Unterzeichnung einmalig festgehalten; der Status ändert sich danach nicht mehr.
    /// </summary>
    public void VermerkeStatus(string status, bool unterzeichnet, DateTimeOffset zeitpunkt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IstUebergeben)
        {
            throw new InvalidOperationException("Das Vertragswerk ist noch nicht an Paperless übergeben.");
        }

        if (IstUnterzeichnet)
        {
            return;
        }

        PaperlessStatus = status.Length > 50 ? status[..50] : status;
        StatusGeprueftAm = zeitpunkt;
        if (unterzeichnet)
        {
            UnterzeichnetAm = zeitpunkt;
        }
    }

    /// <summary>Legt das unterschriebene PDF (und, falls geliefert, das Prüfprotokoll) einmalig ab.</summary>
    public void HinterlegeUnterschrift(byte[] pdf, byte[]? pruefprotokoll, DateTimeOffset zeitpunkt)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (!IstUnterzeichnet)
        {
            throw new InvalidOperationException("Das Vertragswerk ist laut Paperless noch nicht vollständig unterzeichnet.");
        }

        if (Unterschrift is not null)
        {
            throw new InvalidOperationException("Der unterschriebene Vertrag ist bereits abgelegt.");
        }

        if (pdf.Length == 0)
        {
            throw new ArgumentException("Paperless hat ein leeres PDF geliefert.", nameof(pdf));
        }

        Unterschrift = new VertragswerkUnterschrift { Pdf = pdf, Pruefprotokoll = pruefprotokoll is { Length: > 0 } ? pruefprotokoll : null, AbgelegtAm = zeitpunkt };
    }

    /// <summary>Vermerkt die Ablage des unterschriebenen Vertrags im Kanalordner; einmalig.</summary>
    public void VermerkeAblage(string? link, DateTimeOffset zeitpunkt)
    {
        if (Unterschrift is null)
        {
            throw new InvalidOperationException("Abgelegt wird erst der unterschriebene Vertrag.");
        }

        if (AbgelegtAm is not null)
        {
            throw new InvalidOperationException("Der unterschriebene Vertrag ist bereits im Kanalordner abgelegt.");
        }

        AblageLink = string.IsNullOrWhiteSpace(link) ? null : link.Length > 1000 ? link[..1000] : link;
        AbgelegtAm = zeitpunkt;
    }

    /// <summary>Vermerkt die Benachrichtigung über den unterschriebenen Vertrag; einmalig.</summary>
    public void VermerkeMeldung(DateTimeOffset zeitpunkt)
    {
        if (!IstUnterzeichnet)
        {
            throw new InvalidOperationException("Gemeldet wird erst ein unterschriebener Vertrag.");
        }

        GemeldetAm ??= zeitpunkt;
    }

    /// <summary>
    /// Archivvermerk (Entscheidung 08.10.2026): Gesamt-PDF und ZIP liegen jetzt im Kanalordner; die
    /// <see cref="VertragswerkDatei"/> darf danach gelöscht werden. Die unterzeichnete Ausfertigung bleibt im Kalkulator.
    /// </summary>
    public void Archiviere(string pdfLink, string zipLink, DateTimeOffset zeitpunkt)
    {
        Angebot.PruefeLink(pdfLink);
        Angebot.PruefeLink(zipLink);
        if (IstUnterzeichnet)
        {
            throw new InvalidOperationException("Die unterzeichnete Ausfertigung bleibt im Kalkulator.");
        }

        if (IstArchiviert)
        {
            throw new InvalidOperationException("Das Vertragswerk ist bereits archiviert.");
        }

        ArchivLink = pdfLink;
        ArchivZipLink = zipLink;
        ArchiviertAm = zeitpunkt;
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

/// <summary>Der in Paperless unterschriebene Vertrag mit Prüfprotokoll; getrennt gespeichert wie <see cref="VertragswerkDatei"/>.</summary>
public class VertragswerkUnterschrift
{
    public int VertragswerkId { get; set; }
    public required byte[] Pdf { get; init; }
    public byte[]? Pruefprotokoll { get; init; }
    public DateTimeOffset AbgelegtAm { get; init; }
}

/// <summary>Gesamt-PDF und ZIP; getrennt gespeichert, damit Übersichten sie nicht mitladen.</summary>
public class VertragswerkDatei
{
    public int VertragswerkId { get; set; }
    public required byte[] GesamtPdf { get; init; }
    public required byte[] Zip { get; init; }
}
