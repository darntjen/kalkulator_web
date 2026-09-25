using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Tests;

[Collection(DatenbankSammlung.Name)]
public class DatenmodellTests(SqlServerFixture db)
{
    [Fact]
    public async Task Migrationen_sind_vollstaendig_und_passen_zum_Modell()
    {
        await using var kontext = db.NeuerKontext();

        Assert.Empty(await kontext.Database.GetPendingMigrationsAsync());
        Assert.False(kontext.Database.HasPendingModelChanges(), "Modell geändert, aber keine Migration erzeugt (dotnet ef migrations add …).");
    }

    [Fact]
    public async Task Katalog_mit_allen_Sonderfaellen_wird_vollstaendig_gespeichert_und_geladen()
    {
        var daten = new Testdaten();
        await using (var kontext = db.NeuerKontext())
        {
            kontext.Services.AddRange(daten.AlleServices);
            kontext.Preislisten.Add(daten.Preisliste);
            await kontext.SaveChangesAsync();
        }

        await using var lesen = db.NeuerKontext();
        var services = await lesen.Services
            .Include(s => s.Preiskomponenten)
            .Include(s => s.Bestandteile).ThenInclude(b => b.Bestandteil)
            .Include(s => s.Regeln).ThenInclude(r => r.Ziele).ThenInclude(z => z.ZielService)
            .Where(s => s.Code.StartsWith(daten.Code("")))
            .ToDictionaryAsync(s => s.Code);

        Assert.Equal(9, services.Count);

        // Verschachteltes Bundle: B02 enthält B01 und S05, B01 enthält S02–S04.
        var b02 = services[daten.Code("B02")];
        Assert.Equal([daten.Code("B01"), daten.Code("S05")], b02.Bestandteile.Select(b => b.Bestandteil!.Code).Order());
        Assert.Equal(3, services[daten.Code("B01")].Bestandteile.Count);

        // Mehrere Preiskomponenten und Regel mit mehreren Zielen.
        var s25 = services[daten.Code("S25")];
        Assert.Equal(3, s25.Preiskomponenten.Count);
        var regel = Assert.Single(s25.Regeln);
        Assert.Equal(RegelTyp.ErfordertEinenVon, regel.Typ);
        Assert.Equal(2, regel.Ziele.Count);

        // Einmalige Komponente mit Staffel nach Usern.
        var onboarding = services[daten.Code("S01-STD")].Preiskomponenten.Single(p => p.Abrechnungsart == Abrechnungsart.Einmalig);
        Assert.Equal(StaffelBezug.User, onboarding.StaffelBezug);

        var preisliste = await lesen.Preislisten
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .SingleAsync(p => p.Id == daten.Preisliste.Id);

        Assert.Equal(54.90m, preisliste.PreisFuer(b02.Preiskomponenten.Single().Id));
        Assert.Equal(1400m, preisliste.StaffelpreisFuer(onboarding.Id, 31));
        Assert.Null(preisliste.StaffelpreisFuer(onboarding.Id, 501));
        var s41 = services[daten.Code("S41")].Preiskomponenten.Single();
        Assert.Equal(350m, preisliste.StaffelpreisFuer(s41.Id, 50));
        Assert.Equal(550m, preisliste.StaffelpreisFuer(s41.Id, 51));
        Assert.Equal(30.38m, preisliste.ParameterWert(ParameterSchluessel.SupportkontingentAeSatz));
        Assert.Equal(0.55m, preisliste.ParameterWert(ParameterSchluessel.CloudServerMargenteiler));
        Assert.Equal(6.10m, Assert.Single(preisliste.EkPositionen).Kosten(60.75m));
    }

    [Fact]
    public async Task Freigegebene_Preisliste_ist_in_der_Datenbank_gesperrt()
    {
        var daten = new Testdaten();
        await using (var kontext = db.NeuerKontext())
        {
            kontext.Services.AddRange(daten.AlleServices);
            kontext.Preislisten.Add(daten.Preisliste);
            await kontext.SaveChangesAsync();

            daten.Preisliste.Freigeben("Produktmanagement", DateTimeOffset.UtcNow);
            await kontext.SaveChangesAsync();
        }

        // Preis ändern
        await using (var kontext = db.NeuerKontext())
        {
            var preis = await kontext.Preise.FirstAsync(p => p.PreislisteId == daten.Preisliste.Id);
            preis.VkNetto = 1m;
            await Assert.ThrowsAsync<PreislisteGesperrtException>(() => kontext.SaveChangesAsync());
        }

        // Parameter hinzufügen
        await using (var kontext = db.NeuerKontext())
        {
            kontext.Parameter.Add(new Parameter { PreislisteId = daten.Preisliste.Id, Schluessel = "NEU", Wert = 1m });
            await Assert.ThrowsAsync<PreislisteGesperrtException>(() => kontext.SaveChangesAsync());
        }

        // Preisliste umbenennen oder löschen
        await using (var kontext = db.NeuerKontext())
        {
            var liste = await kontext.Preislisten.SingleAsync(p => p.Id == daten.Preisliste.Id);
            liste.Bezeichnung = "geändert";
            await Assert.ThrowsAsync<PreislisteGesperrtException>(() => kontext.SaveChangesAsync());
        }

        await using (var kontext = db.NeuerKontext())
        {
            var liste = await kontext.Preislisten.SingleAsync(p => p.Id == daten.Preisliste.Id);
            kontext.Preislisten.Remove(liste);
            await Assert.ThrowsAsync<PreislisteGesperrtException>(() => kontext.SaveChangesAsync());
        }

        await using var pruefen = db.NeuerKontext();
        Assert.Equal(daten.Preisliste.Bezeichnung, (await pruefen.Preislisten.SingleAsync(p => p.Id == daten.Preisliste.Id)).Bezeichnung);
    }

    [Fact]
    public async Task Preisaenderung_erfolgt_ueber_neuen_Entwurf_und_laesst_die_alte_Version_unveraendert()
    {
        var daten = new Testdaten();
        await using (var kontext = db.NeuerKontext())
        {
            kontext.Services.AddRange(daten.AlleServices);
            kontext.Preislisten.Add(daten.Preisliste);
            daten.Preisliste.Freigeben("Produktmanagement", DateTimeOffset.UtcNow);
            await kontext.SaveChangesAsync();
        }

        int entwurfId;
        var b02Komponente = daten.B02.Preiskomponenten.Single().Id;
        await using (var kontext = db.NeuerKontext())
        {
            var alt = await kontext.Preislisten
                .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
                .SingleAsync(p => p.Id == daten.Preisliste.Id);
            var entwurf = alt.ErzeugeEntwurf(daten.Code("Preisstand 2027-01-01"), new DateOnly(2027, 1, 1));
            entwurf.Preise.Single(p => p.PreiskomponenteId == b02Komponente).VkNetto = 56.90m;
            kontext.Preislisten.Add(entwurf);
            await kontext.SaveChangesAsync();
            entwurfId = entwurf.Id;
        }

        await using var lesen = db.NeuerKontext();
        var altGeladen = await lesen.Preislisten.Include(p => p.Preise).SingleAsync(p => p.Id == daten.Preisliste.Id);
        var neuGeladen = await lesen.Preislisten.Include(p => p.Preise).Include(p => p.Staffeln).SingleAsync(p => p.Id == entwurfId);

        Assert.Equal(54.90m, altGeladen.PreisFuer(b02Komponente));
        Assert.Equal(56.90m, neuGeladen.PreisFuer(b02Komponente));
        Assert.Equal(PreislistenStatus.Entwurf, neuGeladen.Status);
        Assert.Equal(daten.Preisliste.Id, neuGeladen.VorgaengerId);
        Assert.Equal(altGeladen.Preise.Count, neuGeladen.Preise.Count);
        Assert.Equal(5, neuGeladen.Staffeln.Count);
    }

    [Fact]
    public async Task Aenderungen_werden_mit_Benutzer_Zeitpunkt_und_altem_und_neuem_Wert_protokolliert()
    {
        var daten = new Testdaten();
        var zeitpunkt = new DateTimeOffset(2026, 9, 25, 14, 30, 0, TimeSpan.Zero);
        var benutzer = new FesterBenutzer("Erika Produktmanagement");

        await using (var kontext = db.NeuerKontext(benutzer, new FesteZeit(zeitpunkt)))
        {
            kontext.Services.Add(daten.S02);
            await kontext.SaveChangesAsync();
            daten.S02.Bezeichnung = "Endpoint Protection (XDR)";
            await kontext.SaveChangesAsync();
        }

        await using var lesen = db.NeuerKontext();
        var eintraege = await lesen.Aenderungsprotokoll
            .Where(a => a.Entitaet == nameof(Service) && a.Schluessel == daten.S02.Id.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(["Angelegt", "Geändert"], eintraege.Select(e => e.Aktion));
        Assert.All(eintraege, e => Assert.Equal("Erika Produktmanagement", e.Benutzer));
        Assert.All(eintraege, e => Assert.Equal(zeitpunkt, e.Zeitpunkt));
        Assert.Contains("Endpoint Protection (XDR) \\u0026 Richtlinienmanagement", eintraege[1].Aenderungen);
        Assert.Contains("\"Neu\":\"Endpoint Protection (XDR)\"", eintraege[1].Aenderungen);
    }

    [Fact]
    public async Task Service_Code_ist_eindeutig()
    {
        var daten = new Testdaten();
        await using var kontext = db.NeuerKontext();
        kontext.Services.Add(daten.S02);
        kontext.Services.Add(new Service { Code = daten.S02.Code, Bezeichnung = "Doppelt", Typ = ServiceTyp.Einzelservice, Kategorie = daten.Kategorie });

        await Assert.ThrowsAsync<DbUpdateException>(() => kontext.SaveChangesAsync());
    }

    [Fact]
    public async Task Bundle_kann_sich_nicht_selbst_enthalten()
    {
        var daten = new Testdaten();
        await using (var kontext = db.NeuerKontext())
        {
            kontext.Services.Add(daten.B01);
            await kontext.SaveChangesAsync();
        }

        await using var aendern = db.NeuerKontext();
        aendern.BundleBestandteile.Add(new BundleBestandteil { BundleId = daten.B01.Id, BestandteilId = daten.B01.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => aendern.SaveChangesAsync());
    }

    private sealed class FesterBenutzer(string name) : Persistenz.IBenutzerKontext
    {
        public string Name => name;
    }

    private sealed class FesteZeit(DateTimeOffset zeitpunkt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => zeitpunkt;
    }
}
