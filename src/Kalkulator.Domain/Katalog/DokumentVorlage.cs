using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Domain.Katalog;

/// <summary>
/// Vorlage aus dem Vertragswerk, z. B. ein Leistungsschein. Die Word-Dateien kommen als <see cref="Vorlagenversion"/>
/// aus SharePoint (#26, Teil B); verwendet wird nur die vom Produktmanagement bestätigte, aktive Version.
/// <see cref="Version"/> und <see cref="Dateiname"/> zeigen die aktive Version, solange es eine gibt.
/// </summary>
public class DokumentVorlage
{
    public int Id { get; set; }
    public DokumentTyp Typ { get; set; }

    /// <summary>Code im Vertragswerk, z. B. „S02“, „B01“ oder „AVB“.</summary>
    public required string Code { get; set; }

    public required string Bezeichnung { get; set; }
    public required string Version { get; set; }
    public string? Dateiname { get; set; }

    public List<Vorlagenversion> Versionen { get; } = [];

    public Vorlagenversion? AktiveVersion => Versionen.SingleOrDefault(v => v.Status == VorlagenStatus.Aktiv);

    /// <summary>
    /// Nimmt eine Fassung aus der Quelle auf. Ist der Inhalt schon bekannt (gleicher Hash), passiert nichts und es kommt
    /// <c>null</c> zurück. Eine noch offene ältere Fassung wird durch die neue abgelöst.
    /// </summary>
    public Vorlagenversion? NimmAuf(Vorlagenversion neu, DateTimeOffset zeitpunkt)
    {
        if (Versionen.Any(v => string.Equals(v.Sha256, neu.Sha256, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        foreach (var offen in Versionen.Where(v => v.Status == VorlagenStatus.ZurPruefung))
        {
            offen.Entscheide(VorlagenStatus.Abgeloest, "Abgleich", zeitpunkt, "durch eine neuere Fassung ersetzt");
        }

        neu.Nummer = Versionen.Count == 0 ? 1 : Versionen.Max(v => v.Nummer) + 1;
        neu.Status = VorlagenStatus.ZurPruefung;
        Versionen.Add(neu);
        return neu;
    }

    /// <summary>
    /// Bestätigt eine Fassung; sie gilt ab sofort für neue Vertragswerke, die bisher aktive wird abgelöst. Auch eine
    /// abgelöste Fassung lässt sich wieder aktivieren (Rückkehr zur Vorversion). Fassungen mit Fehlern nicht.
    /// </summary>
    public void Aktiviere(Vorlagenversion version, string benutzer, DateTimeOffset zeitpunkt, string? kommentar)
    {
        PruefeEigene(version);
        if (version.Status is not (VorlagenStatus.ZurPruefung or VorlagenStatus.Abgeloest))
        {
            throw new InvalidOperationException($"V{version.Nummer} ist {Text(version.Status)} und lässt sich nicht aktivieren.");
        }

        if (version.HatFehler)
        {
            throw new InvalidOperationException($"V{version.Nummer} hat Fehler in der Vorlagenprüfung und lässt sich nicht aktivieren.");
        }

        AktiveVersion?.Entscheide(VorlagenStatus.Abgeloest, benutzer, zeitpunkt, $"abgelöst durch V{version.Nummer}");
        version.Entscheide(VorlagenStatus.Aktiv, benutzer, zeitpunkt, kommentar);
        Version = version.VersionLaut ?? $"V{version.Nummer}";
        Dateiname = version.Dateiname;
    }

    public void Ablehnen(Vorlagenversion version, string benutzer, DateTimeOffset zeitpunkt, string kommentar)
    {
        PruefeEigene(version);
        if (string.IsNullOrWhiteSpace(kommentar))
        {
            throw new ArgumentException("Bitte begründen, warum die Fassung abgelehnt wird.");
        }

        if (version.Status != VorlagenStatus.ZurPruefung)
        {
            throw new InvalidOperationException($"V{version.Nummer} ist {Text(version.Status)}; ablehnen lassen sich nur Fassungen zur Prüfung.");
        }

        version.Entscheide(VorlagenStatus.Abgelehnt, benutzer, zeitpunkt, kommentar);
    }

    private void PruefeEigene(Vorlagenversion version)
    {
        if (!Versionen.Contains(version))
        {
            throw new ArgumentException($"V{version.Nummer} gehört nicht zur Vorlage {Code}.", nameof(version));
        }
    }

    internal static string Text(VorlagenStatus status) => status switch
    {
        VorlagenStatus.ZurPruefung => "zur Prüfung",
        VorlagenStatus.Aktiv => "aktiv",
        VorlagenStatus.Abgelehnt => "abgelehnt",
        _ => "abgelöst",
    };
}

public enum VorlagenStatus
{
    ZurPruefung = 1,
    Aktiv = 2,
    Abgelehnt = 3,
    Abgeloest = 4,
}

/// <summary>
/// Eine Fassung einer Vorlage, wie sie aus der Quelle kam. Inhalt, Herkunft und Prüfergebnis sind unveränderlich,
/// damit jedes Vertragswerk nachweisbar auf einer bestimmten Fassung beruht; nur die Entscheidung ändert sich.
/// </summary>
public class Vorlagenversion
{
    public int Id { get; set; }
    public int DokumentVorlageId { get; set; }

    /// <summary>Laufende Nummer je Vorlage (V1, V2 …), unabhängig von der Angabe im Dateinamen.</summary>
    public int Nummer { get; internal set; }

    /// <summary>Version laut Dateiname, z. B. „1.0“; kann bei Änderungen ohne Umbenennung gleich bleiben.</summary>
    public string? VersionLaut { get; init; }

    public required string Dateiname { get; init; }

    /// <summary>Herkunft, z. B. „SharePoint“ oder „Ordner“.</summary>
    public required string Quelle { get; init; }

    /// <summary>Pfad der Datei in der Quelle, relativ zum Vorlagenordner.</summary>
    public required string Pfad { get; init; }

    /// <summary>Kennung der Datei in der Quelle (SharePoint-Element-ID oder Pfad).</summary>
    public required string QuellId { get; init; }

    /// <summary>Änderungsstand in der Quelle (ETag bzw. Zeitstempel); gleicher Stand heißt: nicht erneut laden.</summary>
    public string? QuellStand { get; init; }

    public DateTimeOffset? GeaendertInQuelleAm { get; init; }
    public string? GeaendertInQuelleVon { get; init; }

    public required string Sha256 { get; init; }
    public DateTimeOffset AbgerufenAm { get; init; }
    public required string AbgerufenVon { get; init; }

    public IReadOnlyList<VorlagenHinweis> Hinweise { get; init; } = [];
    public IReadOnlyList<EingabeDefinition> Eingaben { get; init; } = [];

    /// <summary>Preiskomponenten, die die Vorlage über <c>{{preis.…}}</c> verwendet.</summary>
    public IReadOnlyList<string> Komponenten { get; init; } = [];

    /// <summary>Rollen der Unterschriftsfelder <c>{{unterschrift.…}}</c> für Paperless (#26, Teil D).</summary>
    public IReadOnlyList<string> Unterschriften { get; init; } = [];

    public bool HatFehler => Hinweise.Any(h => h.IstFehler);

    public VorlagenStatus Status { get; internal set; } = VorlagenStatus.ZurPruefung;
    public string? EntschiedenVon { get; private set; }
    public DateTimeOffset? EntschiedenAm { get; private set; }
    public string? Kommentar { get; private set; }

    /// <summary>Die Word-Datei; getrennt gespeichert, damit Übersichten sie nicht mitladen.</summary>
    public VorlagenDatei? Datei { get; init; }

    internal void Entscheide(VorlagenStatus status, string benutzer, DateTimeOffset zeitpunkt, string? kommentar)
    {
        Status = status;
        EntschiedenVon = benutzer;
        EntschiedenAm = zeitpunkt;
        Kommentar = string.IsNullOrWhiteSpace(kommentar) ? null : kommentar.Trim();
    }
}

public class VorlagenDatei
{
    public int VorlagenversionId { get; set; }
    public required byte[] Inhalt { get; init; }
}
