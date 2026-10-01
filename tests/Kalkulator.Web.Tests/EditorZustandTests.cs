using Kalkulator.Domain.Berechnung;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Web.Components.Editor;

namespace Kalkulator.Web.Tests;

/// <summary>Der Editor übersetzt verlustfrei zwischen Oberfläche und Rechenkern-Eingabe.</summary>
public class EditorZustandTests
{
    private static readonly GeladenerKatalog Katalog = Erzeuge();

    private static GeladenerKatalog Erzeuge()
    {
        var katalog = KatalogErstbefuellung.ErzeugeKatalog();
        return new GeladenerKatalog(katalog.Services, katalog.Preisliste);
    }

    private static readonly IReadOnlySet<string> ConnectCodes = new HashSet<string> { "S01-STD", "S01-PRM", "S01-ENT" };

    [Fact]
    public void Gespeicherte_Eingabe_kommt_unveraendert_zurueck()
    {
        var eingabe = new KalkulationsEingabe
        {
            Positionen = [new("S01-PRM", 1), new("B02", 40), new("S32", 40)],
            AnzahlUser = 40,
            AnzahlMitarbeitende = 55,
            BisherigerMonatspreis = 2500m,
            Supportkontingent = new SupportkontingentEingabe(6, 2),
            ServerBackup = new ServerBackupEingabe(BackupVariante.Objektspeicher, 3, BelegteTb: 2.5m, Lizenzinstanzen: 3),
            CloudServer = new CloudServerEingabe(412.37m, CloudBackup.Kunde),
        };

        var zustand = EditorZustand.Aus("Variante A", new DateOnly(2027, 1, 1), eingabe, ConnectCodes);
        var zurueck = zustand.AlsEingabe();

        Assert.Equal("S01-PRM", zustand.ConnectCode);
        Assert.Equal(new DateOnly(2027, 1, 1), zustand.VertragsbeginnDatum);
        Assert.Equal(eingabe.Positionen.OrderBy(p => p.KomponentenCode), zurueck.Positionen.OrderBy(p => p.KomponentenCode));
        Assert.Equal(eingabe with { Positionen = zurueck.Positionen }, zurueck);
    }

    [Fact]
    public void Mengen_null_und_abgewaehlte_Rechner_fallen_aus_der_Eingabe()
    {
        var zustand = new EditorZustand { ConnectCode = "S01-STD", AnzahlUser = 10 };
        zustand.SetzeMenge("B01", 10);
        zustand.SetzeMenge("S32", 5);
        zustand.SetzeMenge("S32", 0);
        zustand.MitServerBackup = false;
        zustand.BackupVariante = BackupVariante.Cloud;
        zustand.NativGeschuetzteGb = 400;

        var eingabe = zustand.AlsEingabe();

        Assert.Equal([new PositionsEingabe("S01-STD", 1), new PositionsEingabe("B01", 10)], eingabe.Positionen);
        Assert.Null(eingabe.ServerBackup);
        Assert.Null(eingabe.Supportkontingent);
    }

    [Fact]
    public void Felder_der_anderen_Backup_Variante_werden_nicht_mitgeschickt()
    {
        var zustand = new EditorZustand { MitServerBackup = true, BackupVariante = BackupVariante.Cloud, BackupServer = 2, NativGeschuetzteGb = 400, BelegteTb = 3, Lizenzinstanzen = 2 };

        Assert.Equal(new ServerBackupEingabe(BackupVariante.Cloud, 2, 400), zustand.AlsEingabe().ServerBackup);
    }

    [Fact]
    public void Katalog_wird_fuer_den_Editor_gegliedert()
    {
        var (connect, gruppen) = EditorZustand.Gliedere(Katalog);
        var codes = gruppen.SelectMany(g => g.Zeilen).Select(z => z.Code).ToList();

        Assert.Equal(["S01-STD", "S01-PRM", "S01-ENT"], connect.Select(c => c.Code));
        Assert.Equal(249m, connect[0].Preis);
        Assert.Equal("User as a Service", gruppen[0].Name);
        Assert.Contains("B01", codes);
        Assert.Contains("S41-ROADMAP", codes);

        // Sonderrechner, Onboarding und zukünftige Services erscheinen nicht als freie Position.
        Assert.DoesNotContain(codes, c => c.StartsWith("S14", StringComparison.Ordinal) || c is "S60" or "S61" || c.EndsWith("-ONB", StringComparison.Ordinal));
        Assert.DoesNotContain(gruppen, g => g.Name == "Zukünftige Services");

        var s41 = gruppen.SelectMany(g => g.Zeilen).Single(z => z.Code == "S41");
        Assert.Equal("nach Mitarbeitenden", s41.PreisHinweis);
        var client = gruppen.SelectMany(g => g.Zeilen).Single(z => z.Code == "S25-CLIENT");
        Assert.DoesNotContain("–", client.Bezeichnung, StringComparison.Ordinal);
    }

    [Fact]
    public void Gegliederter_Editor_rechnet_RK01_wie_der_Rechenkern()
    {
        var zustand = new EditorZustand { ConnectCode = "S01-STD", AnzahlUser = 15 };
        zustand.SetzeMenge("B01", 15);
        zustand.SetzeMenge("B03", 2);
        zustand.SetzeMenge("B05", 1);

        var ergebnis = Katalog.ErzeugeRechenkern().Berechne(zustand.AlsEingabe());

        Assert.False(ergebnis.HatFehler);
        Assert.Equal(1027.20m, ergebnis.SummeMonatlich);
    }
}
