using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Kalkulator.Infrastructure.Tests;

/// <summary>
/// Startet einmal je Testlauf einen echten SQL Server in Docker und spielt die Migrationen ein.
/// So werden Migrationen und Datenbankregeln (Indizes, Check-Constraints, Genauigkeit) mitgetestet.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var kontext = NeuerKontext();
        await kontext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public KalkulatorDbContext NeuerKontext(IBenutzerKontext? benutzer = null, TimeProvider? zeit = null)
    {
        var options = new DbContextOptionsBuilder<KalkulatorDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        return new KalkulatorDbContext(options, benutzer ?? new SystemBenutzer(), zeit ?? TimeProvider.System);
    }
}

[CollectionDefinition(Name)]
public sealed class DatenbankSammlung : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Datenbank";
}
