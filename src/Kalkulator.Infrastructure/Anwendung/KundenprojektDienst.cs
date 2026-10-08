using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Anwendung;

public sealed record ProjektZeile(
    int Id,
    string Titel,
    string Firma,
    ProjektStatus Status,
    string Verantwortlich,
    int? Wahrscheinlichkeit,
    DateOnly? ErwarteterAbschlussmonat,
    int Kalkulationen);

/// <summary>Ein versendetes Angebot zur Auswahl als „angenommen“.</summary>
public sealed record AngebotsAuswahl(int Id, string Nummer, int Version, string Kalkulation, DateOnly? VersendetAm, decimal SummeMonatlich)
{
    public string Text => $"{Nummer} V{Version} · {Kalkulation}";
}

/// <summary>Ein Angebot des Kundenprojekts mit abgeleitetem Status (Übersicht im Projekt, Filter).</summary>
public sealed record ProjektAngebot(
    int Id,
    string Nummer,
    int Version,
    int KalkulationId,
    string Kalkulation,
    DateOnly Datum,
    DateOnly GueltigBis,
    DateOnly? VersendetAm,
    string ErstelltVon,
    decimal SummeMonatlich,
    AngebotsStatus Status);

public sealed record NeuesKundenprojekt(
    string Firma,
    string? Strasse,
    string? Postleitzahl,
    string? Ort,
    string? Ansprechpartner,
    string? NavisionKundennummer,
    string Titel,
    bool Bestandskunde,
    int? Wahrscheinlichkeit,
    DateOnly? ErwarteterAbschlussmonat);

/// <summary>
/// Anwendungsfälle rund um Kundenprojekte, Projektstatus und Varianten. Jede Methode arbeitet mit einem eigenen,
/// kurzlebigen Kontext, damit lange Blazor-Verbindungen keinen veralteten Stand festhalten.
/// </summary>
public sealed class KundenprojektDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer, TimeProvider zeit)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    public async Task<IReadOnlyList<ProjektZeile>> ListeAsync(string? suche = null, CancellationToken abbruch = default)
    {
        if (!_recht.DarfProjekteSehen)
        {
            throw new KeinZugriffException("Für Kundenprojekte fehlt die Berechtigung.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var abfrage = kontext.Kundenprojekte.AsNoTracking();
        if (!_recht.SiehtAlleProjekte)
        {
            abfrage = abfrage.Where(p => p.Verantwortlich == _recht.Name);
        }

        if (!string.IsNullOrWhiteSpace(suche))
        {
            var text = suche.Trim();
            abfrage = abfrage.Where(p => p.Titel.Contains(text) || p.Kunde!.Firma.Contains(text)
                || (p.Kunde.NavisionKundennummer != null && p.Kunde.NavisionKundennummer.Contains(text)));
        }

        return await abfrage
            .OrderByDescending(p => p.AngelegtAm)
            .Select(p => new ProjektZeile(p.Id, p.Titel, p.Kunde!.Firma, p.Status, p.Verantwortlich, p.Wahrscheinlichkeit,
                p.ErwarteterAbschlussmonat, p.Kalkulationen.Count))
            .ToListAsync(abbruch);
    }

    /// <summary>Projekt mit Kunde, Statusverlauf und Kalkulationen (ohne Kosten); <c>null</c>, wenn es nicht existiert.</summary>
    public async Task<Kundenprojekt?> LadeAsync(int id, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kundenprojekte.AsNoTracking()
            .Include(p => p.Kunde)
            .Include(p => p.StatusEreignisse)
            .Include(p => p.Kalkulationen).ThenInclude(k => k.Versionen)
            .Include(p => p.Kalkulationen).ThenInclude(k => k.Sonderpositionen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, abbruch);

        if (projekt is not null && !_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }

        return projekt;
    }

    /// <summary>
    /// Legt Kunde und Kundenprojekt an. Ein Kunde mit derselben Navision-Kundennummer wird wiederverwendet.
    /// Verantwortlich ist der angemeldete Benutzer.
    /// </summary>
    public async Task<int> AnlegenAsync(NeuesKundenprojekt daten, CancellationToken abbruch = default)
    {
        if (!_recht.DarfKalkulieren)
        {
            throw new KeinZugriffException("Kundenprojekte legen Vertrieb und Vertriebsleitung an.");
        }

        Pflicht(daten.Firma, "Firma");
        Pflicht(daten.Titel, "Titel");

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var nummer = Leer(daten.NavisionKundennummer);
        var kunde = nummer is null ? null : await kontext.Kunden.SingleOrDefaultAsync(k => k.NavisionKundennummer == nummer, abbruch);
        kunde ??= new Kunde
        {
            Firma = daten.Firma.Trim(),
            Strasse = Leer(daten.Strasse),
            Postleitzahl = Leer(daten.Postleitzahl),
            Ort = Leer(daten.Ort),
            Ansprechpartner = Leer(daten.Ansprechpartner),
            NavisionKundennummer = nummer,
        };

        var projekt = Kundenprojekt.Anlegen(kunde, daten.Titel.Trim(), _recht.Name, daten.Bestandskunde, _recht.Name, zeit.GetUtcNow());
        projekt.SetzeForecast(daten.Wahrscheinlichkeit, daten.ErwarteterAbschlussmonat);
        kontext.Kundenprojekte.Add(projekt);
        await kontext.SaveChangesAsync(abbruch);
        return projekt.Id;
    }

    /// <summary>
    /// Setzt den Projektstatus. Bei „Gewonnen“ ist das angenommene Angebot Pflicht: ein als versendet markiertes
    /// Angebot einer Kalkulation dieses Projekts (#26).
    /// </summary>
    public async Task SetzeStatusAsync(int projektId, ProjektStatus neu, string? kommentar, Verlustgrund? verlustgrund,
        int? angenommenesAngebotId = null, CancellationToken abbruch = default)
    {
        if (angenommenesAngebotId is { } angebotId)
        {
            var passt = (await AngeboteZurAnnahmeAsync(projektId, abbruch)).Any(a => a.Id == angebotId);
            if (!passt)
            {
                throw new ArgumentException("Angenommen werden kann nur ein versendetes Angebot dieses Kundenprojekts.", nameof(angenommenesAngebotId));
            }
        }

        await BearbeiteAsync(projektId, p => p.SetzeStatus(neu, _recht.Name, zeit.GetUtcNow(), kommentar, verlustgrund, angenommenesAngebotId), abbruch);
    }

    /// <summary>Versendete Angebote aller Kalkulationen des Projekts, neueste zuerst; Auswahl für „Gewonnen“.</summary>
    public async Task<IReadOnlyList<AngebotsAuswahl>> AngeboteZurAnnahmeAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kundenprojekte.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }

        var kalkulationen = kontext.Kalkulationen.Where(k => k.KundenprojektId == projektId);
        return await kontext.Angebote.AsNoTracking()
            .Where(a => a.VersendetAm != null || a.Id == projekt.AngenommenesAngebotId)
            .Join(kalkulationen, a => a.Version!.KalkulationId, k => k.Id, (a, k) => new { a, k.Titel })
            .OrderByDescending(x => x.a.VersendetAm).ThenByDescending(x => x.a.Id)
            .Select(x => new AngebotsAuswahl(x.a.Id, x.a.Nummer, x.a.Version!.Nummer, x.Titel, x.a.VersendetAm, x.a.Version.SummeMonatlich))
            .ToListAsync(abbruch);
    }

    /// <summary>
    /// Alle Angebote aller Kalkulationen des Projekts, neueste zuerst, mit abgeleitetem Status (Entscheidung
    /// 08.10.2026). Abgeschlossen ist ein Projekt mit „Gewonnen“ oder „Verloren“.
    /// </summary>
    public async Task<IReadOnlyList<ProjektAngebot>> AngeboteAsync(int projektId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kundenprojekte.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter.");
        }

        var kalkulationen = kontext.Kalkulationen.Where(k => k.KundenprojektId == projektId);
        var zeilen = await kontext.Angebote.AsNoTracking()
            .Join(kalkulationen, a => a.Version!.KalkulationId, k => k.Id, (a, k) => new
            {
                a.Id,
                a.Nummer,
                Version = a.Version!.Nummer,
                KalkulationId = k.Id,
                k.Titel,
                a.Datum,
                a.GueltigBis,
                a.VersendetAm,
                a.ErstelltVon,
                a.Version.SummeMonatlich,
            })
            .ToListAsync(abbruch);

        var neueste = zeilen.GroupBy(z => z.Nummer).ToDictionary(g => g.Key, g => g.Max(z => z.Version), StringComparer.Ordinal);
        var abgeschlossen = projekt.Status is ProjektStatus.Gewonnen or ProjektStatus.Verloren;
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);
        return [.. zeilen
            .OrderByDescending(z => z.Datum).ThenByDescending(z => z.Id)
            .Select(z => new ProjektAngebot(z.Id, z.Nummer, z.Version, z.KalkulationId, z.Titel, z.Datum, z.GueltigBis, z.VersendetAm,
                z.ErstelltVon, z.SummeMonatlich,
                Angebotsstatus.Bestimme(z.Id == projekt.AngenommenesAngebotId, abgeschlossen, z.Version < neueste[z.Nummer], z.VersendetAm, z.GueltigBis, heute)))];
    }

    public Task SetzeForecastAsync(int projektId, int? wahrscheinlichkeit, DateOnly? abschlussmonat, CancellationToken abbruch = default) =>
        BearbeiteAsync(projektId, p => p.SetzeForecast(wahrscheinlichkeit, abschlussmonat), abbruch);

    public async Task<int> NeueKalkulationAsync(int projektId, string titel, CancellationToken abbruch = default)
    {
        Pflicht(titel, "Titel");
        Kalkulation? neu = null;
        await BearbeiteAsync(projektId, p => neu = p.NeueKalkulation(titel.Trim(), _recht.Name, zeit.GetUtcNow()), abbruch);
        return neu!.Id;
    }

    /// <summary>Kopiert eine Kalkulation als neue Variante; Sonderpositionen brauchen dort eine neue Freigabe.</summary>
    public async Task<int> DuplizierenAsync(int kalkulationId, string titel, CancellationToken abbruch = default)
    {
        Pflicht(titel, "Titel");
        Kalkulation? kopie = null;
        await BearbeiteKalkulationAsync(kalkulationId, k => kopie = k.Duplizieren(titel.Trim(), _recht.Name, zeit.GetUtcNow()), abbruch);
        return kopie!.Id;
    }

    public Task FuerForecastMarkierenAsync(int kalkulationId, CancellationToken abbruch = default) =>
        BearbeiteKalkulationAsync(kalkulationId, k => k.Kundenprojekt!.FuerForecastMarkieren(k), abbruch);

    private async Task BearbeiteAsync(int projektId, Action<Kundenprojekt> aenderung, CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projekt = await kontext.Kundenprojekte
            .Include(p => p.Kalkulationen).ThenInclude(k => k.Sonderpositionen)
            .SingleOrDefaultAsync(p => p.Id == projektId, abbruch)
            ?? throw new KeyNotFoundException($"Kundenprojekt {projektId} gibt es nicht.");
        PruefeBearbeiten(projekt);
        aenderung(projekt);
        await kontext.SaveChangesAsync(abbruch);
    }

    private async Task BearbeiteKalkulationAsync(int kalkulationId, Action<Kalkulation> aenderung, CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var projektId = await kontext.Kalkulationen.Where(k => k.Id == kalkulationId).Select(k => (int?)k.KundenprojektId).SingleOrDefaultAsync(abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {kalkulationId} gibt es nicht.");
        var projekt = await kontext.Kundenprojekte
            .Include(p => p.Kalkulationen).ThenInclude(k => k.Sonderpositionen)
            .SingleAsync(p => p.Id == projektId, abbruch);
        PruefeBearbeiten(projekt);
        aenderung(projekt.Kalkulationen.Single(k => k.Id == kalkulationId));
        await kontext.SaveChangesAsync(abbruch);
    }

    private void PruefeBearbeiten(Kundenprojekt projekt)
    {
        if (!_recht.DarfBearbeiten(projekt))
        {
            throw new KeinZugriffException(_recht.DarfKalkulieren
                ? "Dieses Kundenprojekt gehört einem anderen Vertriebsmitarbeiter."
                : "Kundenprojekte bearbeiten Vertrieb und Vertriebsleitung.");
        }
    }

    private static void Pflicht(string? wert, string feld)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            throw new ArgumentException($"Bitte „{feld}“ ausfüllen.", feld);
        }
    }

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
