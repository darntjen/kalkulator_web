using Kalkulator.Domain.Kalkulation;
using Kalkulator.Infrastructure.Erstbefuellung;

namespace Kalkulator.Infrastructure.Tests.Referenzkalkulationen;

/// <summary>
/// Die Referenzkalkulationen aus docs/07_referenzkalkulationen.md, gerechnet mit dem echten Katalog aus der
/// Erstbefüllung. Der Kalkulator darf erst produktiv gehen, wenn alle Fälle centgenau stimmen.
/// Keine Datenbank nötig: Der Katalog wird nur im Speicher aufgebaut.
/// </summary>
public class ReferenzkalkulationTests
{
    private static readonly Rechenkern Kern = ErzeugeKern();

    private static Rechenkern ErzeugeKern()
    {
        var katalog = KatalogErstbefuellung.ErzeugeKatalog();
        return new Rechenkern(katalog.Services, katalog.Preisliste);
    }

    private static PositionsEingabe P(string code, int menge = 1) => new(code, menge);

    private static KalkulationsErgebnis Rechne(KalkulationsEingabe eingabe) => Kern.Berechne(eingabe);

    private static void OhneFehler(KalkulationsErgebnis ergebnis) =>
        Assert.True(!ergebnis.HatFehler, string.Join(Environment.NewLine, ergebnis.Meldungen.Select(m => m.Text)));

    private static void FehlerEnthaelt(KalkulationsErgebnis ergebnis, string text) =>
        Assert.Contains(ergebnis.Meldungen, m => m.Schwere == Schwere.Fehler && m.Text.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void RK01_Kleinkunde_mit_Connect_Standard()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B01", 15), P("B03", 2), P("B05")],
            AnzahlUser = 15,
        });

        OhneFehler(ergebnis);
        Assert.Equal(1027.20m, ergebnis.SummeMonatlich);
        Assert.Equal(900.00m, ergebnis.SummeEinmalig);
        Assert.Equal(12326.40m, ergebnis.Jahreswert);
        Assert.Equal(13226.40m, ergebnis.WertErstlaufzeit);

        // Kosten laut EK-Kalkulation: 75,94 + 15 × 13,64 + 2 × 20,08 + 76,74
        Assert.Equal(397.44m, ergebnis.KostenMonatlich);
        Assert.True(ergebnis.KostenVollstaendig);
        Assert.Equal(629.76m, ergebnis.DeckungsbeitragMonatlich);
    }

    [Fact]
    public void RK02_Mittelstand_mit_Addons_und_Supportkontingent()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-PRM"), P("B02", 40), P("B03", 4), P("B05"), P("S31-USER", 40), P("S32", 40)],
            Supportkontingent = new SupportkontingentEingabe(6, 2),
            AnzahlUser = 40,
        });

        OhneFehler(ergebnis);
        Assert.Equal(6095.06m, ergebnis.SummeMonatlich);
        Assert.Equal(2000.00m, ergebnis.SummeEinmalig);
        Assert.Contains(ergebnis.Positionen, p => p.Code == "S60" && p.Betrag == 364.56m);
    }

    [Fact]
    public void RK03_Connect_Enterprise_mit_Netzwerk()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-ENT"), P("B07-SWI", 6), P("B07-WIF", 10), P("S53", 3), P("S21", 2)],
        });

        OhneFehler(ergebnis);
        Assert.Equal(2797.90m, ergebnis.SummeMonatlich);
    }

    [Theory]
    [InlineData(3, 2.5, 7.5, 8, 243.04, 270.00, 26.96)]
    [InlineData(6, 2, 12, 12, 364.56, 405.00, 40.44)]
    public void RK04_Supportkontingent_mit_Rundung_auf_2er_Block(
        int anfragen, decimal ae, decimal bedarf, int kontingent, decimal preis, decimal adHoc, decimal vorteil)
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD")],
            Supportkontingent = new SupportkontingentEingabe(anfragen, ae),
        });

        var s60 = ergebnis.Supportkontingent!;
        Assert.Equal(bedarf, s60.BedarfAe);
        Assert.Equal(kontingent, s60.KontingentAe);
        Assert.Equal(kontingent / 4m, s60.Stunden);
        Assert.Equal(preis, s60.Monatspreis);
        Assert.Equal(adHoc, s60.AdHocPreis);
        Assert.Equal(vorteil, s60.Kundenvorteil);
    }

    [Theory]
    [InlineData("S01-ENT", 30, 2200.00)]
    [InlineData("S01-ENT", 31, 3000.00)]
    [InlineData("S01-PRM", 250, 2800.00)]
    [InlineData("S01-STD", 251, 2800.00)]
    public void RK05_Onboarding_Grenzfaelle(string connect, int user, decimal onboarding)
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P(connect)], AnzahlUser = user });

        Assert.Equal(onboarding, ergebnis.SummeEinmalig);
    }

    [Fact]
    public void RK05_Onboarding_ab_501_User_ist_individuell_und_ohne_Betrag()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD")], AnzahlUser = 501 });

        Assert.Equal(0m, ergebnis.SummeEinmalig);
        Assert.Contains(ergebnis.Meldungen, m => m.Schwere == Schwere.Hinweis && m.Text.Contains("individuell", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(BackupVariante.Cloud, 1, 200, 0, 0, 183.00, 187.00)]
    [InlineData(BackupVariante.Cloud, 3, 400, 0, 0, 213.00, 213.00)]
    [InlineData(BackupVariante.Cloud, 5, 1000, 0, 0, 312.00, 312.00)]
    [InlineData(BackupVariante.Cloud, 8, 2000, 0, 0, 495.00, 495.00)]
    [InlineData(BackupVariante.Cloud, 12, 4000, 0, 0, 831.00, 831.00)]
    [InlineData(BackupVariante.Objektspeicher, 12, 0, 6, 12, 621.60, 621.60)]
    [InlineData(BackupVariante.Objektspeicher, 12, 0, 6, 0, 386.40, 386.40)]
    public void RK06_Server_Backup_nach_Baukasten(
        BackupVariante variante, int server, int gb, decimal tb, int lizenzen, decimal bausteine, decimal monatlich)
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD")],
            ServerBackup = new ServerBackupEingabe(variante, server, gb, tb, lizenzen),
        });

        OhneFehler(ergebnis);
        Assert.Equal(bausteine, ergebnis.ServerBackup!.SummeBausteine);
        Assert.Equal(monatlich, ergebnis.ServerBackup.Monatspreis);
        Assert.Equal(249.00m + monatlich, ergebnis.SummeMonatlich);
    }

    [Fact]
    public void RK07_Ohne_Connect_ist_keine_Kalkulation_moeglich()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("B01", 10)] });

        FehlerEnthaelt(ergebnis, "Connect ist Pflicht");
        Assert.False(ergebnis.AngebotMoeglich);
    }

    [Fact]
    public void RK07_Reine_S41_Kalkulation_braucht_kein_Connect()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S41")], AnzahlMitarbeitende = 35 });

        OhneFehler(ergebnis);
        Assert.True(ergebnis.AngebotMoeglich);
    }

    [Fact]
    public void RK07_S41_mit_weiteren_Services_braucht_Connect()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S41"), P("B01", 10)], AnzahlMitarbeitende = 35 });

        FehlerEnthaelt(ergebnis, "Connect ist Pflicht");
    }

    [Fact]
    public void RK07_Nicht_freigegebene_Sonderposition_sperrt_das_Angebot()
    {
        var offen = new SonderpositionEingabe("Einrichtung Spezialsoftware", "pauschal", 1, 89m, "Kundenwunsch");
        var gesperrt = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD")], Sonderpositionen = [offen] });
        var frei = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD")], Sonderpositionen = [offen with { Freigegeben = true }] });

        FehlerEnthaelt(gesperrt, "Freigabe durch die Vertriebsleitung ausstehend");
        Assert.False(gesperrt.AngebotMoeglich);
        Assert.True(frei.AngebotMoeglich);
        Assert.Equal(249m + 89m, frei.SummeMonatlich);
    }

    [Fact]
    public void RK07_Zwei_Connect_Stufen_sind_nicht_erlaubt()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("S01-PRM")] });

        FehlerEnthaelt(ergebnis, "genau eine Nösse-Connect-Stufe");
    }

    [Fact]
    public void RK07_Einzelservice_im_Bundle_wird_nicht_zusaetzlich_berechnet()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("B01", 20), P("S03", 20)] });

        OhneFehler(ergebnis);
        var s03 = Assert.Single(ergebnis.Positionen, p => p.Code == "S03");
        Assert.Equal(0m, s03.BerechneteMenge);
        Assert.Equal(0m, s03.Betrag);
        Assert.Contains(ergebnis.Meldungen, m => m.Schwere == Schwere.Hinweis && m.Text.Contains("in B01 enthalten", StringComparison.Ordinal));
        Assert.Equal(249m + 20 * 31.90m, ergebnis.SummeMonatlich);
    }

    [Fact]
    public void RK07_Verschachteltes_Bundle_deckt_auch_die_Bestandteile_des_inneren_Bundles_ab()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("B02", 10), P("S03", 15)] });

        var s03 = Assert.Single(ergebnis.Positionen, p => p.Code == "S03");
        Assert.Equal(5m, s03.BerechneteMenge);
        Assert.Equal(5 * 14.90m, s03.Betrag);
    }

    [Fact]
    public void RK07_Zukuenftiger_Service_ist_nicht_auswaehlbar()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("Z-PEN")] });

        Assert.True(ergebnis.HatFehler);
        Assert.DoesNotContain(ergebnis.Positionen, p => p.Code == "Z-PEN");
    }

    [Fact]
    public void RK07_B04_verlangt_den_S14_Baukasten()
    {
        var ohne = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("B04", 2)] });
        var mit = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B04", 2)],
            ServerBackup = new ServerBackupEingabe(BackupVariante.Cloud, 2, 400),
        });

        FehlerEnthaelt(ohne, "S14-Baukasten");
        OhneFehler(mit);
        // S14: 99 € Grund + 2 × 15 € Server + 1 Paket 69 € = 198 € (über der Untergrenze von 187 €).
        Assert.Equal(249m + 2 * 49.90m + 198m, mit.SummeMonatlich);
    }

    [Fact]
    public void RK09_Schwachstellenmanagement_einzeln_mit_Assets()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B01", 30), P("B03", 3), P("S25"), P("S25-CLIENT", 30), P("S25-SERVER", 3)],
        });

        OhneFehler(ergebnis);
        Assert.Equal(1066.00m, ergebnis.Positionen.Where(p => p.ServiceCode == "S25").Sum(p => p.Betrag));
    }

    [Fact]
    public void RK09_B06_mit_S25_Assets()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B06"), P("S25-CLIENT", 30), P("S25-SERVER", 3)],
        });

        OhneFehler(ergebnis);
        Assert.Equal(249m + 2157.00m, ergebnis.SummeMonatlich);
    }

    [Fact]
    public void RK09_S25_ohne_Bundle_B01_bis_B04_ist_nicht_erlaubt()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("S25")] });

        FehlerEnthaelt(ergebnis, "B01, B02, B03 oder B04");
    }

    [Fact]
    public void RK09_Assetpreise_nur_mit_Grundservice()
    {
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = [P("S01-STD"), P("B01", 10), P("S25-CLIENT", 10)] });

        FehlerEnthaelt(ergebnis, "nur zusammen mit dem Grundservice");
    }

    [Theory]
    [InlineData(550.00, 1000.00)]
    [InlineData(412.37, 749.76)]
    public void RK10_Cloud_Server_VK_aus_EK(decimal ek, decimal vk)
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("S21")],
            CloudServer = new CloudServerEingabe(ek, CloudBackup.Kunde),
        });

        OhneFehler(ergebnis);
        var s61 = Assert.Single(ergebnis.Positionen, p => p.Code == "S61");
        Assert.Equal(vk, s61.Betrag);
        Assert.Equal(ek, s61.Kosten);
    }

    [Fact]
    public void RK10_Cloud_Server_braucht_S21()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD")],
            CloudServer = new CloudServerEingabe(550m, CloudBackup.Kunde),
        });

        FehlerEnthaelt(ergebnis, "S21");
    }

    [Fact]
    public void RK10_Cloud_Server_braucht_S21_auch_neben_B05()
    {
        var nurB05 = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B05")],
            CloudServer = new CloudServerEingabe(550m, CloudBackup.Kunde),
        });
        var mitS21 = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B05"), P("S21")],
            CloudServer = new CloudServerEingabe(550m, CloudBackup.Kunde),
        });

        FehlerEnthaelt(nurB05, "S21");
        OhneFehler(mitS21);
        Assert.Equal(129.90m, Assert.Single(mitS21.Positionen, p => p.Code == "S21").Betrag);
    }

    [Fact]
    public void RK10_Cloud_Server_braucht_eine_Backup_Entscheidung()
    {
        var offen = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("S21")],
            CloudServer = new CloudServerEingabe(550m, CloudBackup.Offen),
        });
        var objektspeicher = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("S21")],
            CloudServer = new CloudServerEingabe(550m, CloudBackup.ServerBackup),
            ServerBackup = new ServerBackupEingabe(BackupVariante.Objektspeicher, 1, BelegteTb: 1),
        });

        FehlerEnthaelt(offen, "Datensicherung festlegen");
        FehlerEnthaelt(objektspeicher, "Cloud-Backup");
    }

    [Theory]
    [InlineData(35, false, 350.00, 0)]
    [InlineData(50, false, 350.00, 0)]
    [InlineData(51, true, 550.00, 2400.00)]
    public void RK11_Strategische_IT_Begleitung(int mitarbeitende, bool roadmap, decimal monatlich, decimal einmalig)
    {
        var positionen = roadmap ? new[] { P("S41"), P("S41-ROADMAP") } : [P("S41")];
        var ergebnis = Rechne(new KalkulationsEingabe { Positionen = positionen, AnzahlMitarbeitende = mitarbeitende });

        OhneFehler(ergebnis);
        Assert.Equal(monatlich, ergebnis.SummeMonatlich);
        Assert.Equal(einmalig, ergebnis.SummeEinmalig);
    }

    [Fact]
    public void Vorher_Nachher_Vergleich_fuer_Bestandskunden()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("S01-STD"), P("B01", 15), P("B03", 2), P("B05")],
            BisherigerMonatspreis = 1200m,
        });

        Assert.Equal(-172.80m, ergebnis.Vergleich!.Differenz);
        Assert.Equal(-14.40m, ergebnis.Vergleich.DifferenzProzent);
    }

    [Fact]
    public void Positionen_erscheinen_in_Katalogreihenfolge()
    {
        var ergebnis = Rechne(new KalkulationsEingabe
        {
            Positionen = [P("B05"), P("S32", 5), P("B01", 5), P("S01-PRM")],
            AnzahlUser = 5,
        });

        Assert.Equal(["S01-PRM", "S01-PRM-ONB", "B01", "B05", "S32"], ergebnis.Positionen.Select(p => p.Code));
    }
}
