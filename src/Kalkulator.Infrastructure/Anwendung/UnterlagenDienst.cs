using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Eine hochgeladene Unterlage ohne Inhalt, für die Liste.</summary>
public sealed record UnterlagenZeile(int Id, UnterlagenArt Art, string Dateiname, long Groesse, string? Beschreibung, string HochgeladenVon, DateTimeOffset HochgeladenAm);

/// <summary>
/// Unterlagen eines Kundenprojekts für die Projektansicht. <see cref="LoeschungAm"/> nennt bei abgeschlossenen Projekten
/// den Tag, an dem die Uploads gelöscht werden.
/// </summary>
public sealed record UnterlagenUebersicht(
    string? Kanalordner,
    bool AblageEingerichtet,
    string? AblageGrund,
    IReadOnlyList<UnterlagenZeile> Uploads,
    DateOnly? LoeschungAm,
    bool DarfPflegen);

/// <summary>Kanalordner zur Auswahl, mit Vorschlag für die Firma des Projekts.</summary>
public sealed record KanalAuswahl(IReadOnlyList<Kanalordner> Kanaele, Kanalordner? Vorschlag);

/// <summary>
/// Unterlagen am Kundenprojekt (Phase 4, G-02): der verknüpfte Kanalordner im Team „Kundenprojekte“ (lesend) und
/// ergänzende Uploads in der Datenbank. Pflegen dürfen der verantwortliche Vertrieb, die Vertriebsleitung und die
/// Consultants; sehen darf, wer das Projekt sieht.
/// </summary>
public sealed class UnterlagenDienst(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    TimeProvider zeit,
    IKundenablage ablage,
    IOptions<KundenablageEinstellungen> einstellungen)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    public async Task<UnterlagenUebersicht> UebersichtAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await LadeAsync(kontext, projektId, abbruch);
        var uploads = await kontext.Set<Unterlage>().AsNoTracking().Where(u => u.KundenprojektId == projektId)
            .OrderByDescending(u => u.HochgeladenAm)
            .Select(u => new UnterlagenZeile(u.Id, u.Art, u.Dateiname, u.Groesse, u.Beschreibung, u.HochgeladenVon, u.HochgeladenAm))
            .ToListAsync(abbruch);
        return new UnterlagenUebersicht(projekt.Kanalordner, ablage.Eingerichtet, ablage.Grund, uploads, LoeschungAm(projekt), _recht.DarfUnterlagenPflegen(projekt));
    }

    /// <summary>Tag, an dem die Uploads eines abgeschlossenen Projekts gelöscht werden.</summary>
    public DateOnly? LoeschungAm(Kundenprojekt projekt) =>
        projekt.AbgeschlossenAm is { } am
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(am, Zeitzone).DateTime).AddYears(einstellungen.Value.AufbewahrungJahre)
            : null;

    public async Task<KanalAuswahl> KanaeleAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await LadeAsync(kontext, projektId, abbruch);
        PruefePflegen(projekt);
        var kanaele = await AblageAsync(() => ablage.KanaeleAsync(abbruch));
        return new KanalAuswahl(kanaele, Kundenablage.Vorschlag(projekt.Kunde!.Firma, kanaele));
    }

    /// <summary>Verknüpft das Projekt mit einem Kanalordner; <c>null</c> hebt die Verknüpfung auf.</summary>
    public async Task VerknuepfenAsync(int projektId, string? kanalId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kundenprojekte.Include(p => p.Kunde).SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        PruefeSehen(projekt);
        PruefePflegen(projekt);
        if (kanalId is null)
        {
            projekt.VerknuepfeKanalordner(null, null);
        }
        else
        {
            var kanal = (await AblageAsync(() => ablage.KanaeleAsync(abbruch))).FirstOrDefault(k => k.Id == kanalId)
                ?? throw new KeyNotFoundException("Diesen Kanalordner gibt es im Team „Kundenprojekte“ nicht.");
            projekt.VerknuepfeKanalordner(kanal.Id, kanal.Name);
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Dateien im verknüpften Kanalordner; leer, wenn keiner verknüpft ist.</summary>
    public async Task<IReadOnlyList<AblageDatei>> AblageDateienAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await LadeAsync(kontext, projektId, abbruch);
        return projekt.KanalordnerId is { } kanal ? await AblageAsync(() => ablage.DateienAsync(kanal, abbruch)) : [];
    }

    /// <summary>Eine Datei aus dem verknüpften Kanalordner, z. B. zum Öffnen ohne SharePoint-Link.</summary>
    public async Task<(string Name, byte[] Inhalt)> AblageDateiAsync(int projektId, string dateiId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await LadeAsync(kontext, projektId, abbruch);
        var kanal = projekt.KanalordnerId ?? throw new KeyNotFoundException("Das Kundenprojekt ist mit keinem Kanalordner verknüpft.");
        return await AblageAsync(() => ablage.LadeAsync(kanal, dateiId, abbruch));
    }

    public async Task<int> HochladenAsync(int projektId, UnterlagenArt art, string dateiname, byte[] inhalt, string? beschreibung, CancellationToken abbruch = default)
    {
        Unterlage.Pruefe(dateiname, inhalt.LongLength);
        if (!Enum.IsDefined(art))
        {
            throw new ArgumentException("Bitte die Art der Unterlage wählen.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await LadeAsync(kontext, projektId, abbruch);
        PruefePflegen(projekt);
        // Nur der Dateiname, auch wenn ein Browser den Windows-Pfad mitschickt.
        var name = dateiname.Trim()[(dateiname.Trim().LastIndexOfAny(['/', '\\']) + 1)..];
        var unterlage = new Unterlage
        {
            KundenprojektId = projektId,
            Art = art,
            Dateiname = name,
            Inhaltstyp = Inhaltstyp(name),
            Groesse = inhalt.LongLength,
            Beschreibung = string.IsNullOrWhiteSpace(beschreibung) ? null : beschreibung.Trim()[..Math.Min(beschreibung.Trim().Length, 500)],
            HochgeladenVon = _recht.Name,
            HochgeladenAm = zeit.GetUtcNow(),
            Datei = new UnterlageDatei { Inhalt = inhalt },
        };
        kontext.Add(unterlage);
        await kontext.SaveChangesAsync(abbruch);
        return unterlage.Id;
    }

    public async Task<(string Name, string Inhaltstyp, byte[] Inhalt)> DateiAsync(int unterlageId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var unterlage = await kontext.Set<Unterlage>().AsNoTracking().Include(u => u.Datei).SingleOrDefaultAsync(u => u.Id == unterlageId, abbruch)
            ?? throw new KeyNotFoundException($"Unterlage {unterlageId} gibt es nicht.");
        await LadeAsync(kontext, unterlage.KundenprojektId, abbruch);
        return (unterlage.Dateiname, unterlage.Inhaltstyp, unterlage.Datei!.Inhalt);
    }

    public async Task LoeschenAsync(int unterlageId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var unterlage = await kontext.Set<Unterlage>().Include(u => u.Datei).SingleOrDefaultAsync(u => u.Id == unterlageId, abbruch)
            ?? throw new KeyNotFoundException($"Unterlage {unterlageId} gibt es nicht.");
        var projekt = await LadeAsync(kontext, unterlage.KundenprojektId, abbruch);
        PruefePflegen(projekt);
        kontext.Remove(unterlage);
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// Löscht die Uploads aller Projekte, die vor mehr als <see cref="KundenablageEinstellungen.AufbewahrungJahre"/>
    /// Jahren abgeschlossen wurden (F-13). Angebote und Vertragswerke bleiben. Gibt die Zahl der gelöschten Uploads zurück.
    /// </summary>
    internal async Task<int> AbgelaufeneLoeschenAsync(CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);
        var abgeschlossen = await kontext.Kundenprojekte.AsNoTracking().Include(p => p.StatusEreignisse)
            .Where(p => p.Status == ProjektStatus.Gewonnen || p.Status == ProjektStatus.Verloren)
            .Where(p => kontext.Set<Unterlage>().Any(u => u.KundenprojektId == p.Id))
            .ToListAsync(abbruch);
        var faellig = abgeschlossen.Where(p => LoeschungAm(p) is { } am && am <= heute).Select(p => p.Id).ToList();
        if (faellig.Count == 0)
        {
            return 0;
        }

        var uploads = await kontext.Set<Unterlage>().Include(u => u.Datei).Where(u => faellig.Contains(u.KundenprojektId)).ToListAsync(abbruch);
        kontext.RemoveRange(uploads);
        await kontext.SaveChangesAsync(abbruch);
        return uploads.Count;
    }

    private async Task<Kundenprojekt> LadeAsync(KalkulatorDbContext kontext, int projektId, CancellationToken abbruch)
    {
        var projekt = await kontext.Kundenprojekte.AsNoTracking().Include(p => p.Kunde).Include(p => p.StatusEreignisse)
            .SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        PruefeSehen(projekt);
        return projekt;
    }

    private void PruefeSehen(Kundenprojekt projekt)
    {
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }
    }

    private void PruefePflegen(Kundenprojekt projekt)
    {
        if (!_recht.DarfUnterlagenPflegen(projekt))
        {
            throw new KeinZugriffException("Unterlagen pflegen der verantwortliche Vertrieb, die Vertriebsleitung und die Consultants.");
        }
    }

    /// <summary>Fehler der Ablage (Graph, Ordner) als verständliche Meldung.</summary>
    private static async Task<T> AblageAsync<T>(Func<Task<T>> aufruf)
    {
        try
        {
            return await aufruf();
        }
        catch (Exception e) when (e is HttpRequestException or Azure.Identity.AuthenticationFailedException or DirectoryNotFoundException or Vorlagen.GraphFehler)
        {
            throw new InvalidOperationException($"Die Kundenablage in Teams ist nicht erreichbar: {e.Message}");
        }
    }

    private static string Inhaltstyp(string dateiname) => Path.GetExtension(dateiname).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" or ".md" or ".vtt" or ".csv" => "text/plain; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".eml" => "message/rfc822",
        _ => "application/octet-stream",
    };
}
