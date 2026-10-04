using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Projekte;

namespace Kalkulator.Domain.Tests.Projekte;

public class KundenprojektTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Kundenprojekt NeuesProjekt() =>
        Kundenprojekt.Anlegen(new Kunde { Firma = "Muster GmbH" }, "Managed Services 2027", "vertrieb@noesse.de", false, "vertrieb@noesse.de", Jetzt);

    [Fact]
    public void Neues_Projekt_startet_als_Entwurf_mit_erstem_Statusereignis()
    {
        var projekt = NeuesProjekt();

        Assert.Equal(ProjektStatus.Entwurf, projekt.Status);
        var ereignis = Assert.Single(projekt.StatusEreignisse);
        Assert.Null(ereignis.Alt);
        Assert.Equal(ProjektStatus.Entwurf, ereignis.Neu);
    }

    [Fact]
    public void Statuswechsel_folgen_dem_Zustandsdiagramm_und_werden_protokolliert()
    {
        var projekt = NeuesProjekt();

        projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "v", Jetzt);
        projekt.SetzeStatus(ProjektStatus.Zurueckgestellt, "v", Jetzt, "Budget erst 2027");
        projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "v", Jetzt);
        projekt.SetzeStatus(ProjektStatus.VertragErstellt, "v", Jetzt);
        projekt.SetzeStatus(ProjektStatus.Gewonnen, "v", Jetzt, angenommenesAngebotId: 7);

        Assert.Equal(ProjektStatus.Gewonnen, projekt.Status);
        Assert.Equal(7, projekt.AngenommenesAngebotId);
        Assert.Equal(6, projekt.StatusEreignisse.Count);
        Assert.Equal("Budget erst 2027", projekt.StatusEreignisse[2].Kommentar);
        Assert.Equal(ProjektStatus.Zurueckgestellt, projekt.StatusEreignisse[3].Alt);
    }

    [Theory]
    [InlineData(ProjektStatus.Gewonnen)]
    [InlineData(ProjektStatus.Verloren)]
    [InlineData(ProjektStatus.VertragErstellt)]
    public void Aus_dem_Entwurf_geht_es_nur_zum_versendeten_Angebot(ProjektStatus ziel)
    {
        var projekt = NeuesProjekt();

        Assert.Throws<UngueltigerStatuswechselException>(() =>
            projekt.SetzeStatus(ziel, "v", Jetzt, verlustgrund: ziel == ProjektStatus.Verloren ? Verlustgrund.Preis : null));
        Assert.Single(projekt.StatusEreignisse);
    }

    [Fact]
    public void Gewonnen_braucht_das_angenommene_Angebot_und_geht_direkt_nach_dem_Angebot()
    {
        var projekt = NeuesProjekt();
        projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "v", Jetzt);

        Assert.Throws<ArgumentException>(() => projekt.SetzeStatus(ProjektStatus.Gewonnen, "v", Jetzt));
        Assert.Throws<ArgumentException>(() => projekt.SetzeStatus(ProjektStatus.Zurueckgestellt, "v", Jetzt, angenommenesAngebotId: 3));
        projekt.SetzeStatus(ProjektStatus.Gewonnen, "v", Jetzt, "Zusage per Mail", angenommenesAngebotId: 3);

        Assert.Equal((ProjektStatus.Gewonnen, 3), (projekt.Status, projekt.AngenommenesAngebotId));
    }

    [Fact]
    public void Gewonnen_und_Verloren_sind_endgueltig()
    {
        var projekt = NeuesProjekt();
        projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "v", Jetzt);
        projekt.SetzeStatus(ProjektStatus.Verloren, "v", Jetzt, "Anderer Anbieter günstiger", Verlustgrund.Wettbewerber);

        Assert.Equal(Verlustgrund.Wettbewerber, projekt.Verlustgrund);
        Assert.False(projekt.KannWechselnZu(ProjektStatus.Entwurf));
        Assert.False(projekt.KannWechselnZu(ProjektStatus.AngebotVersendet));
    }

    [Fact]
    public void Verloren_braucht_einen_Verlustgrund_und_nur_Verloren_darf_einen_haben()
    {
        var projekt = NeuesProjekt();
        projekt.SetzeStatus(ProjektStatus.AngebotVersendet, "v", Jetzt);

        Assert.Throws<ArgumentException>(() => projekt.SetzeStatus(ProjektStatus.Verloren, "v", Jetzt));
        Assert.Throws<ArgumentException>(() => projekt.SetzeStatus(ProjektStatus.VertragErstellt, "v", Jetzt, verlustgrund: Verlustgrund.Preis));
        Assert.Equal(ProjektStatus.AngebotVersendet, projekt.Status);
    }

    [Fact]
    public void Forecast_wird_geprueft_und_auf_den_Monatsersten_gesetzt()
    {
        var projekt = NeuesProjekt();

        projekt.SetzeForecast(60, new DateOnly(2026, 12, 17));

        Assert.Equal(60, projekt.Wahrscheinlichkeit);
        Assert.Equal(new DateOnly(2026, 12, 1), projekt.ErwarteterAbschlussmonat);
        Assert.Throws<ArgumentOutOfRangeException>(() => projekt.SetzeForecast(101, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => projekt.SetzeForecast(-1, null));
    }

    [Fact]
    public void Erste_Kalkulation_zaehlt_im_Forecast_und_die_Markierung_ist_eindeutig()
    {
        var projekt = NeuesProjekt();
        var a = projekt.NeueKalkulation("Variante A", "v", Jetzt);
        var b = projekt.NeueKalkulation("Variante B", "v", Jetzt);

        Assert.True(a.FuerForecast);
        Assert.False(b.FuerForecast);

        projekt.FuerForecastMarkieren(b);

        Assert.False(a.FuerForecast);
        Assert.True(b.FuerForecast);
        Assert.Throws<ArgumentException>(() => projekt.FuerForecastMarkieren(NeuesProjekt().NeueKalkulation("fremd", "v", Jetzt)));
    }
}

public class KalkulationTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Kalkulation NeueKalkulation() =>
        Kundenprojekt.Anlegen(new Kunde { Firma = "Muster GmbH" }, "MS", "v", false, "v", Jetzt).NeueKalkulation("Variante A", "v", Jetzt);

    [Fact]
    public void Arbeitsstand_enthaelt_keine_Sonderpositionen_die_kommen_mit_Freigabestatus_dazu()
    {
        var kalkulation = NeueKalkulation();
        kalkulation.AendereEingabe(new KalkulationsEingabe
        {
            Positionen = [new("S01-STD", 1)],
            AnzahlUser = 10,
            Sonderpositionen = [new("ignoriert", "Stück", 1, 10m, "x")],
        });
        var offen = kalkulation.SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch");
        var freigegeben = kalkulation.SonderpositionHinzufuegen("Schulung", "Pauschal", 1, 400m, "Einführung", einmalig: true);
        freigegeben.Freigeben("leitung", Jetzt);

        Assert.Empty(kalkulation.Eingabe.Sonderpositionen);
        var eingabe = kalkulation.VollstaendigeEingabe();
        Assert.Equal(["Sonderreport", "Schulung"], eingabe.Sonderpositionen.Select(s => s.Bezeichnung));
        Assert.False(eingabe.Sonderpositionen[0].Freigegeben);
        Assert.True(eingabe.Sonderpositionen[1].Freigegeben);
        Assert.Equal(2, freigegeben.Reihenfolge);
        Assert.Equal(Freigabestatus.Offen, offen.Status);
    }

    [Fact]
    public void Aenderung_einer_Sonderposition_setzt_die_Freigabe_zurueck()
    {
        var position = NeueKalkulation().SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch");
        position.Freigeben("leitung", Jetzt, "ok");

        position.Aendern("Sonderreport", "Monat", 1, 50m, "Kundenwunsch", false);
        Assert.Equal(Freigabestatus.Freigegeben, position.Status);

        position.Aendern("Sonderreport", "Monat", 1, 45m, "Kundenwunsch", false);
        Assert.Equal(Freigabestatus.Offen, position.Status);
        Assert.Null(position.EntschiedenVon);
        Assert.Null(position.Kommentar);
    }

    [Fact]
    public void Ablehnung_braucht_eine_Begruendung()
    {
        var position = NeueKalkulation().SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch");

        Assert.Throws<ArgumentException>(() => position.Ablehnen("leitung", Jetzt, " "));
        position.Ablehnen("leitung", Jetzt, "Preis zu niedrig");

        Assert.Equal(Freigabestatus.Abgelehnt, position.Status);
        Assert.False(position.AlsEingabe().Freigegeben);
    }

    [Fact]
    public void Duplizieren_kopiert_den_Arbeitsstand_ohne_Versionen_und_Sonderpositionen_brauchen_neue_Freigabe()
    {
        var original = NeueKalkulation();
        original.Vertragsbeginn = new DateOnly(2027, 1, 1);
        original.AendereEingabe(new KalkulationsEingabe { Positionen = [new("S01-STD", 1)], AnzahlUser = 10 });
        original.SonderpositionHinzufuegen("Sonderreport", "Monat", 1, 50m, "Kundenwunsch").Freigeben("leitung", Jetzt);

        var kopie = original.Duplizieren("Variante B", "v", Jetzt);

        Assert.Equal(2, original.Kundenprojekt!.Kalkulationen.Count);
        Assert.False(kopie.FuerForecast);
        Assert.Equal(original.Vertragsbeginn, kopie.Vertragsbeginn);
        Assert.Equal(original.VollstaendigeEingabe().Positionen, kopie.VollstaendigeEingabe().Positionen);
        var sonder = Assert.Single(kopie.Sonderpositionen);
        Assert.Equal(Freigabestatus.Offen, sonder.Status);
        Assert.Null(sonder.EntschiedenVon);
        Assert.Equal(("Sonderreport", 50m), (sonder.Bezeichnung, sonder.Preis));
        Assert.Equal(Freigabestatus.Freigegeben, original.Sonderpositionen[0].Status);
        Assert.NotSame(original.Sonderpositionen[0], kopie.Sonderpositionen[0]);
        Assert.Empty(kopie.Versionen);
    }
}
