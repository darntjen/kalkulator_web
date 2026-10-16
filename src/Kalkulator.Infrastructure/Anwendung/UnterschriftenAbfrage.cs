using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Benachrichtigung;
using Kalkulator.Infrastructure.Paperless;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>
/// Ergebnis einer Abfrage: geprüfte Verträge, neu unterzeichnete, abgelegte PDFs, Fehler sowie Ablagen im Kanalordner
/// und Benachrichtigungen.
/// </summary>
public sealed record UnterschriftenErgebnis(int Geprueft, int Unterzeichnet, int Abgelegt, int Fehler, int Hochgeladen = 0, int Gemeldet = 0);

/// <summary>
/// Fragt bei Paperless den Status übergebener Verträge ab (Entscheidung 16.10.2026: stündlich statt Webhook). Ist ein
/// Vertrag vollständig unterschrieben, wird das vermerkt und das unterschriebene PDF samt Prüfprotokoll abgelegt.
/// Danach wird es verteilt: in den Kanalordner (70_Vertraege) und als Benachrichtigung an die Liste, aus der Power
/// Automate die E-Mails schickt (15.3, 15.6). Jeder Schritt geschieht einmal und wird bei Fehlern im nächsten Takt
/// wiederholt; ein Fehler bei einem Vertrag hält die übrigen nicht auf.
/// </summary>
public sealed class UnterschriftenAbfrage(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    IPaperlessAbruf paperless,
    IOptions<PaperlessEinstellungen> einstellungen,
    IKundenablage ablage,
    IBenachrichtigung benachrichtigung,
    IOptions<BenachrichtigungsEinstellungen> nachrichten,
    TimeProvider zeit,
    ILogger<UnterschriftenAbfrage> protokoll)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

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
        var ablageAn = ablage.Eingerichtet;
        var meldenAn = benachrichtigung.Eingerichtet;
        var offen = await kontext.Vertragswerke.Include(v => v.Unterschrift)
            .Join(kontext.Kundenprojekte, v => v.KundenprojektId, p => p.Id, (v, p) => new { Werk = v, p.KanalordnerId })
            .Where(x => x.Werk.PaperlessDokumentId != null && (nur == null || x.Werk.Id == nur)
                && (x.Werk.Unterschrift == null
                    || (ablageAn && x.Werk.AbgelegtAm == null && x.KanalordnerId != null)
                    || (meldenAn && x.Werk.GemeldetAm == null)))
            .OrderBy(x => x.Werk.Id)
            .Select(x => x.Werk)
            .ToListAsync(abbruch);

        int unterzeichnet = 0, abgelegt = 0, fehler = 0, hochgeladen = 0, gemeldet = 0;
        foreach (var werk in offen)
        {
            try
            {
                if (!werk.IstUnterzeichnet)
                {
                    var stand = await paperless.StandAsync(werk.PaperlessDokumentId!, abbruch);
                    werk.VermerkeStatus(stand.Status, stand.Unterzeichnet, zeit.GetUtcNow());
                    unterzeichnet += werk.IstUnterzeichnet ? 1 : 0;
                }

                if (werk.IstUnterzeichnet && werk.Unterschrift is null && await paperless.ErgebnisAsync(werk.PaperlessDokumentId!, abbruch) is { } ergebnis)
                {
                    werk.HinterlegeUnterschrift(ergebnis.Pdf, ergebnis.Pruefprotokoll, zeit.GetUtcNow());
                    abgelegt++;
                }

                await kontext.SaveChangesAsync(abbruch);

                if (werk.Unterschrift is not null)
                {
                    var (h, g) = await VerteilenAsync(kontext, werk, ablageAn, meldenAn, abbruch);
                    hochgeladen += h;
                    gemeldet += g;
                }
            }
            catch (Exception e) when (e is PaperlessFehler or HttpRequestException or TaskCanceledException or ArgumentException or InvalidOperationException
                && !abbruch.IsCancellationRequested)
            {
                fehler++;
                protokoll.LogWarning("Paperless-Abfrage für Vertragswerk {Id} (Dokument {Dokument}) fehlgeschlagen: {Fehler}", werk.Id, werk.PaperlessDokumentId, e.Message);
                kontext.ChangeTracker.Clear();
            }
        }

        return new UnterschriftenErgebnis(offen.Count, unterzeichnet, abgelegt, fehler, hochgeladen, gemeldet);
    }

    /// <summary>
    /// Legt den unterschriebenen Vertrag (und das Prüfprotokoll) in 70_Vertraege des Kanalordners ab und meldet ihn.
    /// Ohne verknüpften Kanalordner geht die Meldung mit einem Hinweis hinaus; sonst erst nach der Ablage.
    /// </summary>
    private async Task<(int Hochgeladen, int Gemeldet)> VerteilenAsync(KalkulatorDbContext kontext, Vertragswerk werk, bool ablageAn, bool meldenAn, CancellationToken abbruch)
    {
        var projekt = await kontext.Kundenprojekte.AsNoTracking().Include(p => p.Kunde).SingleAsync(p => p.Id == werk.KundenprojektId, abbruch);
        var hochgeladen = 0;
        if (ablageAn && werk.AbgelegtAm is null && projekt.KanalordnerId is { } kanal)
        {
            var name = Path.GetFileNameWithoutExtension(werk.GesamtDateiname);
            var datei = await ablage.AblegenAsync(kanal, "70_Vertraege", $"{name}_unterschrieben.pdf", werk.Unterschrift!.Pdf, abbruch);
            if (werk.Unterschrift.Pruefprotokoll is { } pruefprotokoll)
            {
                await ablage.AblegenAsync(kanal, "70_Vertraege", $"{name}_Pruefprotokoll.pdf", pruefprotokoll, abbruch);
            }

            werk.VermerkeAblage(datei.Link, zeit.GetUtcNow());
            await kontext.SaveChangesAsync(abbruch);
            hochgeladen = 1;
        }

        var ohneAblage = !ablageAn || projekt.KanalordnerId is null;
        if (!meldenAn || werk.GemeldetAm is not null || (werk.AbgelegtAm is null && !ohneAblage))
        {
            return (hochgeladen, 0);
        }

        await benachrichtigung.MeldenAsync(Nachricht(werk, projekt, ohneAblage), abbruch);
        werk.VermerkeMeldung(zeit.GetUtcNow());
        await kontext.SaveChangesAsync(abbruch);
        return (hochgeladen, 1);
    }

    private Nachricht Nachricht(Vertragswerk werk, Kundenprojekt projekt, bool ohneAblage)
    {
        var e = nachrichten.Value;
        var firma = projekt.Kunde?.Firma ?? "";
        var am = TimeZoneInfo.ConvertTime(werk.UnterzeichnetAm!.Value, Zeitzone).ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        var empfaenger = e.EmpfaengerVertrag.Append(projekt.Verantwortlich)
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var link = string.IsNullOrWhiteSpace(e.KalkulatorAdresse) ? null : $"{e.KalkulatorAdresse.TrimEnd('/')}/projekte/{projekt.Id}";
        var text = $"Der Vertrag {werk.Nummer} mit {firma} ist vollständig unterschrieben (festgestellt am {am}). "
            + $"Projekt: {projekt.Titel}. Verantwortlich: {projekt.Verantwortlich}."
            + (ohneAblage ? " Der Vertrag liegt nur im Kalkulator, weil das Projekt mit keinem Kanalordner verknüpft ist." : "");
        return new Nachricht("Vertrag unterschrieben", $"Vertrag unterschrieben: {firma} ({werk.Nummer})", text, empfaenger, werk.AblageLink, link);
    }
}
