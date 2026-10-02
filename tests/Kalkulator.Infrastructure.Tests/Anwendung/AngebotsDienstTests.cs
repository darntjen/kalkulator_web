using DocumentFormat.OpenXml.Packaging;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Tests.Dokumente;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>Angebot erzeugen, archivieren und Versand vermerken gegen eine Datenbank mit freigegebener Preisliste.</summary>
[Collection(DatenbankSammlung.Name)]
public class AngebotsDienstTests(SqlServerFixture db)
{
    private const string Datenbank = "Angebote";
    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static bool _befuellt;

    private static readonly KalkulationsEingabe Rk01 = new()
    {
        Positionen = [new("S01-STD", 1), new("B01", 15), new("B03", 2), new("B05", 1)],
        AnzahlUser = 15,
    };

    private sealed record Dienste(KundenprojektDienst Projekte, KalkulationsDienst Kalkulationen, AngebotsDienst Angebote);

    private async Task<Dienste> DiensteAsync(TestBenutzer benutzer, bool preislisteFreigeben = true)
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

        var fabrik = new Fabrik(() => db.NeuerKontextAufDatenbank(Datenbank, benutzer));
        var einstellungen = Options.Create(new AngebotsEinstellungen { Vorlagenordner = AngebotsdokumentTests.Vorlagenordner() });
        return new Dienste(
            new KundenprojektDienst(fabrik, benutzer, TimeProvider.System),
            new KalkulationsDienst(fabrik, benutzer, TimeProvider.System),
            new AngebotsDienst(fabrik, benutzer, TimeProvider.System, einstellungen));
    }

    private static TestBenutzer Neu(string rolle) => new($"{rolle.ToLowerInvariant()}-{Guid.NewGuid():N}@noesse.de", rolle);

    private async Task<(int Projekt, int Kalkulation, Dienste Dienste)> KalkulationAsync(KalkulationsEingabe eingabe)
    {
        var dienste = await DiensteAsync(Neu(Rollen.Vertrieb));
        var projekt = await dienste.Projekte.AnlegenAsync(new NeuesKundenprojekt("Angebots-Test GmbH & Co. KG", "Am Hafen 3", "26122", "Oldenburg",
            "Herr Max Kunde, Geschäftsführer", null, "MS", false, null, null));
        var id = await dienste.Projekte.NeueKalkulationAsync(projekt, "Variante A");
        var geladen = await dienste.Kalkulationen.LadeAsync(id);
        await dienste.Kalkulationen.SpeichernAsync(id, "Variante A", new DateOnly(2027, 1, 1), eingabe, geladen!.Kalkulation.Zeilenversion);
        return (projekt, id, dienste);
    }

    private static string Text(byte[] datei)
    {
        using var strom = new MemoryStream(datei);
        using var dokument = WordprocessingDocument.Open(strom, false);
        return dokument.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Fact]
    public async Task Angebot_friert_ein_vergibt_Nummer_und_archiviert_das_Dokument()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);

        var erstes = await dienste.Angebote.ErzeugenAsync(id, "Ausgangssituation des Kunden", null);
        var zweites = await dienste.Angebote.ErzeugenAsync(id, null, dienste.Angebote.Heute.AddDays(10));

        var liste = await dienste.Angebote.ListeAsync(id);
        Assert.Equal(["V2", "V1"], liste.Select(a => a.Version));
        Assert.Single(liste.Select(a => a.Nummer).Distinct());
        Assert.Matches(@"^MS-A-\d{4}-\d{4}$", liste[0].Nummer);
        Assert.Equal(dienste.Angebote.Heute.AddDays(30), liste[1].GueltigBis);
        Assert.Equal(dienste.Angebote.Heute.AddDays(10), liste[0].GueltigBis);

        var (name, inhalt) = await dienste.Angebote.DateiAsync(erstes);
        Assert.Equal($"Angebot_{liste[1].Nummer}_V1_Angebots-Test-GmbH-Co-KG.docx", name);
        var text = Text(inhalt);
        Assert.Contains($"{liste[1].Nummer} (V1)", text, StringComparison.Ordinal);
        Assert.Contains("Sehr geehrter Herr Max Kunde,", text, StringComparison.Ordinal);
        Assert.Contains("Ausgangssituation des Kunden", text, StringComparison.Ordinal);
        Assert.Contains("1.027,20 € netto monatlich", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ausgangssituation des Kunden", Text((await dienste.Angebote.DateiAsync(zweites)).Inhalt), StringComparison.Ordinal);

        // Die Version ist eingefroren: weitere Änderungen am Arbeitsstand ändern das Angebot nicht.
        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        var version = await kontext.Kalkulationsversionen.Include(v => v.Positionen).SingleAsync(v => v.KalkulationId == id && v.Nummer == 1);
        Assert.Equal(1027.20m, version.SummeMonatlich);
    }

    [Fact]
    public async Task Angebotsnummern_sind_fortlaufend_und_eindeutig()
    {
        var a = await KalkulationAsync(Rk01);
        var b = await KalkulationAsync(Rk01);

        await Task.WhenAll(a.Dienste.Angebote.ErzeugenAsync(a.Kalkulation, null, null), b.Dienste.Angebote.ErzeugenAsync(b.Kalkulation, null, null));

        var nummerA = (await a.Dienste.Angebote.ListeAsync(a.Kalkulation)).Single().Nummer;
        var nummerB = (await b.Dienste.Angebote.ListeAsync(b.Kalkulation)).Single().Nummer;
        Assert.NotEqual(nummerA, nummerB);
        Assert.Equal(1, Math.Abs(int.Parse(nummerA[^4..], System.Globalization.CultureInfo.InvariantCulture) - int.Parse(nummerB[^4..], System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Fehlerhafte_Kalkulation_erzeugt_kein_Angebot_und_verbraucht_keine_Nummer()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01 with { Positionen = [new("B01", 10)] });

        await Assert.ThrowsAsync<KalkulationNichtAngebotsfaehigException>(() => dienste.Angebote.ErzeugenAsync(id, null, null));

        Assert.Empty(await dienste.Angebote.ListeAsync(id));
        Assert.Null((await dienste.Kalkulationen.LadeAsync(id))!.Kalkulation.Angebotsnummer);
    }

    [Fact]
    public async Task Versandvermerk_setzt_den_Projektstatus()
    {
        var (projekt, id, dienste) = await KalkulationAsync(Rk01);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);

        await dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute);

        Assert.Equal(dienste.Angebote.Heute, (await dienste.Angebote.ListeAsync(id)).Single().VersendetAm);
        var geladen = await dienste.Projekte.LadeAsync(projekt);
        Assert.Equal(ProjektStatus.AngebotVersendet, geladen!.Status);
        Assert.Contains("versendet am", geladen.StatusEreignisse.OrderBy(e => e.Zeitpunkt).Last().Kommentar, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienste.Angebote.AlsVersendetMarkierenAsync(angebot, dienste.Angebote.Heute));
    }

    [Fact]
    public async Task Nur_Bearbeiter_erzeugen_Angebote_Lesende_duerfen_herunterladen()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);
        var angebot = await dienste.Angebote.ErzeugenAsync(id, null, null);

        var consultant = await DiensteAsync(Neu(Rollen.Consultant));
        var fremd = await DiensteAsync(Neu(Rollen.Vertrieb));

        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Angebote.ErzeugenAsync(id, null, null));
        Assert.NotEmpty((await consultant.Angebote.DateiAsync(angebot)).Inhalt);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fremd.Angebote.DateiAsync(angebot));
        await Assert.ThrowsAsync<KeinZugriffException>(() => consultant.Angebote.AlsVersendetMarkierenAsync(angebot, consultant.Angebote.Heute));
    }

    [Fact]
    public async Task Archivierte_Angebote_sind_unveraenderlich()
    {
        var (_, id, dienste) = await KalkulationAsync(Rk01);
        var angebotId = await dienste.Angebote.ErzeugenAsync(id, null, null);

        await using var kontext = db.NeuerKontextAufDatenbank(Datenbank);
        var datei = await kontext.Set<AngebotsDatei>().SingleAsync(d => d.AngebotId == angebotId);
        kontext.Entry(datei).Property(d => d.Inhalt).CurrentValue = [1, 2, 3];
        await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());

        await using var loeschen = db.NeuerKontextAufDatenbank(Datenbank);
        loeschen.Remove(await loeschen.Angebote.SingleAsync(a => a.Id == angebotId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loeschen.SaveChangesAsync());
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
