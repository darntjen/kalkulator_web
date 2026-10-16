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
/// Archiviert jede Nacht zur Uhrzeit <c>Kundenablage:ArchivierungUm</c> (Ortszeit Berlin) die Dokumente abgeschlossener
/// Kundenprojekte im Kanalordner (<see cref="Archivierung"/>). Ohne eingerichtete Kundenablage oder Uhrzeit läuft nichts.
/// </summary>
public sealed class NaechtlicheArchivierung(
    IServiceScopeFactory bereiche,
    IOptions<KundenablageEinstellungen> einstellungen,
    IKundenablage ablage,
    TimeProvider zeit,
    ILoggerFactory protokolle) : BackgroundService
{
    public const string Benutzer = "nächtliche Archivierung";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ablage.Eingerichtet || NaechtlicherVorlagenabgleich.Uhrzeit(einstellungen.Value.ArchivierungUm) is not { } uhrzeit)
        {
            return;
        }

        var protokoll = protokolle.CreateLogger<NaechtlicheArchivierung>();
        while (!stoppingToken.IsCancellationRequested)
        {
            var jetzt = zeit.GetUtcNow();
            await Task.Delay(NaechtlicherVorlagenabgleich.NaechsterLauf(jetzt, uhrzeit) - jetzt, zeit, stoppingToken);
            try
            {
                await using var bereich = bereiche.CreateAsyncScope();
                var optionen = bereich.ServiceProvider.GetRequiredService<DbContextOptions<KalkulatorDbContext>>();
                var benutzer = new Hintergrundbenutzer(Benutzer);
                var archivierung = new Archivierung(new Fabrik(() => new KalkulatorDbContext(optionen, benutzer, zeit)), ablage, zeit,
                    protokolle.CreateLogger<Archivierung>());
                var ergebnis = await archivierung.AusfuehrenAsync(stoppingToken);
                if (ergebnis.Angebote + ergebnis.Vertragswerke + ergebnis.Fehler > 0)
                {
                    protokoll.LogInformation(
                        "Archivierung: {Angebote} Angebote und {Vertragswerke} Vertragswerke in den Kanalordner verschoben, {Fehler} Fehler.",
                        ergebnis.Angebote, ergebnis.Vertragswerke, ergebnis.Fehler);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Der Dienst läuft weiter; der nächste Versuch ist in der nächsten Nacht.
                protokoll.LogError(e, "Archivierung abgebrochen.");
            }
        }
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
