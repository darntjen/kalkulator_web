using Kalkulator.Domain.Katalog;

namespace Kalkulator.Infrastructure.Erstbefuellung;

// Aufbau der Datei katalog.json. Die Klassen bilden nur die Datei ab; fachliche Regeln stehen im Domänenmodell.

internal sealed record KatalogDaten(
    PreislistenDaten Preisliste,
    List<ParameterDaten> Parameter,
    List<VorlagenDaten> DokumentVorlagen,
    List<KategorieDaten> Kategorien,
    List<RegelDaten> Regeln);

internal sealed record PreislistenDaten(string Bezeichnung, DateOnly GueltigAb);

internal sealed record ParameterDaten(string Schluessel, decimal Wert, string? Beschreibung);

internal sealed record VorlagenDaten(DokumentTyp Typ, string Code, string Bezeichnung, string Version, string? Dateiname);

internal sealed record KategorieDaten(string Name, List<ServiceDaten> Services);

internal sealed record ServiceDaten(
    string Code,
    string? ServiceNummer,
    string Bezeichnung,
    ServiceTyp Typ,
    Vertriebsstatus Vertriebsstatus,
    string? Kurzbeschreibung,
    string? Leistungsschein,
    List<KomponentenDaten> Preiskomponenten,
    List<string>? Bestandteile);

internal sealed record KomponentenDaten(
    string Code,
    string Bezeichnung,
    Einheit Einheit,
    Abrechnungsart Abrechnungsart,
    StaffelBezug StaffelBezug,
    int Sortierung,
    string? NavisionArtikelnummer,
    decimal? VkNetto,
    List<StaffelDaten>? Staffeln,
    EkDaten? Ek);

internal sealed record StaffelDaten(int AbMenge, decimal? VkNetto, string? Bezeichnung, string? NavisionArtikelnummer);

internal sealed record EkDaten(
    decimal EkLizenz,
    decimal? AufwandMinuten,
    decimal? BetriebFix,
    decimal Overhead,
    bool AusBestandteilen,
    decimal KostenKorrektur,
    string? Anmerkung);

internal sealed record RegelDaten(string Service, RegelTyp Typ, string Meldung, List<string> Ziele);
