using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Berechnung;

/// <summary>Lädt Katalog und Preisliste aus der Datenbank und baut daraus den <see cref="Rechenkern"/>.</summary>
public sealed class RechenkernLader(KalkulatorDbContext kontext)
{
    /// <summary>
    /// Rechenkern mit der am Stichtag gültigen freigegebenen Preisliste. EK-Werte werden nur mit
    /// <paramref name="mitEinkauf"/> geladen, also nur für berechtigte Rollen und beim Einfrieren (Designprinzip 4).
    /// </summary>
    public async Task<Rechenkern> LadeAsync(DateOnly stichtag, bool mitEinkauf, CancellationToken abbruch = default)
    {
        var preislisteId = await kontext.Preislisten
            .Where(p => p.Status == PreislistenStatus.Freigegeben && p.GueltigAb <= stichtag)
            .OrderByDescending(p => p.GueltigAb).ThenByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(abbruch)
            ?? throw new InvalidOperationException($"Zum {stichtag:dd.MM.yyyy} gibt es keine freigegebene Preisliste.");

        return await LadeAsync(preislisteId, mitEinkauf, abbruch);
    }

    /// <summary>Rechenkern mit einer bestimmten Preisliste, z. B. um eine eingefrorene Version nachzurechnen.</summary>
    public async Task<Rechenkern> LadeAsync(int preislisteId, bool mitEinkauf, CancellationToken abbruch = default)
    {
        // Alle Services laden; EF verknüpft Bundle-Bestandteile und Regelziele dann selbst.
        var services = await kontext.Services
            .Include(s => s.Preiskomponenten)
            .Include(s => s.Bestandteile)
            .Include(s => s.Regeln).ThenInclude(r => r.Ziele)
            .AsSplitQuery()
            .ToListAsync(abbruch);

        var abfrage = kontext.Preislisten.Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter);
        var preisliste = mitEinkauf
            ? await abfrage.Include(p => p.EkPositionen).AsSplitQuery().SingleAsync(p => p.Id == preislisteId, abbruch)
            : await abfrage.AsSplitQuery().SingleAsync(p => p.Id == preislisteId, abbruch);

        return new Rechenkern(services, preisliste);
    }
}
