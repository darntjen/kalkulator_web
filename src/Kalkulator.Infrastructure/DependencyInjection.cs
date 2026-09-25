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
        services.AddDbContext<KalkulatorDbContext>(options => options.UseSqlServer(verbindungszeichenfolge));
        return services;
    }
}
