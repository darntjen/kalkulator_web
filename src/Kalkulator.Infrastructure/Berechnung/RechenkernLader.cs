using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Berechnung;

/// <summary>Katalog und Preisliste, wie sie der Kalkulationseditor braucht.</summary>
public sealed record GeladenerKatalog(IReadOnlyList<Service> Services, Preisliste Preisliste)
{
    /// <summary>Ob mit einer freigegebenen Preisliste gerechnet wird; nur dann lässt sich ein Angebotsstand einfrieren.</summary>
    public bool PreislisteFreigegeben => Preisliste.Status == PreislistenStatus.Freigegeben;

    public Rechenkern ErzeugeRechenkern() => new(Services, Preisliste);
}

/// <summary>Lädt Katalog und Preisliste aus der Datenbank und baut daraus den <see cref="Rechenkern"/>.</summary>
public sealed class RechenkernLader(KalkulatorDbContext kontext)
{
    /// <summary>
    /// Rechenkern mit der am Stichtag gültigen freigegebenen Preisliste. EK-Werte werden nur mit
    /// <paramref name="mitEinkauf"/> geladen, also nur für berechtigte Rollen und beim Einfrieren (Designprinzip 4).
    /// </summary>
    public async Task<Rechenkern> LadeAsync(DateOnly stichtag, bool mitEinkauf, CancellationToken abbruch = default) =>
        (await LadeKatalogAsync(stichtag, mitEinkauf, entwurfZulassen: false, abbruch)).ErzeugeRechenkern();

    /// <summary>
    /// Katalog mit der am Stichtag gültigen freigegebenen Preisliste. Mit <paramref name="entwurfZulassen"/> wird,
    /// solange es keine freigegebene gibt, die jüngste Entwurfs-Preisliste genommen, damit schon kalkuliert werden kann;
    /// eingefroren werden kann damit nicht.
    /// </summary>
    public async Task<GeladenerKatalog> LadeKatalogAsync(DateOnly stichtag, bool mitEinkauf, bool entwurfZulassen, CancellationToken abbruch = default)
    {
        var preislisteId = await kontext.Preislisten
            .Where(p => p.Status == PreislistenStatus.Freigegeben && p.GueltigAb <= stichtag)
            .OrderByDescending(p => p.GueltigAb).ThenByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(abbruch);

        if (preislisteId is null && entwurfZulassen)
        {
            preislisteId = await kontext.Preislisten
                .Where(p => p.Status == PreislistenStatus.Entwurf)
                .OrderByDescending(p => p.GueltigAb).ThenByDescending(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(abbruch);
        }

        if (preislisteId is null)
        {
            throw new InvalidOperationException($"Zum {stichtag:dd.MM.yyyy} gibt es keine freigegebene Preisliste.");
        }

        return await LadeKatalogAsync(preislisteId.Value, mitEinkauf, abbruch);
    }

    /// <summary>Rechenkern mit einer bestimmten Preisliste, z. B. um eine eingefrorene Version nachzurechnen.</summary>
    public async Task<Rechenkern> LadeAsync(int preislisteId, bool mitEinkauf, CancellationToken abbruch = default) =>
        (await LadeKatalogAsync(preislisteId, mitEinkauf, abbruch)).ErzeugeRechenkern();

    private async Task<GeladenerKatalog> LadeKatalogAsync(int preislisteId, bool mitEinkauf, CancellationToken abbruch)
    {
        // Alle Services laden; EF verknüpft Bundle-Bestandteile und Regelziele dann selbst.
        var services = await kontext.Services
            .Include(s => s.Kategorie)
            .Include(s => s.Preiskomponenten)
            .Include(s => s.Bestandteile)
            .Include(s => s.Regeln).ThenInclude(r => r.Ziele)
            .AsSplitQuery()
            .ToListAsync(abbruch);

        var abfrage = kontext.Preislisten.Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter);
        var preisliste = mitEinkauf
            ? await abfrage.Include(p => p.EkPositionen).AsSplitQuery().SingleAsync(p => p.Id == preislisteId, abbruch)
            : await abfrage.AsSplitQuery().SingleAsync(p => p.Id == preislisteId, abbruch);

        return new GeladenerKatalog(services, preisliste);
    }
}
