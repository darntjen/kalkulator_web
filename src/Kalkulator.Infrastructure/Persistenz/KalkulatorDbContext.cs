using System.Text.Json;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PreisParameter = Kalkulator.Domain.Preise.Parameter;

namespace Kalkulator.Infrastructure.Persistenz;

public class KalkulatorDbContext(
    DbContextOptions<KalkulatorDbContext> options,
    IBenutzerKontext benutzer,
    TimeProvider zeit) : DbContext(options)
{
    public DbSet<ServiceKategorie> Kategorien => Set<ServiceKategorie>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Preiskomponente> Preiskomponenten => Set<Preiskomponente>();
    public DbSet<BundleBestandteil> BundleBestandteile => Set<BundleBestandteil>();
    public DbSet<ServiceRegel> Regeln => Set<ServiceRegel>();
    public DbSet<DokumentVorlage> DokumentVorlagen => Set<DokumentVorlage>();
    public DbSet<Preisliste> Preislisten => Set<Preisliste>();
    public DbSet<Preis> Preise => Set<Preis>();
    public DbSet<Preisstaffel> Preisstaffeln => Set<Preisstaffel>();
    public DbSet<PreisParameter> Parameter => Set<PreisParameter>();
    public DbSet<EkPosition> EkPositionen => Set<EkPosition>();
    public DbSet<AenderungsEintrag> Aenderungsprotokoll => Set<AenderungsEintrag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KalkulatorDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    /// <summary>
    /// Speichert Änderungen und schreibt in derselben Transaktion das Änderungsprotokoll (A-08, F-09).
    /// Änderungen an freigegebenen Preislisten werden abgewiesen (ADR-0005).
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await PruefeFreigegebenePreislistenAsync(cancellationToken);
        var protokoll = ErfasseAenderungen();

        var eigeneTransaktion = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var ergebnis = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (protokoll.Count > 0)
            {
                SchreibeProtokoll(protokoll);
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            if (eigeneTransaktion is not null)
            {
                await eigeneTransaktion.CommitAsync(cancellationToken);
            }

            return ergebnis;
        }
        finally
        {
            if (eigeneTransaktion is not null)
            {
                await eigeneTransaktion.DisposeAsync();
            }
        }
    }

    private async Task PruefeFreigegebenePreislistenAsync(CancellationToken cancellationToken)
    {
        // Die Preisliste selbst: Ändern oder Löschen nur, solange sie gespeichert als Entwurf vorliegt.
        // Das Freigeben (Entwurf → Freigegeben) ist damit erlaubt.
        foreach (var eintrag in ChangeTracker.Entries<Preisliste>())
        {
            if (eintrag.State is EntityState.Modified or EntityState.Deleted
                && GespeicherterStatus(eintrag) == PreislistenStatus.Freigegeben)
            {
                throw new PreislisteGesperrtException(eintrag.Entity.Bezeichnung);
            }
        }

        // Preise, Staffeln, Parameter und EK-Werte hängen an einer Preisliste und teilen deren Sperre.
        var abhaengige = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is Preis or Preisstaffel or PreisParameter or EkPosition)
            .ToList();

        foreach (var eintrag in abhaengige)
        {
            var preislisteId = (int)eintrag.Property(nameof(Preis.PreislisteId)).CurrentValue!;
            var status = await GespeicherterStatusAsync(preislisteId, cancellationToken);
            if (status == PreislistenStatus.Freigegeben)
            {
                throw new PreislisteGesperrtException(eintrag.Entity.GetType().Name + " der Preisliste " + preislisteId);
            }
        }
    }

    private static PreislistenStatus? GespeicherterStatus(EntityEntry<Preisliste> eintrag) =>
        eintrag.State == EntityState.Added
            ? null
            : (PreislistenStatus)eintrag.OriginalValues[nameof(Preisliste.Status)]!;

    private async Task<PreislistenStatus?> GespeicherterStatusAsync(int preislisteId, CancellationToken cancellationToken)
    {
        var verfolgt = ChangeTracker.Entries<Preisliste>().FirstOrDefault(e => e.Entity.Id == preislisteId);
        if (verfolgt is not null)
        {
            return GespeicherterStatus(verfolgt);
        }

        return await Preislisten.AsNoTracking()
            .Where(p => p.Id == preislisteId)
            .Select(p => (PreislistenStatus?)p.Status)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private List<(EntityEntry Eintrag, string Aktion, string? Werte)> ErfasseAenderungen() =>
        [.. ChangeTracker.Entries()
            .Where(e => e.Entity is not AenderungsEintrag)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (e, e.State switch
            {
                EntityState.Added => "Angelegt",
                EntityState.Deleted => "Gelöscht",
                _ => "Geändert",
            }, e.State == EntityState.Modified ? GeaenderteWerte(e) : null))];

    private static string GeaenderteWerte(EntityEntry eintrag)
    {
        var aenderungen = eintrag.Properties
            .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            .ToDictionary(p => p.Metadata.Name, p => new { Alt = p.OriginalValue, Neu = p.CurrentValue });
        return JsonSerializer.Serialize(aenderungen);
    }

    // Wird nach dem ersten Speichern aufgerufen, damit neu angelegte Datensätze ihre Schlüssel schon haben.
    private void SchreibeProtokoll(List<(EntityEntry Eintrag, string Aktion, string? Werte)> protokoll)
    {
        var jetzt = zeit.GetUtcNow();
        foreach (var (eintrag, aktion, werte) in protokoll)
        {
            var schluessel = eintrag.Metadata.FindPrimaryKey()!.Properties
                .Select(p => eintrag.Property(p.Name).CurrentValue?.ToString());

            Aenderungsprotokoll.Add(new AenderungsEintrag
            {
                Zeitpunkt = jetzt,
                Benutzer = benutzer.Name,
                Entitaet = eintrag.Metadata.ClrType.Name,
                Schluessel = string.Join("|", schluessel),
                Aktion = aktion,
                Aenderungen = werte,
            });
        }
    }
}
