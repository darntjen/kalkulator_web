using Kalkulator.Documents;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Ein Dokument des Vertragswerks mit der Vorlage und ihrer aktiven Fassung; ohne aktive Fassung ist <see cref="Fassung"/> leer.</summary>
public sealed record VertragswerkDokument(Vertragsdokument Dokument, DokumentVorlage? Vorlage, Vorlagenversion? Fassung);

/// <summary>Welche Dokumente ein Vertragswerk umfasst und welche Angaben ihre Vorlagen verlangen.</summary>
public sealed record Vertragsvorschau(IReadOnlyList<VertragswerkDokument> Dokumente, IReadOnlyList<EingabeDefinition> Eingaben)
{
    public IReadOnlyList<VertragswerkDokument> OhneVorlage => [.. Dokumente.Where(d => d.Fassung is null)];
}

public sealed class VertragswerkEinstellungen
{
    /// <summary>Deckblatt der Gesamtdatei (templates/vertrag/Deckblatt.docx); wird mit dem Programm ausgeliefert.</summary>
    public string Deckblatt { get; set; } = "";
}

/// <summary>Ob und mit welchen Dokumenten das Vertragswerk eines Projekts erzeugt werden kann; <see cref="Sperrgrund"/> sagt, woran es hängt.</summary>
public sealed record VertragswerkBereitschaft(string? Angebot, Vertragsvorschau? Vorschau, IReadOnlyList<string> FehlendeAngaben, string? Sperrgrund, bool DarfErzeugen);

public sealed record VertragswerkZeile(int Id, string Nummer, int Ausfertigung, string ErstelltVon, DateTimeOffset ErstelltAm, IReadOnlyList<VertragswerkEintrag> Dokumente);

/// <summary>
/// Vertragswerk aus dem angenommenen Angebot (#26, Teil C): Dokumente nach Rangfolge, Vertragsangaben, Word-Dateien,
/// PDFs, Gesamt-PDF und ZIP.
/// </summary>
public sealed class VertragswerkDienst(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    TimeProvider zeit,
    IPdfWandler wandler,
    IOptions<VertragswerkEinstellungen> einstellungen)
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

        return await VorschauAsync(kontext, serviceCodes, abbruch);
    }

    /// <summary>Löst die Services in Dokumente auf und ordnet jedem die Vorlage mit ihrer aktiven Fassung zu.</summary>
    internal static async Task<Vertragsvorschau> VorschauAsync(KalkulatorDbContext kontext, IEnumerable<string> serviceCodes, CancellationToken abbruch)
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

        var eingaben = Vertragsangaben.Vereinige(dokumente.Where(d => d.Fassung is not null).SelectMany(d => d.Fassung!.Eingaben));
        return new Vertragsvorschau(dokumente, eingaben);
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
        var werke = await kontext.Vertragswerke.AsNoTracking().Include(v => v.Dokumente)
            .Where(v => v.KundenprojektId == projektId).OrderByDescending(v => v.Ausfertigung).ToListAsync(abbruch);
        return [.. werke.Select(v => new VertragswerkZeile(v.Id, v.Nummer, v.Ausfertigung, v.ErstelltVon, v.ErstelltAm, [.. v.Dokumente.OrderBy(d => d.Reihenfolge)]))];
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
    /// als PDF, dazu Deckblatt mit Verzeichnis, Gesamt-PDF und ZIP. Archiviert als neue Ausfertigung.
    /// </summary>
    public async Task<int> ErzeugenAsync(int projektId, CancellationToken abbruch = default)
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

        var vorschau = bereitschaft.Vorschau!;
        var version = angebot!.Version!;
        var fassungIds = vorschau.Dokumente.Select(d => d.Fassung!.Id).ToList();
        var dateien = await kontext.Set<VorlagenDatei>().AsNoTracking().Where(d => fassungIds.Contains(d.VorlagenversionId))
            .ToDictionaryAsync(d => d.VorlagenversionId, d => d.Inhalt, abbruch);
        var katalog = await kontext.Services.AsNoTracking().Include(s => s.Leistungsschein).ToListAsync(abbruch);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);
        var quelle = new VertragsQuelle(angebot.Nummer, $"{angebot.Nummer} V{version.Nummer}", heute, version, projekt.Kunde!,
            katalog, [.. vorschau.Dokumente.Select(d => d.Dokument)], vorschau.Eingaben);

        var mappe = new List<MappenDokument>();
        foreach (var (eintrag, i) in vorschau.Dokumente.Select((d, i) => (d, i)))
        {
            var fassung = eintrag.Fassung!;
            byte[] docx;
            try
            {
                docx = Vertragsdaten.Erzeuge(dateien[fassung.Id], Vertragsdaten.Fuer(quelle, eintrag.Dokument), fassung.Komponenten);
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
        };
        werk.Dokumente.AddRange(vorschau.Dokumente.Select((d, i) => new VertragswerkEintrag
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
        var vorschau = await VorschauAsync(kontext, version.Positionen.Select(p => p.ServiceCode).OfType<string>(), abbruch);
        var fehlend = version.Vertragsangaben.Fehlend(vorschau.Eingaben);
        var grund = version.FreigabeVertriebsleitungVon is null || version.FreigabeSolutionConsultantVon is null
                ? $"Das Angebot {text} wurde ohne Vertriebsfreigabe von Vertriebsleitung und Solution Consultant erzeugt."
            : vorschau.OhneVorlage.Count > 0
                ? $"Für {string.Join(", ", vorschau.OhneVorlage.Select(d => d.Dokument.Code))} gibt es noch keine freigegebene Vorlage (Katalog › Vertragsvorlagen)."
            : fehlend.Count > 0
                ? $"Die Vorlagen verlangen inzwischen Angaben, die im Angebot {text} fehlen: {string.Join(", ", fehlend)}. Bitte in der Kalkulation ergänzen und ein neues Angebot erzeugen."
            : wandler is KeinPdfWandler kein
                ? kein.Grund
            : null;
        return new VertragswerkBereitschaft(text, vorschau, fehlend, grund, darf);
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
