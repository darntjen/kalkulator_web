using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Managed-Services-Kalkulation innerhalb eines Kundenprojekts; mehrere je Projekt sind Varianten (B-08).
/// Die Kalkulation hat genau einen bearbeitbaren Arbeitsstand. Erst beim Erzeugen eines Angebots entsteht daraus
/// eine unveränderliche <see cref="Kalkulationsversion"/> (V1, V2 …; Entscheidung 01.10.2026).
/// </summary>
public class Kalkulation
{
    public int Id { get; set; }

    public int KundenprojektId { get; set; }
    public Kundenprojekt? Kundenprojekt { get; set; }

    public required string Titel { get; set; }
    public required string ErstelltVon { get; init; }
    public DateTimeOffset ErstelltAm { get; init; }

    /// <summary>Diese Variante zählt im Forecast (E-09); genau eine je Kundenprojekt.</summary>
    public bool FuerForecast { get; internal set; }

    /// <summary>Gewünschter Vertragsbeginn (B-05). Laufzeit und Zahlungsweise legt der Grundvertrag fest.</summary>
    public DateOnly? Vertragsbeginn { get; set; }

    /// <summary>Arbeitsstand ohne Sonderpositionen; diese liegen mit ihrem Freigabestatus in <see cref="Sonderpositionen"/>.</summary>
    public KalkulationsEingabe Eingabe { get; private set; } = new();

    public int LetzteVersionsnummer { get; private set; }

    public byte[] Zeilenversion { get; private set; } = [];

    public List<Sonderposition> Sonderpositionen { get; } = [];
    public List<Kalkulationsversion> Versionen { get; } = [];

    /// <summary>Überschreibt den Arbeitsstand. Mitgegebene Sonderpositionen werden ignoriert.</summary>
    public void AendereEingabe(KalkulationsEingabe eingabe) => Eingabe = eingabe with { Sonderpositionen = [] };

    public Sonderposition SonderpositionHinzufuegen(string bezeichnung, string einheit, int menge, decimal preis, string begruendung, bool einmalig = false)
    {
        var position = new Sonderposition
        {
            Reihenfolge = Sonderpositionen.Count == 0 ? 1 : Sonderpositionen.Max(s => s.Reihenfolge) + 1,
        };
        position.Aendern(bezeichnung, einheit, menge, preis, begruendung, einmalig);
        Sonderpositionen.Add(position);
        return position;
    }

    /// <summary>Arbeitsstand mit allen Sonderpositionen, so wie ihn der Rechenkern braucht.</summary>
    public KalkulationsEingabe VollstaendigeEingabe() =>
        Eingabe with { Sonderpositionen = [.. Sonderpositionen.OrderBy(s => s.Reihenfolge).Select(s => s.AlsEingabe())] };

    public KalkulationsErgebnis Berechne(Rechenkern kern) => kern.Berechne(VollstaendigeEingabe());

    /// <summary>
    /// Friert den Arbeitsstand als neue Version ein: Eingabe, Preisliste, Positionen, Summen und Kosten.
    /// Nur mit einer freigegebenen Preisliste und nur ohne Fehler, also auch nicht mit offenen Sonderpositionen (B-22).
    /// </summary>
    public Kalkulationsversion FriereEin(Rechenkern kern, string benutzer, DateTimeOffset zeitpunkt)
    {
        var preisliste = kern.Preisliste;
        if (preisliste.Status != PreislistenStatus.Freigegeben || preisliste.Id == 0)
        {
            throw new InvalidOperationException(
                $"Angebote entstehen nur mit einer gespeicherten, freigegebenen Preisliste; „{preisliste.Bezeichnung}“ ist es nicht.");
        }

        var eingabe = VollstaendigeEingabe();
        var ergebnis = kern.Berechne(eingabe);
        if (!ergebnis.AngebotMoeglich)
        {
            throw new KalkulationNichtAngebotsfaehigException(ergebnis.Meldungen.Where(m => m.Schwere == Schwere.Fehler).Select(m => m.Text));
        }

        LetzteVersionsnummer++;
        var version = new Kalkulationsversion
        {
            Nummer = LetzteVersionsnummer,
            ErstelltAm = zeitpunkt,
            ErstelltVon = benutzer,
            PreislisteId = preisliste.Id,
            Eingabe = eingabe,
            Vertragsbeginn = Vertragsbeginn,
            SummeMonatlich = ergebnis.SummeMonatlich,
            SummeEinmalig = ergebnis.SummeEinmalig,
        };
        version.Positionen.AddRange(ergebnis.Positionen.Select((p, i) => new VersionsPosition
        {
            Reihenfolge = i + 1,
            Code = p.Code,
            Bezeichnung = p.Bezeichnung,
            ServiceCode = p.ServiceCode,
            Menge = p.Menge,
            BerechneteMenge = p.BerechneteMenge,
            Einzelpreis = p.Einzelpreis,
            Betrag = p.Betrag,
            Abrechnungsart = p.Abrechnungsart,
            Herkunft = p.Herkunft,
            Hinweis = p.Hinweis,
            Kosten = new PositionsKosten { Kosten = p.Kosten },
        }));
        Versionen.Add(version);
        return version;
    }

    /// <summary>
    /// Kopiert den Arbeitsstand in eine neue Kalkulation desselben Kundenprojekts (B-08), z. B. als Variante.
    /// Versionen werden nicht kopiert. Freigaben unveränderter Sonderpositionen bleiben erhalten.
    /// </summary>
    public Kalkulation Duplizieren(string titel, string benutzer, DateTimeOffset zeitpunkt)
    {
        var projekt = Kundenprojekt ?? throw new InvalidOperationException("Zum Duplizieren muss das Kundenprojekt geladen sein.");
        var kopie = projekt.NeueKalkulation(titel, benutzer, zeitpunkt);
        kopie.Vertragsbeginn = Vertragsbeginn;
        kopie.AendereEingabe(Eingabe);
        kopie.Sonderpositionen.AddRange(Sonderpositionen.Select(s => s.Kopie()));
        return kopie;
    }
}

public class KalkulationNichtAngebotsfaehigException(IEnumerable<string> fehler)
    : InvalidOperationException("Die Kalkulation enthält Fehler und kann nicht eingefroren werden: " + string.Join(" ", fehler));
