using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Domain.Tests.Vertrag;

/// <summary>Fassungen einer Vorlage: aufnehmen, aktivieren, ablehnen (#26, Teil B).</summary>
public class DokumentVorlageTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly DokumentVorlage _vorlage = new() { Typ = DokumentTyp.Leistungsschein, Code = "S02", Bezeichnung = "S02", Version = "1.0" };

    private static Vorlagenversion Fassung(string hash, bool fehler = false, string? version = "1.0") => new()
    {
        Dateiname = $"Leistungsschein S02 - X V{version}.docx",
        Quelle = "Test",
        Pfad = "x",
        QuellId = "x",
        Sha256 = hash,
        AbgerufenVon = "Test",
        VersionLaut = version,
        Hinweise = fehler ? [new VorlagenHinweis(true, "kaputt")] : [],
    };

    [Fact]
    public void Gleicher_Inhalt_wird_nicht_erneut_aufgenommen()
    {
        Assert.NotNull(_vorlage.NimmAuf(Fassung("AA"), Jetzt));

        Assert.Null(_vorlage.NimmAuf(Fassung("aa"), Jetzt));
        Assert.Single(_vorlage.Versionen);
    }

    [Fact]
    public void Aktivieren_setzt_Version_und_loest_die_bisherige_ab()
    {
        var v1 = _vorlage.NimmAuf(Fassung("A1"), Jetzt)!;
        _vorlage.Aktiviere(v1, "pm", Jetzt, null);
        var v2 = _vorlage.NimmAuf(Fassung("A2", version: "1.1"), Jetzt)!;

        Assert.Equal(v1, _vorlage.AktiveVersion);
        _vorlage.Aktiviere(v2, "pm", Jetzt, "ok");

        Assert.Equal(v2, _vorlage.AktiveVersion);
        Assert.Equal((VorlagenStatus.Abgeloest, "abgelöst durch V2"), (v1.Status, v1.Kommentar));
        Assert.Equal(("1.1", "Leistungsschein S02 - X V1.1.docx"), (_vorlage.Version, _vorlage.Dateiname));
    }

    [Fact]
    public void Fehlerhafte_abgelehnte_oder_fremde_Fassungen_lassen_sich_nicht_aktivieren()
    {
        var kaputt = _vorlage.NimmAuf(Fassung("B1", fehler: true), Jetzt)!;
        Assert.Throws<InvalidOperationException>(() => _vorlage.Aktiviere(kaputt, "pm", Jetzt, null));

        _vorlage.Ablehnen(kaputt, "pm", Jetzt, "Platzhalter fehlen");
        Assert.Equal(VorlagenStatus.Abgelehnt, kaputt.Status);
        Assert.Throws<InvalidOperationException>(() => _vorlage.Ablehnen(kaputt, "pm", Jetzt, "nochmal"));

        var fremd = new DokumentVorlage { Typ = DokumentTyp.Avb, Code = "AVB", Bezeichnung = "AVB", Version = "" }.NimmAuf(Fassung("C1"), Jetzt)!;
        Assert.Throws<ArgumentException>(() => _vorlage.Aktiviere(fremd, "pm", Jetzt, null));
    }

    [Fact]
    public void Ohne_Versionsangabe_im_Namen_zeigt_die_Vorlage_die_laufende_Nummer()
    {
        var v1 = _vorlage.NimmAuf(Fassung("D1", version: null), Jetzt)!;

        _vorlage.Aktiviere(v1, "pm", Jetzt, null);

        Assert.Equal("V1", _vorlage.Version);
    }
}
