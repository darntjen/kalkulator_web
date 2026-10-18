using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Ergebnis eines Archivlaufs: betrachtete Projekte, archivierte Angebote und Vertragswerke, Fehler.</summary>
public sealed record ArchivErgebnis(int Projekte, int Angebote, int Vertragswerke, int Fehler);

/// <summary>
/// Archiviert die Dokumente abgeschlossener Kundenprojekte im Kanalordner (Entscheidung 08.10.2026, 15.2): Im
/// Kalkulator bleiben nur das angenommene Angebot und der unterschriebene Vertrag, alles Übrige geht nach
/// <c>60_Angebote/Archiv Kalkulator</c> bzw. <c>70_Vertraege/Archiv Kalkulator</c> und wird nur noch verlinkt.
/// <list type="bullet">
/// <item>„Gewonnen“: sobald der unterschriebene Vertrag im Kanalordner liegt; archiviert werden die übrigen Angebote
/// und die nicht unterzeichneten Ausfertigungen des Vertragswerks.</item>
/// <item>„Verloren“: alle Angebote und Ausfertigungen, ohne Wartefrist.</item>
/// </list>
/// Projekte ohne verknüpften Kanalordner bleiben, wie sie sind. Die Datei in der Datenbank wird erst gelöscht, wenn der
/// Upload bestätigt ist; jedes Dokument wird einzeln gespeichert, ein Fehler hält die übrigen nicht auf.
/// </summary>
public sealed class Archivierung(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IKundenablage ablage,
    TimeProvider zeit,
    ILogger<Archivierung> protokoll)
{
    public const string AngebotsOrdner = "60_Angebote/" + Kundenablage.Archivordner;
    public const string VertragsOrdner = "70_Vertraege/" + Kundenablage.Archivordner;

    public async Task<ArchivErgebnis> AusfuehrenAsync(CancellationToken abbruch = default)
    {
        if (!ablage.Eingerichtet)
        {
            throw new InvalidOperationException($"Die Kundenablage ist nicht eingerichtet: {ablage.Grund}");
        }

        List<(int Id, string KanalId, bool Verloren, int? Angenommen)> projekte;
        await using (var kontext = await kontexte.CreateDbContextAsync(abbruch))
        {
            projekte = [.. (await kontext.Kundenprojekte.AsNoTracking()
                    .Where(p => p.KanalordnerId != null
                        && (p.Status == ProjektStatus.Verloren
                            || (p.Status == ProjektStatus.Gewonnen
                                && kontext.Vertragswerke.Any(v => v.KundenprojektId == p.Id && v.UnterzeichnetAm != null && v.AbgelegtAm != null))))
                    .OrderBy(p => p.Id)
                    .Select(p => new { p.Id, p.KanalordnerId, p.Status, p.AngenommenesAngebotId })
                    .ToListAsync(abbruch))
                .Select(p => (p.Id, p.KanalordnerId!, p.Status == ProjektStatus.Verloren, p.AngenommenesAngebotId))];
        }

        int angebote = 0, werke = 0, fehler = 0;
        foreach (var projekt in projekte)
        {
            List<int> angebotIds, werkIds;
            await using (var kontext = await kontexte.CreateDbContextAsync(abbruch))
            {
                var kalkulationen = kontext.Kalkulationen.Where(k => k.KundenprojektId == projekt.Id).Select(k => k.Id);
                angebotIds = await kontext.Angebote
                    .Where(a => a.ArchiviertAm == null && kalkulationen.Contains(a.Version!.KalkulationId)
                        && (projekt.Verloren || a.Id != projekt.Angenommen))
                    .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(abbruch);
                werkIds = await kontext.Vertragswerke
                    .Where(v => v.KundenprojektId == projekt.Id && v.ArchiviertAm == null && v.UnterzeichnetAm == null)
                    .OrderBy(v => v.Id).Select(v => v.Id).ToListAsync(abbruch);
            }

            foreach (var id in angebotIds)
            {
                if (await VersucheAsync("Angebot", id, () => AngebotArchivierenAsync(id, projekt.KanalId, abbruch), abbruch))
                {
                    angebote++;
                }
                else
                {
                    fehler++;
                }
            }

            foreach (var id in werkIds)
            {
                if (await VersucheAsync("Vertragswerk", id, () => WerkArchivierenAsync(id, projekt.KanalId, abbruch), abbruch))
                {
                    werke++;
                }
                else
                {
                    fehler++;
                }
            }
        }

        return new ArchivErgebnis(projekte.Count, angebote, werke, fehler);
    }

    private async Task AngebotArchivierenAsync(int id, string kanalId, CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var angebot = await kontext.Angebote.Include(a => a.Datei).SingleAsync(a => a.Id == id, abbruch);
        var datei = angebot.Datei ?? throw new InvalidOperationException("Die Angebotsdatei fehlt.");
        var abgelegt = await ablage.AblegenAsync(kanalId, AngebotsOrdner, angebot.Dateiname, datei.Inhalt, abbruch);
        angebot.Archiviere(Link(abgelegt), zeit.GetUtcNow());
        kontext.Remove(datei);
        await kontext.SaveChangesAsync(abbruch);
    }

    private async Task WerkArchivierenAsync(int id, string kanalId, CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var werk = await kontext.Vertragswerke.Include(v => v.Datei).SingleAsync(v => v.Id == id, abbruch);
        var datei = werk.Datei ?? throw new InvalidOperationException("Gesamt-PDF und ZIP fehlen.");
        var pdf = await ablage.AblegenAsync(kanalId, VertragsOrdner, werk.GesamtDateiname, datei.GesamtPdf, abbruch);
        var zip = await ablage.AblegenAsync(kanalId, VertragsOrdner, werk.ZipDateiname, datei.Zip, abbruch);
        werk.Archiviere(Link(pdf), Link(zip), zeit.GetUtcNow());
        kontext.Remove(datei);
        await kontext.SaveChangesAsync(abbruch);
    }

    private static string Link(AblageDatei datei) =>
        string.IsNullOrWhiteSpace(datei.Link) ? throw new InvalidOperationException($"Für {datei.Name} liefert die Ablage keinen Link.") : datei.Link;

    private async Task<bool> VersucheAsync(string art, int id, Func<Task> schritt, CancellationToken abbruch)
    {
        try
        {
            await schritt();
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or DbUpdateException
            && !abbruch.IsCancellationRequested)
        {
            protokoll.LogWarning("Archivierung: {Art} {Id} nicht archiviert: {Fehler}", art, id, e.Message);
            return false;
        }
    }
}
