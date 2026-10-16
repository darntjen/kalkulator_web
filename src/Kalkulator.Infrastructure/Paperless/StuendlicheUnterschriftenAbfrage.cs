using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Paperless;

/// <summary>
/// Fragt im Abstand <c>Paperless:AbfrageStunden</c> (Standard: stündlich) ab, welche übergebenen Verträge vollständig
/// unterschrieben sind. Ohne eingerichtetes Paperless oder mit 0 Stunden läuft nichts.
/// </summary>
public sealed class StuendlicheUnterschriftenAbfrage(
    IServiceScopeFactory bereiche,
    IOptions<PaperlessEinstellungen> einstellungen,
    IPaperlessAbruf paperless,
    TimeProvider zeit,
    ILoggerFactory protokolle) : BackgroundService
{
    public const string Benutzer = "Paperless-Abfrage";

    /// <summary>Erster Lauf kurz nach dem Start, damit ein Neustart keine Stunde Verzögerung bringt.</summary>
    private static readonly TimeSpan ErsterLauf = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var e = einstellungen.Value;
        if (!e.Aktiv || e.AbfrageStunden <= 0)
        {
            return;
        }

        var protokoll = protokolle.CreateLogger<StuendlicheUnterschriftenAbfrage>();
        var abstand = TimeSpan.FromHours(e.AbfrageStunden);
        await Task.Delay(ErsterLauf, zeit, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var bereich = bereiche.CreateAsyncScope();
                var optionen = bereich.ServiceProvider.GetRequiredService<DbContextOptions<KalkulatorDbContext>>();
                var benutzer = new Hintergrundbenutzer(Benutzer);
                var abfrage = new UnterschriftenAbfrage(new Fabrik(() => new KalkulatorDbContext(optionen, benutzer, zeit)), benutzer, paperless,
                    einstellungen, zeit, protokolle.CreateLogger<UnterschriftenAbfrage>());
                var ergebnis = await abfrage.AusfuehrenAsync(stoppingToken);
                if (ergebnis.Geprueft > 0)
                {
                    protokoll.LogInformation("Paperless-Abfrage: {Geprueft} geprüft, {Unterzeichnet} neu unterzeichnet, {Abgelegt} abgelegt, {Fehler} Fehler.",
                        ergebnis.Geprueft, ergebnis.Unterzeichnet, ergebnis.Abgelegt, ergebnis.Fehler);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Der Dienst läuft weiter; der nächste Versuch folgt im nächsten Takt.
                protokoll.LogError(ex, "Paperless-Abfrage abgebrochen.");
            }

            await Task.Delay(abstand, zeit, stoppingToken);
        }
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
