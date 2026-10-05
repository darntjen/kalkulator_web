using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Kundensituation eines Projekts für die Projektansicht; <see cref="Verknuepft"/> nennt je Herausforderung die Services, die sie lösen.</summary>
public sealed record Kundensituation(
    IReadOnlyList<Herausforderung> Herausforderungen,
    IReadOnlyDictionary<int, IReadOnlyList<string>> Verknuepft,
    bool DarfPflegen);

/// <summary>Analyse- und Workshop-Angebote eines Projekts mit den Uploads, die als Dokument wählbar sind.</summary>
public sealed record AnalyseAngebote(IReadOnlyList<AnalyseAngebot> Angebote, IReadOnlyList<UnterlagenZeile> Dokumente, bool DarfPflegen);

/// <summary>
/// Kundensituation (G-03) und Analyse-/Workshop-Angebote (J-01) eines Kundenprojekts. Herausforderungen pflegen der
/// verantwortliche Vertrieb, die Vertriebsleitung und die Consultants; Analyse-Angebote erfasst, wer das Projekt
/// bearbeitet (Vertrieb, Vertriebsleitung).
/// </summary>
public sealed class KundensituationDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer, TimeProvider zeit)
{
    private readonly Berechtigung _recht = new(benutzer);

    public async Task<Kundensituation> LadeAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await ProjektAsync(kontext, projektId, abbruch);
        var liste = await kontext.Set<Herausforderung>().AsNoTracking().Where(h => h.KundenprojektId == projektId)
            .OrderBy(h => h.Dimension).ThenBy(h => h.Prioritaet).ThenBy(h => h.Id).ToListAsync(abbruch);
        return new Kundensituation(liste, await VerknuepftAsync(kontext, projektId, abbruch), _recht.DarfUnterlagenPflegen(projekt));
    }

    public async Task<int> AnlegenAsync(int projektId, HerausforderungsDaten daten, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        PruefePflegen(await ProjektAsync(kontext, projektId, abbruch));
        var h = Herausforderung.Neu(projektId, daten, _recht.Name, zeit.GetUtcNow());
        kontext.Add(h);
        await kontext.SaveChangesAsync(abbruch);
        return h.Id;
    }

    public async Task AendernAsync(int id, HerausforderungsDaten daten, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var h = await kontext.Set<Herausforderung>().SingleOrDefaultAsync(x => x.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Herausforderung {id} gibt es nicht.");
        PruefePflegen(await ProjektAsync(kontext, h.KundenprojektId, abbruch));
        h.Aendern(daten, _recht.Name, zeit.GetUtcNow());
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Löscht eine Herausforderung, die mit keinem Service mehr verknüpft ist.</summary>
    public async Task LoeschenAsync(int id, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var h = await kontext.Set<Herausforderung>().SingleOrDefaultAsync(x => x.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Herausforderung {id} gibt es nicht.");
        PruefePflegen(await ProjektAsync(kontext, h.KundenprojektId, abbruch));
        if ((await VerknuepftAsync(kontext, h.KundenprojektId, abbruch)).TryGetValue(id, out var services))
        {
            throw new InvalidOperationException($"„{h.Titel}“ ist noch mit {string.Join(", ", services)} verknüpft. Bitte die Verknüpfung erst in der Kalkulation lösen.");
        }

        kontext.Remove(h);
        await kontext.SaveChangesAsync(abbruch);
    }

    public async Task<AnalyseAngebote> AnalyseAngeboteAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await ProjektAsync(kontext, projektId, abbruch);
        var angebote = await kontext.Set<AnalyseAngebot>().AsNoTracking().Where(a => a.KundenprojektId == projektId)
            .OrderByDescending(a => a.Datum).ThenByDescending(a => a.Id).ToListAsync(abbruch);
        var dokumente = await kontext.Set<Unterlage>().AsNoTracking().Where(u => u.KundenprojektId == projektId)
            .OrderByDescending(u => u.HochgeladenAm)
            .Select(u => new UnterlagenZeile(u.Id, u.Art, u.Dateiname, u.Groesse, u.Beschreibung, u.HochgeladenVon, u.HochgeladenAm))
            .ToListAsync(abbruch);
        return new AnalyseAngebote(angebote, dokumente, _recht.DarfBearbeiten(projekt));
    }

    public async Task<int> AnalyseAngebotErfassenAsync(int projektId, AnalyseAngebotsDaten daten, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        PruefeBearbeiten(await ProjektAsync(kontext, projektId, abbruch));
        var angebot = AnalyseAngebot.Neu(projektId, daten, _recht.Name, zeit.GetUtcNow());
        await PruefeAsync(kontext, angebot, abbruch);
        kontext.Add(angebot);
        await kontext.SaveChangesAsync(abbruch);
        return angebot.Id;
    }

    public async Task AnalyseAngebotAendernAsync(int id, AnalyseAngebotsDaten daten, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var angebot = await kontext.Set<AnalyseAngebot>().SingleOrDefaultAsync(a => a.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Analyse-Angebot {id} gibt es nicht.");
        PruefeBearbeiten(await ProjektAsync(kontext, angebot.KundenprojektId, abbruch));
        angebot.Aendern(daten, zeit.GetUtcNow());
        await PruefeAsync(kontext, angebot, abbruch);
        await kontext.SaveChangesAsync(abbruch);
    }

    public async Task AnalyseAngebotLoeschenAsync(int id, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var angebot = await kontext.Set<AnalyseAngebot>().SingleOrDefaultAsync(a => a.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Analyse-Angebot {id} gibt es nicht.");
        PruefeBearbeiten(await ProjektAsync(kontext, angebot.KundenprojektId, abbruch));
        kontext.Remove(angebot);
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Die Nummer ist eindeutig, das Dokument gehört zum selben Projekt.</summary>
    private static async Task PruefeAsync(KalkulatorDbContext kontext, AnalyseAngebot angebot, CancellationToken abbruch)
    {
        if (await kontext.Set<AnalyseAngebot>().AnyAsync(a => a.Nummer == angebot.Nummer && a.Id != angebot.Id, abbruch))
        {
            throw new InvalidOperationException($"Das Angebot {angebot.Nummer} ist bereits erfasst.");
        }

        if (angebot.UnterlageId is { } unterlage
            && !await kontext.Set<Unterlage>().AnyAsync(u => u.Id == unterlage && u.KundenprojektId == angebot.KundenprojektId, abbruch))
        {
            throw new ArgumentException("Das gewählte Dokument gehört nicht zu diesem Kundenprojekt.");
        }
    }

    /// <summary>Je Herausforderung die Services aller Kalkulationen des Projekts, mit denen sie verknüpft ist.</summary>
    private static async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> VerknuepftAsync(KalkulatorDbContext kontext, int projektId, CancellationToken abbruch)
    {
        var kalkulationen = await kontext.Kalkulationen.AsNoTracking().Where(k => k.KundenprojektId == projektId).ToListAsync(abbruch);
        return kalkulationen.SelectMany(k => k.Zuordnungen.Select(z => (z.HerausforderungId, Text: $"{z.ServiceCode} ({k.Titel})")))
            .GroupBy(z => z.HerausforderungId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(z => z.Text).Distinct()]);
    }

    private async Task<Kundenprojekt> ProjektAsync(KalkulatorDbContext kontext, int projektId, CancellationToken abbruch)
    {
        var projekt = await kontext.Kundenprojekte.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }

        return projekt;
    }

    private void PruefePflegen(Kundenprojekt projekt)
    {
        if (!_recht.DarfUnterlagenPflegen(projekt))
        {
            throw new KeinZugriffException("Die Kundensituation pflegen der verantwortliche Vertrieb, die Vertriebsleitung und die Consultants.");
        }
    }

    private void PruefeBearbeiten(Kundenprojekt projekt)
    {
        if (!_recht.DarfBearbeiten(projekt))
        {
            throw new KeinZugriffException("Analyse- und Workshop-Angebote erfassen der verantwortliche Vertrieb und die Vertriebsleitung.");
        }
    }
}
