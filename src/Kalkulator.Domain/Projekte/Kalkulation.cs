using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Preise;
using Kalkulator.Domain.Vertrag;

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

    /// <summary>Angaben für das Vertragswerk, die die Vorlagen verlangen (#26, Teil C); werden mit dem Angebot eingefroren.</summary>
    public Vertragsangaben Vertragsangaben { get; private set; } = new();

    public void AendereVertragsangaben(Vertragsangaben angaben) => Vertragsangaben = angaben;

    /// <summary>Welcher Service welche Herausforderung des Kunden löst (G-04); Teil des Arbeitsstands.</summary>
    public IReadOnlyList<Zuordnung> Zuordnungen { get; private set; } = [];

    public void AendereZuordnungen(IEnumerable<Zuordnung> zuordnungen) =>
        Zuordnungen = [.. zuordnungen.Distinct().OrderBy(z => z.ServiceCode, StringComparer.Ordinal).ThenBy(z => z.HerausforderungId)];

    public int LetzteVersionsnummer { get; private set; }

    /// <summary>Wird mit dem ersten Angebot vergeben und gilt für alle weiteren Versionen dieser Kalkulation.</summary>
    public string? Angebotsnummer { get; private set; }

    public void VergebeAngebotsnummer(string nummer)
    {
        if (Angebotsnummer is not null)
        {
            throw new InvalidOperationException($"Die Kalkulation hat bereits die Angebotsnummer {Angebotsnummer}.");
        }

        Angebotsnummer = nummer;
    }

    public byte[] Zeilenversion { get; private set; } = [];

    public List<Sonderposition> Sonderpositionen { get; } = [];
    public List<Kalkulationsversion> Versionen { get; } = [];

    /// <summary>Vertriebsfreigaben samt Verlauf; gültig sind nur die aktiven (#26).</summary>
    public List<Vertriebsfreigabe> Vertriebsfreigaben { get; } = [];

    public Vertriebsfreigabe? AktiveFreigabe(FreigabeRolle rolle) =>
        Vertriebsfreigaben.SingleOrDefault(f => f.IstAktiv && f.Rolle == rolle);

    /// <summary>Vertriebsleitung und Solution Consultant haben den aktuellen Arbeitsstand freigegeben.</summary>
    public bool IstVertriebsfreigegeben => Enum.GetValues<FreigabeRolle>().All(r => AktiveFreigabe(r) is not null);

    /// <summary>
    /// Gibt den Arbeitsstand aus Sicht einer Rolle frei. Voraussetzungen: Die Kalkulation ist angebotsfähig (keine
    /// Fehler, keine offenen Sonderpositionen), und beide Freigaben kommen von verschiedenen Personen. Wer das
    /// Kundenprojekt verantwortet, darf mit passender Rolle selbst freigeben (Entscheidung 04.10.2026).
    /// </summary>
    public Vertriebsfreigabe VertriebFreigeben(FreigabeRolle rolle, string benutzer, DateTimeOffset zeitpunkt, string? kommentar, KalkulationsErgebnis ergebnis)
    {
        if (!ergebnis.AngebotMoeglich)
        {
            throw new KalkulationNichtAngebotsfaehigException(ergebnis.Meldungen.Where(m => m.Schwere == Schwere.Fehler).Select(m => m.Text));
        }

        if (AktiveFreigabe(rolle) is { } vorhanden)
        {
            throw new InvalidOperationException($"Die Freigabe {Text(rolle)} liegt bereits vor ({vorhanden.Benutzer}).");
        }

        if (Vertriebsfreigaben.Any(f => f.IstAktiv && Gleich(f.Benutzer, benutzer)))
        {
            throw new InvalidOperationException("Die beiden Freigaben müssen von verschiedenen Personen kommen.");
        }

        var freigabe = new Vertriebsfreigabe
        {
            Rolle = rolle,
            Benutzer = benutzer,
            Zeitpunkt = zeitpunkt,
            Kommentar = string.IsNullOrWhiteSpace(kommentar) ? null : kommentar.Trim(),
        };
        Vertriebsfreigaben.Add(freigabe);
        return freigabe;
    }

    /// <summary>Zieht die eigene Freigabe zurück, z. B. nach neuen Erkenntnissen.</summary>
    public void VertriebsfreigabeZurueckziehen(FreigabeRolle rolle, string benutzer, DateTimeOffset zeitpunkt)
    {
        var freigabe = AktiveFreigabe(rolle) ?? throw new InvalidOperationException($"Eine Freigabe {Text(rolle)} liegt nicht vor.");
        if (!Gleich(freigabe.Benutzer, benutzer))
        {
            throw new InvalidOperationException("Nur wer freigegeben hat, kann die Freigabe zurückziehen.");
        }

        freigabe.Aufheben(benutzer, zeitpunkt, "zurückgezogen");
    }

    /// <summary>Hebt alle aktiven Freigaben auf, weil sich der Arbeitsstand geändert hat.</summary>
    /// <returns>Ob es aktive Freigaben gab.</returns>
    public bool HebeVertriebsfreigabenAuf(string benutzer, DateTimeOffset zeitpunkt, string grund)
    {
        var aktive = Vertriebsfreigaben.Where(f => f.IstAktiv).ToList();
        foreach (var f in aktive)
        {
            f.Aufheben(benutzer, zeitpunkt, grund);
        }

        return aktive.Count > 0;
    }

    private static string Text(FreigabeRolle rolle) =>
        rolle == FreigabeRolle.Vertriebsleitung ? "der Vertriebsleitung" : "des Solution Consultants";

    private static bool Gleich(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

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

    public void SonderpositionEntfernen(Sonderposition position)
    {
        if (!Sonderpositionen.Remove(position))
        {
            throw new ArgumentException("Die Sonderposition gehört nicht zu dieser Kalkulation.", nameof(position));
        }
    }

    /// <summary>Arbeitsstand mit allen Sonderpositionen, so wie ihn der Rechenkern braucht.</summary>
    public KalkulationsEingabe VollstaendigeEingabe() =>
        Eingabe with { Sonderpositionen = [.. Sonderpositionen.OrderBy(s => s.Reihenfolge).Select(s => s.AlsEingabe())] };

    public KalkulationsErgebnis Berechne(Rechenkern kern) => kern.Berechne(VollstaendigeEingabe());

    /// <summary>
    /// Friert den Arbeitsstand als neue Version ein: Eingabe, Preisliste, Positionen, Summen und Kosten.
    /// Nur mit einer freigegebenen Preisliste und nur ohne Fehler, also auch nicht mit offenen Sonderpositionen (B-22).
    /// </summary>
    public Kalkulationsversion FriereEin(Rechenkern kern, string benutzer, DateTimeOffset zeitpunkt, IReadOnlyList<Loesungsbezug>? loesungsbezuege = null)
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
            Vertragsangaben = Vertragsangaben,
            // Nur Bezüge zu Services, die tatsächlich im Angebot stehen.
            Loesungsbezuege = [.. (loesungsbezuege ?? []).Where(l => ergebnis.Positionen.Any(p => p.ServiceCode == l.ServiceCode))],
            SummeMonatlich = ergebnis.SummeMonatlich,
            SummeEinmalig = ergebnis.SummeEinmalig,
            FreigabeVertriebsleitungVon = AktiveFreigabe(FreigabeRolle.Vertriebsleitung)?.Benutzer,
            FreigabeVertriebsleitungAm = AktiveFreigabe(FreigabeRolle.Vertriebsleitung)?.Zeitpunkt,
            FreigabeSolutionConsultantVon = AktiveFreigabe(FreigabeRolle.SolutionConsultant)?.Benutzer,
            FreigabeSolutionConsultantAm = AktiveFreigabe(FreigabeRolle.SolutionConsultant)?.Zeitpunkt,
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
    /// Versionen werden nicht kopiert. Sonderpositionen brauchen in der Kopie eine neue Freigabe (Entscheidung 02.10.2026).
    /// </summary>
    public Kalkulation Duplizieren(string titel, string benutzer, DateTimeOffset zeitpunkt)
    {
        var projekt = Kundenprojekt ?? throw new InvalidOperationException("Zum Duplizieren muss das Kundenprojekt geladen sein.");
        var kopie = projekt.NeueKalkulation(titel, benutzer, zeitpunkt);
        kopie.Vertragsbeginn = Vertragsbeginn;
        kopie.AendereEingabe(Eingabe);
        kopie.AendereVertragsangaben(Vertragsangaben);
        kopie.AendereZuordnungen(Zuordnungen);
        kopie.Sonderpositionen.AddRange(Sonderpositionen.Select(s => s.Kopie()));
        return kopie;
    }
}

public class KalkulationNichtAngebotsfaehigException(IEnumerable<string> fehler)
    : InvalidOperationException("Die Kalkulation enthält Fehler und kann nicht eingefroren werden: " + string.Join(" ", fehler));
