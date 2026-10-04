using System.Globalization;
using System.Text.Json;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Documents.Angebot;

/// <summary>Absender des Angebots; mit #4 aus dem Entra-Profil (Frage 9.6).</summary>
public sealed record AngebotsAbsender(string Name, string Funktion, string Telefon, string Email);

/// <summary>Alles, was in ein Angebot einfließt. Die Version ist eingefroren; Katalog und Preisliste sind die der Version.</summary>
public sealed record AngebotsQuelle(
    string Angebotsnummer,
    Kalkulationsversion Version,
    Kunde Kunde,
    DateOnly Datum,
    DateOnly GueltigBis,
    string? Freitext,
    AngebotsAbsender Absender,
    IReadOnlyList<Service> Katalog,
    Preisliste Preisliste,
    Textbausteine Textbausteine);

/// <summary>Textbausteine aus <c>templates/angebot/textbausteine.json</c> (C-04); austauschbar ohne Codeänderung.</summary>
public sealed record Textbausteine(ConnectTexte Connect, IReadOnlyDictionary<string, Leistungstext> Leistungen)
{
    private static readonly JsonSerializerOptions Optionen = new() { PropertyNameCaseInsensitive = true };

    public static Textbausteine Lies(Stream json) =>
        JsonSerializer.Deserialize<Textbausteine>(json, Optionen) ?? throw new VorlagenFehler("Die Textbausteine sind leer.");
}

public sealed record ConnectTexte(string Kurztext, IReadOnlyList<Dictionary<string, string>> Sla);

public sealed record Leistungstext(string? Beschreibung, IReadOnlyList<string>? Enthalten, string? NichtEnthalten);

/// <summary>Erzeugt das Word-Angebot aus Vorlage und eingefrorener Kalkulationsversion (C-01 bis C-07).</summary>
public static class Angebotsdokument
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static byte[] Erzeuge(byte[] vorlage, AngebotsQuelle quelle) => WordVorlage.Befuellen(vorlage, Daten(quelle));

    /// <summary>Die Werte für alle Platzhalter der Angebotsvorlage (docs/08_angebotsvorlage.md, Abschnitt 3).</summary>
    public static Datensatz Daten(AngebotsQuelle quelle)
    {
        var version = quelle.Version;
        var services = quelle.Katalog.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var komponenten = quelle.Katalog.SelectMany(s => s.Preiskomponenten).ToDictionary(k => k.Code, StringComparer.Ordinal);
        var positionen = version.Positionen.OrderBy(p => p.Reihenfolge).ToList();
        var connect = positionen.Where(p => p.ServiceCode is { } c && services.TryGetValue(c, out var s) && s.Typ == ServiceTyp.Connect)
            .Select(p => services[p.ServiceCode!]).FirstOrDefault();
        var preise = quelle.Preisliste;

        var daten = new Datensatz
        {
            ["angebot.nummer"] = quelle.Angebotsnummer,
            ["angebot.version"] = version.Bezeichnung,
            ["angebot.datum"] = Datum(quelle.Datum),
            ["angebot.gueltig_bis"] = Datum(quelle.GueltigBis),
            ["kunde.firma"] = quelle.Kunde.Firma,
            ["kunde.ansprechpartner"] = quelle.Kunde.Ansprechpartner ?? "",
            ["kunde.strasse"] = quelle.Kunde.Strasse ?? "",
            ["kunde.plz_ort"] = $"{quelle.Kunde.Postleitzahl} {quelle.Kunde.Ort}".Trim(),
            ["kunde.anrede"] = Anrede(quelle.Kunde.Ansprechpartner),
            ["absender.name"] = quelle.Absender.Name,
            ["absender.funktion"] = quelle.Absender.Funktion,
            ["absender.telefon"] = quelle.Absender.Telefon,
            ["absender.email"] = quelle.Absender.Email,
            ["freitext.ausgangssituation"] = quelle.Freitext?.Trim() ?? "",
            ["servicebeginn"] = version.Vertragsbeginn is { } b ? Datum(b) : "nach Absprache",
            ["servicebeginn.text"] = version.Vertragsbeginn is { } b2 ? $"zum {Datum(b2)}" : "zum vereinbarten Termin",
            ["summe.monatlich"] = Euro(version.SummeMonatlich),
            ["summe.einmalig"] = Euro(version.SummeEinmalig),
            ["kennzahl.jahreswert"] = Euro(version.Jahreswert),
            ["kennzahl.erstlaufzeit"] = Euro(version.WertErstlaufzeit),
            ["satz.ebene1"] = Euro(preise.ParameterWert(ParameterSchluessel.AeSatzEbene1)),
            ["satz.ebene2"] = Euro(preise.ParameterWert(ParameterSchluessel.AeSatzEbene2)),
            ["satz.ebene3"] = Euro(preise.ParameterWert(ParameterSchluessel.AeSatzEbene3)),
            ["connect.vorhanden"] = connect is not null,
            ["connect.name"] = connect?.Bezeichnung ?? "Strategische IT-Begleitung",
            ["connect.stufe"] = connect is null ? "" : Vertragspaket.Stufe(connect),
            ["connect.kurztext"] = connect is null ? "ohne Nösse Connect" : quelle.Textbausteine.Connect.Kurztext,
            ["sla"] = connect is null ? new List<Datensatz>() : Sla(quelle.Textbausteine, Vertragspaket.Stufe(connect)),
            ["umfang"] = Umfang(version, positionen, services, komponenten),
            ["leistungen"] = Leistungen(positionen, services, komponenten, quelle.Textbausteine),
            ["positionen"] = Positionen(positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich), services, komponenten),
            ["vertragsbestandteile"] = Vertragsbestandteile(positionen, quelle.Katalog),
        };

        var einmalig = positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Einmalig && p.Betrag != 0).ToList();
        daten["hat_einmalig"] = einmalig.Count > 0;
        daten["einmalig"] = einmalig.Select(p => new Datensatz
        {
            ["bezeichnung"] = p.Bezeichnung + (p.Herkunft == PositionsHerkunft.Sonderposition ? " (Sonderposition)" : ""),
            ["betrag"] = Euro(p.Betrag),
            ["hinweis"] = "",
        }).ToList();
        daten["einmalig.kurz"] = einmalig.Count == 1 ? einmalig[0].Bezeichnung : einmalig.Count > 1 ? "für einmalige Leistungen" : "";

        var vorteil = BundleVorteil(positionen, services, komponenten, preise);
        daten["bundlevorteil"] = vorteil is null ? new List<Datensatz>() : [vorteil];

        var bisher = version.Eingabe.BisherigerMonatspreis;
        daten["vergleich.vorhanden"] = bisher is not null;
        if (bisher is { } alt)
        {
            var vergleich = new VorherNachher(alt, version.SummeMonatlich);
            daten["vergleich.bisher"] = Euro(alt);
            daten["vergleich.differenz"] = (vergleich.Differenz >= 0 ? "+" : "−") + Euro(Math.Abs(vergleich.Differenz))
                + (vergleich.DifferenzProzent is { } pz ? $" ({(pz >= 0 ? "+" : "−")}{Math.Abs(pz).ToString("0.0", Deutsch)} %)" : "");
        }

        return daten;
    }

    private static List<Datensatz> Sla(Textbausteine texte, string stufe) =>
        [.. texte.Connect.Sla
            .Where(z => z.TryGetValue(stufe, out var w) && w != "–")
            .Select(z => new Datensatz { ["parameter"] = z["parameter"], ["wert"] = z[stufe] })];

    /// <summary>„40 User, 4 Server · User as a Service Premium, …“</summary>
    private static string Umfang(Kalkulationsversion version, List<VersionsPosition> positionen,
        Dictionary<string, Service> services, Dictionary<string, Preiskomponente> komponenten)
    {
        var mengen = new List<string>();
        if (version.Eingabe.AnzahlUser > 0)
        {
            mengen.Add($"{version.Eingabe.AnzahlUser} User");
        }

        var server = positionen.Where(p => komponenten.TryGetValue(p.Code, out var k) && k.Einheit == Einheit.Server && p.Herkunft == PositionsHerkunft.Katalog)
            .Select(p => p.Menge).DefaultIfEmpty(0).Max();
        if (server > 0)
        {
            mengen.Add($"{Menge(server)} Server");
        }

        var namen = GebuchteServices(positionen, services).Where(s => s.Typ != ServiceTyp.Connect).Select(s => s.Bezeichnung).ToList();
        return string.Join(" · ", new[] { string.Join(", ", mengen), string.Join(", ", namen) }.Where(t => t.Length > 0));
    }

    /// <summary>Gebuchte Services in Katalogreihenfolge, ohne solche, die nur über ein Bundle mitlaufen (berechnete Menge 0).</summary>
    private static List<Service> GebuchteServices(List<VersionsPosition> positionen, Dictionary<string, Service> services) =>
        [.. positionen
            .Where(p => p.ServiceCode is not null && p.Herkunft != PositionsHerkunft.Onboarding && p.BerechneteMenge > 0)
            .Select(p => services[p.ServiceCode!])
            .Distinct()
            .OrderBy(s => s.Kategorie?.Sortierung ?? 0).ThenBy(s => s.Sortierung)];

    private static List<Datensatz> Leistungen(List<VersionsPosition> positionen, Dictionary<string, Service> services,
        Dictionary<string, Preiskomponente> komponenten, Textbausteine texte)
    {
        var liste = new List<Datensatz>();
        foreach (var service in GebuchteServices(positionen, services).Where(s => s.Typ != ServiceTyp.Connect))
        {
            var eigene = positionen.Where(p => p.ServiceCode == service.Code && p.BerechneteMenge > 0 && p.Herkunft != PositionsHerkunft.Onboarding).ToList();
            texte.Leistungen.TryGetValue(service.Code, out var text);

            var enthalten = text?.Enthalten?.Select(t => Ersetze(t, service, eigene)).ToList()
                ?? (service.Typ == ServiceTyp.Bundle ? Bestandteile(service).Select(b => $"{b.Bezeichnung} ({b.Code})").ToList() : []);

            liste.Add(new Datensatz
            {
                ["titel"] = $"{service.Bezeichnung} ({Leistungsschein(service)})",
                ["preistext"] = Preistext(service, eigene, komponenten),
                ["beschreibung"] = text?.Beschreibung ?? service.Kurzbeschreibung ?? "",
                ["hat_enthalten"] = enthalten.Count > 0,
                ["enthalten"] = enthalten.Select(e => new Datensatz { ["text"] = e }).ToList(),
                ["nicht_enthalten"] = text?.NichtEnthalten ?? "",
            });
        }

        return liste;
    }

    private static string Ersetze(string text, Service service, List<VersionsPosition> eigene)
    {
        if (service.Code != KatalogCodes.Supportkontingent || eigene.Count == 0)
        {
            return text;
        }

        var ae = eigene[0].Menge;
        return text.Replace("{menge}", Menge(ae), StringComparison.Ordinal).Replace("{stunden}", Menge(ae / 4m), StringComparison.Ordinal);
    }

    /// <summary>„54,90 € je User / Monat, 40 User“; Sonderrechner und Mehrkomponenten-Services zusammengefasst.</summary>
    private static string Preistext(Service service, List<VersionsPosition> eigene, Dictionary<string, Preiskomponente> komponenten)
    {
        if (service.Code == KatalogCodes.ServerBackup)
        {
            return $"{Euro(eigene.Sum(p => p.Betrag))} / Monat nach Baukasten";
        }

        var teile = eigene.Select(p =>
        {
            var einheit = komponenten.TryGetValue(p.Code, out var k) ? k.Einheit : Einheit.Pauschal;
            var preis = p.Einzelpreis is { } e ? Euro(e) : Euro(p.Betrag);
            var takt = p.Abrechnungsart == Abrechnungsart.Einmalig ? "einmalig" : "/ Monat";
            return Pauschal(einheit)
                ? $"{preis} pauschal {takt}"
                : $"{preis} {EinheitJe(einheit)} {takt}, {Menge(p.BerechneteMenge)} {EinheitMehrzahl(einheit)}";
        });
        return string.Join("; ", teile);
    }

    private static List<Datensatz> Positionen(IEnumerable<VersionsPosition> positionen, Dictionary<string, Service> services,
        Dictionary<string, Preiskomponente> komponenten) =>
        [.. positionen
            .Where(p => p.BerechneteMenge > 0 && p.Betrag != 0)
            .Select(p =>
            {
                var sonder = p.Herkunft == PositionsHerkunft.Sonderposition;
                var service = p.ServiceCode is { } c && services.TryGetValue(c, out var s) ? s : null;
                var einheit = sonder ? p.Hinweis ?? ""
                    : komponenten.TryGetValue(p.Code, out var k) ? (Pauschal(k.Einheit) ? "pauschal" : EinheitJe(k.Einheit)) : "pauschal";
                return new Datensatz
                {
                    ["code"] = sonder ? "SP" : service is null ? p.Code : Leistungsschein(service),
                    ["bezeichnung"] = sonder ? $"{p.Bezeichnung} (Sonderposition)" : p.Bezeichnung,
                    ["einheit"] = einheit,
                    ["menge"] = Menge(p.BerechneteMenge),
                    ["einzelpreis"] = p.Einzelpreis is { } e ? Euro(e) : "",
                    ["betrag"] = Euro(p.Betrag),
                };
            })];

    private static List<Datensatz> Vertragsbestandteile(List<VersionsPosition> positionen, IReadOnlyList<Service> katalog)
    {
        var codes = positionen.Where(p => p.ServiceCode is not null).Select(p => p.ServiceCode!);
        var paket = Vertragspaket.Aufloesen(codes, katalog);
        var zeilen = new List<string>();
        foreach (var dokument in paket.Where(d => d.Bundle is null))
        {
            zeilen.Add(dokument.Art switch
            {
                VertragsdokumentArt.Avv => "Auftragsverarbeitungsvereinbarung (Anlage AVV)",
                VertragsdokumentArt.Grundvertrag => dokument.Bezeichnung,
                VertragsdokumentArt.Avb => $"{dokument.Bezeichnung} (Anlage AVB)",
                VertragsdokumentArt.Sla => dokument.Bezeichnung.Replace("Service Level Agreement", "Service Level Agreement (Anlage SLA)", StringComparison.Ordinal),
                VertragsdokumentArt.Connect => $"Leistungsschein {dokument.Code} – {dokument.Bezeichnung}",
                VertragsdokumentArt.Bundle => $"Bundle {dokument.Code} mit Leistungsscheinen {string.Join(", ", paket.Where(d => d.Bundle == dokument.Code).Select(d => d.Code))}",
                _ => "",
            });
        }

        var einzeln = paket.Where(d => d.Art == VertragsdokumentArt.Leistungsschein && d.Bundle is null).Select(d => d.Code).ToList();
        if (einzeln.Count > 0)
        {
            zeilen.Add((einzeln.Count == 1 ? "Leistungsschein " : "Leistungsscheine ") + string.Join(", ", einzeln));
        }

        return [.. zeilen.Where(z => z.Length > 0).Select(z => new Datensatz { ["text"] = z })];
    }

    /// <summary>
    /// B-17: Was die gebuchten Bundles gegenüber der Einzelbuchung ihrer Services sparen. Gezählt werden Bestandteile
    /// mit derselben Einheit wie das Bundle; fehlt einem Bestandteil ein solcher Preis, zählt das Bundle nicht.
    /// </summary>
    private static Datensatz? BundleVorteil(List<VersionsPosition> positionen, Dictionary<string, Service> services,
        Dictionary<string, Preiskomponente> komponenten, Preisliste preise)
    {
        var summe = 0m;
        var bundles = new List<string>();
        foreach (var p in positionen.Where(p => p.Herkunft == PositionsHerkunft.Katalog && p.BerechneteMenge > 0 && p.Einzelpreis is not null))
        {
            if (p.ServiceCode is not { } code || services[code].Typ != ServiceTyp.Bundle || !komponenten.TryGetValue(p.Code, out var bundleKomponente))
            {
                continue;
            }

            decimal? einzeln = 0m;
            foreach (var bestandteil in Bestandteile(services[code]))
            {
                var passend = bestandteil.Preiskomponenten.FirstOrDefault(k => k.Einheit == bundleKomponente.Einheit && k.Abrechnungsart == Abrechnungsart.Monatlich);
                einzeln = passend is null ? null : einzeln + preise.PreisFuer(passend);
            }

            if (einzeln is { } e && e > p.Einzelpreis)
            {
                summe += (e - p.Einzelpreis.Value) * p.BerechneteMenge;
                bundles.Add(code);
            }
        }

        return summe <= 0 ? null : new Datensatz { ["bundles"] = string.Join(", ", bundles), ["betrag"] = Euro(Math.Round(summe, 2, MidpointRounding.AwayFromZero)) };
    }

    /// <summary>Einzelservices eines Bundles; verschachtelte Bundles werden aufgelöst.</summary>
    private static IEnumerable<Service> Bestandteile(Service bundle) =>
        bundle.Bestandteile.Select(b => b.Bestandteil!)
            .SelectMany(s => s.Typ == ServiceTyp.Bundle ? Bestandteile(s) : [s])
            .OrderBy(s => s.Code, StringComparer.Ordinal);

    private static string Leistungsschein(Service service) => service.Typ == ServiceTyp.Connect ? Vertragspaket.ConnectLeistungsschein : service.Code;

    private static string Anrede(string? ansprechpartner)
    {
        var name = ansprechpartner?.Split(',')[0].Trim() ?? "";
        return name.StartsWith("Frau ", StringComparison.Ordinal) ? $"Sehr geehrte {name}"
            : name.StartsWith("Herr ", StringComparison.Ordinal) ? $"Sehr geehrter {name}"
            : "Sehr geehrte Damen und Herren";
    }

    private static bool Pauschal(Einheit einheit) => einheit is Einheit.Pauschal or Einheit.Kunde;

    private static string EinheitJe(Einheit einheit) => einheit switch
    {
        Einheit.AdUmgebung => "je AD-Umgebung",
        Einheit.AccessPoint => "je Access Point",
        Einheit.Netzwerkgeraet => "je Netzwerkgerät",
        Einheit.Abrechnungseinheit => "je AE",
        Einheit.Terabyte => "je TB",
        Einheit.Nas => "je NAS",
        _ => "je " + einheit,
    };

    private static string EinheitMehrzahl(Einheit einheit) => einheit switch
    {
        Einheit.Client => "Clients",
        Einheit.Firewall => "Firewalls",
        Einheit.Tenant => "Tenants",
        Einheit.AdUmgebung => "AD-Umgebungen",
        Einheit.Switch => "Switches",
        Einheit.AccessPoint => "Access Points",
        Einheit.Netzwerkgeraet => "Netzwerkgeräte",
        Einheit.Device => "Geräte",
        Einheit.Abrechnungseinheit => "AE",
        Einheit.Backupumgebung => "Backupumgebungen",
        Einheit.Paket => "Pakete",
        Einheit.Terabyte => "TB",
        Einheit.Instanz => "Instanzen",
        _ => einheit.ToString(),
    };

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00 €", Deutsch);

    private static string Menge(decimal menge) => menge.ToString("#,##0.##", Deutsch);

    private static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", Deutsch);
}
