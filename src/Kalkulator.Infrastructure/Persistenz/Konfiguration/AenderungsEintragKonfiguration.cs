using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kalkulator.Infrastructure.Persistenz.Konfiguration;

internal sealed class AenderungsEintragKonfiguration : IEntityTypeConfiguration<AenderungsEintrag>
{
    public void Configure(EntityTypeBuilder<AenderungsEintrag> builder)
    {
        builder.ToTable("Aenderungsprotokoll", "protokoll");
        builder.Property(a => a.Benutzer).HasMaxLength(200);
        builder.Property(a => a.Entitaet).HasMaxLength(100);
        builder.Property(a => a.Schluessel).HasMaxLength(100);
        builder.Property(a => a.Aktion).HasMaxLength(20);
        builder.HasIndex(a => new { a.Entitaet, a.Schluessel });
        builder.HasIndex(a => a.Zeitpunkt);
    }
}
