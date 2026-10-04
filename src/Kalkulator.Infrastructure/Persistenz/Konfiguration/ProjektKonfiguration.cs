using System.Text.Json;
using System.Text.Json.Serialization;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;
using Kalkulator.Domain.Projekte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kalkulator.Infrastructure.Persistenz.Konfiguration;

internal sealed class KundeKonfiguration : IEntityTypeConfiguration<Kunde>
{
    public void Configure(EntityTypeBuilder<Kunde> builder)
    {
        builder.ToTable("Kunden", "projekte");
        builder.Property(k => k.Firma).HasMaxLength(200);
        builder.Property(k => k.Strasse).HasMaxLength(200);
        builder.Property(k => k.Postleitzahl).HasMaxLength(10);
        builder.Property(k => k.Ort).HasMaxLength(100);
        builder.Property(k => k.Ansprechpartner).HasMaxLength(200);
        builder.Property(k => k.NavisionKundennummer).HasMaxLength(20);
        builder.HasIndex(k => k.NavisionKundennummer).IsUnique().HasFilter("[NavisionKundennummer] IS NOT NULL");
    }
}

internal sealed class KundenprojektKonfiguration : IEntityTypeConfiguration<Kundenprojekt>
{
    public void Configure(EntityTypeBuilder<Kundenprojekt> builder)
    {
        builder.ToTable("Kundenprojekte", "projekte", t =>
            t.HasCheckConstraint("CK_Kundenprojekte_Wahrscheinlichkeit", "[Wahrscheinlichkeit] BETWEEN 0 AND 100"));
        builder.Property(p => p.Titel).HasMaxLength(200);
        builder.Property(p => p.Verantwortlich).HasMaxLength(200);
        builder.Property(p => p.AngelegtVon).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Verlustgrund).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Zeilenversion).IsRowVersion();
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.Kunde).WithMany().HasForeignKey(p => p.KundeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.StatusEreignisse).WithOne().HasForeignKey(e => e.KundenprojektId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Kalkulationen).WithOne(k => k.Kundenprojekt).HasForeignKey(k => k.KundenprojektId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Angebot>().WithMany().HasForeignKey(p => p.AngenommenesAngebotId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StatusEreignisKonfiguration : IEntityTypeConfiguration<StatusEreignis>
{
    public void Configure(EntityTypeBuilder<StatusEreignis> builder)
    {
        builder.ToTable("StatusEreignisse", "projekte");
        builder.Property(e => e.Alt).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Neu).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Verlustgrund).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Benutzer).HasMaxLength(200);
        builder.Property(e => e.Kommentar).HasMaxLength(2000);
        builder.HasIndex(e => new { e.KundenprojektId, e.Zeitpunkt });
    }
}

internal sealed class KalkulationKonfiguration : IEntityTypeConfiguration<Kalkulation>
{
    public void Configure(EntityTypeBuilder<Kalkulation> builder)
    {
        builder.ToTable("Kalkulationen", "kalkulation");
        builder.Property(k => k.Titel).HasMaxLength(200);
        builder.Property(k => k.ErstelltVon).HasMaxLength(200);
        builder.Property(k => k.Angebotsnummer).HasMaxLength(20);
        builder.HasIndex(k => k.Angebotsnummer).IsUnique().HasFilter("[Angebotsnummer] IS NOT NULL");
        builder.Property(k => k.Eingabe).HasConversion(EingabeJson.Konverter, EingabeJson.Vergleich);
        builder.Property(k => k.Zeilenversion).IsRowVersion();

        // Genau eine Variante je Kundenprojekt zählt im Forecast (E-09).
        builder.HasIndex(k => k.KundenprojektId).IsUnique().HasFilter("[FuerForecast] = 1").HasDatabaseName("IX_Kalkulationen_Forecast");

        builder.HasMany(k => k.Sonderpositionen).WithOne().HasForeignKey(s => s.KalkulationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(k => k.Versionen).WithOne().HasForeignKey(v => v.KalkulationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(k => k.Vertriebsfreigaben).WithOne().HasForeignKey(f => f.KalkulationId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(k => k.IstVertriebsfreigegeben);
    }
}

internal sealed class VertriebsfreigabeKonfiguration : IEntityTypeConfiguration<Vertriebsfreigabe>
{
    public void Configure(EntityTypeBuilder<Vertriebsfreigabe> builder)
    {
        builder.ToTable("Vertriebsfreigaben", "kalkulation");
        builder.Property(f => f.Rolle).HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.Benutzer).HasMaxLength(200);
        builder.Property(f => f.Kommentar).HasMaxLength(2000);
        builder.Property(f => f.AufgehobenVon).HasMaxLength(200);
        builder.Property(f => f.Aufhebungsgrund).HasMaxLength(500);
        builder.Ignore(f => f.IstAktiv);

        // Je Rolle höchstens eine aktive Freigabe.
        builder.HasIndex(f => new { f.KalkulationId, f.Rolle }).IsUnique().HasFilter("[AufgehobenAm] IS NULL").HasDatabaseName("IX_Vertriebsfreigaben_Aktiv");
    }
}

internal sealed class SonderpositionKonfiguration : IEntityTypeConfiguration<Sonderposition>
{
    public void Configure(EntityTypeBuilder<Sonderposition> builder)
    {
        builder.ToTable("Sonderpositionen", "kalkulation");
        builder.Property(s => s.Bezeichnung).HasMaxLength(200);
        builder.Property(s => s.Einheit).HasMaxLength(50);
        builder.Property(s => s.Preis).HasPrecision(12, 2);
        builder.Property(s => s.Begruendung).HasMaxLength(2000);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.EntschiedenVon).HasMaxLength(200);
        builder.Property(s => s.Kommentar).HasMaxLength(2000);
    }
}

internal sealed class KalkulationsversionKonfiguration : IEntityTypeConfiguration<Kalkulationsversion>
{
    public void Configure(EntityTypeBuilder<Kalkulationsversion> builder)
    {
        builder.ToTable("Kalkulationsversionen", "kalkulation");
        builder.HasIndex(v => new { v.KalkulationId, v.Nummer }).IsUnique();
        builder.Property(v => v.ErstelltVon).HasMaxLength(200);
        builder.Property(v => v.Eingabe).HasConversion(EingabeJson.Konverter, EingabeJson.Vergleich);
        builder.Property(v => v.SummeMonatlich).HasPrecision(12, 2);
        builder.Property(v => v.SummeEinmalig).HasPrecision(12, 2);
        builder.Property(v => v.FreigabeVertriebsleitungVon).HasMaxLength(200);
        builder.Property(v => v.FreigabeSolutionConsultantVon).HasMaxLength(200);
        builder.Ignore(v => v.Bezeichnung);
        builder.Ignore(v => v.Jahreswert);
        builder.Ignore(v => v.WertErstlaufzeit);

        builder.HasOne<Preisliste>().WithMany().HasForeignKey(v => v.PreislisteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(v => v.Positionen).WithOne().HasForeignKey(p => p.KalkulationsversionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VersionsPositionKonfiguration : IEntityTypeConfiguration<VersionsPosition>
{
    public void Configure(EntityTypeBuilder<VersionsPosition> builder)
    {
        builder.ToTable("VersionsPositionen", "kalkulation");
        builder.HasIndex(p => new { p.KalkulationsversionId, p.Reihenfolge }).IsUnique();
        builder.HasIndex(p => p.ServiceCode);
        builder.Property(p => p.Code).HasMaxLength(30);
        builder.Property(p => p.Bezeichnung).HasMaxLength(200);
        builder.Property(p => p.ServiceCode).HasMaxLength(20);
        builder.Property(p => p.Menge).HasPrecision(12, 2);
        builder.Property(p => p.BerechneteMenge).HasPrecision(12, 2);
        builder.Property(p => p.Einzelpreis).HasPrecision(12, 2);
        builder.Property(p => p.Betrag).HasPrecision(12, 2);
        builder.Property(p => p.Abrechnungsart).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Herkunft).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Hinweis).HasMaxLength(500);
        builder.HasOne(p => p.Kosten).WithOne().HasForeignKey<PositionsKosten>(k => k.VersionsPositionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PositionsKostenKonfiguration : IEntityTypeConfiguration<PositionsKosten>
{
    public void Configure(EntityTypeBuilder<PositionsKosten> builder)
    {
        // Wie die EK-Positionen im Schema „intern“, getrennt von den Vertriebsdaten (Designprinzip 4).
        builder.ToTable("PositionsKosten", "intern");
        builder.HasKey(k => k.VersionsPositionId);
        builder.Property(k => k.Kosten).HasPrecision(12, 2);
    }
}

internal sealed class AngebotKonfiguration : IEntityTypeConfiguration<Angebot>
{
    public void Configure(EntityTypeBuilder<Angebot> builder)
    {
        builder.ToTable("Angebote", "kalkulation");
        builder.HasIndex(a => a.KalkulationsversionId).IsUnique();
        builder.HasIndex(a => a.Nummer);
        builder.Property(a => a.Nummer).HasMaxLength(20);
        builder.Property(a => a.Freitext).HasMaxLength(4000);
        builder.Property(a => a.Dateiname).HasMaxLength(260);
        builder.Property(a => a.Vorlage).HasMaxLength(260);
        builder.Property(a => a.ErstelltVon).HasMaxLength(200);
        builder.Property(a => a.VersendetVon).HasMaxLength(200);
        builder.HasOne(a => a.Version).WithMany().HasForeignKey(a => a.KalkulationsversionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Datei).WithOne().HasForeignKey<AngebotsDatei>(d => d.AngebotId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AngebotsDateiKonfiguration : IEntityTypeConfiguration<AngebotsDatei>
{
    public void Configure(EntityTypeBuilder<AngebotsDatei> builder)
    {
        builder.ToTable("AngebotsDateien", "kalkulation");
        builder.HasKey(d => d.AngebotId);
    }
}

internal sealed class NummernkreisKonfiguration : IEntityTypeConfiguration<Nummernkreis>
{
    public void Configure(EntityTypeBuilder<Nummernkreis> builder)
    {
        builder.ToTable("Nummernkreise", "projekte");
        builder.HasKey(n => new { n.Kreis, n.Jahr });
        builder.Property(n => n.Kreis).HasMaxLength(10);
    }
}

/// <summary>Speichert die Kalkulationseingabe als JSON; Aufzählungen als Text, damit die Spalte lesbar bleibt.</summary>
internal static class EingabeJson
{
    private static readonly JsonSerializerOptions Optionen = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Die Eingabe so, wie sie gespeichert wird; z. B. um Arbeitsstände zu vergleichen.</summary>
    public static string Text(KalkulationsEingabe eingabe) => JsonSerializer.Serialize(eingabe, Optionen);

    public static readonly ValueConverter<KalkulationsEingabe, string> Konverter = new(
        e => JsonSerializer.Serialize(e, Optionen),
        s => JsonSerializer.Deserialize<KalkulationsEingabe>(s, Optionen)!);

    public static readonly ValueComparer<KalkulationsEingabe> Vergleich = new(
        (a, b) => JsonSerializer.Serialize(a, Optionen) == JsonSerializer.Serialize(b, Optionen),
        e => JsonSerializer.Serialize(e, Optionen).GetHashCode(StringComparison.Ordinal),
        e => JsonSerializer.Deserialize<KalkulationsEingabe>(JsonSerializer.Serialize(e, Optionen), Optionen)!);
}
