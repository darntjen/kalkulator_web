using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;

namespace Kalkulator.Infrastructure.Tests;

/// <summary>Kleiner Ausschnitt des echten Katalogs mit allen strukturellen Sonderfällen.</summary>
internal sealed class Testdaten
{
    // Codes sind je Test eindeutig, weil alle Tests dieselbe Datenbank nutzen.
    private readonly string _praefix = Guid.NewGuid().ToString("N")[..6];

    public string Code(string code) => $"{_praefix}-{code}";

    public ServiceKategorie Kategorie { get; }
    public Service ConnectStandard { get; }
    public Service S02 { get; }
    public Service S03 { get; }
    public Service S04 { get; }
    public Service S05 { get; }
    public Service B01 { get; }
    public Service B02 { get; }
    public Service S25 { get; }
    public Service S41 { get; }
    public Preisliste Preisliste { get; }

    public Testdaten()
    {
        Kategorie = new ServiceKategorie { Name = Code("User as a Service") };

        ConnectStandard = Service("S01-STD", "Nösse Connect Standard", ServiceTyp.Connect);
        var connectMonat = Komponente(ConnectStandard, "S01-STD", Einheit.Kunde);
        var connectOnboarding = Komponente(ConnectStandard, "S01-STD-ONB", Einheit.Pauschal, Abrechnungsart.Einmalig, StaffelBezug.User);

        S02 = Service("S02", "Endpoint Protection (XDR) & Richtlinienmanagement", ServiceTyp.Einzelservice);
        S03 = Service("S03", "Patch Management", ServiceTyp.Einzelservice);
        S04 = Service("S04", "Monitoring", ServiceTyp.Einzelservice);
        S05 = Service("S05", "M365 Backup", ServiceTyp.Einzelservice);
        var s02 = Komponente(S02, "S02", Einheit.User);
        Komponente(S03, "S03", Einheit.User);
        Komponente(S04, "S04", Einheit.User);
        Komponente(S05, "S05", Einheit.User);

        B01 = Service("B01", "User as a Service Standard", ServiceTyp.Bundle);
        var b01 = Komponente(B01, "B01", Einheit.User);
        B01.Bestandteile.AddRange([new() { Bestandteil = S02 }, new() { Bestandteil = S03 }, new() { Bestandteil = S04 }]);

        // Verschachtelt: B02 enthält das Bundle B01 und zusätzlich S05.
        B02 = Service("B02", "User as a Service Premium", ServiceTyp.Bundle);
        var b02 = Komponente(B02, "B02", Einheit.User);
        B02.Bestandteile.AddRange([new() { Bestandteil = B01 }, new() { Bestandteil = S05 }]);

        // Mehrere Preiskomponenten und eine Regel mit mehreren Zielen.
        S25 = Service("S25", "Schwachstellenmanagement", ServiceTyp.Einzelservice);
        var s25Grund = Komponente(S25, "S25", Einheit.Kunde);
        var s25Client = Komponente(S25, "S25-CLIENT", Einheit.Client);
        var s25Server = Komponente(S25, "S25-SERVER", Einheit.Server);
        var regel = new ServiceRegel { Typ = RegelTyp.ErfordertEinenVon, Meldung = "S25 setzt ein Bundle B01–B04 voraus." };
        regel.Ziele.AddRange([new() { ZielService = B01 }, new() { ZielService = B02 }]);
        S25.Regeln.Add(regel);

        // Staffel nach Mitarbeitenden.
        S41 = Service("S41", "Strategische IT-Begleitung", ServiceTyp.Einzelservice);
        var s41 = Komponente(S41, "S41", Einheit.Pauschal, staffel: StaffelBezug.Mitarbeitende);

        Preisliste = new Preisliste { Bezeichnung = Code("Preisstand 2026-07-15"), GueltigAb = new DateOnly(2026, 7, 15) };
        Preis(connectMonat, 249.00m);
        Preis(s02, 14.90m);
        Preis(b01, 31.90m);
        Preis(b02, 54.90m);
        Preis(s25Grund, 499.00m);
        Preis(s25Client, 12.00m);
        Preis(s25Server, 69.00m);
        Staffel(connectOnboarding, 1, 900m);
        Staffel(connectOnboarding, 31, 1400m);
        Staffel(connectOnboarding, 501, null);
        Staffel(s41, 1, 350m);
        Staffel(s41, 51, 550m);
        Preisliste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.SupportkontingentAeSatz, Wert = 30.38m });
        Preisliste.Parameter.Add(new Parameter { Schluessel = ParameterSchluessel.CloudServerMargenteiler, Wert = 0.55m });
        Preisliste.EkPositionen.Add(new EkPosition { Preiskomponente = s02, EkLizenz = 3.59m, AufwandMinuten = 1, Overhead = 1.50m });
    }

    public IEnumerable<Service> AlleServices => [ConnectStandard, S02, S03, S04, S05, B01, B02, S25, S41];

    private Service Service(string code, string bezeichnung, ServiceTyp typ) =>
        new() { Code = Code(code), Bezeichnung = bezeichnung, Typ = typ, Kategorie = Kategorie };

    private Preiskomponente Komponente(Service service, string code, Einheit einheit,
        Abrechnungsart art = Abrechnungsart.Monatlich, StaffelBezug staffel = StaffelBezug.Keine)
    {
        var komponente = new Preiskomponente { Code = Code(code), Bezeichnung = service.Bezeichnung, Einheit = einheit, Abrechnungsart = art, StaffelBezug = staffel };
        service.Preiskomponenten.Add(komponente);
        return komponente;
    }

    private void Preis(Preiskomponente komponente, decimal vk) =>
        Preisliste.Preise.Add(new Preis { Preiskomponente = komponente, VkNetto = vk });

    private void Staffel(Preiskomponente komponente, int ab, decimal? vk) =>
        Preisliste.Staffeln.Add(new Preisstaffel { Preiskomponente = komponente, AbMenge = ab, VkNetto = vk });
}
