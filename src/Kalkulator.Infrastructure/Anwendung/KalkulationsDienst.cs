using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Eine Kalkulation mit ihrem Kundenprojekt und den Rechten des angemeldeten Benutzers daran.</summary>
public sealed record KalkulationsDaten(
    Kalkulation Kalkulation,
    Kundenprojekt Projekt,
    bool DarfBearbeiten,
    bool DarfEinkaufSehen,
    bool DarfSonderpositionenFreigeben);

public sealed record SonderpositionsDaten(string Bezeichnung, string Einheit, int Menge, decimal Preis, string Begruendung, bool Einmalig);

/// <summary>
/// Anwendungsfälle des Kalkulationseditors: laden, Katalog bereitstellen, Arbeitsstand speichern, Sonderpositionen
/// pflegen und freigeben. Rechnen selbst geschieht im <see cref="Rechenkern"/> ohne Datenbank.
/// </summary>
public sealed class KalkulationsDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer, TimeProvider zeit)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    public async Task<KalkulationsDaten?> LadeAsync(int id, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var kalkulation = await kontext.Kalkulationen.AsNoTracking()
            .Include(k => k.Kundenprojekt).ThenInclude(p => p!.Kunde)
            .Include(k => k.Sonderpositionen)
            .Include(k => k.Versionen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(k => k.Id == id, abbruch);
        if (kalkulation is null)
        {
            return null;
        }

        var projekt = kalkulation.Kundenprojekt!;
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Diese Kalkulation gehört zu einem Kundenprojekt eines anderen Vertriebsmitarbeiters.");
        }

        return new KalkulationsDaten(kalkulation, projekt, _recht.DarfBearbeiten(projekt), _recht.DarfEinkaufSehen, _recht.DarfSonderpositionenFreigeben);
    }

    /// <summary>
    /// Katalog mit der heute gültigen Preisliste; ohne freigegebene Preisliste die jüngste im Entwurf.
    /// EK-Werte nur für die Führung (Designprinzip 4).
    /// </summary>
    public async Task<GeladenerKatalog> KatalogAsync(CancellationToken abbruch = default)
    {
        if (!_recht.DarfProjekteSehen)
        {
            throw new KeinZugriffException("Für Kalkulationen fehlt die Berechtigung.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);
        return await new RechenkernLader(kontext).LadeKatalogAsync(heute, _recht.DarfEinkaufSehen, entwurfZulassen: true, abbruch);
    }

    /// <summary>
    /// Überschreibt den Arbeitsstand. <paramref name="zeilenversion"/> ist der Stand, auf dem bearbeitet wurde; hat
    /// jemand anderes inzwischen gespeichert, gibt es eine <see cref="DbUpdateConcurrencyException"/>.
    /// </summary>
    /// <returns>Die neue Zeilenversion für das nächste Speichern.</returns>
    public async Task<byte[]> SpeichernAsync(int id, string titel, DateOnly? vertragsbeginn, KalkulationsEingabe eingabe, byte[] zeilenversion, CancellationToken abbruch = default)
    {
        if (string.IsNullOrWhiteSpace(titel))
        {
            throw new ArgumentException("Bitte einen Titel angeben.", nameof(titel));
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var kalkulation = await LadeZumBearbeitenAsync(kontext, id, abbruch);
        kontext.Entry(kalkulation).Property(k => k.Zeilenversion).OriginalValue = zeilenversion;
        kalkulation.Titel = titel.Trim();
        kalkulation.Vertragsbeginn = vertragsbeginn;
        kalkulation.AendereEingabe(eingabe);
        await kontext.SaveChangesAsync(abbruch);
        return kalkulation.Zeilenversion;
    }

    public Task<IReadOnlyList<Sonderposition>> SonderpositionHinzufuegenAsync(int id, SonderpositionsDaten daten, CancellationToken abbruch = default)
    {
        Pruefe(daten);
        return SonderpositionenAsync(id, Recht.DarfBearbeiten, k => k.SonderpositionHinzufuegen(daten.Bezeichnung.Trim(), daten.Einheit.Trim(), daten.Menge, daten.Preis, daten.Begruendung.Trim(), daten.Einmalig), abbruch);
    }

    /// <summary>Ändert eine Sonderposition; jede inhaltliche Änderung setzt die Freigabe zurück.</summary>
    public Task<IReadOnlyList<Sonderposition>> SonderpositionAendernAsync(int id, int positionId, SonderpositionsDaten daten, CancellationToken abbruch = default)
    {
        Pruefe(daten);
        return SonderpositionenAsync(id, Recht.DarfBearbeiten, k => Position(k, positionId).Aendern(daten.Bezeichnung.Trim(), daten.Einheit.Trim(), daten.Menge, daten.Preis, daten.Begruendung.Trim(), daten.Einmalig), abbruch);
    }

    public Task<IReadOnlyList<Sonderposition>> SonderpositionEntfernenAsync(int id, int positionId, CancellationToken abbruch = default) =>
        SonderpositionenAsync(id, Recht.DarfBearbeiten, k => k.SonderpositionEntfernen(Position(k, positionId)), abbruch);

    /// <summary>Freigabe durch die Vertriebsleitung (B-22); wird im Änderungsprotokoll festgehalten.</summary>
    public Task<IReadOnlyList<Sonderposition>> SonderpositionFreigebenAsync(int id, int positionId, string? kommentar, CancellationToken abbruch = default) =>
        SonderpositionenAsync(id, _ => _recht.DarfSonderpositionenFreigeben, k => Position(k, positionId).Freigeben(_recht.Name, zeit.GetUtcNow(), kommentar), abbruch);

    public Task<IReadOnlyList<Sonderposition>> SonderpositionAblehnenAsync(int id, int positionId, string kommentar, CancellationToken abbruch = default) =>
        SonderpositionenAsync(id, _ => _recht.DarfSonderpositionenFreigeben, k => Position(k, positionId).Ablehnen(_recht.Name, zeit.GetUtcNow(), kommentar), abbruch);

    private async Task<IReadOnlyList<Sonderposition>> SonderpositionenAsync(
        int id, Func<Kundenprojekt, bool> erlaubt, Action<Kalkulation> aenderung, CancellationToken abbruch)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var kalkulation = await kontext.Kalkulationen
            .Include(k => k.Kundenprojekt)
            .Include(k => k.Sonderpositionen)
            .SingleOrDefaultAsync(k => k.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {id} gibt es nicht.");
        if (!erlaubt(kalkulation.Kundenprojekt!))
        {
            throw new KeinZugriffException("Für diese Aktion fehlt die Berechtigung.");
        }

        aenderung(kalkulation);
        await kontext.SaveChangesAsync(abbruch);
        return [.. kalkulation.Sonderpositionen.OrderBy(s => s.Reihenfolge)];
    }

    private async Task<Kalkulation> LadeZumBearbeitenAsync(KalkulatorDbContext kontext, int id, CancellationToken abbruch)
    {
        var kalkulation = await kontext.Kalkulationen
            .Include(k => k.Kundenprojekt)
            .SingleOrDefaultAsync(k => k.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {id} gibt es nicht.");
        if (!_recht.DarfBearbeiten(kalkulation.Kundenprojekt!))
        {
            throw new KeinZugriffException("Diese Kalkulation darfst du nicht bearbeiten.");
        }

        return kalkulation;
    }

    /// <summary>B-21: Bezeichnung, Einheit, Menge, Preis (nur positiv) und Begründung sind Pflicht.</summary>
    private static void Pruefe(SonderpositionsDaten daten)
    {
        if (string.IsNullOrWhiteSpace(daten.Bezeichnung) || string.IsNullOrWhiteSpace(daten.Einheit) || string.IsNullOrWhiteSpace(daten.Begruendung))
        {
            throw new ArgumentException("Bitte Bezeichnung, Einheit und Begründung der Sonderposition ausfüllen.", nameof(daten));
        }

        if (daten.Menge <= 0 || daten.Preis <= 0)
        {
            throw new ArgumentException("Menge und Preis einer Sonderposition müssen größer als 0 sein.", nameof(daten));
        }
    }

    private static Sonderposition Position(Kalkulation kalkulation, int positionId) =>
        kalkulation.Sonderpositionen.SingleOrDefault(s => s.Id == positionId)
        ?? throw new KeyNotFoundException($"Sonderposition {positionId} gibt es in dieser Kalkulation nicht.");
}
