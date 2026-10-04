using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Ablage;

/// <summary>
/// Löscht täglich zur eingestellten Uhrzeit die Uploads von Kundenprojekten, die seit mehr als drei Jahren
/// abgeschlossen sind (F-13, Entscheidung 04.10.2026). Dateien im Teams-Kanalordner bleiben unberührt.
/// </summary>
public sealed class UnterlagenAufbewahrung(
    IServiceScopeFactory bereiche,
    IOptions<KundenablageEinstellungen> einstellungen,
    IKundenablage ablage,
    TimeProvider zeit,
    ILogger<UnterlagenAufbewahrung> protokoll) : BackgroundService
{
    public const string Benutzer = "Aufbewahrungsfrist";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (NaechtlicherVorlagenabgleich.Uhrzeit(einstellungen.Value.LoeschungUm) is not { } uhrzeit)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var jetzt = zeit.GetUtcNow();
            await Task.Delay(NaechtlicherVorlagenabgleich.NaechsterLauf(jetzt, uhrzeit) - jetzt, zeit, stoppingToken);
            try
            {
                await using var bereich = bereiche.CreateAsyncScope();
                var optionen = bereich.ServiceProvider.GetRequiredService<DbContextOptions<KalkulatorDbContext>>();
                var benutzer = new Hintergrundbenutzer(Benutzer);
                var dienst = new UnterlagenDienst(new Fabrik(() => new KalkulatorDbContext(optionen, benutzer, zeit)), benutzer, zeit, ablage, einstellungen);
                var geloescht = await dienst.AbgelaufeneLoeschenAsync(stoppingToken);
                if (geloescht > 0)
                {
                    protokoll.LogInformation("Aufbewahrungsfrist: {Anzahl} Uploads abgeschlossener Kundenprojekte gelöscht.", geloescht);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                protokoll.LogError(e, "Löschung abgelaufener Uploads abgebrochen.");
            }
        }
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
