using System.Text.Json;
using System.Text.Json.Serialization;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using PreisParameter = Kalkulator.Domain.Preise.Parameter;

namespace Kalkulator.Infrastructure.Erstbefuellung;

/// <summary>Ergebnis eines Laufs der Erstbefüllung.</summary>
public sealed record ErstbefuellungsErgebnis(bool Ausgefuehrt, int Services, int Preiskomponenten, string Meldung);

/// <summary>
/// Befüllt einen leeren Katalog aus der eingebetteten Datei <c>katalog.json</c> (Issue #7).
/// Die Preisliste wird als <b>Entwurf</b> angelegt, damit das Produktmanagement sie prüfen und freigeben kann.
/// Enthält der Katalog bereits Services, passiert nichts: Die Erstbefüllung überschreibt keine gepflegten Daten.
/// </summary>
public static class KatalogErstbefuellung
{
    private const string Ressource = "Kalkulator.Infrastructure.Erstbefuellung.katalog.json";

    private static readonly JsonSerializerOptions JsonOptionen = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public static async Task<ErstbefuellungsErgebnis> AusfuehrenAsync(KalkulatorDbContext kontext, CancellationToken abbruch = default)
    {
        if (await kontext.Services.AnyAsync(abbruch))
        {
            return new ErstbefuellungsErgebnis(false, 0, 0, "Der Katalog enthält bereits Services. Die Erstbefüllung wurde übersprungen.");
        }

        var daten = LadeDaten();
        var preisliste = new Preisliste { Bezeichnung = daten.Preisliste.Bezeichnung, GueltigAb = daten.Preisliste.GueltigAb };
        preisliste.Parameter.AddRange(daten.Parameter.Select(p => new PreisParameter { Schluessel = p.Schluessel, Wert = p.Wert, Beschreibung = p.Beschreibung }));

        // Leistungsscheine werden über ihren Code zugeordnet; je Code gibt es in der Erstbefüllung genau eine Version.
        var vorlagen = daten.DokumentVorlagen.ToDictionary(
            v => v.Code,
            v => new DokumentVorlage { Typ = v.Typ, Code = v.Code, Bezeichnung = v.Bezeichnung, Version = v.Version, Dateiname = v.Dateiname });

        var services = new Dictionary<string, Service>();
        var kategorieSortierung = 0;
        foreach (var kategorieDaten in daten.Kategorien)
        {
            var kategorie = new ServiceKategorie { Name = kategorieDaten.Name, Sortierung = ++kategorieSortierung * 10 };
            var serviceSortierung = 0;
            foreach (var s in kategorieDaten.Services)
            {
                var service = new Service
                {
                    Code = s.Code,
                    ServiceNummer = s.ServiceNummer,
                    Bezeichnung = s.Bezeichnung,
                    Kurzbeschreibung = s.Kurzbeschreibung,
                    Typ = s.Typ,
                    Vertriebsstatus = s.Vertriebsstatus,
                    Sortierung = ++serviceSortierung * 10,
                    Kategorie = kategorie,
                    Leistungsschein = s.Leistungsschein is null ? null : vorlagen[s.Leistungsschein],
                };
                foreach (var k in s.Preiskomponenten)
                {
                    service.Preiskomponenten.Add(Komponente(k, preisliste));
                }

                services.Add(service.Code, service);
            }
        }

        foreach (var s in daten.Kategorien.SelectMany(k => k.Services).Where(s => s.Bestandteile is not null))
        {
            services[s.Code].Bestandteile.AddRange(s.Bestandteile!.Select(code => new BundleBestandteil { Bestandteil = services[code] }));
        }

        foreach (var r in daten.Regeln)
        {
            var regel = new ServiceRegel { Typ = r.Typ, Meldung = r.Meldung };
            regel.Ziele.AddRange(r.Ziele.Select(code => new ServiceRegelZiel { ZielService = services[code] }));
            services[r.Service].Regeln.Add(regel);
        }

        kontext.DokumentVorlagen.AddRange(vorlagen.Values);
        kontext.Services.AddRange(services.Values);
        kontext.Preislisten.Add(preisliste);
        await kontext.SaveChangesAsync(abbruch);

        var komponenten = services.Values.Sum(s => s.Preiskomponenten.Count);
        return new ErstbefuellungsErgebnis(true, services.Count, komponenten,
            $"Katalog angelegt: {services.Count} Services, {komponenten} Preiskomponenten, Preisliste „{preisliste.Bezeichnung}“ als Entwurf.");
    }

    private static Preiskomponente Komponente(KomponentenDaten k, Preisliste preisliste)
    {
        var komponente = new Preiskomponente
        {
            Code = k.Code,
            Bezeichnung = k.Bezeichnung,
            Einheit = k.Einheit,
            Abrechnungsart = k.Abrechnungsart,
            StaffelBezug = k.StaffelBezug,
            Sortierung = k.Sortierung,
            NavisionArtikelnummer = k.NavisionArtikelnummer,
        };

        if (k.Staffeln is { Count: > 0 })
        {
            preisliste.Staffeln.AddRange(k.Staffeln.Select(s => new Preisstaffel
            {
                Preiskomponente = komponente,
                AbMenge = s.AbMenge,
                VkNetto = s.VkNetto,
                Bezeichnung = s.Bezeichnung,
                NavisionArtikelnummer = s.NavisionArtikelnummer,
            }));
        }
        else
        {
            // Auch „auf Anfrage“ bzw. per Sonderrechner bepreiste Komponenten erhalten einen Eintrag (VK null).
            preisliste.Preise.Add(new Preis { Preiskomponente = komponente, VkNetto = k.VkNetto });
        }

        if (k.Ek is { } ek)
        {
            preisliste.EkPositionen.Add(new EkPosition
            {
                Preiskomponente = komponente,
                EkLizenz = ek.EkLizenz,
                AufwandMinuten = ek.AufwandMinuten,
                BetriebFix = ek.BetriebFix,
                Overhead = ek.Overhead,
                AusBestandteilen = ek.AusBestandteilen,
                KostenKorrektur = ek.KostenKorrektur,
                Anmerkung = ek.Anmerkung,
            });
        }

        return komponente;
    }

    private static KatalogDaten LadeDaten()
    {
        using var strom = typeof(KatalogErstbefuellung).Assembly.GetManifestResourceStream(Ressource)
            ?? throw new InvalidOperationException($"Die eingebettete Datei „{Ressource}“ fehlt.");
        return JsonSerializer.Deserialize<KatalogDaten>(strom, JsonOptionen)
            ?? throw new InvalidOperationException("Die Datei katalog.json ist leer.");
    }
}
