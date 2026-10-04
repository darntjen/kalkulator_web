using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kalkulator.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registriert Datenbankzugriff und Hilfsdienste. Die Verbindung wird erst beim ersten Zugriff geöffnet.</summary>
    public static IServiceCollection AddKalkulatorInfrastruktur(this IServiceCollection services, string? verbindungszeichenfolge)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IBenutzerKontext, SystemBenutzer>();
        // Fabrik je Anfrage bzw. Blazor-Verbindung, weil der Kontext den angemeldeten Benutzer braucht;
        // die Dienste erzeugen daraus kurzlebige Kontexte. Der Kontext selbst bleibt zusätzlich als Scoped-Dienst abrufbar.
        services.AddDbContextFactory<KalkulatorDbContext>(options => options.UseSqlServer(verbindungszeichenfolge), ServiceLifetime.Scoped);
        services.AddScoped<RechenkernLader>();
        services.AddScoped<KundenprojektDienst>();
        services.AddScoped<KalkulationsDienst>();
        services.AddScoped<AngebotsDienst>();
        services.AddScoped<KatalogDienst>();
        services.AddScoped<PreislistenDienst>();
        services.AddOptions<AngebotsEinstellungen>();
        return services;
    }
}
