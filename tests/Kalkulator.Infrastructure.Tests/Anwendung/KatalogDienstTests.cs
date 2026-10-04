using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>
/// Pflege von Services, Komponenten, Bundles, Regeln und Vorlagen (#6) gegen eine befüllte Datenbank mit freigegebener
/// Preisliste. Die Tests legen eigene Codes an und stören sich deshalb nicht gegenseitig.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public class KatalogDienstTests(SqlServerFixture db)
{
    private const string Datenbank = "Katalogpflege";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _befuellt;

    private static readonly TestBenutzer Pm = new("produktmanagement@noesse.de", Rollen.Produktmanagement);

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }

    private async Task<KatalogDienst> DienstAsync(TestBenutzer? benutzer = null)
    {
        await Sperre.WaitAsync();
        try
        {
            if (!_befuellt)
            {
                await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
                await kontext.Database.MigrateAsync();
                await KatalogErstbefuellung.AusfuehrenAsync(kontext);
                (await kontext.Preislisten.SingleAsync()).Freigeben("produktmanagement", DateTimeOffset.UtcNow);
                await kontext.SaveChangesAsync();
                _befuellt = true;
            }
        }
        finally
        {
            Sperre.Release();
        }

        var wer = benutzer ?? Pm;
        return new KatalogDienst(new Fabrik(() => db.NeuerKontextAufDatenbank(Datenbank, wer)), wer);
    }

    private static string NeuerCode() => "T" + Random.Shared.Next(100, 999).ToString(System.Globalization.CultureInfo.InvariantCulture) + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();

    private async Task<int> IdAsync(string code)
    {
        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        return await kontext.Services.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    private async Task<int> NeuerServiceAsync(KatalogDienst dienst, string code, ServiceTyp typ = ServiceTyp.Einzelservice)
    {
        var kategorie = (await dienst.KategorienAsync()).First().Id;
        return await dienst.ServiceAnlegenAsync(new NeuerService(code, "Test " + code, typ, kategorie));
    }

    private static KomponentenStammdaten Komponente(string bezeichnung = "je Server", StaffelBezug staffel = StaffelBezug.Keine) =>
        new(bezeichnung, Einheit.Server, Abrechnungsart.Monatlich, staffel, null, 1);

    [Fact]
    public async Task Fuehrung_liest_nur_Vertrieb_sieht_den_Katalog_nicht()
    {
        var fuehrung = await DienstAsync(new TestBenutzer("fuehrung@noesse.de", Rollen.Fuehrung));
        var vertrieb = await DienstAsync(new TestBenutzer("vertrieb@noesse.de", Rollen.Vertrieb));

        Assert.True((await fuehrung.ServicesAsync()).Count >= 40);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.ServiceAnlegenAsync(new NeuerService("TX1", "x", ServiceTyp.Einzelservice, 1)));
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.KategorieAnlegenAsync("x"));
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.ServicesAsync());
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.ProtokollAsync(ProtokollBereich.Alle, null, null));
    }

    [Fact]
    public async Task Neuer_Service_ist_zunaechst_zukuenftig_Code_wird_geprueft()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();

        var id = await NeuerServiceAsync(dienst, code.ToLowerInvariant());

        var service = (await dienst.ServiceAsync(id))!.Service;
        Assert.Equal(code, service.Code);
        Assert.Equal(Vertriebsstatus.Zukuenftig, service.Vertriebsstatus);
        await Assert.ThrowsAsync<ArgumentException>(() => NeuerServiceAsync(dienst, code));
        await Assert.ThrowsAsync<ArgumentException>(() => NeuerServiceAsync(dienst, "S 99!"));
    }

    [Fact]
    public async Task Stammdaten_und_Leistungsschein_werden_gespeichert()
    {
        var dienst = await DienstAsync();
        var id = await NeuerServiceAsync(dienst, NeuerCode());
        var ansicht = (await dienst.ServiceAsync(id))!;
        var schein = ansicht.Leistungsscheine.First(d => d.Code == "S04");
        var grundvertrag = await IdVorlageAsync("GRUNDVERTRAG");

        await dienst.ServiceSpeichernAsync(id, new ServiceStammdaten("Neuer Name", "NOS-TEST-01", "Kurz", ansicht.Service.KategorieId,
            Vertriebsstatus.AufAnfrage, 7, schein.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.ServiceSpeichernAsync(id, new ServiceStammdaten("x", null, null,
            ansicht.Service.KategorieId, Vertriebsstatus.AufAnfrage, 7, grundvertrag)));

        var service = (await dienst.ServiceAsync(id))!.Service;
        Assert.Equal(("Neuer Name", "NOS-TEST-01", Vertriebsstatus.AufAnfrage, 7, "S04"),
            (service.Bezeichnung, service.ServiceNummer, service.Vertriebsstatus, service.Sortierung, service.Leistungsschein!.Code));
    }

    private async Task<int> IdVorlageAsync(string code)
    {
        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        return await kontext.DokumentVorlagen.Where(d => d.Code == code).Select(d => d.Id).FirstAsync();
    }

    [Fact]
    public async Task Komponenten_aus_freigegebenen_Preislisten_lassen_sich_weder_loeschen_noch_umstaffeln()
    {
        var dienst = await DienstAsync();
        var s02 = (await dienst.ServiceAsync(await IdAsync("S02")))!;
        var komponente = s02.Service.Preiskomponenten.Single();

        Assert.DoesNotContain(komponente.Id, s02.LoeschbareKomponenten);
        Assert.NotNull(s02.GrundNichtLoeschbar);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.KomponenteLoeschenAsync(komponente.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.KomponenteSpeichernAsync(komponente.Id, Komponente(komponente.Bezeichnung, StaffelBezug.User)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.ServiceLoeschenAsync(s02.Service.Id));
    }

    [Fact]
    public async Task Komponente_in_einem_Arbeitsstand_ist_verwendet()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();
        var service = await NeuerServiceAsync(dienst, code);
        var komponente = await dienst.KomponenteAnlegenAsync(service, code + "-SERVER", Komponente());
        Assert.Contains(komponente, (await dienst.ServiceAsync(service))!.LoeschbareKomponenten);

        await using (var kontext = db.NeuerKontextAufDatenbank(Datenbank))
        {
            var projekt = Kundenprojekt.Anlegen(new Kunde { Firma = "Katalogtest " + code }, "Test", "vertrieb@noesse.de", false, "vertrieb@noesse.de", DateTimeOffset.UtcNow);
            var kalkulation = projekt.NeueKalkulation("Variante A", "vertrieb@noesse.de", DateTimeOffset.UtcNow);
            kalkulation.AendereEingabe(new KalkulationsEingabe { Positionen = [new("S01-STD", 1), new(code + "-SERVER", 2)] });
            kontext.Kundenprojekte.Add(projekt);
            await kontext.SaveChangesAsync();
        }

        var ansicht = (await dienst.ServiceAsync(service))!;
        Assert.DoesNotContain(komponente, ansicht.LoeschbareKomponenten);
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.KomponenteLoeschenAsync(komponente));
        Assert.Contains("Kalkulationen", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unbenutzter_Service_wird_samt_Entwurfswerten_geloescht()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();
        var service = await NeuerServiceAsync(dienst, code);
        var komponente = await dienst.KomponenteAnlegenAsync(service, code + "-USER", Komponente("je User"));
        await dienst.RegelAnlegenAsync(service, new RegelStammdaten(RegelTyp.ErfordertAlle, "Braucht Connect", [await IdAsync("S01-STD")]));
        int entwurf;
        await using (var kontext = db.NeuerKontextAufDatenbank(Datenbank))
        {
            var freigegeben = await kontext.Preislisten.Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
                .FirstAsync(p => p.Status == Domain.Preise.PreislistenStatus.Freigegeben);
            var neu = freigegeben.ErzeugeEntwurf("Entwurf " + code, new DateOnly(2030, 1, 1));
            neu.Preise.Add(new Domain.Preise.Preis { PreiskomponenteId = komponente, VkNetto = 9.90m });
            kontext.Preislisten.Add(neu);
            await kontext.SaveChangesAsync();
            entwurf = neu.Id;
        }

        await dienst.ServiceLoeschenAsync(service);

        await using var pruefen = db.NeuerKontextAufDatenbank(Datenbank);
        Assert.False(await pruefen.Services.AnyAsync(s => s.Id == service));
        Assert.False(await pruefen.Preise.AnyAsync(p => p.PreiskomponenteId == komponente));
        Assert.True(await pruefen.Preise.AnyAsync(p => p.PreislisteId == entwurf));
        var protokoll = await dienst.ProtokollAsync(ProtokollBereich.Katalog, null, null, 1000);
        Assert.Contains(protokoll, z => z.Objekt == "Service" && z.Aktion == "Gelöscht" && z.Bezug == code);
        Assert.Contains(protokoll, z => z.Objekt == "Preiskomponente" && z.Aktion == "Gelöscht" && z.Bezug == code + "-USER");
    }

    [Fact]
    public async Task Bundles_lassen_sich_zusammenstellen_aber_ohne_Kreise()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();
        var bundle = await NeuerServiceAsync(dienst, code, ServiceTyp.Bundle);
        var einzel = await NeuerServiceAsync(dienst, NeuerCode());

        await dienst.BestandteilHinzufuegenAsync(bundle, einzel);
        await dienst.BestandteilHinzufuegenAsync(bundle, await IdAsync("B01"));

        Assert.Equal(2, (await dienst.ServiceAsync(bundle))!.Service.Bestandteile.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.BestandteilHinzufuegenAsync(bundle, einzel));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.BestandteilHinzufuegenAsync(einzel, bundle));
        var (connect, b01, b02) = (await IdAsync("S01-STD"), await IdAsync("B01"), await IdAsync("B02"));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.BestandteilHinzufuegenAsync(bundle, connect));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.BestandteilHinzufuegenAsync(b01, b02));
        Assert.Contains($"Bundle {code}", (await dienst.ServiceAsync(einzel))!.VerwendetIn);

        await dienst.BestandteilEntfernenAsync(bundle, einzel);
        Assert.Single((await dienst.ServiceAsync(bundle))!.Service.Bestandteile);
    }

    [Fact]
    public async Task Regeln_brauchen_Ziele_und_eine_Meldung()
    {
        var dienst = await DienstAsync();
        var service = await NeuerServiceAsync(dienst, NeuerCode());
        var b03 = await IdAsync("B03");
        var b04 = await IdAsync("B04");

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.RegelAnlegenAsync(service, new RegelStammdaten(RegelTyp.ErfordertEinenVon, "x", [])));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.RegelAnlegenAsync(service, new RegelStammdaten(RegelTyp.ErfordertEinenVon, " ", [b03])));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.RegelAnlegenAsync(service, new RegelStammdaten(RegelTyp.ErfordertEinenVon, "x", [service])));
        var regel = await dienst.RegelAnlegenAsync(service, new RegelStammdaten(RegelTyp.ErfordertEinenVon, "Braucht Server as a Service", [b03, b04]));
        await dienst.RegelSpeichernAsync(regel, new RegelStammdaten(RegelTyp.SchliesstAus, "Nicht mit B04", [b04]));

        var gespeichert = Assert.Single((await dienst.ServiceAsync(service))!.Service.Regeln);
        Assert.Equal((RegelTyp.SchliesstAus, "Nicht mit B04"), (gespeichert.Typ, gespeichert.Meldung));
        Assert.Equal([b04], gespeichert.Ziele.Select(z => z.ZielServiceId));

        await dienst.RegelLoeschenAsync(regel);
        Assert.Empty((await dienst.ServiceAsync(service))!.Service.Regeln);
    }

    [Fact]
    public async Task Vorlagen_und_Kategorien_sind_eindeutig()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();

        var id = await dienst.VorlageAnlegenAsync(new VorlagenStammdaten(DokumentTyp.Leistungsschein, code.ToLowerInvariant(), "Leistungsschein " + code));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.VorlageAnlegenAsync(new VorlagenStammdaten(DokumentTyp.Leistungsschein, code, "doppelt")));
        Assert.Equal(code, (await dienst.VorlagenAsync()).Single(v => v.Vorlage.Id == id).Vorlage.Code);

        var s02 = await IdVorlageAsync("S02");
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dienst.VorlageSpeichernAsync(s02, new VorlagenStammdaten(DokumentTyp.Avb, "S02", "Leistungsschein S02")));
        Assert.Contains("zugeordnet", fehler.Message, StringComparison.Ordinal);

        var kategorie = await dienst.KategorieAnlegenAsync("Kategorie " + code);
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.KategorieAnlegenAsync("Kategorie " + code));
        await dienst.KategorieSpeichernAsync(kategorie, "Umbenannt " + code, 99);
        Assert.Contains(await dienst.KategorienAsync(), k => k.Id == kategorie && k.Name == "Umbenannt " + code && k.Sortierung == 99);
    }

    [Fact]
    public async Task Protokoll_zeigt_lesbare_Bezuege_und_alte_und_neue_Werte()
    {
        var dienst = await DienstAsync();
        var code = NeuerCode();
        var service = await NeuerServiceAsync(dienst, code);
        var ansicht = (await dienst.ServiceAsync(service))!;
        await dienst.ServiceSpeichernAsync(service, new ServiceStammdaten("Geänderter Name", null, null, ansicht.Service.KategorieId, Vertriebsstatus.Geparkt, 1, null));

        var protokoll = await dienst.ProtokollAsync(ProtokollBereich.Katalog, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, 1000);

        var aenderung = protokoll.First(z => z.Bezug == code && z.Aktion == "Geändert");
        Assert.Equal(Pm.Name, aenderung.Benutzer);
        Assert.Contains(aenderung.Aenderungen, f => f.Feld == "Bezeichnung" && f.Alt == "Test " + code && f.Neu == "Geänderter Name");
        Assert.Contains(aenderung.Aenderungen, f => f.Feld == "Vertriebsstatus" && f.Alt == "Zukuenftig" && f.Neu == "Geparkt");
        var angelegt = protokoll.First(z => z.Bezug == code && z.Aktion == "Angelegt");
        Assert.Contains(angelegt.Aenderungen, f => f.Feld == "Code" && f.Neu == code);
        Assert.DoesNotContain(await dienst.ProtokollAsync(ProtokollBereich.Einkauf, null, null, 1000), z => z.Objekt != "EK");
    }
}
