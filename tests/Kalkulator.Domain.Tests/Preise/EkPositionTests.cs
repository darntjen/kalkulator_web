using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Tests.Preise;

// Erwartete Werte aus „Kalkulation EK und Deckungsbeitrag V1.0.xlsx“ (EK-Kostensatz 60,75 €/h).
public class EkPositionTests
{
    private const decimal Kostensatz = 60.75m;

    [Theory]
    [InlineData("Connect Standard", 0, 75, 0, 75.94)]
    [InlineData("Connect Premium", 0, 240, 5, 248.00)]
    [InlineData("S02 Endpoint Protection", 3.59, 1, 1.50, 6.10)]
    [InlineData("S03 Patch Management", 1.50, 2, 1.50, 5.03)]
    [InlineData("S21 Firewall", 0, 35, 5, 40.44)]
    [InlineData("S24 ITSB", 0, 300, 50, 353.75)]
    public void Kosten_nach_Aufwand_entsprechen_der_Excel_Kalkulation(string position, decimal ek, decimal minuten, decimal overhead, decimal erwartet)
    {
        var ekPosition = new EkPosition { EkLizenz = ek, AufwandMinuten = minuten, Overhead = overhead, Anmerkung = position };

        Assert.Equal(erwartet, ekPosition.Kosten(Kostensatz));
    }

    [Fact]
    public void Kosten_mit_festem_Betriebsaufwand_MDR_User()
    {
        var mdr = new EkPosition { EkLizenz = 11.66m, BetriebFix = 20m };

        Assert.Equal(31.66m, mdr.Kosten(Kostensatz));
    }

    [Fact]
    public void Kostenkorrektur_wird_beruecksichtigt()
    {
        var position = new EkPosition { EkLizenz = 1.50m, AufwandMinuten = 1, Overhead = 1.50m, KostenKorrektur = -1.50m };

        Assert.Equal(2.51m, position.Kosten(Kostensatz));
    }
}
