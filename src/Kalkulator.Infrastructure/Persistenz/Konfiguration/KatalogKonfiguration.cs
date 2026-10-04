using System.Text.Json;
using System.Text.Json.Serialization;
using Kalkulator.Domain.Katalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kalkulator.Infrastructure.Persistenz.Konfiguration;

internal sealed class ServiceKategorieKonfiguration : IEntityTypeConfiguration<ServiceKategorie>
{
    public void Configure(EntityTypeBuilder<ServiceKategorie> builder)
    {
        builder.ToTable("Kategorien", "katalog");
        builder.Property(k => k.Name).HasMaxLength(100);
        builder.HasIndex(k => k.Name).IsUnique();
    }
}

internal sealed class ServiceKonfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "katalog");
        builder.Property(s => s.Code).HasMaxLength(20);
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.ServiceNummer).HasMaxLength(40);
        builder.Property(s => s.Bezeichnung).HasMaxLength(200);
        builder.Property(s => s.Kurzbeschreibung).HasMaxLength(1000);
        builder.Property(s => s.Typ).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Vertriebsstatus).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(s => s.DarfAngebotenWerden);

        builder.HasOne(s => s.Kategorie).WithMany().HasForeignKey(s => s.KategorieId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Leistungsschein).WithMany().HasForeignKey(s => s.LeistungsscheinId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Preiskomponenten).WithOne(p => p.Service).HasForeignKey(p => p.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Regeln).WithOne(r => r.Service).HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Bestandteile).WithOne(b => b.Bundle).HasForeignKey(b => b.BundleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PreiskomponenteKonfiguration : IEntityTypeConfiguration<Preiskomponente>
{
    public void Configure(EntityTypeBuilder<Preiskomponente> builder)
    {
        builder.ToTable("Preiskomponenten", "katalog");
        builder.Property(p => p.Code).HasMaxLength(30);
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Bezeichnung).HasMaxLength(200);
        builder.Property(p => p.Einheit).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Abrechnungsart).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.StaffelBezug).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.NavisionArtikelnummer).HasMaxLength(20);
    }
}

internal sealed class BundleBestandteilKonfiguration : IEntityTypeConfiguration<BundleBestandteil>
{
    public void Configure(EntityTypeBuilder<BundleBestandteil> builder)
    {
        builder.ToTable("BundleBestandteile", "katalog");
        builder.HasKey(b => new { b.BundleId, b.BestandteilId });
        builder.HasOne(b => b.Bestandteil).WithMany().HasForeignKey(b => b.BestandteilId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_BundleBestandteile_NichtSichSelbst", "[BundleId] <> [BestandteilId]"));
    }
}

internal sealed class ServiceRegelKonfiguration : IEntityTypeConfiguration<ServiceRegel>
{
    public void Configure(EntityTypeBuilder<ServiceRegel> builder)
    {
        builder.ToTable("Regeln", "katalog");
        builder.Property(r => r.Typ).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Meldung).HasMaxLength(500);
        builder.HasMany(r => r.Ziele).WithOne().HasForeignKey(z => z.RegelId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ServiceRegelZielKonfiguration : IEntityTypeConfiguration<ServiceRegelZiel>
{
    public void Configure(EntityTypeBuilder<ServiceRegelZiel> builder)
    {
        builder.ToTable("RegelZiele", "katalog");
        builder.HasKey(z => new { z.RegelId, z.ZielServiceId });
        builder.HasOne(z => z.ZielService).WithMany().HasForeignKey(z => z.ZielServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DokumentVorlageKonfiguration : IEntityTypeConfiguration<DokumentVorlage>
{
    public void Configure(EntityTypeBuilder<DokumentVorlage> builder)
    {
        builder.ToTable("DokumentVorlagen", "katalog");
        builder.Property(d => d.Typ).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Code).HasMaxLength(20);
        builder.Property(d => d.Bezeichnung).HasMaxLength(200);
        builder.Property(d => d.Version).HasMaxLength(20);
        builder.Property(d => d.Dateiname).HasMaxLength(260);

        // Eine Zeile je Vorlage; ihre Fassungen hängen als Vorlagenversionen daran (#26, Teil B).
        builder.HasIndex(d => d.Code).IsUnique();
        builder.HasMany(d => d.Versionen).WithOne().HasForeignKey(v => v.DokumentVorlageId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(d => d.AktiveVersion);
    }
}

internal sealed class VorlagenversionKonfiguration : IEntityTypeConfiguration<Vorlagenversion>
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<Vorlagenversion> builder)
    {
        builder.ToTable("Vorlagenversionen", "katalog");
        builder.HasIndex(v => new { v.DokumentVorlageId, v.Nummer }).IsUnique();
        builder.Property(v => v.VersionLaut).HasMaxLength(20);
        builder.Property(v => v.Dateiname).HasMaxLength(260);
        builder.Property(v => v.Quelle).HasMaxLength(20);
        builder.Property(v => v.Pfad).HasMaxLength(400);
        builder.Property(v => v.QuellId).HasMaxLength(200);
        builder.Property(v => v.QuellStand).HasMaxLength(200);
        builder.Property(v => v.GeaendertInQuelleVon).HasMaxLength(200);
        builder.Property(v => v.Sha256).HasMaxLength(64).IsFixedLength();
        builder.Property(v => v.AbgerufenVon).HasMaxLength(200);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.EntschiedenVon).HasMaxLength(200);
        builder.Property(v => v.Kommentar).HasMaxLength(1000);
        Liste(builder.Property(v => v.Hinweise));
        Liste(builder.Property(v => v.Eingaben));
        Liste(builder.Property(v => v.Komponenten));
        Liste(builder.Property(v => v.Unterschriften));
        builder.Ignore(v => v.HatFehler);
        builder.HasOne(v => v.Datei).WithOne().HasForeignKey<VorlagenDatei>(d => d.VorlagenversionId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Listen als JSON-Text; sie werden einmal beim Abgleich geschrieben und danach nur gelesen.</summary>
    internal static PropertyBuilder<IReadOnlyList<T>> Liste<T>(PropertyBuilder<IReadOnlyList<T>> eigenschaft) =>
        eigenschaft.HasConversion(
            new ValueConverter<IReadOnlyList<T>, string>(
                l => JsonSerializer.Serialize(l, Json),
                s => JsonSerializer.Deserialize<List<T>>(s, Json) ?? new List<T>()),
            new ValueComparer<IReadOnlyList<T>>(
                (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                l => JsonSerializer.Serialize(l, Json).GetHashCode(StringComparison.Ordinal),
                l => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(l, Json), Json)!))
        .HasColumnType("nvarchar(max)");
}

internal sealed class VorlagenDateiKonfiguration : IEntityTypeConfiguration<VorlagenDatei>
{
    public void Configure(EntityTypeBuilder<VorlagenDatei> builder)
    {
        builder.ToTable("VorlagenDateien", "katalog");
        builder.HasKey(d => d.VorlagenversionId);
    }
}

internal sealed class VorlagenabgleichKonfiguration : IEntityTypeConfiguration<Vorlagenabgleich>
{
    public void Configure(EntityTypeBuilder<Vorlagenabgleich> builder)
    {
        builder.ToTable("Vorlagenabgleiche", "katalog");
        builder.Property(a => a.Quelle).HasMaxLength(20);
        builder.Property(a => a.AusgeloestVon).HasMaxLength(200);
        builder.Property(a => a.Fehler).HasMaxLength(2000);
        builder.Property(a => a.Bericht).HasColumnType("nvarchar(max)");
        builder.HasIndex(a => a.Beginn);
    }
}
