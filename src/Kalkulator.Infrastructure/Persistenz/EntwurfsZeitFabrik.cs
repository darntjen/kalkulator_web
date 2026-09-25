using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalkulator.Infrastructure.Persistenz;

/// <summary>Wird nur von <c>dotnet ef</c> zum Erzeugen von Migrationen verwendet.</summary>
internal sealed class EntwurfsZeitFabrik : IDesignTimeDbContextFactory<KalkulatorDbContext>
{
    public KalkulatorDbContext CreateDbContext(string[] args)
    {
        var verbindung = Environment.GetEnvironmentVariable("KALKULATOR_DB")
            ?? "Server=localhost;Database=Kalkulator;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<KalkulatorDbContext>().UseSqlServer(verbindung).Options;
        return new KalkulatorDbContext(options, new SystemBenutzer(), TimeProvider.System);
    }
}
