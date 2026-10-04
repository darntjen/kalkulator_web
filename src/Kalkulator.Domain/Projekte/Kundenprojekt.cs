namespace Kalkulator.Domain.Projekte;

/// <summary>
/// Oberstes Objekt: alles, was einem Kunden zu einem Vorhaben angeboten wird (G-01). Trägt Projektstatus,
/// Statushistorie und Forecast; enthält die Managed-Services-Kalkulationen (Varianten).
/// </summary>
public class Kundenprojekt
{
    /// <summary>Erlaubte Wechsel laut Zustandsdiagramm in docs/03_fachmodell.md.</summary>
    private static readonly Dictionary<ProjektStatus, ProjektStatus[]> Uebergaenge = new()
    {
        [ProjektStatus.Entwurf] = [ProjektStatus.AngebotVersendet],
        // „Gewonnen“ direkt nach dem Angebot: Der Kunde sagt zu, danach entsteht das Vertragswerk (#26).
        [ProjektStatus.AngebotVersendet] =
            [ProjektStatus.Entwurf, ProjektStatus.VertragErstellt, ProjektStatus.Gewonnen, ProjektStatus.Verloren, ProjektStatus.Zurueckgestellt],
        [ProjektStatus.VertragErstellt] =
            [ProjektStatus.Entwurf, ProjektStatus.Gewonnen, ProjektStatus.Verloren, ProjektStatus.Zurueckgestellt],
        [ProjektStatus.Zurueckgestellt] = [ProjektStatus.AngebotVersendet],
        [ProjektStatus.Gewonnen] = [],
        [ProjektStatus.Verloren] = [],
    };

    public int Id { get; set; }
    public required string Titel { get; set; }

    public int KundeId { get; set; }
    public Kunde? Kunde { get; set; }

    /// <summary>Verantwortlicher Vertrieb (Anmeldename aus Entra ID).</summary>
    public required string Verantwortlich { get; set; }

    public bool Bestandskunde { get; set; }

    public ProjektStatus Status { get; private set; } = ProjektStatus.Entwurf;
    public Verlustgrund? Verlustgrund { get; private set; }

    /// <summary>Angebot, das der Kunde angenommen hat; gesetzt mit dem Status „Gewonnen“ (#26).</summary>
    public int? AngenommenesAngebotId { get; private set; }

    /// <summary>Abschlusswahrscheinlichkeit in Prozent (G-05).</summary>
    public int? Wahrscheinlichkeit { get; private set; }

    /// <summary>Erwarteter Abschlussmonat, immer der Monatserste (G-05).</summary>
    public DateOnly? ErwarteterAbschlussmonat { get; private set; }

    public DateTimeOffset AngelegtAm { get; private set; }
    public string AngelegtVon { get; private set; } = "";

    public byte[] Zeilenversion { get; private set; } = [];

    public List<StatusEreignis> StatusEreignisse { get; } = [];
    public List<Kalkulation> Kalkulationen { get; } = [];

    public static Kundenprojekt Anlegen(Kunde kunde, string titel, string verantwortlich, bool bestandskunde, string benutzer, DateTimeOffset zeitpunkt)
    {
        var projekt = new Kundenprojekt
        {
            Kunde = kunde,
            Titel = titel,
            Verantwortlich = verantwortlich,
            Bestandskunde = bestandskunde,
            AngelegtAm = zeitpunkt,
            AngelegtVon = benutzer,
        };
        projekt.StatusEreignisse.Add(new StatusEreignis { Neu = ProjektStatus.Entwurf, Zeitpunkt = zeitpunkt, Benutzer = benutzer });
        return projekt;
    }

    public bool KannWechselnZu(ProjektStatus neu) => Uebergaenge[Status].Contains(neu);

    /// <summary>
    /// Setzt den Projektstatus und protokolliert den Wechsel. Bei „Verloren“ ist ein Verlustgrund Pflicht, bei
    /// „Gewonnen“ das angenommene Angebot (geprüft vom Anwendungsdienst, weil Angebote ein eigenes Aggregat sind).
    /// </summary>
    public void SetzeStatus(ProjektStatus neu, string benutzer, DateTimeOffset zeitpunkt, string? kommentar = null, Verlustgrund? verlustgrund = null,
        int? angenommenesAngebotId = null)
    {
        if (!KannWechselnZu(neu))
        {
            throw new UngueltigerStatuswechselException(Status, neu);
        }

        if (neu == ProjektStatus.Gewonnen && angenommenesAngebotId is null)
        {
            throw new ArgumentException("Bei „Gewonnen“ bitte das angenommene Angebot wählen.", nameof(angenommenesAngebotId));
        }

        if (neu != ProjektStatus.Gewonnen && angenommenesAngebotId is not null)
        {
            throw new ArgumentException("Ein angenommenes Angebot gehört nur zum Status „Gewonnen“.", nameof(angenommenesAngebotId));
        }

        if (neu == ProjektStatus.Verloren && verlustgrund is null)
        {
            throw new ArgumentException("Bei „Verloren“ ist ein Verlustgrund Pflicht.", nameof(verlustgrund));
        }

        if (neu != ProjektStatus.Verloren && verlustgrund is not null)
        {
            throw new ArgumentException("Ein Verlustgrund gehört nur zum Status „Verloren“.", nameof(verlustgrund));
        }

        StatusEreignisse.Add(new StatusEreignis
        {
            Alt = Status,
            Neu = neu,
            Zeitpunkt = zeitpunkt,
            Benutzer = benutzer,
            Verlustgrund = verlustgrund,
            Kommentar = string.IsNullOrWhiteSpace(kommentar) ? null : kommentar.Trim(),
        });
        Status = neu;
        Verlustgrund = verlustgrund;
        AngenommenesAngebotId = angenommenesAngebotId;
    }

    /// <summary>Pflegt den Forecast (G-05). Der Monat wird auf den Monatsersten gesetzt.</summary>
    public void SetzeForecast(int? wahrscheinlichkeit, DateOnly? abschlussmonat)
    {
        if (wahrscheinlichkeit is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(wahrscheinlichkeit), "Die Wahrscheinlichkeit liegt zwischen 0 und 100 %.");
        }

        Wahrscheinlichkeit = wahrscheinlichkeit;
        ErwarteterAbschlussmonat = abschlussmonat is { } monat ? new DateOnly(monat.Year, monat.Month, 1) : null;
    }

    /// <summary>
    /// Legt eine neue Managed-Services-Kalkulation an. Die erste Kalkulation eines Projekts zählt für den Forecast.
    /// </summary>
    public Kalkulation NeueKalkulation(string titel, string benutzer, DateTimeOffset zeitpunkt)
    {
        var kalkulation = new Kalkulation
        {
            Kundenprojekt = this,
            Titel = titel,
            ErstelltVon = benutzer,
            ErstelltAm = zeitpunkt,
            FuerForecast = Kalkulationen.Count == 0,
        };
        Kalkulationen.Add(kalkulation);
        return kalkulation;
    }

    /// <summary>
    /// Kennzeichnet die Variante, die im Forecast zählt (E-09). Genau eine Kalkulation je Projekt;
    /// dafür müssen alle Kalkulationen des Projekts geladen sein.
    /// </summary>
    public void FuerForecastMarkieren(Kalkulation kalkulation)
    {
        if (!Kalkulationen.Contains(kalkulation))
        {
            throw new ArgumentException("Die Kalkulation gehört nicht zu diesem Kundenprojekt.", nameof(kalkulation));
        }

        foreach (var k in Kalkulationen)
        {
            k.FuerForecast = ReferenceEquals(k, kalkulation);
        }
    }
}
