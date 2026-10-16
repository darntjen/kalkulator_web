using Kalkulator.Domain.Projekte;

namespace Kalkulator.Domain.Tests.Projekte;

public class AngebotsstatusTests
{
    private static readonly DateOnly Heute = new(2026, 10, 8);
    private static readonly DateOnly Gestern = Heute.AddDays(-1);

    [Theory]
    [InlineData(false, false, false, false, 10, AngebotsStatus.Erzeugt)]
    [InlineData(false, false, false, true, 10, AngebotsStatus.Versendet)]
    [InlineData(false, false, false, true, 0, AngebotsStatus.Versendet)]
    [InlineData(false, false, false, true, -1, AngebotsStatus.Abgelaufen)]
    [InlineData(false, false, false, false, -1, AngebotsStatus.Erzeugt)]
    [InlineData(false, false, true, true, -1, AngebotsStatus.Ersetzt)]
    [InlineData(false, true, true, true, 10, AngebotsStatus.NichtAngenommen)]
    [InlineData(true, true, false, true, -1, AngebotsStatus.Angenommen)]
    public void Status_folgt_dem_Vorrang(bool angenommen, bool abgeschlossen, bool neuere, bool versendet, int gueltigTage, AngebotsStatus erwartet)
    {
        var status = Angebotsstatus.Bestimme(angenommen, abgeschlossen, neuere, versendet ? Gestern : null, Heute.AddDays(gueltigTage), Heute);

        Assert.Equal(erwartet, status);
    }
}
