using Kalkulator.Documents;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Paperless;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Ein Dokument des Vertragswerks mit der Vorlage und ihrer aktiven Fassung; ohne aktive Fassung ist <see cref="Fassung"/> leer.</summary>
public sealed record VertragswerkDokument(Vertragsdokument Dokument, DokumentVorlage? Vorlage, Vorlagenversion? Fassung);

/// <summary>
/// Welche Dokumente ein Vertragswerk umfasst und welche Angaben ihre Vorlagen verlangen. <see cref="Dokumente"/> sind
/// alle Dokumente des Vertrags (z. B. für die Anlagenliste im Grundvertrag); erzeugt werden nur die
/// <see cref="Erzeugte"/>, solange <see cref="Umfang"/> das Vertragswerk im Testbetrieb auf einzelne Vorlagen beschränkt.
/// </summary>
public sealed record Vertragsvorschau(IReadOnlyList<VertragswerkDokument> Dokumente, IReadOnlyList<EingabeDefinition> Eingaben, IReadOnlyList<string>? Umfang = null)
{
    public IReadOnlyList<VertragswerkDokument> Erzeugte => [.. Dokumente.Where(ImUmfang)];

    /// <summary>Dokumente, die wegen <see cref="Umfang"/> nicht erzeugt werden.</summary>
    public IReadOnlyList<VertragswerkDokument> Ausgelassen => [.. Dokumente.Where(d => !ImUmfang(d))];

    public IReadOnlyList<VertragswerkDokument> OhneVorlage => [.. Erzeugte.Where(d => d.Fassung is null)];

    /// <summary>Rollen aller Unterschriftsfelder der aktiven Fassungen, sortiert.</summary>
    public IReadOnlyList<string> Unterschriften =>
        [.. Erzeugte.Where(d => d.Fassung is not null).SelectMany(d => d.Fassung!.Unterschriften).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Vorlagencode eines Dokuments, z. B. GRUNDVERTRAG oder S14; ohne Vorlage der Code des Dokuments.</summary>
    public static string Code(VertragswerkDokument d) => d.Vorlage?.Code ?? d.Dokument.Code;

    private bool ImUmfang(VertragswerkDokument d) =>
        Umfang is not { Count: > 0 } || Umfang.Contains(Code(d), StringComparer.OrdinalIgnoreCase);
}

public sealed class VertragswerkEinstellungen
{
    /// <summary>Deckblatt der Gesamtdatei (templates/vertrag/Deckblatt.docx); wird mit dem Programm ausgeliefert.</summary>
    public string Deckblatt { get; set; } = "";

    /// <summary>
    /// Testbetrieb: Vorlagencodes der Dokumente, die erzeugt werden, z. B. nur „GRUNDVERTRAG“, solange die übrigen
    /// Vorlagen noch nicht umgestellt sind. Leer = alle Dokumente (Normalbetrieb).
    /// </summary>
    public List<string> Umfang { get; set; } = [];
}

/// <summary>
/// Ob und mit welchen Dokumenten das Vertragswerk eines Projekts erzeugt werden kann; <see cref="Sperrgrund"/> sagt, woran
/// es hängt. Ist die Übergabe an Paperless eingerichtet, nennt <see cref="Unterzeichnerrollen"/> die Rollen, deren
/// Unterzeichner beim Erzeugen anzugeben sind; <see cref="Hinweis"/> warnt z. B. vor Vorlagen ohne Unterschriftsfeld.
/// </summary>
public sealed record VertragswerkBereitschaft(
    string? Angebot,
    Vertragsvorschau? Vorschau,
    IReadOnlyList<string> FehlendeAngaben,
    string? Sperrgrund,
    bool DarfErzeugen,
    bool Paperless = false,
    IReadOnlyList<string>? Unterzeichnerrollen = null,
    string? Hinweis = null);

public sealed record VertragswerkZeile(
    int Id,
    string Nummer,
    int Ausfertigung,
    string ErstelltVon,
    DateTimeOffset ErstelltAm,
    IReadOnlyList<VertragswerkEintrag> Dokumente,
    string? PaperlessDokumentId = null,
    DateTimeOffset? UebergebenAm = null,
    string? UebergabeFehler = null,
    DateTimeOffset? UebergabeVersuchtAm = null,
    IReadOnlyList<Vertragsfreigabe>? Freigaben = null)
{
    public Vertragsfreigabe? Freigabe(VertragsfreigabeArt art) => Freigaben?.FirstOrDefault(f => f.Art == art);

    public bool IstAbgelehnt => Freigaben?.Any(f => !f.Erteilt) == true;

    public bool IstFreigegeben => !IstAbgelehnt && Enum.GetValues<VertragsfreigabeArt>().All(a => Freigabe(a)?.Erteilt == true);
}

/// <summary>Ein Vertragswerk, das auf eine Freigabe des angemeldeten Benutzers wartet (Startseite).</summary>
public sealed record OffeneVertragsfreigabe(
    int VertragswerkId,
    int ProjektId,
    string Firma,
    string Projekt,
    string Nummer,
    int Ausfertigung,
    DateTimeOffset ErstelltAm,
    IReadOnlyList<VertragsfreigabeArt> Offen);

/// <summary>
/// Vertragswerk aus dem angenommenen Angebot (#26, Teil C): Dokumente nach Rangfolge, Vertragsangaben, Word-Dateien,
/// PDFs, Gesamt-PDF und ZIP. Jede Ausfertigung prüfen AVV und Technik parallel (Entscheidung 05.10.2026); erst wenn
/// beide freigegeben haben, geht das Gesamt-PDF mit seinen Unterschriftsfeldern an Paperless (Teil D). Schlägt die
/// Übergabe fehl, bleibt das Vertragswerk erhalten und kann erneut übergeben werden.
/// </summary>
public sealed class VertragswerkDienst(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    TimeProvider zeit,
    IPdfWandler wandler,
    IOptions<VertragswerkEinstellungen> einstellungen,
    IPaperlessUebergabe paperless,
    IOptions<PaperlessEinstellungen> paperlessEinstellungen)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    /// <summary>
    /// Dokumente und Vertragsangaben für die gebuchten Services einer Kalkulation, z. B. aus dem aktuellen Ergebnis im Editor.
    /// </summary>
    public async Task<Vertragsvorschau> VorschauAsync(int kalkulationId, IEnumerable<string> serviceCodes, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kalkulationen.AsNoTracking().Where(k => k.Id == kalkulationId).Select(k => k.Kundenprojekt).SingleOrDefaultAsync(abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {kalkulationId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Diese Kalkulation gehört zu einem Kundenprojekt eines anderen Vertriebsmitarbeiters.");
        }

        return await VorschauAsync(kontext, serviceCodes, abbruch, einstellungen.Value.Umfang);
    }

    /// <summary>Löst die Services in Dokumente auf und ordnet jedem die Vorlage mit ihrer aktiven Fassung zu.</summary>
    internal static async Task<Vertragsvorschau> VorschauAsync(KalkulatorDbContext kontext, IEnumerable<string> serviceCodes, CancellationToken abbruch, IReadOnlyList<string>? umfang = null)
    {
        // Alle Services mit Bestandteilen laden; EF verknüpft verschachtelte Bundles selbst.
        var services = await kontext.Services.Include(s => s.Bestandteile).Include(s => s.Leistungsschein).ToListAsync(abbruch);
        var vorlagen = await kontext.DokumentVorlagen.Include(d => d.Versionen).ToListAsync(abbruch);
        var nachCode = services.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var codes = serviceCodes.Where(nachCode.ContainsKey).Distinct(StringComparer.Ordinal).ToList();

        var dokumente = Vertragspaket.Aufloesen(codes, services).Select(d =>
        {
            var vorlage = VorlageFuer(d, nachCode, vorlagen);
            return new VertragswerkDokument(d, vorlage, vorlage?.AktiveVersion);
        }).ToList();

        var vorschau = new Vertragsvorschau(dokumente, [], umfang);
        return vorschau with { Eingaben = Vertragsangaben.Vereinige(vorschau.Erzeugte.Where(d => d.Fassung is not null).SelectMany(d => d.Fassung!.Eingaben)) };
    }

    /// <summary>Was zum Erzeugen des Vertragswerks eines Projekts fehlt; für die Projektansicht.</summary>
    public async Task<VertragswerkBereitschaft> BereitschaftAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var (projekt, angebot) = await LadeAsync(kontext, projektId, abbruch);
        return await BereitschaftAsync(kontext, projekt, angebot, abbruch);
    }

    public async Task<IReadOnlyList<VertragswerkZeile>> ListeAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        await LadeAsync(kontext, projektId, abbruch);
        var werke = await kontext.Vertragswerke.AsNoTracking().Include(v => v.Dokumente).Include(v => v.Freigaben)
            .Where(v => v.KundenprojektId == projektId).OrderByDescending(v => v.Ausfertigung).ToListAsync(abbruch);
        return [.. werke.Select(v => new VertragswerkZeile(v.Id, v.Nummer, v.Ausfertigung, v.ErstelltVon, v.ErstelltAm, [.. v.Dokumente.OrderBy(d => d.Reihenfolge)],
            v.PaperlessDokumentId, v.UebergebenAm, v.UebergabeFehler, v.UebergabeVersuchtAm, [.. v.Freigaben.OrderBy(f => f.Art)]))];
    }

    /// <summary>Gesamt-PDF oder ZIP eines erzeugten Vertragswerks.</summary>
    public async Task<(string Name, byte[] Inhalt)> DateiAsync(int vertragswerkId, bool zip, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var werk = await kontext.Vertragswerke.AsNoTracking().Include(v => v.Datei).SingleOrDefaultAsync(v => v.Id == vertragswerkId, abbruch)
            ?? throw new KeyNotFoundException($"Vertragswerk {vertragswerkId} gibt es nicht.");
        await LadeAsync(kontext, werk.KundenprojektId, abbruch);
        return zip ? (werk.ZipDateiname, werk.Datei!.Zip) : (werk.GesamtDateiname, werk.Datei!.GesamtPdf);
    }

    /// <summary>
    /// Erzeugt das Vertragswerk aus dem angenommenen Angebot: jedes Dokument aus seiner aktiven Vorlagenfassung befüllt und
    /// als PDF, dazu Deckblatt mit Verzeichnis, Gesamt-PDF und ZIP. Archiviert als neue Ausfertigung, die danach AVV und
    /// Technik prüfen; <paramref name="unterzeichner"/> nennt die Personen der abgefragten Rollen für Paperless.
    /// </summary>
    public async Task<int> ErzeugenAsync(int projektId, IReadOnlyList<Unterzeichner>? unterzeichner = null, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var (projekt, angebot) = await LadeAsync(kontext, projektId, abbruch);
        if (!_recht.DarfBearbeiten(projekt))
        {
            throw new KeinZugriffException("Das Vertragswerk erzeugt der Vertrieb, der das Kundenprojekt verantwortet, oder die Vertriebsleitung.");
        }

        var bereitschaft = await BereitschaftAsync(kontext, projekt, angebot, abbruch);
        if (bereitschaft.Sperrgrund is { } grund)
        {
            throw new InvalidOperationException(grund);
        }

        var personen = PruefeUnterzeichner(bereitschaft.Unterzeichnerrollen ?? [], unterzeichner ?? []);
        var vorschau = bereitschaft.Vorschau!;
        var version = angebot!.Version!;
        var erzeugte = vorschau.Erzeugte;
        var fassungIds = erzeugte.Select(d => d.Fassung!.Id).ToList();
        var dateien = await kontext.Set<VorlagenDatei>().AsNoTracking().Where(d => fassungIds.Contains(d.VorlagenversionId))
            .ToDictionaryAsync(d => d.VorlagenversionId, d => d.Inhalt, abbruch);
        var katalog = await kontext.Services.AsNoTracking().Include(s => s.Leistungsschein).ToListAsync(abbruch);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);
        var quelle = new VertragsQuelle(angebot.Nummer, $"{angebot.Nummer} V{version.Nummer}", heute, version, projekt.Kunde!,
            katalog, [.. vorschau.Dokumente.Select(d => d.Dokument)], vorschau.Eingaben);

        var mappe = new List<MappenDokument>();
        foreach (var (eintrag, i) in erzeugte.Select((d, i) => (d, i)))
        {
            var fassung = eintrag.Fassung!;
            byte[] docx;
            try
            {
                docx = Vertragsdaten.Erzeuge(dateien[fassung.Id], Vertragsdaten.Fuer(quelle, eintrag.Dokument), fassung.Komponenten, fassung.Unterschriften);
            }
            catch (VorlagenFehler e)
            {
                throw new InvalidOperationException($"{eintrag.Vorlage!.Code} (Fassung V{fassung.Nummer}): {e.Message}");
            }

            mappe.Add(new MappenDokument(i + 1, eintrag.Vorlage!.Code, eintrag.Dokument.Bezeichnung, Fassung(eintrag), await PdfAsync(docx, eintrag.Vorlage.Code, abbruch)));
        }

        var deckblatt = WordVorlage.Befuellen(await File.ReadAllBytesAsync(einstellungen.Value.Deckblatt, abbruch),
            Vertragsmappe.Deckblatt(Vertragsdaten.Gemeinsam(quelle), mappe));
        var gesamt = Vertragsmappe.Zusammenfuehren([await PdfAsync(deckblatt, "Deckblatt", abbruch), .. mappe.Select(m => m.Pdf)]);
        var name = $"Vertrag_{angebot.Nummer}_{Vertragsmappe.Dateiteil(projekt.Kunde!.Firma)}";

        var ausfertigung = await kontext.Vertragswerke.Where(v => v.KundenprojektId == projektId).MaxAsync(v => (int?)v.Ausfertigung, abbruch) ?? 0;
        var werk = new Vertragswerk
        {
            KundenprojektId = projektId,
            AngebotId = angebot.Id,
            Nummer = angebot.Nummer,
            Ausfertigung = ausfertigung + 1,
            ErstelltVon = _recht.Name,
            ErstelltAm = zeit.GetUtcNow(),
            GesamtDateiname = name + ".pdf",
            ZipDateiname = name + ".zip",
            Datei = new VertragswerkDatei { GesamtPdf = gesamt, Zip = Vertragsmappe.Zip(mappe, name + ".pdf", gesamt) },
            Unterzeichner = personen,
        };
        werk.Dokumente.AddRange(erzeugte.Select((d, i) => new VertragswerkEintrag
        {
            Reihenfolge = i + 1,
            Code = d.Vorlage!.Code,
            Bezeichnung = d.Dokument.Bezeichnung,
            VorlagenversionId = d.Fassung!.Id,
            Fassung = Fassung(d),
        }));
        kontext.Vertragswerke.Add(werk);
        await kontext.SaveChangesAsync(abbruch);
        return werk.Id;
    }

    /// <summary>
    /// Freigabe oder Ablehnung der neuesten Ausfertigung durch AVV bzw. Technik. Liegt danach auch die andere Freigabe
    /// vor und ist Paperless eingerichtet, geht das Vertragswerk gleich an Paperless.
    /// </summary>
    public async Task FreigebenAsync(int vertragswerkId, VertragsfreigabeArt art, bool erteilt, string? begruendung = null, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var werk = await kontext.Vertragswerke.Include(v => v.Freigaben).Include(v => v.Datei).Include(v => v.Dokumente)
            .SingleOrDefaultAsync(v => v.Id == vertragswerkId, abbruch)
            ?? throw new KeyNotFoundException($"Vertragswerk {vertragswerkId} gibt es nicht.");
        var (projekt, _) = await LadeAsync(kontext, werk.KundenprojektId, abbruch);
        if (!_recht.DarfVertragFreigeben(art))
        {
            throw new KeinZugriffException(art == VertragsfreigabeArt.Avv
                ? "Die AVV-Freigabe erteilt die Rolle „Freigabe AVV“."
                : "Die technische Freigabe erteilt die Rolle „Freigabe Technik“.");
        }

        await PruefeNeuesteAsync(kontext, werk, abbruch);
        werk.Pruefe(art, erteilt, begruendung, _recht.Name, zeit.GetUtcNow());
        await kontext.SaveChangesAsync(abbruch);

        if (werk.IstFreigegeben && paperlessEinstellungen.Value.Aktiv)
        {
            await UebergebenAsync(kontext, werk, projekt.Kunde!.Firma, werk.Datei!.GesamtPdf, await RollenAsync(kontext, werk, abbruch), abbruch);
        }
    }

    /// <summary>Neueste, weder abgelehnte noch übergebene Ausfertigungen, die auf eine Freigabe des Benutzers warten.</summary>
    public async Task<IReadOnlyList<OffeneVertragsfreigabe>> OffeneFreigabenAsync(CancellationToken abbruch = default)
    {
        var arten = Enum.GetValues<VertragsfreigabeArt>().Where(_recht.DarfVertragFreigeben).ToList();
        if (arten.Count == 0)
        {
            return [];
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var werke = await kontext.Vertragswerke.AsNoTracking().Include(v => v.Freigaben)
            .Where(v => v.PaperlessDokumentId == null).ToListAsync(abbruch);
        var neueste = werke.GroupBy(v => v.KundenprojektId).Select(g => g.MaxBy(v => v.Ausfertigung)!).Where(v => !v.IstAbgelehnt).ToList();
        var ids = neueste.Select(v => v.KundenprojektId).ToList();
        var projekte = await kontext.Kundenprojekte.AsNoTracking().Include(p => p.Kunde)
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, abbruch);
        return [.. neueste
            .Select(v => (Werk: v, Offen: arten.Where(a => v.Freigabe(a) is null).ToList()))
            .Where(x => x.Offen.Count > 0 && projekte.ContainsKey(x.Werk.KundenprojektId) && projekte[x.Werk.KundenprojektId].Status == ProjektStatus.Gewonnen)
            .OrderBy(x => x.Werk.ErstelltAm)
            .Select(x =>
            {
                var p = projekte[x.Werk.KundenprojektId];
                return new OffeneVertragsfreigabe(x.Werk.Id, p.Id, p.Kunde!.Firma, p.Titel, x.Werk.Nummer, x.Werk.Ausfertigung, x.Werk.ErstelltAm, x.Offen);
            })];
    }

    /// <summary>Nur die neueste Ausfertigung wird geprüft und übergeben, damit keine überholte Fassung zum Kunden gelangt.</summary>
    private static async Task PruefeNeuesteAsync(KalkulatorDbContext kontext, Vertragswerk werk, CancellationToken abbruch)
    {
        if (await kontext.Vertragswerke.AnyAsync(v => v.KundenprojektId == werk.KundenprojektId && v.Ausfertigung > werk.Ausfertigung, abbruch))
        {
            throw new InvalidOperationException($"Ausfertigung {werk.Ausfertigung} ist überholt; geprüft und übergeben wird nur die neueste Ausfertigung.");
        }
    }

    /// <summary>Rollen der Unterschriftsfelder aus den Vorlagenfassungen des Vertragswerks.</summary>
    private static async Task<IReadOnlyList<string>> RollenAsync(KalkulatorDbContext kontext, Vertragswerk werk, CancellationToken abbruch)
    {
        var fassungIds = werk.Dokumente.Select(d => d.VorlagenversionId).ToList();
        return (await kontext.Set<Vorlagenversion>().AsNoTracking().Where(v => fassungIds.Contains(v.Id)).ToListAsync(abbruch))
            .SelectMany(v => v.Unterschriften).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Übergibt ein Vertragswerk erneut an Paperless, dessen erste Übergabe fehlgeschlagen ist.</summary>
    public async Task ErneutUebergebenAsync(int vertragswerkId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var werk = await kontext.Vertragswerke.Include(v => v.Datei).Include(v => v.Dokumente).Include(v => v.Freigaben)
            .SingleOrDefaultAsync(v => v.Id == vertragswerkId, abbruch)
            ?? throw new KeyNotFoundException($"Vertragswerk {vertragswerkId} gibt es nicht.");
        var (projekt, _) = await LadeAsync(kontext, werk.KundenprojektId, abbruch);
        if (!_recht.DarfBearbeiten(projekt))
        {
            throw new KeinZugriffException("Das Vertragswerk übergibt der Vertrieb, der das Kundenprojekt verantwortet, oder die Vertriebsleitung.");
        }

        if (!paperlessEinstellungen.Value.Aktiv)
        {
            throw new InvalidOperationException("Die Übergabe an Paperless ist nicht eingerichtet.");
        }

        if (werk.IstUebergeben)
        {
            throw new InvalidOperationException($"Das Vertragswerk ist bereits an Paperless übergeben (Dokument {werk.PaperlessDokumentId}).");
        }

        if (!werk.IstFreigegeben)
        {
            throw new InvalidOperationException("An Paperless geht das Vertragswerk erst nach den Freigaben durch AVV und Technik.");
        }

        await PruefeNeuesteAsync(kontext, werk, abbruch);
        await UebergebenAsync(kontext, werk, projekt.Kunde!.Firma, werk.Datei!.GesamtPdf, await RollenAsync(kontext, werk, abbruch), abbruch);
    }

    /// <summary>
    /// Sucht die Unterschriftsfelder im Gesamt-PDF, ordnet ihnen Slots und Teilnehmer zu und legt das Dokument in
    /// Paperless an. Das Ergebnis, auch ein Fehler, wird am Vertragswerk vermerkt; das Vertragswerk selbst bleibt.
    /// </summary>
    private async Task UebergebenAsync(KalkulatorDbContext kontext, Vertragswerk werk, string firma, byte[] gesamt, IReadOnlyList<string> rollen, CancellationToken abbruch)
    {
        try
        {
            var auftrag = Auftrag(paperlessEinstellungen.Value, werk, firma, gesamt, rollen);
            var id = await paperless.UebergebenAsync(auftrag, abbruch);
            werk.VermerkeUebergabe(id, zeit.GetUtcNow());
        }
        catch (PaperlessFehler e)
        {
            werk.VermerkeUebergabeFehler(e.Message, zeit.GetUtcNow());
        }

        await kontext.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Auftrag für Paperless; prüft, dass jede Rolle der Vorlagen im PDF ein Feld und einen Unterzeichner hat.</summary>
    internal static PaperlessAuftrag Auftrag(PaperlessEinstellungen e, Vertragswerk werk, string firma, byte[] gesamt, IReadOnlyList<string> rollen)
    {
        IReadOnlyList<Unterschriftsfeld> stellen;
        try
        {
            stellen = Unterschriftsfelder.Finde(gesamt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PaperlessFehler($"Die Unterschriftsfelder im PDF konnten nicht gelesen werden: {ex.Message}");
        }

        var ohneFeld = rollen.Where(r => !stellen.Any(s => s.Rolle == r)).ToList();
        if (ohneFeld.Count > 0)
        {
            throw new PaperlessFehler($"Im PDF fehlt das Unterschriftsfeld für {string.Join(", ", ohneFeld)}. Bitte die PDF-Umwandlung prüfen.");
        }

        var teilnehmer = new List<PaperlessTeilnehmer>();
        foreach (var rolle in stellen.Select(s => s.Rolle).Distinct(StringComparer.Ordinal))
        {
            var einstellung = e.Rolle(rolle);
            var slot = string.IsNullOrWhiteSpace(einstellung.Slot) ? rolle : einstellung.Slot.Trim();
            if (einstellung.AusVorlage || teilnehmer.Any(t => t.Slot == slot))
            {
                continue;
            }

            if (einstellung.Fest)
            {
                teilnehmer.Add(new PaperlessTeilnehmer(slot, einstellung.Name!.Trim(), einstellung.EMail!.Trim()));
            }
            else if (werk.Unterzeichner.FirstOrDefault(u => u.Rolle == rolle) is { } person)
            {
                teilnehmer.Add(new PaperlessTeilnehmer(slot, person.Name, person.EMail));
            }
            else
            {
                throw new PaperlessFehler($"Für die Rolle „{rolle}“ fehlt der Unterzeichner.");
            }
        }

        var felder = stellen.Select(s =>
        {
            var einstellung = e.Rolle(s.Rolle);
            return new PaperlessFeld(string.IsNullOrWhiteSpace(einstellung.Slot) ? s.Rolle : einstellung.Slot.Trim(), s);
        }).ToList();
        return new PaperlessAuftrag($"Vertrag {werk.Nummer} – {firma}", werk.GesamtDateiname, gesamt, teilnehmer, felder);
    }

    /// <summary>Jede abgefragte Rolle braucht Name und gültige E-Mail-Adresse; weitere Angaben werden ignoriert.</summary>
    private static List<Unterzeichner> PruefeUnterzeichner(IReadOnlyList<string> rollen, IReadOnlyList<Unterzeichner> angaben)
    {
        var ergebnis = new List<Unterzeichner>();
        var fehler = new List<string>();
        foreach (var rolle in rollen)
        {
            var person = angaben.FirstOrDefault(u => u.Rolle == rolle);
            var name = person?.Name?.Trim() ?? "";
            var mail = person?.EMail?.Trim() ?? "";
            if (name.Length == 0 || name.Length > 200)
            {
                fehler.Add($"Name für „{rolle}“");
            }
            else if (!GueltigeMail(mail))
            {
                fehler.Add($"gültige E-Mail-Adresse für „{rolle}“");
            }
            else
            {
                ergebnis.Add(new Unterzeichner(rolle, name, mail));
            }
        }

        return fehler.Count > 0
            ? throw new InvalidOperationException($"Für Paperless fehlt: {string.Join(", ", fehler)}.")
            : ergebnis;
    }

    private static bool GueltigeMail(string mail) =>
        mail.Length is > 0 and <= 254 && System.Net.Mail.MailAddress.TryCreate(mail, out var adresse) && adresse.Address == mail && mail.Contains('.', StringComparison.Ordinal);

    /// <summary>Umwandlung mit verständlicher Meldung, falls Microsoft 365 oder LibreOffice nicht erreichbar sind.</summary>
    private async Task<byte[]> PdfAsync(byte[] docx, string dokument, CancellationToken abbruch)
    {
        try
        {
            return await wandler.InPdfAsync(docx, abbruch);
        }
        catch (Exception e) when (e is HttpRequestException or Azure.Identity.AuthenticationFailedException or InvalidOperationException
            || (e is TaskCanceledException && !abbruch.IsCancellationRequested))
        {
            throw new InvalidOperationException($"Die PDF-Umwandlung ({wandler.Name}) ist für {dokument} fehlgeschlagen: {e.Message}");
        }
    }

    private static string Fassung(VertragswerkDokument d) =>
        $"{d.Vorlage!.Code} V{d.Fassung!.Nummer}" + (d.Fassung.VersionLaut is { } v ? $" ({v})" : "");

    /// <summary>Projekt mit Kunde und angenommenem Angebot samt eingefrorener Version; prüft das Leserecht.</summary>
    private async Task<(Kundenprojekt Projekt, Angebot? Angebot)> LadeAsync(KalkulatorDbContext kontext, int projektId, CancellationToken abbruch)
    {
        var projekt = await kontext.Kundenprojekte.AsNoTracking().Include(p => p.Kunde).SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }

        var angebot = projekt.AngenommenesAngebotId is { } id
            ? await kontext.Angebote.AsNoTracking().Include(a => a.Version!).ThenInclude(v => v.Positionen).SingleOrDefaultAsync(a => a.Id == id, abbruch)
            : null;
        return (projekt, angebot);
    }

    private async Task<VertragswerkBereitschaft> BereitschaftAsync(KalkulatorDbContext kontext, Kundenprojekt projekt, Angebot? angebot, CancellationToken abbruch)
    {
        var darf = _recht.DarfBearbeiten(projekt);
        if (projekt.Status != ProjektStatus.Gewonnen || angebot?.Version is null)
        {
            return new VertragswerkBereitschaft(null, null, [], "Das Vertragswerk entsteht, sobald das Projekt mit dem angenommenen Angebot auf „Gewonnen“ steht.", darf);
        }

        var version = angebot.Version;
        var text = $"{angebot.Nummer} V{version.Nummer}";
        var vorschau = await VorschauAsync(kontext, version.Positionen.Select(p => p.ServiceCode).OfType<string>(), abbruch, einstellungen.Value.Umfang);
        var fehlend = version.Vertragsangaben.Fehlend(vorschau.Eingaben);
        var grund = version.FreigabeVertriebsleitungVon is null || version.FreigabeSolutionConsultantVon is null
                ? $"Das Angebot {text} wurde ohne Vertriebsfreigabe von Vertriebsleitung und Solution Consultant erzeugt."
            : vorschau.OhneVorlage.Count > 0
                ? $"Für {string.Join(", ", vorschau.OhneVorlage.Select(d => d.Dokument.Code))} gibt es noch keine freigegebene Vorlage (Katalog › Vertragsvorlagen)."
            : fehlend.Count > 0
                ? $"Die Vorlagen verlangen inzwischen Angaben, die im Angebot {text} fehlen: {string.Join(", ", fehlend)}. Bitte in der Kalkulation ergänzen und ein neues Angebot erzeugen."
            : vorschau.Erzeugte.Count == 0
                ? $"Im Testbetrieb (Vertragswerk:Umfang = {string.Join(", ", vorschau.Umfang!)}) bleibt kein Dokument des Vertrags übrig."
            : wandler is KeinPdfWandler kein
                ? kein.Grund
            : null;

        var p = paperlessEinstellungen.Value;
        if (!p.Aktiv)
        {
            return new VertragswerkBereitschaft(text, vorschau, fehlend, grund, darf);
        }

        var rollen = vorschau.Unterschriften;
        var hinweis = rollen.Count == 0
            ? "Die Vorlagen enthalten kein Unterschriftsfeld ({{unterschrift.Rolle}}); Paperless erhält das Dokument ohne Felder."
            : null;
        return new VertragswerkBereitschaft(text, vorschau, fehlend, grund, darf, true, [.. rollen.Where(r => !p.Rolle(r).Fest)], hinweis);
    }

    /// <summary>
    /// Vorlage eines Dokuments: Rahmendokumente über ihren Code, Leistungsscheine über die Zuordnung am Service
    /// (z. B. S01-STD → S01), sonst über den gleichlautenden Code.
    /// </summary>
    private static DokumentVorlage? VorlageFuer(Vertragsdokument dokument, IReadOnlyDictionary<string, Service> services, IReadOnlyList<DokumentVorlage> vorlagen)
    {
        var code = dokument.Art switch
        {
            VertragsdokumentArt.Grundvertrag => "GRUNDVERTRAG",
            VertragsdokumentArt.Avv or VertragsdokumentArt.Avb or VertragsdokumentArt.Sla => dokument.Code.ToUpperInvariant(),
            _ => services.TryGetValue(dokument.Code, out var s) && s.Leistungsschein is { } schein ? schein.Code : dokument.Code,
        };
        return vorlagen.FirstOrDefault(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
