using Kalkulator.Infrastructure.Paperless;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Ergebnis einer Abfrage: geprüfte Dokumente, neu unterzeichnete, abgelegte PDFs und Fehler.</summary>
public sealed record UnterschriftenErgebnis(int Geprueft, int Unterzeichnet, int Abgelegt, int Fehler);

/// <summary>
/// Fragt bei Paperless den Status übergebener Verträge ab (Entscheidung 16.10.2026: stündlich statt Webhook). Ist ein
/// Vertrag vollständig unterschrieben, wird das vermerkt und das unterschriebene PDF samt Prüfprotokoll abgelegt.
/// Abgefragt wird, bis das PDF abgelegt ist; ein Fehler bei einem Vertrag hält die übrigen nicht auf.
/// </summary>
public sealed class UnterschriftenAbfrage(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    IPaperlessAbruf paperless,
    IOptions<PaperlessEinstellungen> einstellungen,
    TimeProvider zeit,
    ILogger<UnterschriftenAbfrage> protokoll)
{
    private readonly Berechtigung _recht = new(benutzer);

    /// <summary>Alle übergebenen Verträge, deren unterschriebenes PDF noch fehlt (Hintergrunddienst).</summary>
    public Task<UnterschriftenErgebnis> AusfuehrenAsync(CancellationToken abbruch = default) => AbfragenAsync(null, abbruch);

    /// <summary>Einen Vertrag sofort abfragen, z. B. aus der Projektansicht; wer das Kundenprojekt bearbeiten darf.</summary>
    public async Task<UnterschriftenErgebnis> PruefeAsync(int vertragswerkId, CancellationToken abbruch = default)
    {
        await using (var kontext = await kontexte.CreateDbContextAsync(abbruch))
        {
            var projektId = await kontext.Vertragswerke.Where(v => v.Id == vertragswerkId).Select(v => (int?)v.KundenprojektId).SingleOrDefaultAsync(abbruch)
                ?? throw new KeyNotFoundException($"Vertragswerk {vertragswerkId} gibt es nicht.");
            var projekt = await kontext.Kundenprojekte.AsNoTracking().SingleAsync(p => p.Id == projektId, abbruch);
            if (!_recht.DarfBearbeiten(projekt))
            {
                throw new KeinZugriffException("Den Status in Paperless fragt der Vertrieb ab, der das Kundenprojekt verantwortet, oder die Vertriebsleitung.");
            }
        }

        return await AbfragenAsync(vertragswerkId, abbruch);
    }

    private async Task<UnterschriftenErgebnis> AbfragenAsync(int? nur, CancellationToken abbruch)
    {
        if (!einstellungen.Value.Aktiv)
        {
            throw new InvalidOperationException("Paperless ist nicht eingerichtet (API-Schlüssel Paperless__ApiSchluessel fehlt).");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var offen = await kontext.Vertragswerke
            .Where(v => v.PaperlessDokumentId != null && v.Unterschrift == null && (nur == null || v.Id == nur))
            .OrderBy(v => v.Id)
            .ToListAsync(abbruch);

        int unterzeichnet = 0, abgelegt = 0, fehler = 0;
        foreach (var werk in offen)
        {
            try
            {
                var war = werk.IstUnterzeichnet;
                if (!war)
                {
                    var stand = await paperless.StandAsync(werk.PaperlessDokumentId!, abbruch);
                    werk.VermerkeStatus(stand.Status, stand.Unterzeichnet, zeit.GetUtcNow());
                    unterzeichnet += werk.IstUnterzeichnet ? 1 : 0;
                }

                if (werk.IstUnterzeichnet && await paperless.ErgebnisAsync(werk.PaperlessDokumentId!, abbruch) is { } ergebnis)
                {
                    werk.HinterlegeUnterschrift(ergebnis.Pdf, ergebnis.Pruefprotokoll, zeit.GetUtcNow());
                    abgelegt++;
                }

                await kontext.SaveChangesAsync(abbruch);
            }
            catch (Exception e) when (e is PaperlessFehler or HttpRequestException or TaskCanceledException or ArgumentException && !abbruch.IsCancellationRequested)
            {
                fehler++;
                protokoll.LogWarning("Paperless-Abfrage für Vertragswerk {Id} (Dokument {Dokument}) fehlgeschlagen: {Fehler}", werk.Id, werk.PaperlessDokumentId, e.Message);
                kontext.ChangeTracker.Clear();
            }
        }

        return new UnterschriftenErgebnis(offen.Count, unterzeichnet, abgelegt, fehler);
    }
}
