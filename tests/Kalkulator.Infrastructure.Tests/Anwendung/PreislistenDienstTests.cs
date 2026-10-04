using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>
/// Preislisten-Workflow (#6, ADR-0005) gegen eine echte Datenbank. Jeder Test bekommt eine eigene, frisch befüllte
/// Datenbank, weil Freigaben den Zustand für alle folgenden Prüfungen verändern.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class PreislistenDienstTests(SqlServerFixture db)
{
    /// <summary>„Heute“ für alle Tests: Sonntag, 4. Oktober 2026, mittags.</summary>
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly TestBenutzer Pm = new("produktmanagement@noesse.de", Rollen.Produktmanagement);

    private sealed class FesteZeit(DateTimeOffset zeitpunkt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => zeitpunkt;
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }

    /// <summary>Neue Datenbank mit Erstbefüllung; die Preisliste „Preisstand 15.07.2026“ (Id 1) ist ein Entwurf.</summary>
    private async Task<string> NeueDatenbankAsync()
    {
        var name = "Preislisten_" + Guid.NewGuid().ToString("N")[..12];
        await using var kontext = db.NeuerKontextAufDatenbank(name);
        await kontext.Database.MigrateAsync();
        await KatalogErstbefuellung.AusfuehrenAsync(kontext);
        return name;
    }

    private PreislistenDienst Dienst(string datenbank, TestBenutzer benutzer) =>
        new(new Fabrik(() => db.NeuerKontextAufDatenbank(datenbank, benutzer)), benutzer, new FesteZeit(Jetzt));

    private async Task<int> KomponenteAsync(string datenbank, string code)
    {
        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        return await kontext.Preiskomponenten.Where(k => k.Code == code).Select(k => k.Id).SingleAsync();
    }

    [Fact]
    public async Task Nur_Produktmanagement_pflegt_Fuehrung_liest_Vertrieb_sieht_nichts()
    {
        var datenbank = await NeueDatenbankAsync();
        var fuehrung = Dienst(datenbank, new TestBenutzer("fuehrung@noesse.de", Rollen.Fuehrung));
        var vertrieb = Dienst(datenbank, new TestBenutzer("vertrieb@noesse.de", Rollen.Vertrieb));

        Assert.Single(await fuehrung.ListeAsync());
        Assert.NotEmpty((await fuehrung.LadeAsync(1))!.Preisliste.EkPositionen);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.PreiseSpeichernAsync(1, []));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.FreigebenAsync(1));
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.ListeAsync());
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.ExportAsync(1));
    }

    [Fact]
    public async Task Erste_Preisliste_wird_freigegeben_und_ist_danach_unveraenderlich()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);

        Assert.DoesNotContain(await dienst.PruefenAsync(1), h => h.IstFehler);
        await dienst.FreigebenAsync(1);

        var liste = (await dienst.LadeAsync(1))!.Preisliste;
        Assert.Equal(PreislistenStatus.Freigegeben, liste.Status);
        Assert.Equal(Pm.Name, liste.FreigegebenVon);
        Assert.Equal(Jetzt, liste.FreigegebenAm);
        var s02 = await KomponenteAsync(datenbank, "S02");
        await Assert.ThrowsAsync<PreislisteGesperrtException>(() => dienst.PreiseSpeichernAsync(1, [new PreisWert(s02, 1m)]));
        await Assert.ThrowsAsync<PreislisteGesperrtException>(() => dienst.EntwurfLoeschenAsync(1));
        Assert.True((await dienst.ListeAsync()).Single().Aktuell);
    }

    [Fact]
    public async Task Entwurf_kopiert_alle_Werte_und_braucht_eine_eindeutige_Bezeichnung()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);

        var id = await dienst.EntwurfAnlegenAsync(1, "Preisstand 01.01.2027", new DateOnly(2027, 1, 1));

        var quelle = (await dienst.LadeAsync(1))!.Preisliste;
        var kopie = (await dienst.LadeAsync(id))!;
        Assert.Equal("Preisstand 15.07.2026", kopie.Vorgaenger);
        Assert.Equal(quelle.Preise.Count, kopie.Preisliste.Preise.Count);
        Assert.Equal(quelle.Staffeln.Count, kopie.Preisliste.Staffeln.Count);
        Assert.Equal(quelle.Parameter.Count, kopie.Preisliste.Parameter.Count);
        Assert.Equal(quelle.EkPositionen.Count, kopie.Preisliste.EkPositionen.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.EntwurfAnlegenAsync(1, "Preisstand 01.01.2027", new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public async Task Neue_Preisliste_gilt_erst_ab_ihrem_Datum_und_nie_rueckwirkend()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);
        await dienst.FreigebenAsync(1);
        var s02 = await KomponenteAsync(datenbank, "S02");
        var id = await dienst.EntwurfAnlegenAsync(1, "Preisstand 01.01.2027", new DateOnly(2026, 7, 1));
        await dienst.PreiseSpeichernAsync(id, [new PreisWert(s02, 15.90m)]);

        Assert.Contains(await dienst.PruefenAsync(id), h => h.IstFehler && h.Text.Contains("15.07.2026", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.FreigebenAsync(id));

        await dienst.StammdatenSpeichernAsync(id, "Preisstand 01.01.2027", new DateOnly(2026, 9, 1));
        Assert.Contains(await dienst.PruefenAsync(id), h => h.IstFehler && h.Text.Contains("rückwirkend", StringComparison.Ordinal));

        await dienst.StammdatenSpeichernAsync(id, "Preisstand 01.01.2027", new DateOnly(2027, 1, 1));
        await dienst.FreigebenAsync(id);

        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        var lader = new RechenkernLader(kontext);
        var heute = await lader.LadeKatalogAsync(new DateOnly(2026, 12, 31), mitEinkauf: false, entwurfZulassen: false);
        var spaeter = await lader.LadeKatalogAsync(new DateOnly(2027, 1, 1), mitEinkauf: false, entwurfZulassen: false);
        Assert.Equal(14.90m, heute.Preisliste.PreisFuer(s02));
        Assert.Equal(15.90m, spaeter.Preisliste.PreisFuer(s02));
    }

    [Fact]
    public async Task Preise_Staffeln_Parameter_und_EK_werden_gepflegt_und_geprueft()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);
        var s02 = await KomponenteAsync(datenbank, "S02");
        var s41 = await KomponenteAsync(datenbank, "S41");
        var b01 = await KomponenteAsync(datenbank, "B01");

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.PreiseSpeichernAsync(1, [new PreisWert(s02, -1m)]));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.PreiseSpeichernAsync(1, [new PreisWert(s41, 400m)]));
        await dienst.PreiseSpeichernAsync(1, [new PreisWert(s02, 15.50m)]);

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.StaffelSpeichernAsync(1, s41, [new StaffelWert(1, 350m, null, null), new StaffelWert(1, 400m, null, null)]));
        await dienst.StaffelSpeichernAsync(1, s41, [new StaffelWert(1, 360m, "bis 50 MA", null), new StaffelWert(51, 560m, "ab 51 MA", null), new StaffelWert(201, null, "individuell", null)]);

        var parameter = (await dienst.LadeAsync(1))!.Preisliste.Parameter.Select(p => new ParameterWert(p.Schluessel, p.Wert, p.Beschreibung)).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.ParameterSpeichernAsync(1, [.. parameter.Where(p => p.Schluessel != ParameterSchluessel.AeSatzEbene1)]));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.ParameterSpeichernAsync(1, [.. parameter, new ParameterWert("kein schlüssel", 1, null)]));
        await dienst.ParameterSpeichernAsync(1, [.. parameter.Where(p => p.Schluessel != ParameterSchluessel.MargeRotUnter), new ParameterWert("test_wert", 2.5m, "Test")]);

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.EkSpeichernAsync(1, new Dictionary<int, EkWert?> { [s41] = new(0, 120, null, 0, AusBestandteilen: true, 0, null) }));
        await dienst.EkSpeichernAsync(1, new Dictionary<int, EkWert?>
        {
            [s41] = new(0, 120, null, 0, false, 0, "Monatstermin 2 h"),
            [b01] = null,
        });

        var liste = (await dienst.LadeAsync(1))!.Preisliste;
        Assert.Equal(15.50m, liste.PreisFuer(s02));
        Assert.Equal([1, 51, 201], liste.Staffeln.Where(s => s.PreiskomponenteId == s41).OrderBy(s => s.AbMenge).Select(s => s.AbMenge));
        Assert.Null(liste.Staffeln.Single(s => s.PreiskomponenteId == s41 && s.AbMenge == 201).VkNetto);
        Assert.DoesNotContain(liste.Parameter, p => p.Schluessel == ParameterSchluessel.MargeRotUnter);
        Assert.Equal(2.5m, liste.ParameterWert("TEST_WERT"));
        Assert.Equal("Monatstermin 2 h", liste.EkPositionen.Single(e => e.PreiskomponenteId == s41).Anmerkung);
        Assert.DoesNotContain(liste.EkPositionen, e => e.PreiskomponenteId == b01);

        var hinweise = await dienst.PruefenAsync(1);
        Assert.Contains(hinweise, h => !h.IstFehler && h.Text.Contains("Ohne EK", StringComparison.Ordinal) && h.Text.Contains("B01", StringComparison.Ordinal));
        Assert.DoesNotContain(hinweise, h => System.Text.RegularExpressions.Regex.IsMatch(h.Text, @"\bS41[,.]"));
    }

    [Fact]
    public async Task Nur_tatsaechlich_geaenderte_Preise_landen_im_Protokoll()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);
        var ansicht = (await dienst.LadeAsync(1))!;
        var werte = ansicht.Services.SelectMany(s => s.Preiskomponenten)
            .Where(k => k.StaffelBezug == Domain.Katalog.StaffelBezug.Keine)
            .Select(k => new PreisWert(k.Id, ansicht.Preisliste.PreisFuer(k.Id)))
            .ToList();
        var s02 = await KomponenteAsync(datenbank, "S02");

        await dienst.PreiseSpeichernAsync(1, [.. werte.Select(w => w.KomponenteId == s02 ? w with { VkNetto = 15.20m } : w)]);

        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        var eintrag = Assert.Single(await kontext.Aenderungsprotokoll.Where(e => e.Entitaet == nameof(Preis) && e.Aktion == "Geändert").ToListAsync());
        Assert.Equal(Pm.Name, eintrag.Benutzer);
        Assert.Contains("\"Alt\":14.90", eintrag.Aenderungen, StringComparison.Ordinal);
        Assert.Contains("\"Neu\":15.20", eintrag.Aenderungen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entwurf_loeschen_entfernt_alle_Werte()
    {
        var datenbank = await NeueDatenbankAsync();
        var dienst = Dienst(datenbank, Pm);
        await dienst.FreigebenAsync(1);
        var id = await dienst.EntwurfAnlegenAsync(1, "Verworfen", new DateOnly(2027, 1, 1));

        await dienst.EntwurfLoeschenAsync(id);

        await using var kontext = db.NeuerKontextAufDatenbank(datenbank);
        Assert.False(await kontext.Preislisten.AnyAsync(p => p.Id == id));
        Assert.False(await kontext.Preise.AnyAsync(p => p.PreislisteId == id));
        Assert.False(await kontext.EkPositionen.AnyAsync(p => p.PreislisteId == id));
        Assert.True(await kontext.Preise.AnyAsync(p => p.PreislisteId == 1));
    }

    [Fact]
    public async Task Export_ist_eine_gueltige_Excel_Mappe_mit_allen_Blaettern()
    {
        var datenbank = await NeueDatenbankAsync();
        var (name, inhalt) = await Dienst(datenbank, new TestBenutzer("fuehrung@noesse.de", Rollen.Fuehrung)).ExportAsync(1);

        Assert.Equal("Katalog_Preisstand_15.07.2026.xlsx", name);
        using var strom = new MemoryStream(inhalt);
        using var mappe = SpreadsheetDocument.Open(strom, false);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(mappe)
            .Select(f => $"{f.Part?.Uri}: {f.Path?.XPath}: {f.Description}"));
        var teil = mappe.WorkbookPart!;
        var blattliste = teil.Workbook!.Sheets!;
        var blaetter = blattliste.Elements<Sheet>().Select(s => s.Name!.Value).ToList();
        Assert.Equal(["Preisliste", "Services", "Preise", "Staffeln", "Parameter", "Regeln", "EK-Kalkulation"], blaetter);

        var ek = (WorksheetPart)teil.GetPartById(blattliste.Elements<Sheet>().Last().Id!);
        var zeilen = ek.Worksheet!.Descendants<Row>().Select(r => r.Elements<Cell>().Select(c => c.InnerText).ToList()).ToList();
        var s02 = zeilen.Single(z => z.Count > 1 && z[1] == "S02");
        Assert.Equal("6.10", s02[9]);
        Assert.Equal("14.90", s02[10]);
        Assert.Equal("grün", s02[13]);
    }
}
