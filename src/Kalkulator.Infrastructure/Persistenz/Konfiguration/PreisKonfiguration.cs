using Kalkulator.Domain.Preise;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalkulator.Infrastructure.Persistenz.Konfiguration;

internal sealed class PreislisteKonfiguration : IEntityTypeConfiguration<Preisliste>
{
    public void Configure(EntityTypeBuilder<Preisliste> builder)
    {
        builder.ToTable("Preislisten", "preise");
        builder.Property(p => p.Bezeichnung).HasMaxLength(100);
        builder.HasIndex(p => p.Bezeichnung).IsUnique();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.FreigegebenVon).HasMaxLength(200);
        builder.HasOne<Preisliste>().WithMany().HasForeignKey(p => p.VorgaengerId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(p => p.IstAenderbar);

        builder.HasMany(p => p.Preise).WithOne().HasForeignKey(p => p.PreislisteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Staffeln).WithOne().HasForeignKey(s => s.PreislisteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Parameter).WithOne().HasForeignKey(p => p.PreislisteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.EkPositionen).WithOne().HasForeignKey(e => e.PreislisteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PreisKonfiguration : IEntityTypeConfiguration<Preis>
{
    public void Configure(EntityTypeBuilder<Preis> builder)
    {
        builder.ToTable("Preise", "preise");
        builder.Property(p => p.VkNetto).HasPrecision(12, 2);
        builder.HasIndex(p => new { p.PreislisteId, p.PreiskomponenteId }).IsUnique();
        builder.HasOne(p => p.Preiskomponente).WithMany().HasForeignKey(p => p.PreiskomponenteId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PreisstaffelKonfiguration : IEntityTypeConfiguration<Preisstaffel>
{
    public void Configure(EntityTypeBuilder<Preisstaffel> builder)
    {
        builder.ToTable("Preisstaffeln", "preise");
        builder.Property(s => s.VkNetto).HasPrecision(12, 2);
        builder.HasIndex(s => new { s.PreislisteId, s.PreiskomponenteId, s.AbMenge }).IsUnique();
        builder.HasOne(s => s.Preiskomponente).WithMany().HasForeignKey(s => s.PreiskomponenteId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_Preisstaffeln_AbMenge", "[AbMenge] >= 0"));
    }
}

internal sealed class ParameterKonfiguration : IEntityTypeConfiguration<Parameter>
{
    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.ToTable("Parameter", "preise");
        builder.Property(p => p.Schluessel).HasMaxLength(60);
        builder.Property(p => p.Wert).HasPrecision(14, 4);
        builder.Property(p => p.Beschreibung).HasMaxLength(500);
        builder.HasIndex(p => new { p.PreislisteId, p.Schluessel }).IsUnique();
    }
}

internal sealed class EkPositionKonfiguration : IEntityTypeConfiguration<EkPosition>
{
    public void Configure(EntityTypeBuilder<EkPosition> builder)
    {
        // Eigene Tabelle in eigenem Schema: EK-Daten sind vom Vertrieb getrennt (Entscheidung 34).
        builder.ToTable("EkPositionen", "intern");
        builder.Property(e => e.EkLizenz).HasPrecision(12, 2);
        builder.Property(e => e.AufwandMinuten).HasPrecision(8, 2);
        builder.Property(e => e.BetriebFix).HasPrecision(12, 2);
        builder.Property(e => e.Overhead).HasPrecision(12, 2);
        builder.Property(e => e.KostenKorrektur).HasPrecision(12, 2);
        builder.Property(e => e.Anmerkung).HasMaxLength(1000);
        builder.HasIndex(e => new { e.PreislisteId, e.PreiskomponenteId }).IsUnique();
        builder.HasOne(e => e.Preiskomponente).WithMany().HasForeignKey(e => e.PreiskomponenteId).OnDelete(DeleteBehavior.Restrict);
    }
}
