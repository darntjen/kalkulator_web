using System.Globalization;
using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Projekte;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Documents.Vertrag;

/// <summary>
/// Alles, was in die Dokumente eines Vertragswerks einfließt (#26, Teil C). Grundlage ist die eingefrorene Version des
/// angenommenen Angebots; der Katalog braucht die Zuordnung der Leistungsscheine.
/// </summary>
public sealed record VertragsQuelle(
    string Vertragsnummer,
    string Angebot,
    DateOnly Datum,
    Kalkulationsversion Version,
    Kunde Kunde,
    IReadOnlyList<Service> Katalog,
    IReadOnlyList<Vertragsdokument> Dokumente,
    IReadOnlyList<EingabeDefinition> Eingaben);

/// <summary>Werte für die Platzhalter der Vertragsvorlagen (<see cref="Vertragsplatzhalter"/>).</summary>
public static class Vertragsdaten
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Werte für ein Dokument; bei Leistungsscheinen mit <c>schein.*</c> für <paramref name="dokument"/>.</summary>
    public static Datensatz Fuer(VertragsQuelle quelle, Vertragsdokument dokument)
    {
        var daten = Gemeinsam(quelle);
        var services = quelle.Katalog.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var code = ScheinCode(dokument, services);
        var positionen = quelle.Version.Positionen.OrderBy(p => p.Reihenfolge)
            .Where(p => p.ServiceCode is { } s && services.TryGetValue(s, out var service) && ScheinVon(service) == code)
            .ToList();
        daten["schein.code"] = code;
        daten["schein.bezeichnung"] = dokument.Bezeichnung;
        daten["schein.summe"] = Euro(positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich).Sum(p => p.Betrag));
        daten["schein.positionen"] = Positionen(positionen, services);
        return daten;
    }

    /// <summary>Werte, die in allen Dokumenten gleich sind.</summary>
    public static Datensatz Gemeinsam(VertragsQuelle quelle)
    {
        var version = quelle.Version;
        var kunde = quelle.Kunde;
        var services = quelle.Katalog.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var positionen = version.Positionen.OrderBy(p => p.Reihenfolge).ToList();
        var connect = positionen.Select(p => p.ServiceCode is { } s ? services.GetValueOrDefault(s) : null)
            .FirstOrDefault(s => s?.Typ == ServiceTyp.Connect);
        var ort = $"{kunde.Postleitzahl} {kunde.Ort}".Trim();

        var daten = new Datensatz
        {
            ["kunde.firma"] = kunde.Firma,
            ["kunde.strasse"] = kunde.Strasse ?? "",
            ["kunde.plz"] = kunde.Postleitzahl ?? "",
            ["kunde.ort"] = kunde.Ort ?? "",
            ["kunde.anschrift"] = string.Join("\n", new[] { kunde.Firma, kunde.Strasse, ort }.Where(t => !string.IsNullOrWhiteSpace(t))),
            ["kunde.ansprechpartner"] = kunde.Ansprechpartner ?? "",
            ["kunde.navision"] = kunde.NavisionKundennummer ?? "",
            ["vertrag.nummer"] = quelle.Vertragsnummer,
            ["vertrag.datum"] = Datum(quelle.Datum),
            ["vertrag.beginn"] = version.Vertragsbeginn is { } b ? Datum(b) : "",
            ["vertrag.angebot"] = quelle.Angebot,
            ["vertrag.connectstufe"] = connect is null ? "Standard" : Vertragspaket.Stufe(connect),
            ["summe.monatlich"] = Euro(version.SummeMonatlich),
            ["summe.einmalig"] = Euro(version.SummeEinmalig),
            ["positionen"] = Positionen(positionen.Where(p => p.Abrechnungsart != Abrechnungsart.Einmalig), services),
            ["einmalig"] = Positionen(positionen.Where(p => p.Abrechnungsart == Abrechnungsart.Einmalig), services),
            ["anlagen"] = quelle.Dokumente.Where(d => d.Art != VertragsdokumentArt.Grundvertrag).Select(d => new Datensatz
            {
                ["anlage.code"] = d.Art switch
                {
                    VertragsdokumentArt.Avv or VertragsdokumentArt.Avb or VertragsdokumentArt.Sla => $"Anlage {d.Code}",
                    _ => ScheinCode(d, services),
                },
                ["anlage.bezeichnung"] = d.Bezeichnung,
                ["anlage.art"] = d.Art switch
                {
                    VertragsdokumentArt.Bundle => "Bundle-Leistungsschein",
                    VertragsdokumentArt.Connect or VertragsdokumentArt.Leistungsschein => "Leistungsschein",
                    _ => "Anlage",
                },
            }).ToList(),
        };

        // Preise je Komponente: {{preis.S14-SERVER.menge}} usw.; der Block {{#preis.S14-SERVER}} erscheint nur, wenn gebucht.
        foreach (var gruppe in positionen.GroupBy(p => p.Code, StringComparer.Ordinal))
        {
            var menge = gruppe.Sum(p => p.Menge);
            var summe = gruppe.Sum(p => p.Betrag);
            daten[$"preis.{gruppe.Key}"] = true;
            daten[$"preis.{gruppe.Key}.menge"] = Menge(menge);
            daten[$"preis.{gruppe.Key}.einzelpreis"] = gruppe.First().Einzelpreis is { } ep ? Euro(ep) : "";
            daten[$"preis.{gruppe.Key}.summe"] = Euro(summe);
        }

        Eingaben(daten, version.Vertragsangaben, quelle.Eingaben);
        return daten;
    }

    /// <summary>
    /// Füllt eine Vorlage. Preisfelder nicht gebuchter Komponenten bleiben leer und ihre Blöcke entfallen; dafür werden
    /// die Komponenten der Vorlage vorab leer eingetragen.
    /// </summary>
    public static byte[] Erzeuge(byte[] vorlage, Datensatz daten, IEnumerable<string> komponentenDerVorlage)
    {
        foreach (var k in komponentenDerVorlage.Where(k => !daten.ContainsKey($"preis.{k}")))
        {
            daten[$"preis.{k}"] = false;
            foreach (var feld in Vertragsplatzhalter.PreisFelder)
            {
                daten[$"preis.{k}.{feld}"] = "";
            }
        }

        return WordVorlage.Befuellen(vorlage, daten);
    }

    private static void Eingaben(Datensatz daten, Vertragsangaben angaben, IReadOnlyList<EingabeDefinition> definitionen)
    {
        foreach (var d in definitionen)
        {
            var schluessel = Vertragsplatzhalter.EingabePraefix + d.Name;
            switch (d.Art)
            {
                case EingabeArt.Text:
                    daten[schluessel] = angaben.Wert(d.Name) ?? "";
                    break;
                case EingabeArt.Auswahl:
                    var wahl = angaben.Wert(d.Name);
                    daten[schluessel] = wahl ?? "";
                    foreach (var option in d.Optionen)
                    {
                        daten[$"{schluessel}={option}"] = new Ankreuzfeld(option == wahl);
                    }

                    break;
                default:
                    daten[schluessel] = angaben.Liste(d.Name).Select(zeile =>
                    {
                        var eintrag = new Datensatz();
                        foreach (var spalte in d.Optionen)
                        {
                            eintrag[$"{d.Name}.{spalte}"] = zeile.TryGetValue(spalte, out var wert) ? wert : "";
                        }

                        return eintrag;
                    }).ToList();
                    break;
            }
        }
    }

    private static List<Datensatz> Positionen(IEnumerable<VersionsPosition> positionen, IReadOnlyDictionary<string, Service> services) =>
        [.. positionen.Where(p => p.Betrag != 0 || p.Herkunft != PositionsHerkunft.Katalog).Select(p => new Datensatz
        {
            ["position.code"] = p.ServiceCode is { } s && services.TryGetValue(s, out var service) ? ScheinVon(service) : p.Code,
            ["position.bezeichnung"] = p.Bezeichnung + (p.Herkunft == PositionsHerkunft.Sonderposition ? " (Sonderposition)" : ""),
            ["position.menge"] = Menge(p.Menge),
            ["position.einheit"] = "",
            ["position.einzelpreis"] = p.Einzelpreis is { } ep ? Euro(ep) : "",
            ["position.gesamtpreis"] = Euro(p.Betrag),
            ["position.abrechnung"] = p.Abrechnungsart == Abrechnungsart.Einmalig ? "einmalig" : "monatlich",
        })];

    /// <summary>Code des Leistungsscheins, zu dem ein Service gehört (S01-STD → S01).</summary>
    private static string ScheinVon(Service service) => service.Leistungsschein?.Code ?? service.Code;

    private static string ScheinCode(Vertragsdokument dokument, IReadOnlyDictionary<string, Service> services) =>
        services.TryGetValue(dokument.Code, out var s) ? ScheinVon(s) : dokument.Code;

    private static string Euro(decimal betrag) => betrag.ToString("#,##0.00", Deutsch) + " €";

    private static string Menge(decimal menge) => menge.ToString("#,##0.##", Deutsch);

    private static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", Deutsch);
}
