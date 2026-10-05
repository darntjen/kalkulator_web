using Kalkulator.Domain.Projekte;

namespace Kalkulator.Domain.Tests.Projekte;

public class VertragswerkTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static Vertragswerk NeuesWerk() => new()
    {
        Nummer = "A-2026-001",
        Ausfertigung = 1,
        ErstelltVon = "vertrieb@noesse.de",
        ErstelltAm = Jetzt,
        GesamtDateiname = "Vertragswerk.pdf",
        ZipDateiname = "Vertragswerk.zip",
    };

    [Fact]
    public void Freigegeben_erst_mit_AVV_und_Technik_in_beliebiger_Reihenfolge()
    {
        var werk = NeuesWerk();

        werk.Pruefe(VertragsfreigabeArt.Technik, true, "  ", "technik@noesse.de", Jetzt);
        Assert.False(werk.IstFreigegeben);
        Assert.Null(werk.Freigabe(VertragsfreigabeArt.Technik)!.Begruendung);

        werk.Pruefe(VertragsfreigabeArt.Avv, true, " passt ", "avv@noesse.de", Jetzt);
        Assert.True(werk.IstFreigegeben);
        Assert.False(werk.IstAbgelehnt);
        Assert.Equal("passt", werk.Freigabe(VertragsfreigabeArt.Avv)!.Begruendung);
    }

    [Fact]
    public void Ablehnung_braucht_Begruendung_und_sperrt_die_Ausfertigung()
    {
        var werk = NeuesWerk();

        Assert.Throws<ArgumentException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, false, " ", "avv@noesse.de", Jetzt));
        Assert.Throws<ArgumentException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, false, new string('x', 1001), "avv@noesse.de", Jetzt));
        Assert.Empty(werk.Freigaben);

        werk.Pruefe(VertragsfreigabeArt.Avv, false, "Unterauftragnehmer fehlt", "avv@noesse.de", Jetzt);

        Assert.True(werk.IstAbgelehnt);
        Assert.False(werk.IstFreigegeben);
        var fehler = Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Technik, true, null, "technik@noesse.de", Jetzt));
        Assert.Contains("abgelehnt", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Jede_Pruefung_nur_einmal_und_nicht_nach_der_Uebergabe()
    {
        var werk = NeuesWerk();
        werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv@noesse.de", Jetzt);

        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv2@noesse.de", Jetzt));
        Assert.Throws<ArgumentOutOfRangeException>(() => werk.Pruefe((VertragsfreigabeArt)9, true, null, "x@noesse.de", Jetzt));

        werk.VermerkeUebergabe("101", Jetzt);
        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Technik, true, null, "technik@noesse.de", Jetzt));
    }
}
