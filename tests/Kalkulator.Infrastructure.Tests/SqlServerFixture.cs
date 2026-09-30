using Kalkulator.Infrastructure.Persistenz;
using Microsoft.Data.SqlClient;
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

    public KalkulatorDbContext NeuerKontext(IBenutzerKontext? benutzer = null, TimeProvider? zeit = null) =>
        Kontext(_container.GetConnectionString(), benutzer, zeit);

    /// <summary>
    /// Kontext auf eine eigene, noch nicht migrierte Datenbank im selben Container,
    /// z. B. für Tests, die einen leeren Katalog brauchen.
    /// </summary>
    public KalkulatorDbContext NeuerKontextAufDatenbank(string datenbank)
    {
        var verbindung = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = datenbank };
        return Kontext(verbindung.ConnectionString, null, null);
    }

    private static KalkulatorDbContext Kontext(string verbindung, IBenutzerKontext? benutzer, TimeProvider? zeit)
    {
        var options = new DbContextOptionsBuilder<KalkulatorDbContext>()
            .UseSqlServer(verbindung)
            .Options;
        return new KalkulatorDbContext(options, benutzer ?? new SystemBenutzer(), zeit ?? TimeProvider.System);
    }
}

[CollectionDefinition(Name)]
public sealed class DatenbankSammlung : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Datenbank";
}
