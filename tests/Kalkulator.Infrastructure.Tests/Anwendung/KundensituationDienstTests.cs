using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Kundensituation (G-03), Verknüpfung mit den Services (G-04) und Analyse-/Workshop-Angebote (J-01).</summary>
[Collection(DatenbankSammlung.Name)]
public class KundensituationDienstTests(SqlServerFixture db)
{
    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private KundensituationDienst Situation(TestBenutzer b) => new(new Fabrik(() => db.NeuerKontext(b)), b, TimeProvider.System);

    private KundenprojektDienst Projekte(TestBenutzer b) => new(new Fabrik(() => db.NeuerKontext(b)), b, TimeProvider.System);

    private KalkulationsDienst Kalkulationen(TestBenutzer b) => new(new Fabrik(() => db.NeuerKontext(b)), b, TimeProvider.System);

    private static HerausforderungsDaten Daten(string titel, Dimension dimension = Dimension.Technisch, Prioritaet prioritaet = Prioritaet.Hoch) =>
        new(dimension, titel, "Ob die Sicherung zurückkommt, ist nicht nachgewiesen.", "Höchstens fünf Tage Ausfall tragbar", prioritaet, "Infrastruktur-Analyse vom 12.08.2026");

    private static Task<int> ProjektAsync(KundenprojektDienst projekte) =>
        projekte.AnlegenAsync(new NeuesKundenprojekt("Situation GmbH", null, null, null, null, null, "Managed Services", false, null, null));

    [Fact]
    public async Task Herausforderungen_pflegen_Vertrieb_und_Consultant_nicht_Fuehrung()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var projekt = await ProjektAsync(Projekte(vertrieb));
        var dienst = Situation(vertrieb);

        var id = await dienst.AnlegenAsync(projekt, Daten("  Datensicherung nicht nachweislich wiederherstellbar  "));
        await Situation(Neu(Rollen.Consultant)).AnlegenAsync(projekt, Daten("Kein zentrales Update-Management", prioritaet: Prioritaet.Mittel));
        await dienst.AnlegenAsync(projekt, Daten("IT-Budget soll planbar werden", Dimension.Kaufmaennisch, Prioritaet.Niedrig));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.AnlegenAsync(projekt, Daten("  ")));
        await Assert.ThrowsAsync<KeinZugriffException>(() => Situation(Neu(Rollen.Fuehrung)).AnlegenAsync(projekt, Daten("x")));
        await Assert.ThrowsAsync<KeinZugriffException>(() => Situation(Neu(Rollen.Vertrieb)).LadeAsync(projekt));

        var situation = await dienst.LadeAsync(projekt);
        Assert.True(situation.DarfPflegen);
        Assert.False((await Situation(Neu(Rollen.Fuehrung)).LadeAsync(projekt)).DarfPflegen);
        Assert.Equal(
            ["IT-Budget soll planbar werden", "Datensicherung nicht nachweislich wiederherstellbar", "Kein zentrales Update-Management"],
            situation.Herausforderungen.Select(h => h.Titel));

        await dienst.AendernAsync(id, Daten("Datensicherung nicht nachgewiesen", prioritaet: Prioritaet.Mittel) with { Quelle = null });
        var geaendert = (await dienst.LadeAsync(projekt)).Herausforderungen.Single(h => h.Id == id);
        Assert.Equal(("Datensicherung nicht nachgewiesen", Prioritaet.Mittel, (string?)null, vertrieb.Name), (geaendert.Titel, geaendert.Prioritaet, geaendert.Quelle, geaendert.GeaendertVon));
    }

    [Fact]
    public async Task Verknuepfung_gehoert_zum_Stand_und_sperrt_das_Loeschen()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var projekte = Projekte(vertrieb);
        var projekt = await ProjektAsync(projekte);
        var kalkulationId = await projekte.NeueKalkulationAsync(projekt, "Variante A");
        var situation = Situation(vertrieb);
        var sicherung = await situation.AnlegenAsync(projekt, Daten("Datensicherung nicht nachgewiesen"));
        var fremd = await Situation(vertrieb).AnlegenAsync(await ProjektAsync(projekte), Daten("Fremdes Projekt"));
        var kalkulationen = Kalkulationen(vertrieb);
        var eingabe = new KalkulationsEingabe { Positionen = [new("S01-STD", 1), new("B03", 2)], AnzahlUser = 5 };
        var geladen = (await kalkulationen.LadeAsync(kalkulationId))!;
        var version = await kalkulationen.SpeichernAsync(kalkulationId, "Variante A", null, eingabe, geladen.Kalkulation.Zeilenversion);
        var standOhne = (await kalkulationen.LadeAsync(kalkulationId))!.Stand;

        await Assert.ThrowsAsync<ArgumentException>(() => kalkulationen.SpeichernAsync(kalkulationId, "Variante A", null, eingabe, version, null, [new("B03", fremd)]));
        await kalkulationen.SpeichernAsync(kalkulationId, "Variante A", null, eingabe, version, null, [new("B03", sicherung), new("B03", sicherung)]);

        var daten = (await kalkulationen.LadeAsync(kalkulationId))!;
        Assert.Equal([new Zuordnung("B03", sicherung)], daten.Kalkulation.Zuordnungen);
        Assert.NotEqual(standOhne, daten.Stand);
        Assert.Contains(daten.Herausforderungen, h => h.Id == sicherung);
        Assert.DoesNotContain(daten.Herausforderungen, h => h.Id == fremd);

        Assert.Equal(["B03 (Variante A)"], (await situation.LadeAsync(projekt)).Verknuepft[sicherung]);
        var gesperrt = await Assert.ThrowsAsync<InvalidOperationException>(() => situation.LoeschenAsync(sicherung));
        Assert.Contains("B03 (Variante A)", gesperrt.Message, StringComparison.Ordinal);

        // Die Kopie einer Kalkulation übernimmt die Verknüpfung.
        var kopie = await projekte.DuplizierenAsync(kalkulationId, "Variante B");
        Assert.Equal([new Zuordnung("B03", sicherung)], (await kalkulationen.LadeAsync(kopie))!.Kalkulation.Zuordnungen);

        foreach (var k in new[] { kalkulationId, kopie })
        {
            var stand = (await kalkulationen.LadeAsync(k))!;
            await kalkulationen.SpeichernAsync(k, stand.Kalkulation.Titel, null, eingabe, stand.Kalkulation.Zeilenversion, null, []);
        }

        await situation.LoeschenAsync(sicherung);
        Assert.Empty((await situation.LadeAsync(projekt)).Herausforderungen);
    }

    [Fact]
    public async Task Analyse_Angebote_mit_Navision_Nummer_Status_und_Dokument()
    {
        var vertrieb = Neu(Rollen.Vertrieb);
        var projekt = await ProjektAsync(Projekte(vertrieb));
        var dienst = Situation(vertrieb);
        var nummer = "an " + Random.Shared.Next(100000, 999999);
        var unterlagen = new UnterlagenDienst(new Fabrik(() => db.NeuerKontext(vertrieb)), vertrieb, TimeProvider.System,
            new Ablage.KeineKundenablage(), Microsoft.Extensions.Options.Options.Create(new Ablage.KundenablageEinstellungen()));
        var dokument = await unterlagen.HochladenAsync(projekt, UnterlagenArt.Angebot, "Angebot Analyse.pdf", [1, 2, 3], null);

        var id = await dienst.AnalyseAngebotErfassenAsync(projekt, new AnalyseAngebotsDaten(nummer, new DateOnly(2026, 6, 20), 4900.004m, AnalyseAngebotsStatus.Versendet, null, dokument));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.AnalyseAngebotErfassenAsync(projekt, new AnalyseAngebotsDaten(nummer.ToUpperInvariant(), new DateOnly(2026, 6, 21), 1, AnalyseAngebotsStatus.Versendet, null, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.AnalyseAngebotErfassenAsync(projekt, new AnalyseAngebotsDaten(" ", new DateOnly(2026, 6, 21), 1, AnalyseAngebotsStatus.Versendet, null, null)));
        await Assert.ThrowsAsync<KeinZugriffException>(() => Situation(Neu(Rollen.Consultant)).AnalyseAngebotErfassenAsync(projekt, new AnalyseAngebotsDaten("AN1", new DateOnly(2026, 6, 21), 1, AnalyseAngebotsStatus.Versendet, null, null)));

        var angebot = Assert.Single((await dienst.AnalyseAngeboteAsync(projekt)).Angebote);
        Assert.Equal((Analyse(nummer), 4900.00m, AnalyseAngebotsStatus.Versendet, (int?)dokument), (angebot.Nummer, angebot.Paketpreis, angebot.Status, angebot.UnterlageId));

        await dienst.AnalyseAngebotAendernAsync(id, new AnalyseAngebotsDaten(nummer, new DateOnly(2026, 6, 20), 4900m, AnalyseAngebotsStatus.Beauftragt, "Zusage per Mail", dokument));
        var beauftragt = Assert.Single((await dienst.AnalyseAngeboteAsync(projekt)).Angebote);
        Assert.Equal((AnalyseAngebotsStatus.Beauftragt, "Zusage per Mail"), (beauftragt.Status, beauftragt.Bemerkung));
        Assert.True(beauftragt.StatusSeit >= angebot.StatusSeit);

        // Wird das Dokument gelöscht, bleibt das Angebot ohne Verweis.
        await unterlagen.LoeschenAsync(dokument);
        Assert.Null(Assert.Single((await dienst.AnalyseAngeboteAsync(projekt)).Angebote).UnterlageId);
        await dienst.AnalyseAngebotLoeschenAsync(id);
        Assert.Empty((await dienst.AnalyseAngeboteAsync(projekt)).Angebote);
    }

    private static string Analyse(string nummer) => AnalyseAngebot.Normalisiere(nummer);
}
