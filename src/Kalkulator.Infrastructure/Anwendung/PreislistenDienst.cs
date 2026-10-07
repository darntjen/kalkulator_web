using System.Text.RegularExpressions;
using Kalkulator.Documents;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using PreisParameter = Kalkulator.Domain.Preise.Parameter;

namespace Kalkulator.Infrastructure.Anwendung;

public sealed record PreislistenZeile(
    int Id,
    string Bezeichnung,
    DateOnly GueltigAb,
    PreislistenStatus Status,
    string? FreigegebenVon,
    DateTimeOffset? FreigegebenAm,
    string? Vorgaenger,
    bool Aktuell);

/// <summary>Preisliste mit allen Werten und der Katalog, auf den sie sich bezieht.</summary>
public sealed record PreislistenAnsicht(Preisliste Preisliste, IReadOnlyList<Service> Services, string? Vorgaenger);

public sealed record PreisWert(int KomponenteId, decimal? VkNetto);

public sealed record StaffelWert(int AbMenge, decimal? VkNetto, string? Bezeichnung, string? NavisionArtikelnummer);

public sealed record ParameterWert(string Schluessel, decimal Wert, string? Beschreibung);

public sealed record EkWert(
    decimal EkLizenz,
    decimal? AufwandMinuten,
    decimal? BetriebFix,
    decimal Overhead,
    bool AusBestandteilen,
    decimal KostenKorrektur,
    string? Anmerkung);

/// <summary>
/// Preislisten-Workflow (A-05, A-07, ADR-0005): Entwurf als Kopie anlegen, Preise, Staffeln, Parameter und EK pflegen,
/// prüfen und mit Gültigkeitsdatum freigeben. Freigegebene Preislisten sind unveränderlich; das setzt zusätzlich
/// der Datenbankkontext durch. Lesen dürfen Produktmanagement und Führung, ändern nur das Produktmanagement.
/// </summary>
public sealed partial class PreislistenDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer, TimeProvider zeit)
{
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    public DateOnly Heute => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,59}$")]
    private static partial Regex Schluesselformat();

    public async Task<IReadOnlyList<PreislistenZeile>> ListeAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var listen = await kontext.Preislisten.AsNoTracking().OrderByDescending(p => p.GueltigAb).ThenByDescending(p => p.Id).ToListAsync(abbruch);
        var namen = listen.ToDictionary(p => p.Id, p => p.Bezeichnung);
        var heute = Heute;
        var aktuell = listen
            .Where(p => p.Status == PreislistenStatus.Freigegeben && p.GueltigAb <= heute)
            .OrderByDescending(p => p.GueltigAb).ThenByDescending(p => p.Id)
            .FirstOrDefault()?.Id;
        return [.. listen.Select(p => new PreislistenZeile(p.Id, p.Bezeichnung, p.GueltigAb, p.Status, p.FreigegebenVon, p.FreigegebenAm,
            p.VorgaengerId is { } v && namen.TryGetValue(v, out var name) ? name : null, p.Id == aktuell))];
    }

    /// <summary>Preisliste mit Preisen, Staffeln, Parametern und EK sowie der vollständige Katalog; <c>null</c>, wenn es sie nicht gibt.</summary>
    public async Task<PreislistenAnsicht?> LadeAsync(int id, CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var preisliste = await kontext.Preislisten.AsNoTracking()
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, abbruch);
        if (preisliste is null)
        {
            return null;
        }

        var services = await LadeServicesAsync(kontext, abbruch);
        VerknuepfeKomponenten(preisliste, services);
        var vorgaenger = preisliste.VorgaengerId is { } v
            ? await kontext.Preislisten.Where(p => p.Id == v).Select(p => p.Bezeichnung).SingleOrDefaultAsync(abbruch)
            : null;
        return new PreislistenAnsicht(preisliste, services, vorgaenger);
    }

    /// <summary>Neuer Entwurf als Kopie einer bestehenden Preisliste (A-05).</summary>
    public async Task<int> EntwurfAnlegenAsync(int vorlageId, string bezeichnung, DateOnly gueltigAb, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var name = Pflicht(bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        await PruefeEindeutigAsync(kontext, name, null, abbruch);
        var vorlage = await kontext.Preislisten.AsNoTracking()
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == vorlageId, abbruch)
            ?? throw new KeyNotFoundException($"Preisliste {vorlageId} gibt es nicht.");

        var entwurf = vorlage.ErzeugeEntwurf(name, gueltigAb);
        kontext.Preislisten.Add(entwurf);
        await kontext.SaveChangesAsync(abbruch);
        return entwurf.Id;
    }

    public async Task StammdatenSpeichernAsync(int id, string bezeichnung, DateOnly gueltigAb, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var name = Pflicht(bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        await PruefeEindeutigAsync(kontext, name, id, abbruch);
        liste.Bezeichnung = name;
        liste.GueltigAb = gueltigAb;
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// Verkaufspreise ohne Staffel. <c>null</c> heißt „auf Anfrage“ bzw. Sonderrechner. Nur geänderte Werte werden
    /// geschrieben, damit das Änderungsprotokoll lesbar bleibt.
    /// </summary>
    public async Task PreiseSpeichernAsync(int id, IReadOnlyList<PreisWert> werte, CancellationToken abbruch = default)
    {
        PruefePflegen();
        foreach (var w in werte)
        {
            NichtNegativ(w.VkNetto, "Der Verkaufspreis");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        var komponenten = await kontext.Preiskomponenten.AsNoTracking().ToDictionaryAsync(k => k.Id, abbruch);
        var vorhanden = await kontext.Preise.Where(p => p.PreislisteId == id).ToDictionaryAsync(p => p.PreiskomponenteId, abbruch);
        foreach (var w in werte)
        {
            if (!komponenten.TryGetValue(w.KomponenteId, out var komponente))
            {
                throw new KeyNotFoundException($"Preiskomponente {w.KomponenteId} gibt es nicht.");
            }

            if (komponente.StaffelBezug != StaffelBezug.Keine)
            {
                throw new ArgumentException($"{komponente.Code} ist gestaffelt; der Preis steht in der Staffel.");
            }

            if (vorhanden.TryGetValue(w.KomponenteId, out var preis))
            {
                if (preis.VkNetto != w.VkNetto)
                {
                    preis.VkNetto = w.VkNetto;
                }
            }
            else
            {
                kontext.Preise.Add(new Preis { PreislisteId = liste.Id, PreiskomponenteId = w.KomponenteId, VkNetto = w.VkNetto });
            }
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Ersetzt die Staffel einer Komponente vollständig; unveränderte Stufen bleiben unangetastet.</summary>
    public async Task StaffelSpeichernAsync(int id, int komponenteId, IReadOnlyList<StaffelWert> stufen, CancellationToken abbruch = default)
    {
        PruefePflegen();
        foreach (var s in stufen)
        {
            if (s.AbMenge < 0)
            {
                throw new ArgumentException("Die Untergrenze einer Staffelstufe darf nicht negativ sein.");
            }

            NichtNegativ(s.VkNetto, "Der Staffelpreis");
        }

        if (stufen.GroupBy(s => s.AbMenge).FirstOrDefault(g => g.Count() > 1) is { } doppelt)
        {
            throw new ArgumentException($"Die Untergrenze „ab {doppelt.Key}“ kommt in der Staffel mehrfach vor.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        var komponente = await kontext.Preiskomponenten.AsNoTracking().SingleOrDefaultAsync(k => k.Id == komponenteId, abbruch)
            ?? throw new KeyNotFoundException($"Preiskomponente {komponenteId} gibt es nicht.");
        if (komponente.StaffelBezug == StaffelBezug.Keine)
        {
            throw new ArgumentException($"{komponente.Code} ist nicht gestaffelt.");
        }

        var alt = await kontext.Preisstaffeln.Where(s => s.PreislisteId == id && s.PreiskomponenteId == komponenteId).ToListAsync(abbruch);
        foreach (var stufe in alt.Where(a => stufen.All(s => s.AbMenge != a.AbMenge)))
        {
            kontext.Preisstaffeln.Remove(stufe);
        }

        foreach (var s in stufen)
        {
            var stufe = alt.SingleOrDefault(a => a.AbMenge == s.AbMenge);
            if (stufe is null)
            {
                stufe = new Preisstaffel { PreislisteId = liste.Id, PreiskomponenteId = komponenteId, AbMenge = s.AbMenge };
                kontext.Preisstaffeln.Add(stufe);
            }

            stufe.VkNetto = s.VkNetto;
            stufe.Bezeichnung = Leer(s.Bezeichnung);
            stufe.NavisionArtikelnummer = Leer(s.NavisionArtikelnummer);
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// Setzt die Parameter der Preisliste. Nicht übergebene Parameter werden entfernt, außer den Pflichtparametern,
    /// ohne die der Rechenkern nicht rechnet.
    /// </summary>
    public async Task ParameterSpeichernAsync(int id, IReadOnlyList<ParameterWert> werte, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var bereinigt = werte.Select(w => w with { Schluessel = (w.Schluessel ?? "").Trim().ToUpperInvariant() }).ToList();
        foreach (var w in bereinigt)
        {
            if (!Schluesselformat().IsMatch(w.Schluessel))
            {
                throw new ArgumentException($"„{w.Schluessel}“ ist kein gültiger Schlüssel (Großbuchstaben, Ziffern und _).");
            }
        }

        if (bereinigt.GroupBy(w => w.Schluessel).FirstOrDefault(g => g.Count() > 1) is { } doppelt)
        {
            throw new ArgumentException($"Der Parameter {doppelt.Key} kommt mehrfach vor.");
        }

        if (ParameterSchluessel.Pflicht.FirstOrDefault(p => bereinigt.All(w => w.Schluessel != p)) is { } fehlt)
        {
            throw new ArgumentException($"Der Parameter {fehlt} ist Pflicht und kann nicht entfernt werden.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        var alt = await kontext.Parameter.Where(p => p.PreislisteId == id).ToListAsync(abbruch);
        foreach (var p in alt.Where(a => bereinigt.All(w => w.Schluessel != a.Schluessel)))
        {
            kontext.Parameter.Remove(p);
        }

        foreach (var w in bereinigt)
        {
            var parameter = alt.SingleOrDefault(a => a.Schluessel == w.Schluessel);
            if (parameter is null)
            {
                parameter = new PreisParameter { PreislisteId = liste.Id, Schluessel = w.Schluessel };
                kontext.Parameter.Add(parameter);
            }

            parameter.Wert = w.Wert;
            parameter.Beschreibung = Leer(w.Beschreibung);
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// EK-Werte je Preiskomponente: ein Wert legt an oder ändert, <c>null</c> entfernt den EK. Nicht enthaltene
    /// Komponenten bleiben unverändert.
    /// </summary>
    public async Task EkSpeichernAsync(int id, IReadOnlyDictionary<int, EkWert?> werte, CancellationToken abbruch = default)
    {
        PruefePflegen();
        foreach (var w in werte.Values.OfType<EkWert>())
        {
            NichtNegativ(w.EkLizenz, "Der EK");
            NichtNegativ(w.AufwandMinuten, "Der Aufwand");
            NichtNegativ(w.BetriebFix, "Der feste Betriebsaufwand");
            NichtNegativ(w.Overhead, "Der Overhead");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        var komponenten = await kontext.Preiskomponenten.AsNoTracking().Include(k => k.Service).ToDictionaryAsync(k => k.Id, abbruch);
        var alt = await kontext.EkPositionen.Where(e => e.PreislisteId == id).ToDictionaryAsync(e => e.PreiskomponenteId, abbruch);
        foreach (var (komponenteId, wert) in werte)
        {
            if (!komponenten.TryGetValue(komponenteId, out var komponente))
            {
                throw new KeyNotFoundException($"Preiskomponente {komponenteId} gibt es nicht.");
            }

            alt.TryGetValue(komponenteId, out var ek);
            if (wert is null)
            {
                if (ek is not null)
                {
                    kontext.EkPositionen.Remove(ek);
                }

                continue;
            }

            if (wert.AusBestandteilen && komponente.Service!.Typ != ServiceTyp.Bundle)
            {
                throw new ArgumentException($"{komponente.Code}: „Aus Bestandteilen“ gibt es nur bei Bundles.");
            }

            if (ek is null)
            {
                ek = new EkPosition { PreislisteId = liste.Id, PreiskomponenteId = komponenteId };
                kontext.EkPositionen.Add(ek);
            }

            ek.EkLizenz = wert.EkLizenz;
            ek.AufwandMinuten = wert.AufwandMinuten;
            ek.BetriebFix = wert.BetriebFix;
            ek.Overhead = wert.Overhead;
            ek.AusBestandteilen = wert.AusBestandteilen;
            ek.KostenKorrektur = wert.KostenKorrektur;
            ek.Anmerkung = Leer(wert.Anmerkung);
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Prüfung vor der Freigabe: Katalog-Prüfung des Fachmodells plus Gültigkeitsdatum gegen die bisherigen Preislisten.</summary>
    public async Task<IReadOnlyList<PruefHinweis>> PruefenAsync(int id, CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        return await PruefenAsync(kontext, id, abbruch);
    }

    /// <summary>Gibt einen Entwurf frei; danach ist er unveränderlich und gilt ab seinem Gültigkeitsdatum.</summary>
    public async Task FreigebenAsync(int id, CancellationToken abbruch = default)
    {
        if (!_recht.DarfKatalogFreigeben)
        {
            throw new KeinZugriffException("Preislisten gibt nur das Produktmanagement oder der Admin frei.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var fehler = (await PruefenAsync(kontext, id, abbruch)).Where(h => h.IstFehler).ToList();
        if (fehler.Count > 0)
        {
            throw new InvalidOperationException("Die Preisliste kann noch nicht freigegeben werden: " + string.Join(" ", fehler.Select(f => f.Text)));
        }

        var liste = await EntwurfAsync(kontext, id, abbruch);
        liste.Freigeben(_recht.Name, zeit.GetUtcNow());
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Löscht einen Entwurf, der nicht mehr gebraucht wird. Freigegebene Preislisten bleiben immer erhalten.</summary>
    public async Task EntwurfLoeschenAsync(int id, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var liste = await EntwurfAsync(kontext, id, abbruch);
        if (await kontext.Preislisten.AnyAsync(p => p.VorgaengerId == id, abbruch))
        {
            throw new InvalidOperationException("Aus diesem Entwurf wurde schon eine weitere Preisliste kopiert; er bleibt deshalb erhalten.");
        }

        if (await kontext.Kalkulationsversionen.AnyAsync(v => v.PreislisteId == id, abbruch))
        {
            throw new InvalidOperationException("Mit diesem Entwurf wurde bereits ein Angebotsstand eingefroren.");
        }

        await kontext.Preislisten.Where(p => p.Id == id)
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .LoadAsync(abbruch);
        kontext.Preislisten.Remove(liste);
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// Katalog und Preisliste als Excel-Arbeitsmappe (A-09), als Ersatz für das Mastersheet. Die EK-Kalkulation ist
    /// enthalten, weil nur Produktmanagement und Führung exportieren dürfen.
    /// </summary>
    public async Task<(string Dateiname, byte[] Inhalt)> ExportAsync(int id, CancellationToken abbruch = default)
    {
        var ansicht = await LadeAsync(id, abbruch) ?? throw new KeyNotFoundException($"Preisliste {id} gibt es nicht.");
        var liste = ansicht.Preisliste;
        var services = ansicht.Services;
        var komponenten = services.SelectMany(s => s.Preiskomponenten.OrderBy(k => k.Sortierung).Select(k => (Service: s, Komponente: k))).ToList();
        var schwellen = Margenschwellen.Aus(liste);

        var info = new Tabellenblatt("Preisliste", ["Angabe", "Wert"],
        [
            ["Preisliste", liste.Bezeichnung],
            ["Status", liste.Status == PreislistenStatus.Freigegeben ? "freigegeben" : "Entwurf"],
            ["Gültig ab", Zelle.Datum(liste.GueltigAb)],
            ["Freigegeben von", liste.FreigegebenVon],
            ["Freigegeben am", liste.FreigegebenAm is { } am ? Zelle.Datum(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(am, Zeitzone).DateTime)) : null],
            ["Vorgänger", ansicht.Vorgaenger],
            ["Exportiert von", _recht.Name],
            ["Exportiert am", Zelle.Datum(Heute)],
            ["Hinweis", "Interne Daten: Das Blatt „EK-Kalkulation“ ist nur für Produktmanagement und Führung bestimmt."],
        ]);

        var serviceBlatt = new Tabellenblatt("Services",
            ["Kategorie", "Code", "Service-ID", "Bezeichnung", "Typ", "Vertriebsstatus", "Leistungsschein", "Bestandteile", "Kurzbeschreibung"],
            services.Select(s => (IReadOnlyList<Zelle>)
            [
                s.Kategorie!.Name, s.Code, s.ServiceNummer, s.Bezeichnung, KatalogTexte.Typ(s.Typ), KatalogTexte.Vertriebsstatus(s.Vertriebsstatus),
                s.Leistungsschein is { } l ? $"{l.Code} V{l.Version}" : null,
                string.Join(", ", s.Bestandteile.Select(b => b.Bestandteil!.Code).Order()), s.Kurzbeschreibung,
            ]));

        var preisBlatt = new Tabellenblatt("Preise",
            ["Service", "Komponente", "Bezeichnung", "Einheit", "Abrechnung", "Staffel", "Navision-Artikel", "VK netto", "Hinweis"],
            komponenten.Select(z =>
            {
                var eintrag = liste.Preise.SingleOrDefault(p => p.PreiskomponenteId == z.Komponente.Id);
                var hinweis = z.Komponente.StaffelBezug != StaffelBezug.Keine ? "siehe Blatt „Staffeln“"
                    : eintrag is null ? "nicht hinterlegt"
                    : eintrag.VkNetto is null ? "auf Anfrage / Sonderrechner"
                    : null;
                return (IReadOnlyList<Zelle>)
                [
                    z.Service.Code, z.Komponente.Code, z.Komponente.Bezeichnung, KatalogTexte.Einheit(z.Komponente.Einheit),
                    KatalogTexte.Abrechnungsart(z.Komponente.Abrechnungsart), KatalogTexte.StaffelBezug(z.Komponente.StaffelBezug),
                    z.Komponente.NavisionArtikelnummer, Zelle.Euro(eintrag?.VkNetto), hinweis,
                ];
            }));

        var staffelBlatt = new Tabellenblatt("Staffeln", ["Komponente", "Bezeichnung", "Staffel", "ab Menge", "Stufe", "VK netto", "Navision-Artikel"],
            komponenten.SelectMany(z => liste.Staffeln.Where(s => s.PreiskomponenteId == z.Komponente.Id).OrderBy(s => s.AbMenge)
                .Select(s => (IReadOnlyList<Zelle>)
                [
                    z.Komponente.Code, z.Komponente.Bezeichnung, KatalogTexte.StaffelBezug(z.Komponente.StaffelBezug), s.AbMenge, s.Bezeichnung,
                    s.VkNetto is null ? "individuell" : Zelle.Euro(s.VkNetto), s.NavisionArtikelnummer,
                ])));

        var parameterBlatt = new Tabellenblatt("Parameter", ["Schlüssel", "Wert", "Beschreibung"],
            liste.Parameter.OrderBy(p => p.Schluessel).Select(p => (IReadOnlyList<Zelle>)[p.Schluessel, Zelle.Zahl(p.Wert), p.Beschreibung]));

        var regelBlatt = new Tabellenblatt("Regeln", ["Service", "Regel", "Ziele", "Meldung"],
            services.SelectMany(s => s.Regeln.Select(r => (IReadOnlyList<Zelle>)
            [
                s.Code, KatalogTexte.Regel(r.Typ), string.Join(", ", r.Ziele.Select(z => z.ZielService!.Code).Order()), r.Meldung,
            ])));

        var satz = liste.ParameterWertOder(ParameterSchluessel.EkKostensatzProStunde, 0m);
        var ekBlatt = new Tabellenblatt("EK-Kalkulation",
            ["Service", "Komponente", "Einheit", "EK/Lizenz", "Aufwand Min.", "Betrieb", "Overhead", "Korrektur", "Aus Bestandteilen",
             "Kosten", "VK", "DB", "Marge", "Ampel", "Anmerkung"],
            komponenten.Where(z => z.Komponente.Abrechnungsart == Abrechnungsart.Monatlich).Select(z =>
            {
                var ek = liste.EkPositionen.SingleOrDefault(e => e.PreiskomponenteId == z.Komponente.Id);
                var pruefung = KomponentenPruefung.Fuer(liste, z.Komponente, schwellen);
                return (IReadOnlyList<Zelle>)
                [
                    z.Service.Code, z.Komponente.Code, KatalogTexte.Einheit(z.Komponente.Einheit),
                    Zelle.Euro(ek?.EkLizenz), Zelle.Zahl(ek?.AufwandMinuten), Zelle.Euro(ek?.Betrieb(satz)), Zelle.Euro(ek?.Overhead),
                    Zelle.Euro(ek?.KostenKorrektur), ek is null ? null : ek.AusBestandteilen ? "ja" : "nein",
                    Zelle.Euro(pruefung.Kosten), Zelle.Euro(pruefung.Vk), Zelle.Euro(pruefung.Deckungsbeitrag), Zelle.Prozent(pruefung.Marge),
                    KatalogTexte.Ampel(pruefung.Ampel), ek?.Anmerkung,
                ];
            }));

        var datei = ExcelMappe.Erzeuge([info, serviceBlatt, preisBlatt, staffelBlatt, parameterBlatt, regelBlatt, ekBlatt]);
        var name = string.Concat(liste.Bezeichnung.Select(z => char.IsLetterOrDigit(z) || z is '.' or '-' ? z : '_'));
        return ($"Katalog_{name}.xlsx", datei);
    }

    private async Task<IReadOnlyList<PruefHinweis>> PruefenAsync(KalkulatorDbContext kontext, int id, CancellationToken abbruch)
    {
        var liste = await kontext.Preislisten.AsNoTracking()
            .Include(p => p.Preise).Include(p => p.Staffeln).Include(p => p.Parameter).Include(p => p.EkPositionen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Preisliste {id} gibt es nicht.");
        var services = await LadeServicesAsync(kontext, abbruch);
        VerknuepfeKomponenten(liste, services);

        var hinweise = new List<PruefHinweis>();

        // Ohne freigegebene Vorgängerin (Erstbefüllung) gilt jedes Datum. Danach muss eine neue Preisliste später gelten
        // als die letzte freigegebene und darf nicht rückwirkend in Kraft treten.
        var letzte = await kontext.Preislisten.AsNoTracking()
            .Where(p => p.Status == PreislistenStatus.Freigegeben && p.Id != id)
            .OrderByDescending(p => p.GueltigAb)
            .Select(p => new { p.Bezeichnung, p.GueltigAb })
            .FirstOrDefaultAsync(abbruch);
        if (letzte is not null && liste.Status == PreislistenStatus.Entwurf)
        {
            if (liste.GueltigAb <= letzte.GueltigAb)
            {
                hinweise.Add(new PruefHinweis(true, $"„Gültig ab“ muss nach dem {letzte.GueltigAb:dd.MM.yyyy} liegen („{letzte.Bezeichnung}“)."));
            }
            else if (liste.GueltigAb < Heute)
            {
                hinweise.Add(new PruefHinweis(true, "Eine Preisliste kann nicht rückwirkend gelten; „Gültig ab“ ist frühestens heute."));
            }
        }

        hinweise.AddRange(Freigabepruefung.Pruefe(liste, services));
        return hinweise;
    }

    internal static async Task<List<Service>> LadeServicesAsync(KalkulatorDbContext kontext, CancellationToken abbruch) =>
        Verknuepfe(await kontext.Services.AsNoTracking()
            .Include(s => s.Kategorie)
            .Include(s => s.Leistungsschein)
            .Include(s => s.Preiskomponenten)
            .Include(s => s.Bestandteile)
            .Include(s => s.Regeln).ThenInclude(r => r.Ziele)
            .AsSplitQuery()
            .ToListAsync(abbruch));

    /// <summary>Ohne Tracking verknüpft EF die Bestandteile nicht mit den geladenen Services; das holt diese Methode nach.</summary>
    private static List<Service> Verknuepfe(List<Service> services)
    {
        var nachId = services.ToDictionary(s => s.Id);
        foreach (var b in services.SelectMany(s => s.Bestandteile))
        {
            b.Bestandteil = nachId[b.BestandteilId];
            b.Bundle = nachId[b.BundleId];
        }

        foreach (var k in services.SelectMany(s => s.Preiskomponenten))
        {
            k.Service = nachId[k.ServiceId];
        }

        foreach (var z in services.SelectMany(s => s.Regeln).SelectMany(r => r.Ziele))
        {
            z.ZielService = nachId[z.ZielServiceId];
        }

        foreach (var r in services.SelectMany(s => s.Regeln))
        {
            r.Service = nachId[r.ServiceId];
        }

        return [.. services.OrderBy(s => s.Kategorie!.Sortierung).ThenBy(s => s.Sortierung).ThenBy(s => s.Code)];
    }

    private static void VerknuepfeKomponenten(Preisliste liste, IReadOnlyList<Service> services)
    {
        var komponenten = services.SelectMany(s => s.Preiskomponenten).ToDictionary(k => k.Id);
        foreach (var p in liste.Preise)
        {
            p.Preiskomponente = komponenten.GetValueOrDefault(p.PreiskomponenteId);
        }

        foreach (var s in liste.Staffeln)
        {
            s.Preiskomponente = komponenten.GetValueOrDefault(s.PreiskomponenteId);
        }

        foreach (var e in liste.EkPositionen)
        {
            e.Preiskomponente = komponenten.GetValueOrDefault(e.PreiskomponenteId);
        }
    }

    private static async Task<Preisliste> EntwurfAsync(KalkulatorDbContext kontext, int id, CancellationToken abbruch)
    {
        var liste = await kontext.Preislisten.SingleOrDefaultAsync(p => p.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Preisliste {id} gibt es nicht.");
        return liste.IstAenderbar ? liste : throw new PreislisteGesperrtException(liste.Bezeichnung);
    }

    private static async Task PruefeEindeutigAsync(KalkulatorDbContext kontext, string bezeichnung, int? ausser, CancellationToken abbruch)
    {
        if (await kontext.Preislisten.AnyAsync(p => p.Bezeichnung == bezeichnung && p.Id != ausser, abbruch))
        {
            throw new ArgumentException($"Eine Preisliste „{bezeichnung}“ gibt es schon.");
        }
    }

    private void PruefeSehen()
    {
        if (!_recht.DarfKatalogSehen)
        {
            throw new KeinZugriffException("Preislisten sehen nur Produktmanagement und Führung.");
        }
    }

    private void PruefePflegen()
    {
        if (!_recht.DarfKatalogPflegen)
        {
            throw new KeinZugriffException("Preislisten pflegt und gibt nur das Produktmanagement frei.");
        }
    }

    private static string Pflicht(string? wert, string feld) =>
        string.IsNullOrWhiteSpace(wert) ? throw new ArgumentException($"{feld} fehlt.") : wert.Trim();

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

    private static void NichtNegativ(decimal? wert, string feld)
    {
        if (wert < 0)
        {
            throw new ArgumentException($"{feld} darf nicht negativ sein.");
        }
    }
}
