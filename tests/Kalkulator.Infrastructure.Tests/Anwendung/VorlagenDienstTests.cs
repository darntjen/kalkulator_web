using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Tests.Dokumente;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using TestBenutzer = Kalkulator.Infrastructure.Tests.Anwendung.KundenprojektDienstTests.TestBenutzer;

namespace Kalkulator.Infrastructure.Tests.Anwendung;

/// <summary>
/// Abgleich der Vertragsvorlagen mit einer Quelle und Freigabe durch das Produktmanagement (#26, Teil B). Als Quelle
/// dient ein Ordner mit derselben Struktur wie in SharePoint; jeder Test bekommt eine eigene Datenbank.
/// </summary>
[Collection(DatenbankSammlung.Name)]
public sealed class VorlagenDienstTests(SqlServerFixture db) : IDisposable
{
    private static readonly TestBenutzer Pm = new("produktmanagement@noesse.de", Rollen.Produktmanagement);

    private readonly string _ordner = Directory.CreateTempSubdirectory("vorlagen-").FullName;

    public void Dispose() => Directory.Delete(_ordner, recursive: true);

    private static readonly byte[] GrundvertragMitPlatzhaltern = VorlagenpruefungTests.Dokument(
        "Managed-Services-Vertrag mit {{kunde.anschrift}}, Nummer {{vertrag.nummer}}, Beginn {{vertrag.beginn}}",
        "{{#positionen}}",
        "{{position.code}} {{position.bezeichnung}} {{position.gesamtpreis}}",
        "{{/positionen}}",
        "Gesamt {{summe.monatlich}}");

    private static readonly byte[] GrundvertragWieHeute = VorlagenpruefungTests.Dokument(
        "Managed-Services-Vertrag mit [Firma und Anschrift des Kunden], Vertragsnummer [Vertragsnummer]");

    private void Datei(string pfad, byte[] inhalt, DateTime? geaendert = null)
    {
        var voll = Path.Combine(_ordner, pfad);
        Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
        File.WriteAllBytes(voll, inhalt);
        File.SetLastWriteTimeUtc(voll, geaendert ?? DateTime.UtcNow);
    }

    private void Standardablage()
    {
        Datei("Rahmen/Rahmenvertrag 01 - Grundvertrag V1.0.docx", GrundvertragWieHeute);
        Datei("Rahmen/Rahmenvertrag 02 - AVB Cloud und Managed Services V1.0.docx", VorlagenpruefungTests.Dokument("AVB"));
        Datei("Leistungsscheine/Leistungsschein S02 - Endpoint Protection (User) V1.0.docx", VorlagenpruefungTests.Dokument("Leistungsschein S02"));
        Datei("Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/Vorlage Leistungsschein S14 V5.7.docx",
            VorlagenpruefungTests.Dokument("Vertreter {{eingabe.Vertreter}}", "{{eingabe.Variante=Cloud-Backup}} Cloud-Backup"));
        Datei("Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/S14 Baukasten und Preisbausteine V5.7.docx", VorlagenpruefungTests.Dokument("intern"));
        Datei("Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/Archiv/Vorlage Leistungsschein S14 V5.6.docx", VorlagenpruefungTests.Dokument("alt"));
        Datei("Bundles/Bundle B01 - User as a Service Standard V1.0.docx", VorlagenpruefungTests.Dokument("Bundle B01"));
    }

    private async Task<string> NeueDatenbankAsync()
    {
        var name = "Vorlagen_" + Guid.NewGuid().ToString("N")[..12];
        await using var kontext = db.NeuerKontextAufDatenbank(name);
        await kontext.Database.MigrateAsync();
        await KatalogErstbefuellung.AusfuehrenAsync(kontext);
        return name;
    }

    private VorlagenDienst Dienst(string datenbank, TestBenutzer benutzer, IVorlagenQuelle? quelle = null) =>
        new(new Fabrik(() => db.NeuerKontextAufDatenbank(datenbank, benutzer)), benutzer, TimeProvider.System, quelle ?? new OrdnerQuelle(_ordner));

    private static VorlagenUebersicht Vorlage(IReadOnlyList<VorlagenUebersicht> liste, string code) => liste.Single(v => v.Vorlage.Code == code);

    [Fact]
    public async Task Abgleich_ordnet_Dateien_zu_prueft_sie_und_ueberspringt_Archiv_und_Unbekanntes()
    {
        var datenbank = await NeueDatenbankAsync();
        Standardablage();
        var dienst = Dienst(datenbank, Pm);

        var abgleich = await dienst.AbgleichenAsync();

        Assert.Null(abgleich.Fehler);
        Assert.Equal(7, abgleich.Dateien);
        Assert.Equal(5, abgleich.NeueFassungen);
        Assert.Contains("Nicht zugeordnet: Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/S14 Baukasten und Preisbausteine V5.7.docx", abgleich.Bericht, StringComparison.Ordinal);
        Assert.DoesNotContain("V5.6", abgleich.Bericht, StringComparison.Ordinal);
        Assert.Contains("Ohne Datei in der Quelle: B02, B03,", abgleich.Bericht, StringComparison.Ordinal);

        var liste = await dienst.UebersichtAsync();
        var grundvertrag = Assert.Single(Vorlage(liste, "GRUNDVERTRAG").Fassungen);
        Assert.Equal((VorlagenStatus.ZurPruefung, 1, "1.0"), (grundvertrag.Status, grundvertrag.Nummer, grundvertrag.VersionLaut));
        Assert.True(grundvertrag.HatFehler);
        Assert.Equal("Rahmen/Rahmenvertrag 01 - Grundvertrag V1.0.docx", grundvertrag.Pfad);

        var s14 = Assert.Single(Vorlage(liste, "S14").Fassungen);
        Assert.False(s14.HatFehler);
        Assert.Equal(["Vertreter", "Variante"], s14.Eingaben.Select(e => e.Name));
        Assert.Equal(EingabeArt.Auswahl, s14.Eingaben[1].Art);
        Assert.Contains("S02", Vorlage(liste, "S02").Services);

        // Ohne Änderungen in der Quelle passiert beim zweiten Mal nichts.
        var zweiter = await dienst.AbgleichenAsync();
        Assert.Equal(0, zweiter.NeueFassungen);
        Assert.Equal(2, (await dienst.AbgleicheAsync()).Count);
    }

    [Fact]
    public async Task Neue_Fassung_wartet_auf_Freigabe_und_loest_die_alte_erst_dann_ab()
    {
        var datenbank = await NeueDatenbankAsync();
        Datei("Rahmen/Rahmenvertrag 01 - Grundvertrag V1.0.docx", GrundvertragWieHeute, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var dienst = Dienst(datenbank, Pm);
        await dienst.AbgleichenAsync();
        var v1 = Assert.Single(Vorlage(await dienst.UebersichtAsync(), "GRUNDVERTRAG").Fassungen);

        // V1 hat Fehler (keine Platzhalter) und lässt sich nicht aktivieren; ablehnen nur mit Begründung.
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.AktivierenAsync(v1.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.AblehnenAsync(v1.Id, " "));
        await dienst.AblehnenAsync(v1.Id, "Platzhalter fehlen noch");

        // Datei in SharePoint überarbeitet, Name unverändert: neue Fassung V2.
        Datei("Rahmen/Rahmenvertrag 01 - Grundvertrag V1.0.docx", GrundvertragMitPlatzhaltern, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var abgleich = await dienst.AbgleichenAsync();
        Assert.Contains("GRUNDVERTRAG: neue Fassung V2", abgleich.Bericht, StringComparison.Ordinal);

        var vorlage = Vorlage(await dienst.UebersichtAsync(), "GRUNDVERTRAG");
        var v2 = vorlage.Offen!;
        Assert.Equal(2, v2.Nummer);
        Assert.False(v2.HatFehler, string.Join("; ", v2.Hinweise.Select(h => h.Text)));
        Assert.Null(vorlage.Aktiv);

        await dienst.AktivierenAsync(v2.Id, "geprüft");
        vorlage = Vorlage(await dienst.UebersichtAsync(), "GRUNDVERTRAG");
        Assert.Equal(v2.Id, vorlage.Aktiv!.Id);
        Assert.Equal(("1.0", "Rahmenvertrag 01 - Grundvertrag V1.0.docx"), (vorlage.Vorlage.Version, vorlage.Vorlage.Dateiname));
        Assert.Equal((Pm.Name, "geprüft"), (vorlage.Aktiv.EntschiedenVon, vorlage.Aktiv.Kommentar));
        Assert.Equal(VorlagenStatus.Abgelehnt, vorlage.Fassungen.Single(f => f.Nummer == 1).Status);
        Assert.Equal(GrundvertragMitPlatzhaltern, (await dienst.DateiAsync(v2.Id)).Inhalt);
    }

    [Fact]
    public async Task Neuere_Fassung_ersetzt_eine_noch_offene_und_aeltere_aktive_laesst_sich_zurueckholen()
    {
        var datenbank = await NeueDatenbankAsync();
        var pfad = "Leistungsscheine/Leistungsschein S02 - Endpoint Protection (User) V1.0.docx";
        Datei(pfad, VorlagenpruefungTests.Dokument("Fassung 1"), new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var dienst = Dienst(datenbank, Pm);
        await dienst.AbgleichenAsync();
        await dienst.AktivierenAsync(Vorlage(await dienst.UebersichtAsync(), "S02").Offen!.Id, null);

        Datei(pfad, VorlagenpruefungTests.Dokument("Fassung 2"), new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
        await dienst.AbgleichenAsync();
        Datei(pfad, VorlagenpruefungTests.Dokument("Fassung 3"), new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        await dienst.AbgleichenAsync();

        var fassungen = Vorlage(await dienst.UebersichtAsync(), "S02").Fassungen;
        Assert.Equal(
            [(3, VorlagenStatus.ZurPruefung), (2, VorlagenStatus.Abgeloest), (1, VorlagenStatus.Aktiv)],
            fassungen.Select(f => (f.Nummer, f.Status)));

        await dienst.AktivierenAsync(fassungen[0].Id, null);
        await dienst.AktivierenAsync(fassungen[2].Id, "zurück auf Fassung 1");
        fassungen = Vorlage(await dienst.UebersichtAsync(), "S02").Fassungen;
        Assert.Equal(1, fassungen.Single(f => f.Status == VorlagenStatus.Aktiv).Nummer);
        Assert.Equal(VorlagenStatus.Abgeloest, fassungen[0].Status);
    }

    [Fact]
    public async Task Neue_Vorlage_aus_der_Quelle_wird_angelegt()
    {
        var datenbank = await NeueDatenbankAsync();
        Datei("Leistungsscheine/Leistungsschein S70 - Neuer Dienst V1.0.docx", VorlagenpruefungTests.Dokument("S70"));
        var dienst = Dienst(datenbank, Pm);

        var abgleich = await dienst.AbgleichenAsync();

        Assert.Contains("S70: neue Vorlage angelegt (Neuer Dienst)", abgleich.Bericht, StringComparison.Ordinal);
        var s70 = Vorlage(await dienst.UebersichtAsync(), "S70");
        Assert.Equal(DokumentTyp.Leistungsschein, s70.Vorlage.Typ);
        Assert.Single(s70.Fassungen);
    }

    [Fact]
    public async Task Ohne_Quelle_oder_bei_fehlendem_Ordner_steht_der_Grund_im_Abgleich()
    {
        var datenbank = await NeueDatenbankAsync();

        var ohne = await Dienst(datenbank, Pm, new KeineVorlagenquelle()).AbgleichenAsync();
        var fehlt = await Dienst(datenbank, Pm, new OrdnerQuelle(Path.Combine(_ordner, "gibt-es-nicht"))).AbgleichenAsync();

        Assert.Contains("keine Vorlagenquelle eingerichtet", ohne.Fehler, StringComparison.Ordinal);
        Assert.Contains("gibt es nicht", fehlt.Fehler, StringComparison.Ordinal);
        Assert.NotNull(fehlt.Ende);
    }

    [Fact]
    public async Task Nur_Produktmanagement_gleicht_ab_und_entscheidet_Fuehrung_liest_Vertrieb_sieht_nichts()
    {
        var datenbank = await NeueDatenbankAsync();
        Standardablage();
        var pm = Dienst(datenbank, Pm);
        await pm.AbgleichenAsync();
        var fassung = Vorlage(await pm.UebersichtAsync(), "S02").Offen!;
        var fuehrung = Dienst(datenbank, new TestBenutzer("fuehrung@noesse.de", Rollen.Fuehrung));
        var vertrieb = Dienst(datenbank, new TestBenutzer("vertrieb@noesse.de", Rollen.Vertrieb));

        Assert.NotEmpty(await fuehrung.UebersichtAsync());
        Assert.NotEmpty((await fuehrung.DateiAsync(fassung.Id)).Inhalt);
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.AbgleichenAsync());
        await Assert.ThrowsAsync<KeinZugriffException>(() => fuehrung.AktivierenAsync(fassung.Id, null));
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.UebersichtAsync());
        await Assert.ThrowsAsync<KeinZugriffException>(() => vertrieb.DateiAsync(fassung.Id));
    }

    [Fact]
    public async Task Fassungen_und_Dateien_sind_unveraenderlich_nur_die_Entscheidung_aendert_sich()
    {
        var datenbank = await NeueDatenbankAsync();
        Standardablage();
        await Dienst(datenbank, Pm).AbgleichenAsync();

        await using (var kontext = db.NeuerKontextAufDatenbank(datenbank))
        {
            var datei = await kontext.Set<VorlagenDatei>().FirstAsync();
            kontext.Entry(datei).Property(d => d.Inhalt).CurrentValue = [1, 2, 3];
            await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
        }

        await using (var kontext = db.NeuerKontextAufDatenbank(datenbank))
        {
            var fassung = await kontext.Vorlagenversionen.FirstAsync();
            kontext.Entry(fassung).Property(f => f.Sha256).CurrentValue = new string('0', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
        }

        await using (var kontext = db.NeuerKontextAufDatenbank(datenbank))
        {
            kontext.Remove(await kontext.Vorlagenversionen.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => kontext.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Nur_ein_Abgleich_zur_Zeit()
    {
        var datenbank = await NeueDatenbankAsync();
        var quelle = new LangsameQuelle();
        var dienst = Dienst(datenbank, Pm, quelle);

        var erster = dienst.AbgleichenAsync();
        await quelle.Gestartet.Task;
        var zweiter = await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.AbgleichenAsync());
        quelle.Weiter.SetResult();
        await erster;

        Assert.Contains("läuft gerade", zweiter.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-10-04T00:00:00Z", "2026-10-04T00:30:00Z")] // 02:30 Sommerzeit = 00:30 UTC
    [InlineData("2026-10-04T01:00:00Z", "2026-10-05T00:30:00Z")]
    [InlineData("2026-10-25T00:00:00Z", "2026-10-25T01:30:00Z")] // Umstellung auf Winterzeit
    public void Naechtlicher_Lauf_rechnet_in_Berliner_Ortszeit(string jetzt, string erwartet) =>
        Assert.Equal(DateTimeOffset.Parse(erwartet, System.Globalization.CultureInfo.InvariantCulture),
            NaechtlicherVorlagenabgleich.NaechsterLauf(DateTimeOffset.Parse(jetzt, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(2, 30)));

    [Theory]
    [InlineData("02:30", true)]
    [InlineData("2:30", true)]
    [InlineData("", false)]
    [InlineData("nachts", false)]
    public void Uhrzeit_aus_der_Konfiguration(string text, bool gueltig) =>
        Assert.Equal(gueltig, NaechtlicherVorlagenabgleich.Uhrzeit(text) is not null);

    private sealed class LangsameQuelle : IVorlagenQuelle
    {
        public TaskCompletionSource Gestartet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Weiter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "Test";

        public string Ort => "Test";

        public async Task<IReadOnlyList<QuellDatei>> ListeAsync(CancellationToken abbruch)
        {
            Gestartet.SetResult();
            await Weiter.Task;
            return [];
        }

        public Task<byte[]> LadeAsync(QuellDatei datei, CancellationToken abbruch) => throw new NotSupportedException();
    }

    private sealed class Fabrik(Func<KalkulatorDbContext> erzeugen) : IDbContextFactory<KalkulatorDbContext>
    {
        public KalkulatorDbContext CreateDbContext() => erzeugen();
    }
}
