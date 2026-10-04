using System.Text;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Unterlagen am Kundenprojekt (Phase 4): Kanalordner, Uploads, Rechte und Aufbewahrungsfrist.</summary>
[Collection(DatenbankSammlung.Name)]
public sealed class UnterlagenDienstTests(SqlServerFixture db) : IDisposable
{
    private readonly string _ablage = Directory.CreateTempSubdirectory("kundenablage").FullName;

    public void Dispose() => Directory.Delete(_ablage, recursive: true);

    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;

        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private UnterlagenDienst Unterlagen(TestBenutzer benutzer, TimeProvider? zeit = null, IKundenablage? ablage = null) =>
        new(new Fabrik(() => db.NeuerKontext(benutzer, zeit)), benutzer, zeit ?? TimeProvider.System, ablage ?? new OrdnerKundenablage(_ablage),
            Options.Create(new KundenablageEinstellungen()));

    private KundenprojektDienst Projekte(TestBenutzer benutzer, TimeProvider? zeit = null) =>
        new(new Fabrik(() => db.NeuerKontext(benutzer, zeit)), benutzer, zeit ?? TimeProvider.System);

    private static Task<int> AnlegenAsync(KundenprojektDienst projekte, string firma) =>
        projekte.AnlegenAsync(new NeuesKundenprojekt(firma, null, null, null, null, null, "Managed Services", false, null, null));

    /// <summary>Kanalordner mit den Ordnern des Nösse-Standards und je einer Datei.</summary>
    private void Kanal(string name, params string[] dateien)
    {
        foreach (var datei in dateien)
        {
            var pfad = Path.Combine(_ablage, name, datei);
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.WriteAllText(pfad, "Inhalt " + datei);
        }
    }

    [Fact]
    public void Vorschlag_nur_bei_eindeutigem_Kanal_auch_fuer_gekuerzte_Namen()
    {
        Kanalordner[] kanaele =
        [
            new("1", "Muster Spedition GmbH"),
            new("2", "AAV - Verband für Flächenrecycling und Altlasten"),
            new("3", "Muster Spedition GmbH & Co. KG"),
        ];

        Assert.Equal("1", Kundenablage.Vorschlag("muster spedition gmbh", kanaele)?.Id);
        Assert.Equal("2", Kundenablage.Vorschlag("AAV - Verband für Flächenrecycling und Altlastensanierung", kanaele)?.Id);
        Assert.Null(Kundenablage.Vorschlag("Muster", kanaele));
    }

    [Fact]
    public async Task Ordnerablage_liest_nur_die_Bereiche_des_Standards_und_bleibt_im_Kanal()
    {
        Kanal("Muster Spedition GmbH",
            "00_Kundenakte/Steckbrief_v01.md",
            "30_Analyse/Analyse.pdf",
            "80_Protokolle/_Rohtranskripte/2026-08-12.vtt",
            "50_Umsetzung/Plan.docx",
            "70_Vertraege/Vertrag.pdf",
            "99_Archiv/Alt.pdf");
        Kanal("Andere GmbH", "30_Analyse/Fremd.pdf");
        var ablage = new OrdnerKundenablage(_ablage);

        Assert.Equal(["Andere GmbH", "Muster Spedition GmbH"], (await ablage.KanaeleAsync(default)).Select(k => k.Name));
        var dateien = await ablage.DateienAsync("Muster Spedition GmbH", default);
        Assert.Equal(["00_Kundenakte/Steckbrief_v01.md", "30_Analyse/Analyse.pdf", "80_Protokolle/_Rohtranskripte/2026-08-12.vtt"], dateien.Select(d => d.Pfad));
        Assert.Equal("80_Protokolle", dateien[2].Bereich);

        var (name, inhalt) = await ablage.LadeAsync("Muster Spedition GmbH", "30_Analyse/Analyse.pdf", default);
        Assert.Equal(("Analyse.pdf", "Inhalt 30_Analyse/Analyse.pdf"), (name, Encoding.UTF8.GetString(inhalt)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.LadeAsync("Muster Spedition GmbH", "70_Vertraege/Vertrag.pdf", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.LadeAsync("Muster Spedition GmbH", "../Andere GmbH/30_Analyse/Fremd.pdf", default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ablage.DateienAsync("../", default));
    }

    [Fact]
    public async Task Kanalordner_verknuepfen_mit_Vorschlag_und_Dateien_anzeigen()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var firma = "Kanal GmbH " + Guid.NewGuid().ToString("N")[..6];
        Kanal(firma, "30_Analyse/Analyse.pdf");
        Kanal("Andere GmbH", "30_Analyse/Fremd.pdf");
        var id = await AnlegenAsync(Projekte(vertrieb), firma);
        var dienst = Unterlagen(vertrieb);

        var auswahl = await dienst.KanaeleAsync(id);
        Assert.Equal(firma, auswahl.Vorschlag?.Name);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => dienst.VerknuepfenAsync(id, "gibt es nicht"));
        await dienst.VerknuepfenAsync(id, auswahl.Vorschlag!.Id);

        Assert.Equal(firma, (await dienst.UebersichtAsync(id)).Kanalordner);
        Assert.Equal(["Analyse.pdf"], (await dienst.AblageDateienAsync(id)).Select(d => d.Name));
        Assert.Equal("Analyse.pdf", (await dienst.AblageDateiAsync(id, "30_Analyse/Analyse.pdf")).Name);

        // Ein Consultant darf verknüpfen und hochladen, ein fremder Vertrieb nicht einmal sehen.
        var consultant = Unterlagen(Neu(Rollen.Consultant));
        Assert.True((await consultant.UebersichtAsync(id)).DarfPflegen);
        var fremd = Unterlagen(Neu(Rollen.Vertrieb));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.AblageDateienAsync(id));
        var fuehrung = Unterlagen(Neu(Rollen.Fuehrung));
        Assert.False((await fuehrung.UebersichtAsync(id)).DarfPflegen);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.VerknuepfenAsync(id, null));
    }

    [Fact]
    public async Task Ohne_Kundenablage_bleibt_der_Upload_moeglich()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var id = await AnlegenAsync(Projekte(vertrieb), "Ohne Teams GmbH");
        var dienst = Unterlagen(vertrieb, ablage: new KeineKundenablage());

        var uebersicht = await dienst.UebersichtAsync(id);
        Assert.False(uebersicht.AblageEingerichtet);
        Assert.Contains("nicht eingerichtet", uebersicht.AblageGrund, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.KanaeleAsync(id));
        Assert.Empty(await dienst.AblageDateienAsync(id));
        await dienst.HochladenAsync(id, UnterlagenArt.Recherche, "Recherche.md", Encoding.UTF8.GetBytes("# Recherche"), null);
    }

    [Fact]
    public async Task Upload_pruefen_herunterladen_und_loeschen()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var id = await AnlegenAsync(Projekte(vertrieb), "Upload GmbH");
        var dienst = Unterlagen(vertrieb);

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.HochladenAsync(id, UnterlagenArt.Analyse, "virus.exe", [1, 2], null));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.HochladenAsync(id, UnterlagenArt.Analyse, "leer.pdf", [], null));

        var consultant = Neu(Rollen.Consultant);
        var upload = await Unterlagen(consultant).HochladenAsync(id, UnterlagenArt.Analyse, @"C:\Temp\Analyse 2026.pdf", [37, 80, 68, 70], "  Bewertungsmatrix  ");

        var zeile = Assert.Single((await dienst.UebersichtAsync(id)).Uploads);
        Assert.Equal((UnterlagenArt.Analyse, "Analyse 2026.pdf", 4L, "Bewertungsmatrix", consultant.Name), (zeile.Art, zeile.Dateiname, zeile.Groesse, zeile.Beschreibung, zeile.HochgeladenVon));
        var (name, typ, inhalt) = await dienst.DateiAsync(upload);
        Assert.Equal(("Analyse 2026.pdf", "application/pdf"), (name, typ));
        Assert.Equal([37, 80, 68, 70], inhalt);

        await Assert.ThrowsAsync<KeinZugriffException>(() => Unterlagen(Neu(Rollen.Vertrieb)).DateiAsync(upload));
        await Assert.ThrowsAsync<KeinZugriffException>(() => Unterlagen(Neu(Rollen.Fuehrung)).LoeschenAsync(upload));
        await dienst.LoeschenAsync(upload);
        Assert.Empty((await dienst.UebersichtAsync(id)).Uploads);
        await using var kontext = db.NeuerKontext();
        Assert.False(await kontext.Set<UnterlageDatei>().AnyAsync(d => d.UnterlageId == upload));
    }

    [Fact]
    public async Task Uploads_abgeschlossener_Projekte_werden_nach_drei_Jahren_geloescht()
    {
        var uhr = new Uhr(new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        var vertrieb = Neu(Rollen.Vertrieb);
        var projekte = Projekte(vertrieb, uhr);
        var verloren = await AnlegenAsync(projekte, "Verloren GmbH");
        var offen = await AnlegenAsync(projekte, "Offen GmbH");
        var dienst = Unterlagen(vertrieb, uhr);
        await dienst.HochladenAsync(verloren, UnterlagenArt.Analyse, "a.pdf", [1], null);
        await dienst.HochladenAsync(offen, UnterlagenArt.Analyse, "b.pdf", [1], null);
        await projekte.SetzeStatusAsync(verloren, ProjektStatus.AngebotVersendet, null, null);
        await projekte.SetzeStatusAsync(verloren, ProjektStatus.Verloren, "anderer Anbieter", Verlustgrund.Preis);

        Assert.Equal(new DateOnly(2029, 1, 10), (await dienst.UebersichtAsync(verloren)).LoeschungAm);
        Assert.Null((await dienst.UebersichtAsync(offen)).LoeschungAm);

        uhr.Jetzt = new DateTimeOffset(2029, 1, 9, 12, 0, 0, TimeSpan.Zero);
        await dienst.AbgelaufeneLoeschenAsync(default);
        Assert.Single((await dienst.UebersichtAsync(verloren)).Uploads);

        uhr.Jetzt = new DateTimeOffset(2029, 1, 10, 3, 15, 0, TimeSpan.Zero);
        var geloescht = await new UnterlagenDienst(new Fabrik(() => db.NeuerKontext(new Vorlagen.Hintergrundbenutzer(UnterlagenAufbewahrung.Benutzer), uhr)),
            new Vorlagen.Hintergrundbenutzer(UnterlagenAufbewahrung.Benutzer), uhr, new KeineKundenablage(), Options.Create(new KundenablageEinstellungen()))
            .AbgelaufeneLoeschenAsync(default);

        Assert.True(geloescht >= 1);
        Assert.Empty((await dienst.UebersichtAsync(verloren)).Uploads);
        Assert.Single((await dienst.UebersichtAsync(offen)).Uploads);
    }
}
