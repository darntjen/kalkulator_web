using System.Globalization;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Vorlagen;

/// <summary>
/// Gleicht die Vertragsvorlagen jede Nacht zur eingestellten Uhrzeit (Ortszeit Berlin) mit der Quelle ab. Neue Fassungen
/// warten danach auf die Bestätigung durch das Produktmanagement. Ohne Quelle oder Uhrzeit läuft nichts.
/// </summary>
public sealed class NaechtlicherVorlagenabgleich(
    IServiceScopeFactory bereiche,
    IOptions<VorlagenEinstellungen> einstellungen,
    IVorlagenQuelle quelle,
    TimeProvider zeit,
    ILogger<NaechtlicherVorlagenabgleich> protokoll) : BackgroundService
{
    public const string Benutzer = "nächtlicher Abgleich";

    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (quelle is KeineVorlagenquelle || Uhrzeit(einstellungen.Value.AbgleichUm) is not { } uhrzeit)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var jetzt = zeit.GetUtcNow();
            await Task.Delay(NaechsterLauf(jetzt, uhrzeit) - jetzt, zeit, stoppingToken);
            try
            {
                await using var bereich = bereiche.CreateAsyncScope();
                var optionen = bereich.ServiceProvider.GetRequiredService<DbContextOptions<KalkulatorDbContext>>();
                var benutzer = new Hintergrundbenutzer(Benutzer);
                var dienst = new VorlagenDienst(new Fabrik(() => new KalkulatorDbContext(optionen, benutzer, zeit)), benutzer, zeit, quelle);
                var abgleich = await dienst.FuehreAbgleichAusAsync(Benutzer, stoppingToken);
                if (abgleich.Fehler is not null)
                {
                    protokoll.LogWarning("Vorlagenabgleich fehlgeschlagen: {Fehler}", abgleich.Fehler);
                }
                else
                {
                    protokoll.LogInformation("Vorlagenabgleich: {Dateien} Dateien, {Neu} neue Fassungen.", abgleich.Dateien, abgleich.NeueFassungen);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Der Dienst läuft weiter; der nächste Versuch ist in der nächsten Nacht.
                protokoll.LogError(e, "Vorlagenabgleich abgebrochen.");
            }
        }
    }

    /// <summary>Nächster Zeitpunkt mit der eingestellten Ortszeit nach <paramref name="jetzt"/>.</summary>
    internal static DateTimeOffset NaechsterLauf(DateTimeOffset jetzt, TimeOnly uhrzeit)
    {
        var ortszeit = TimeZoneInfo.ConvertTime(jetzt, Zeitzone);
        var tag = DateOnly.FromDateTime(ortszeit.DateTime);
        for (var i = 0; i < 3; i++)
        {
            var lokal = tag.AddDays(i).ToDateTime(uhrzeit);
            var lauf = new DateTimeOffset(lokal, Zeitzone.GetUtcOffset(lokal));
            if (lauf > jetzt)
            {
                return lauf;
            }
        }

        return jetzt.AddDays(1);
    }

    internal static TimeOnly? Uhrzeit(string? text) =>
        TimeOnly.TryParseExact(text?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var uhrzeit) ? uhrzeit : null;

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}

/// <summary>Benutzer für Arbeiten ohne Anmeldung (z. B. nächtlicher Abgleich); hat keine Rollen.</summary>
public sealed class Hintergrundbenutzer(string name) : IBenutzerKontext
{
    public string Name => name;

    public bool IstInRolle(string rolle) => false;
}
