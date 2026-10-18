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

    private static Vertragswerk VonVertriebsleitungFreigegeben()
    {
        var werk = NeuesWerk();
        werk.Pruefe(VertragsfreigabeArt.Vertriebsleitung, true, null, "vl@noesse.de", Jetzt);
        return werk;
    }

    [Fact]
    public void Zuerst_die_Vertriebsleitung_danach_AVV_und_Technik()
    {
        var werk = NeuesWerk();

        Assert.True(werk.IstVorgelegt(VertragsfreigabeArt.Vertriebsleitung));
        Assert.False(werk.IstVorgelegt(VertragsfreigabeArt.Avv));
        Assert.False(werk.IstVorgelegt(VertragsfreigabeArt.Technik));
        var fehler = Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv@noesse.de", Jetzt));
        Assert.Contains("Vertriebsleitung", fehler.Message, StringComparison.Ordinal);
        Assert.Empty(werk.Freigaben);

        werk.Pruefe(VertragsfreigabeArt.Vertriebsleitung, true, null, "vl@noesse.de", Jetzt);

        Assert.True(werk.IstVorgelegt(VertragsfreigabeArt.Avv));
        Assert.True(werk.IstVorgelegt(VertragsfreigabeArt.Technik));
        Assert.False(werk.IstFreigegeben);
    }

    [Fact]
    public void Freigegeben_erst_mit_allen_drei_AVV_und_Technik_in_beliebiger_Reihenfolge()
    {
        var werk = VonVertriebsleitungFreigegeben();

        werk.Pruefe(VertragsfreigabeArt.Technik, true, "  ", "technik@noesse.de", Jetzt);
        Assert.False(werk.IstFreigegeben);
        Assert.Null(werk.Freigabe(VertragsfreigabeArt.Technik)!.Begruendung);

        werk.Pruefe(VertragsfreigabeArt.Avv, true, " passt ", "avv@noesse.de", Jetzt);
        Assert.True(werk.IstFreigegeben);
        Assert.False(werk.IstAbgelehnt);
        Assert.Equal("passt", werk.Freigabe(VertragsfreigabeArt.Avv)!.Begruendung);
    }

    [Fact]
    public void Ohne_Vertriebsleitung_nicht_freigegeben()
    {
        var werk = NeuesWerk();
        werk.Freigaben.Add(new Vertragsfreigabe { Art = VertragsfreigabeArt.Avv, Erteilt = true, Von = "avv@noesse.de", Am = Jetzt });
        werk.Freigaben.Add(new Vertragsfreigabe { Art = VertragsfreigabeArt.Technik, Erteilt = true, Von = "technik@noesse.de", Am = Jetzt });

        Assert.False(werk.IstFreigegeben);
    }

    [Fact]
    public void Erzeugt_die_Vertriebsleitung_selbst_entfaellt_ihre_Pruefung()
    {
        var werk = NeuesWerk();

        werk.GibFreigabeBeimErzeugen(Jetzt);

        var vl = werk.Freigabe(VertragsfreigabeArt.Vertriebsleitung)!;
        Assert.True(vl.Erteilt);
        Assert.Equal("vertrieb@noesse.de", vl.Von);
        Assert.Equal(Vertragswerk.BeimErzeugen, vl.Begruendung);
        Assert.True(werk.IstVorgelegt(VertragsfreigabeArt.Avv));
        Assert.True(werk.IstVorgelegt(VertragsfreigabeArt.Technik));
    }

    [Fact]
    public void Ablehnung_der_Vertriebsleitung_sperrt_die_Ausfertigung()
    {
        var werk = NeuesWerk();

        werk.Pruefe(VertragsfreigabeArt.Vertriebsleitung, false, "Laufzeit falsch", "vl@noesse.de", Jetzt);

        Assert.True(werk.IstAbgelehnt);
        Assert.False(werk.IstVorgelegt(VertragsfreigabeArt.Avv));
        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv@noesse.de", Jetzt));
    }

    [Fact]
    public void Ablehnung_braucht_Begruendung_und_sperrt_die_Ausfertigung()
    {
        var werk = VonVertriebsleitungFreigegeben();

        Assert.Throws<ArgumentException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, false, " ", "avv@noesse.de", Jetzt));
        Assert.Throws<ArgumentException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, false, new string('x', 1001), "avv@noesse.de", Jetzt));
        Assert.Single(werk.Freigaben);

        werk.Pruefe(VertragsfreigabeArt.Avv, false, "Unterauftragnehmer fehlt", "avv@noesse.de", Jetzt);

        Assert.True(werk.IstAbgelehnt);
        Assert.False(werk.IstFreigegeben);
        var fehler = Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Technik, true, null, "technik@noesse.de", Jetzt));
        Assert.Contains("abgelehnt", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Jede_Pruefung_nur_einmal_und_nicht_nach_der_Uebergabe()
    {
        var werk = VonVertriebsleitungFreigegeben();
        werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv@noesse.de", Jetzt);

        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Avv, true, null, "avv2@noesse.de", Jetzt));
        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Vertriebsleitung, true, null, "vl2@noesse.de", Jetzt));
        Assert.Throws<ArgumentOutOfRangeException>(() => werk.Pruefe((VertragsfreigabeArt)9, true, null, "x@noesse.de", Jetzt));

        werk.VermerkeUebergabe("101", Jetzt);
        Assert.Throws<InvalidOperationException>(() => werk.Pruefe(VertragsfreigabeArt.Technik, true, null, "technik@noesse.de", Jetzt));
    }

    [Fact]
    public void Status_erst_nach_Uebergabe_Unterzeichnung_einmalig()
    {
        var werk = NeuesWerk();
        Assert.Throws<InvalidOperationException>(() => werk.VermerkeStatus("dispatched", false, Jetzt));

        werk.VermerkeUebergabe("101", Jetzt);
        werk.VermerkeStatus("dispatched", false, Jetzt.AddHours(1));
        Assert.Equal(("dispatched", Jetzt.AddHours(1)), (werk.PaperlessStatus, werk.StatusGeprueftAm));
        Assert.False(werk.IstUnterzeichnet);
        Assert.Throws<InvalidOperationException>(() => werk.HinterlegeUnterschrift([1], null, Jetzt));

        werk.VermerkeStatus("completed", true, Jetzt.AddHours(2));
        Assert.Equal(Jetzt.AddHours(2), werk.UnterzeichnetAm);

        // Nach der Unterzeichnung ändert eine weitere Abfrage nichts mehr.
        werk.VermerkeStatus("anders", false, Jetzt.AddHours(3));
        Assert.Equal(("completed", Jetzt.AddHours(2)), (werk.PaperlessStatus, werk.UnterzeichnetAm));
    }

    [Fact]
    public void Unterschriebener_Vertrag_wird_einmal_abgelegt()
    {
        var werk = NeuesWerk();
        werk.VermerkeUebergabe("101", Jetzt);
        werk.VermerkeStatus("completed", true, Jetzt);

        Assert.Throws<ArgumentException>(() => werk.HinterlegeUnterschrift([], null, Jetzt));
        werk.HinterlegeUnterschrift([1, 2], [], Jetzt);

        Assert.Equal([1, 2], werk.Unterschrift!.Pdf);
        Assert.Null(werk.Unterschrift.Pruefprotokoll);
        Assert.Throws<InvalidOperationException>(() => werk.HinterlegeUnterschrift([3], null, Jetzt));
    }
    [Fact]
    public void Archiviert_wird_nur_eine_nicht_unterzeichnete_Ausfertigung_und_nur_einmal()
    {
        var werk = NeuesWerk();
        Assert.Throws<ArgumentException>(() => werk.Archiviere("", "zip", Jetzt));
        Assert.Throws<ArgumentException>(() => werk.Archiviere(new string('x', Angebot.LinkLaenge + 1), "zip", Jetzt));

        werk.Archiviere("https://sp/pdf", "https://sp/zip", Jetzt);
        Assert.Equal(("https://sp/pdf", "https://sp/zip", (DateTimeOffset?)Jetzt), (werk.ArchivLink, werk.ArchivZipLink, werk.ArchiviertAm));
        Assert.Throws<InvalidOperationException>(() => werk.Archiviere("https://sp/pdf", "https://sp/zip", Jetzt));

        var unterzeichnet = NeuesWerk();
        unterzeichnet.VermerkeUebergabe("101", Jetzt);
        unterzeichnet.VermerkeStatus("completed", true, Jetzt);
        Assert.Throws<InvalidOperationException>(() => unterzeichnet.Archiviere("https://sp/pdf", "https://sp/zip", Jetzt));
    }

    [Fact]
    public void Angebot_wird_einmal_archiviert()
    {
        var angebot = new Angebot { Nummer = "MS-A-2026-0001", Dateiname = "a.docx", Vorlage = "v.docx", ErstelltVon = "t" };
        Assert.Throws<ArgumentException>(() => angebot.Archiviere(" ", Jetzt));

        angebot.Archiviere("https://sp/a.docx", Jetzt);
        Assert.True(angebot.IstArchiviert);
        Assert.Equal("https://sp/a.docx", angebot.ArchivLink);
        Assert.Throws<InvalidOperationException>(() => angebot.Archiviere("https://sp/b.docx", Jetzt));
    }
}
