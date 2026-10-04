using System.Text.Json;
using System.Text.RegularExpressions;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Anwendung;

public sealed record ServiceZeile(
    int Id,
    string Code,
    string Bezeichnung,
    string Kategorie,
    ServiceTyp Typ,
    Vertriebsstatus Vertriebsstatus,
    int Komponenten,
    string? Leistungsschein,
    IReadOnlyList<string> Bestandteile);

/// <summary>Service mit allem, was die Bearbeitungsseite braucht, und den Angaben, was sich löschen lässt.</summary>
public sealed record ServiceAnsicht(
    Service Service,
    IReadOnlyList<Service> AlleServices,
    IReadOnlyList<ServiceKategorie> Kategorien,
    IReadOnlyList<DokumentVorlage> Leistungsscheine,
    IReadOnlyList<string> VerwendetIn,
    IReadOnlySet<int> LoeschbareKomponenten,
    string? GrundNichtLoeschbar);

public sealed record NeuerService(string Code, string Bezeichnung, ServiceTyp Typ, int KategorieId);

public sealed record ServiceStammdaten(
    string Bezeichnung,
    string? ServiceNummer,
    string? Kurzbeschreibung,
    int KategorieId,
    Vertriebsstatus Vertriebsstatus,
    int Sortierung,
    int? LeistungsscheinId);

public sealed record KomponentenStammdaten(
    string Bezeichnung,
    Einheit Einheit,
    Abrechnungsart Abrechnungsart,
    StaffelBezug StaffelBezug,
    string? NavisionArtikelnummer,
    int Sortierung);

public sealed record RegelStammdaten(RegelTyp Typ, string Meldung, IReadOnlyList<int> ZielServiceIds);

/// <summary>Stammdaten einer Vorlage; Version und Dateiname ergeben sich aus der aktiven Fassung (#26, Teil B).</summary>
public sealed record VorlagenStammdaten(DokumentTyp Typ, string Code, string Bezeichnung);

public sealed record VorlagenZeile(DokumentVorlage Vorlage, IReadOnlyList<string> Services);

public enum ProtokollBereich
{
    Alle = 0,
    Katalog = 1,
    Preise = 2,
    Einkauf = 3,
}

public sealed record FeldAenderung(string Feld, string? Alt, string? Neu);

public sealed record ProtokollZeile(
    DateTimeOffset Zeitpunkt,
    string Benutzer,
    string Objekt,
    string Bezug,
    string Aktion,
    IReadOnlyList<FeldAenderung> Aenderungen);

/// <summary>
/// Pflege des Katalogs (A-07, A-10 bis A-12): Kategorien, Services, Preiskomponenten, Bundle-Zusammensetzung, Regeln und
/// Vertragsvorlagen, dazu das Änderungsprotokoll (A-08). Der Katalog ist nicht versioniert (ADR-0005); deshalb sind
/// Codes nach dem Anlegen fest, und gelöscht wird nur, was nirgends verwendet wird. Alles andere stellt man über den
/// Vertriebsstatus still.
/// </summary>
public sealed partial class KatalogDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer)
{
    private static readonly string[] KatalogObjekte =
        [nameof(ServiceKategorie), nameof(Service), nameof(Preiskomponente), nameof(BundleBestandteil), nameof(ServiceRegel), nameof(ServiceRegelZiel), nameof(DokumentVorlage), nameof(Vorlagenversion)];

    private static readonly string[] PreisObjekte = [nameof(Preisliste), nameof(Preis), nameof(Preisstaffel), nameof(Parameter)];

    private static readonly string[] EinkaufObjekte = [nameof(EkPosition)];

    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,29}$")]
    private static partial Regex Codeformat();

    // ---------- Kategorien ----------

    public async Task<IReadOnlyList<ServiceKategorie>> KategorienAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        return await kontext.Kategorien.AsNoTracking().OrderBy(k => k.Sortierung).ThenBy(k => k.Name).ToListAsync(abbruch);
    }

    public async Task<int> KategorieAnlegenAsync(string name, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var text = Pflicht(name, "Name");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        if (await kontext.Kategorien.AnyAsync(k => k.Name == text, abbruch))
        {
            throw new ArgumentException($"Die Kategorie „{text}“ gibt es schon.");
        }

        var sortierung = await kontext.Kategorien.MaxAsync(k => (int?)k.Sortierung, abbruch) ?? 0;
        var kategorie = new ServiceKategorie { Name = text, Sortierung = sortierung + 1 };
        kontext.Kategorien.Add(kategorie);
        await kontext.SaveChangesAsync(abbruch);
        return kategorie.Id;
    }

    public async Task KategorieSpeichernAsync(int id, string name, int sortierung, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var text = Pflicht(name, "Name");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var kategorie = await kontext.Kategorien.SingleOrDefaultAsync(k => k.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Kategorie {id} gibt es nicht.");
        if (await kontext.Kategorien.AnyAsync(k => k.Name == text && k.Id != id, abbruch))
        {
            throw new ArgumentException($"Die Kategorie „{text}“ gibt es schon.");
        }

        kategorie.Name = text;
        kategorie.Sortierung = sortierung;
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Services ----------

    public async Task<IReadOnlyList<ServiceZeile>> ServicesAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var services = await PreislistenDienst.LadeServicesAsync(kontext, abbruch);
        return [.. services.Select(s => new ServiceZeile(s.Id, s.Code, s.Bezeichnung, s.Kategorie!.Name, s.Typ, s.Vertriebsstatus,
            s.Preiskomponenten.Count, s.Leistungsschein?.Code, [.. s.Bestandteile.Select(b => b.Bestandteil!.Code).Order()]))];
    }

    /// <summary>Service mit Komponenten, Bestandteilen und Regeln; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<ServiceAnsicht?> ServiceAsync(int id, CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var services = await PreislistenDienst.LadeServicesAsync(kontext, abbruch);
        var service = services.SingleOrDefault(s => s.Id == id);
        if (service is null)
        {
            return null;
        }

        var verwendetIn = services.Where(s => s.Bestandteile.Any(b => b.BestandteilId == id)).Select(s => $"Bundle {s.Code}")
            .Concat(services.SelectMany(s => s.Regeln).Where(r => r.ServiceId != id && r.Ziele.Any(z => z.ZielServiceId == id))
                .Select(r => $"Regel von {r.Service!.Code}"))
            .Distinct()
            .ToList();

        var loeschbar = new HashSet<int>();
        foreach (var k in service.Preiskomponenten)
        {
            if (await KomponenteVerwendetAsync(kontext, k, abbruch) is null)
            {
                loeschbar.Add(k.Id);
            }
        }

        var grund = verwendetIn.Count > 0
            ? "Wird verwendet: " + string.Join(", ", verwendetIn) + "."
            : loeschbar.Count < service.Preiskomponenten.Count
                ? "Mindestens eine Preiskomponente ist in einer freigegebenen Preisliste oder in Kalkulationen verwendet."
                : null;

        var kategorien = await kontext.Kategorien.AsNoTracking().OrderBy(k => k.Sortierung).ToListAsync(abbruch);
        var scheine = await kontext.DokumentVorlagen.AsNoTracking()
            .Where(d => d.Typ == DokumentTyp.Leistungsschein)
            .OrderBy(d => d.Code).ThenBy(d => d.Version)
            .ToListAsync(abbruch);
        return new ServiceAnsicht(service, services, kategorien, scheine, verwendetIn, loeschbar, grund);
    }

    /// <summary>Legt einen Service an; Code und Typ sind danach fest, weil Kalkulationen und Regeln darauf verweisen.</summary>
    public async Task<int> ServiceAnlegenAsync(NeuerService daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var code = Code(daten.Code);
        var bezeichnung = Pflicht(daten.Bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        if (await kontext.Services.AnyAsync(s => s.Code == code, abbruch))
        {
            throw new ArgumentException($"Einen Service {code} gibt es schon.");
        }

        if (!await kontext.Kategorien.AnyAsync(k => k.Id == daten.KategorieId, abbruch))
        {
            throw new ArgumentException("Bitte eine Kategorie wählen.");
        }

        var sortierung = await kontext.Services.Where(s => s.KategorieId == daten.KategorieId).MaxAsync(s => (int?)s.Sortierung, abbruch) ?? 0;

        // Neue Services sind zunächst „zukünftig“, damit sie erst nach Pflege von Komponenten und Preisen angeboten werden.
        var service = new Service
        {
            Code = code,
            Bezeichnung = bezeichnung,
            Typ = daten.Typ,
            KategorieId = daten.KategorieId,
            Sortierung = sortierung + 1,
            Vertriebsstatus = Vertriebsstatus.Zukuenftig,
        };
        kontext.Services.Add(service);
        await kontext.SaveChangesAsync(abbruch);
        return service.Id;
    }

    public async Task ServiceSpeichernAsync(int id, ServiceStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var bezeichnung = Pflicht(daten.Bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var service = await kontext.Services.SingleOrDefaultAsync(s => s.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Service {id} gibt es nicht.");
        if (!await kontext.Kategorien.AnyAsync(k => k.Id == daten.KategorieId, abbruch))
        {
            throw new ArgumentException("Bitte eine Kategorie wählen.");
        }

        if (daten.LeistungsscheinId is { } schein
            && !await kontext.DokumentVorlagen.AnyAsync(d => d.Id == schein && d.Typ == DokumentTyp.Leistungsschein, abbruch))
        {
            throw new ArgumentException("Als Leistungsschein lässt sich nur eine Vorlage vom Typ „Leistungsschein“ zuordnen.");
        }

        service.Bezeichnung = bezeichnung;
        service.ServiceNummer = Leer(daten.ServiceNummer);
        service.Kurzbeschreibung = Leer(daten.Kurzbeschreibung);
        service.KategorieId = daten.KategorieId;
        service.Vertriebsstatus = daten.Vertriebsstatus;
        service.Sortierung = daten.Sortierung;
        service.LeistungsscheinId = daten.LeistungsscheinId;
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>Löscht einen Service, der nirgends verwendet wird; sonst bitte den Vertriebsstatus auf „geparkt“ setzen.</summary>
    public async Task ServiceLoeschenAsync(int id, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var ansicht = await ServiceAsync(id, abbruch) ?? throw new KeyNotFoundException($"Service {id} gibt es nicht.");
        if (ansicht.GrundNichtLoeschbar is { } grund)
        {
            throw new InvalidOperationException($"{ansicht.Service.Code} lässt sich nicht löschen. {grund} Stattdessen den Vertriebsstatus auf „geparkt“ setzen.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var service = await kontext.Services
            .Include(s => s.Preiskomponenten).Include(s => s.Bestandteile).Include(s => s.Regeln).ThenInclude(r => r.Ziele)
            .SingleAsync(s => s.Id == id, abbruch);
        await EntferneEntwurfswerteAsync(kontext, [.. service.Preiskomponenten.Select(k => k.Id)], abbruch);
        kontext.Services.Remove(service);
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Preiskomponenten ----------

    public async Task<int> KomponenteAnlegenAsync(int serviceId, string code, KomponentenStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var kurz = Code(code);
        var bezeichnung = Pflicht(daten.Bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        if (!await kontext.Services.AnyAsync(s => s.Id == serviceId, abbruch))
        {
            throw new KeyNotFoundException($"Service {serviceId} gibt es nicht.");
        }

        if (await kontext.Preiskomponenten.AnyAsync(k => k.Code == kurz, abbruch))
        {
            throw new ArgumentException($"Eine Preiskomponente {kurz} gibt es schon.");
        }

        var komponente = new Preiskomponente { ServiceId = serviceId, Code = kurz, Bezeichnung = bezeichnung };
        Uebernimm(komponente, daten, bezeichnung);
        kontext.Preiskomponenten.Add(komponente);
        await kontext.SaveChangesAsync(abbruch);
        return komponente.Id;
    }

    public async Task KomponenteSpeichernAsync(int id, KomponentenStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        var bezeichnung = Pflicht(daten.Bezeichnung, "Bezeichnung");
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var komponente = await kontext.Preiskomponenten.SingleOrDefaultAsync(k => k.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Preiskomponente {id} gibt es nicht.");
        if (komponente.StaffelBezug != daten.StaffelBezug && await KomponenteVerwendetAsync(kontext, komponente, abbruch) is { } grund)
        {
            throw new InvalidOperationException($"Die Staffelung von {komponente.Code} lässt sich nicht mehr ändern: {grund}");
        }

        Uebernimm(komponente, daten, bezeichnung);
        await kontext.SaveChangesAsync(abbruch);
    }

    public async Task KomponenteLoeschenAsync(int id, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var komponente = await kontext.Preiskomponenten.SingleOrDefaultAsync(k => k.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Preiskomponente {id} gibt es nicht.");
        if (await KomponenteVerwendetAsync(kontext, komponente, abbruch) is { } grund)
        {
            throw new InvalidOperationException($"{komponente.Code} lässt sich nicht löschen: {grund}");
        }

        await EntferneEntwurfswerteAsync(kontext, [id], abbruch);
        kontext.Preiskomponenten.Remove(komponente);
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Bundles ----------

    public async Task BestandteilHinzufuegenAsync(int bundleId, int bestandteilId, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var services = await kontext.Services.Include(s => s.Bestandteile).ToListAsync(abbruch);
        var bundle = services.SingleOrDefault(s => s.Id == bundleId) ?? throw new KeyNotFoundException($"Service {bundleId} gibt es nicht.");
        var bestandteil = services.SingleOrDefault(s => s.Id == bestandteilId) ?? throw new KeyNotFoundException($"Service {bestandteilId} gibt es nicht.");
        if (bundle.Typ != ServiceTyp.Bundle)
        {
            throw new ArgumentException($"{bundle.Code} ist kein Bundle.");
        }

        if (bestandteil.Typ == ServiceTyp.Connect)
        {
            throw new ArgumentException("Nösse Connect ist nie Teil eines Bundles.");
        }

        if (bundle.Bestandteile.Any(b => b.BestandteilId == bestandteilId))
        {
            throw new ArgumentException($"{bestandteil.Code} ist schon in {bundle.Code} enthalten.");
        }

        // Verschachtelte Bundles sind erlaubt (B02 enthält B01), Kreise nicht.
        if (bestandteil.Enthaelt(bundle))
        {
            throw new ArgumentException($"{bestandteil.Code} enthält bereits {bundle.Code}; ein Bundle kann sich nicht selbst enthalten.");
        }

        kontext.BundleBestandteile.Add(new BundleBestandteil { BundleId = bundleId, BestandteilId = bestandteilId });
        await kontext.SaveChangesAsync(abbruch);
    }

    public async Task BestandteilEntfernenAsync(int bundleId, int bestandteilId, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var eintrag = await kontext.BundleBestandteile.SingleOrDefaultAsync(b => b.BundleId == bundleId && b.BestandteilId == bestandteilId, abbruch)
            ?? throw new KeyNotFoundException("Dieser Bestandteil gehört nicht zum Bundle.");
        kontext.BundleBestandteile.Remove(eintrag);
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Regeln ----------

    public async Task<int> RegelAnlegenAsync(int serviceId, RegelStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        if (!await kontext.Services.AnyAsync(s => s.Id == serviceId, abbruch))
        {
            throw new KeyNotFoundException($"Service {serviceId} gibt es nicht.");
        }

        var regel = new ServiceRegel { ServiceId = serviceId, Typ = daten.Typ, Meldung = "" };
        await UebernimmAsync(kontext, regel, serviceId, daten, abbruch);
        kontext.Regeln.Add(regel);
        await kontext.SaveChangesAsync(abbruch);
        return regel.Id;
    }

    public async Task RegelSpeichernAsync(int regelId, RegelStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var regel = await kontext.Regeln.Include(r => r.Ziele).SingleOrDefaultAsync(r => r.Id == regelId, abbruch)
            ?? throw new KeyNotFoundException($"Regel {regelId} gibt es nicht.");
        await UebernimmAsync(kontext, regel, regel.ServiceId, daten, abbruch);
        await kontext.SaveChangesAsync(abbruch);
    }

    public async Task RegelLoeschenAsync(int regelId, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var regel = await kontext.Regeln.Include(r => r.Ziele).SingleOrDefaultAsync(r => r.Id == regelId, abbruch)
            ?? throw new KeyNotFoundException($"Regel {regelId} gibt es nicht.");
        kontext.Regeln.Remove(regel);
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Vertragsvorlagen ----------

    public async Task<IReadOnlyList<VorlagenZeile>> VorlagenAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var vorlagen = await kontext.DokumentVorlagen.AsNoTracking().OrderBy(d => d.Typ).ThenBy(d => d.Code).ThenBy(d => d.Version).ToListAsync(abbruch);
        var zuordnung = await kontext.Services.AsNoTracking()
            .Where(s => s.LeistungsscheinId != null)
            .Select(s => new { s.LeistungsscheinId, s.Code })
            .ToListAsync(abbruch);
        return [.. vorlagen.Select(v => new VorlagenZeile(v, [.. zuordnung.Where(z => z.LeistungsscheinId == v.Id).Select(z => z.Code).Order()]))];
    }

    public async Task<int> VorlageAnlegenAsync(VorlagenStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var vorlage = new DokumentVorlage { Code = "", Bezeichnung = "", Version = "" };
        await UebernimmAsync(kontext, vorlage, daten, abbruch);
        kontext.DokumentVorlagen.Add(vorlage);
        await kontext.SaveChangesAsync(abbruch);
        return vorlage.Id;
    }

    public async Task VorlageSpeichernAsync(int id, VorlagenStammdaten daten, CancellationToken abbruch = default)
    {
        PruefePflegen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var vorlage = await kontext.DokumentVorlagen.SingleOrDefaultAsync(d => d.Id == id, abbruch)
            ?? throw new KeyNotFoundException($"Vorlage {id} gibt es nicht.");
        if (vorlage.Typ == DokumentTyp.Leistungsschein && daten.Typ != DokumentTyp.Leistungsschein
            && await kontext.Services.AnyAsync(s => s.LeistungsscheinId == id, abbruch))
        {
            throw new InvalidOperationException("Diese Vorlage ist Services als Leistungsschein zugeordnet; der Typ bleibt deshalb „Leistungsschein“.");
        }

        await UebernimmAsync(kontext, vorlage, daten, abbruch);
        await kontext.SaveChangesAsync(abbruch);
    }

    // ---------- Änderungsprotokoll ----------

    /// <summary>
    /// Änderungen an Katalog, Preislisten und EK, neueste zuerst (A-08). Die Bezüge werden, soweit die Objekte noch
    /// existieren, in Codes übersetzt (z. B. „S02-USER in Preisstand 15.07.2026“).
    /// </summary>
    public async Task<IReadOnlyList<ProtokollZeile>> ProtokollAsync(ProtokollBereich bereich, DateOnly? von, DateOnly? bis, int anzahl = 200, CancellationToken abbruch = default)
    {
        PruefeSehen();
        string[] objekte = bereich switch
        {
            ProtokollBereich.Katalog => KatalogObjekte,
            ProtokollBereich.Preise => PreisObjekte,
            ProtokollBereich.Einkauf => EinkaufObjekte,
            _ => [.. KatalogObjekte, .. PreisObjekte, .. EinkaufObjekte],
        };

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var abfrage = kontext.Aenderungsprotokoll.AsNoTracking().Where(e => objekte.Contains(e.Entitaet));
        if (von is { } v)
        {
            var ab = Tagesbeginn(v);
            abfrage = abfrage.Where(e => e.Zeitpunkt >= ab);
        }

        if (bis is { } b)
        {
            var vor = Tagesbeginn(b.AddDays(1));
            abfrage = abfrage.Where(e => e.Zeitpunkt < vor);
        }

        var eintraege = await abfrage.OrderByDescending(e => e.Id).Take(Math.Clamp(anzahl, 1, 1000)).ToListAsync(abbruch);
        var namen = await NamenAsync(kontext, abbruch);
        return [.. eintraege.Select(e =>
        {
            var felder = Felder(e.Aenderungen);
            return new ProtokollZeile(e.Zeitpunkt, e.Benutzer, Objektname(e.Entitaet), namen.Bezug(e.Entitaet, e.Schluessel, felder), e.Aktion, felder);
        })];
    }

    /// <summary>Mitternacht deutscher Zeit als Zeitpunkt, damit „von“ und „bis“ ganze Kalendertage meinen.</summary>
    private static DateTimeOffset Tagesbeginn(DateOnly tag)
    {
        var lokal = tag.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(lokal, Zeitzone.GetUtcOffset(lokal));
    }

    private sealed record Namen(
        IReadOnlyDictionary<int, string> Services,
        IReadOnlyDictionary<int, string> Komponenten,
        IReadOnlyDictionary<int, string> Preislisten,
        IReadOnlyDictionary<int, (int Liste, int Komponente)> Preise,
        IReadOnlyDictionary<int, (int Liste, int Komponente)> Staffeln,
        IReadOnlyDictionary<int, (int Liste, string Schluessel)> Parameter,
        IReadOnlyDictionary<int, (int Liste, int Komponente)> Ek,
        IReadOnlyDictionary<int, string> Kategorien,
        IReadOnlyDictionary<int, string> Vorlagen,
        IReadOnlyDictionary<int, int> Regeln,
        IReadOnlyDictionary<int, string> Fassungen)
    {
        /// <summary>
        /// Lesbarer Bezug eines Protokolleintrags. Existiert das Objekt nicht mehr, helfen die beim Löschen
        /// festgehaltenen Werte (Code, Komponente, Preisliste).
        /// </summary>
        public string Bezug(string objekt, string schluessel, IReadOnlyList<FeldAenderung> felder)
        {
            var teile = schluessel.Split('|');
            int Zahl(int i) => int.TryParse(teile.ElementAtOrDefault(i), out var z) ? z : 0;
            string? Feld(string name) => felder.FirstOrDefault(f => f.Feld == name) is { } f ? f.Neu ?? f.Alt : null;
            int FeldZahl(string name) => int.TryParse(Feld(name), out var z) ? z : 0;
            string Benannt(string art, int id) => Feld("Code") ?? Feld("Bezeichnung") ?? Feld("Name") ?? $"{art} {id}";
            string S(int id) => Services.GetValueOrDefault(id, $"Service {id}");
            string K(int id) => Komponenten.GetValueOrDefault(id, $"Komponente {id}");
            string L(int id) => Preislisten.GetValueOrDefault(id, $"Preisliste {id}");
            string InListe(IReadOnlyDictionary<int, (int Liste, int Komponente)> nachId, string art) =>
                nachId.TryGetValue(Zahl(0), out var x) ? $"{K(x.Komponente)} in {L(x.Liste)}"
                : FeldZahl("PreiskomponenteId") is > 0 and var k ? $"{K(k)} in {L(FeldZahl("PreislisteId"))}"
                : $"{art} {Zahl(0)}";
            return objekt switch
            {
                nameof(Service) => Services.GetValueOrDefault(Zahl(0)) ?? Benannt("Service", Zahl(0)),
                nameof(Preiskomponente) => Komponenten.GetValueOrDefault(Zahl(0)) ?? Benannt("Komponente", Zahl(0)),
                nameof(ServiceKategorie) => Kategorien.GetValueOrDefault(Zahl(0)) ?? Benannt("Kategorie", Zahl(0)),
                nameof(DokumentVorlage) => Vorlagen.GetValueOrDefault(Zahl(0)) ?? Benannt("Vorlage", Zahl(0)),
                nameof(Vorlagenversion) => Fassungen.GetValueOrDefault(Zahl(0)) ?? $"Fassung {Zahl(0)}",
                nameof(BundleBestandteil) => $"{S(Zahl(0))} enthält {S(Zahl(1))}",
                nameof(ServiceRegel) => Regeln.TryGetValue(Zahl(0), out var r) ? $"Regel {Zahl(0)} von {S(r)}"
                    : FeldZahl("ServiceId") is > 0 and var rs ? $"Regel {Zahl(0)} von {S(rs)}" : $"Regel {Zahl(0)}",
                nameof(ServiceRegelZiel) => $"Regel {Zahl(0)} → {S(Zahl(1))}",
                nameof(Preisliste) => Preislisten.GetValueOrDefault(Zahl(0)) ?? Benannt("Preisliste", Zahl(0)),
                nameof(Preis) => InListe(Preise, "Preis"),
                nameof(Preisstaffel) => InListe(Staffeln, "Staffelstufe"),
                nameof(Parameter) => Parameter.TryGetValue(Zahl(0), out var pa) ? $"{pa.Schluessel} in {L(pa.Liste)}"
                    : Feld("Schluessel") is { } ps ? $"{ps} in {L(FeldZahl("PreislisteId"))}" : $"Parameter {Zahl(0)}",
                nameof(EkPosition) => InListe(Ek, "EK-Position"),
                _ => schluessel,
            };
        }
    }

    private static async Task<Namen> NamenAsync(KalkulatorDbContext kontext, CancellationToken abbruch) => new(
        await kontext.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, abbruch),
        await kontext.Preiskomponenten.AsNoTracking().ToDictionaryAsync(k => k.Id, k => k.Code, abbruch),
        await kontext.Preislisten.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Bezeichnung, abbruch),
        await kontext.Preise.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.PreislisteId, p.PreiskomponenteId), abbruch),
        await kontext.Preisstaffeln.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.PreislisteId, p.PreiskomponenteId), abbruch),
        await kontext.Parameter.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.PreislisteId, p.Schluessel), abbruch),
        await kontext.EkPositionen.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.PreislisteId, p.PreiskomponenteId), abbruch),
        await kontext.Kategorien.AsNoTracking().ToDictionaryAsync(k => k.Id, k => k.Name, abbruch),
        await kontext.DokumentVorlagen.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Code, abbruch),
        await kontext.Regeln.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.ServiceId, abbruch),
        await kontext.Vorlagenversionen.AsNoTracking()
            .Join(kontext.DokumentVorlagen, v => v.DokumentVorlageId, d => d.Id, (v, d) => new { v.Id, d.Code, v.Nummer })
            .ToDictionaryAsync(v => v.Id, v => $"{v.Code} V{v.Nummer}", abbruch));

    private static string Objektname(string objekt) => objekt switch
    {
        nameof(ServiceKategorie) => "Kategorie",
        nameof(Service) => "Service",
        nameof(Preiskomponente) => "Preiskomponente",
        nameof(BundleBestandteil) => "Bundle-Bestandteil",
        nameof(ServiceRegel) => "Regel",
        nameof(ServiceRegelZiel) => "Regel-Ziel",
        nameof(DokumentVorlage) => "Vorlage",
        nameof(Vorlagenversion) => "Vorlagenfassung",
        nameof(Preisliste) => "Preisliste",
        nameof(Preis) => "Preis",
        nameof(Preisstaffel) => "Staffelstufe",
        nameof(Parameter) => "Parameter",
        nameof(EkPosition) => "EK",
        _ => objekt,
    };

    private static List<FeldAenderung> Felder(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var dokument = JsonDocument.Parse(json);
            return [.. dokument.RootElement.EnumerateObject().Select(f => new FeldAenderung(f.Name,
                Text(f.Value.TryGetProperty("Alt", out var alt) ? alt : default),
                Text(f.Value.TryGetProperty("Neu", out var neu) ? neu : default)))];
        }
        catch (JsonException)
        {
            return [new FeldAenderung("Änderungen", null, json)];
        }

        static string? Text(JsonElement wert) => wert.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => wert.GetString(),
            _ => wert.GetRawText(),
        };
    }

    // ---------- Hilfen ----------

    /// <summary>
    /// Grund, warum eine Komponente nicht mehr gelöscht oder umgestaffelt werden darf, oder <c>null</c>: Sie steckt in
    /// einer freigegebenen Preisliste, in einem Arbeitsstand oder in einem eingefrorenen Angebotsstand.
    /// </summary>
    private static async Task<string?> KomponenteVerwendetAsync(KalkulatorDbContext kontext, Preiskomponente komponente, CancellationToken abbruch)
    {
        var id = komponente.Id;
        var freigegeben = kontext.Preislisten.Where(p => p.Status == PreislistenStatus.Freigegeben).Select(p => p.Id);
        if (await kontext.Preise.AnyAsync(p => p.PreiskomponenteId == id && freigegeben.Contains(p.PreislisteId), abbruch)
            || await kontext.Preisstaffeln.AnyAsync(p => p.PreiskomponenteId == id && freigegeben.Contains(p.PreislisteId), abbruch)
            || await kontext.EkPositionen.AnyAsync(p => p.PreiskomponenteId == id && freigegeben.Contains(p.PreislisteId), abbruch))
        {
            return "Sie ist in einer freigegebenen Preisliste enthalten.";
        }

        // Die Eingabe ist JSON; gesucht wird der Code als Positionswert.
        var muster = $"%\"KomponentenCode\":\"{komponente.Code}\"%";
        var inArbeitsstand = await kontext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM kalkulation.Kalkulationen WHERE Eingabe LIKE {muster}")
            .SingleAsync(abbruch);
        var inVersion = await kontext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM kalkulation.VersionsPositionen WHERE Code = {komponente.Code}")
            .SingleAsync(abbruch);
        return inArbeitsstand + inVersion > 0 ? "Sie wird in Kalkulationen verwendet." : null;
    }

    /// <summary>Entfernt Preise, Staffeln und EK der Komponenten aus Entwürfen; freigegebene Werte gibt es dann nicht.</summary>
    private static async Task EntferneEntwurfswerteAsync(KalkulatorDbContext kontext, IReadOnlyList<int> komponenten, CancellationToken abbruch)
    {
        kontext.Preise.RemoveRange(await kontext.Preise.Where(p => komponenten.Contains(p.PreiskomponenteId)).ToListAsync(abbruch));
        kontext.Preisstaffeln.RemoveRange(await kontext.Preisstaffeln.Where(p => komponenten.Contains(p.PreiskomponenteId)).ToListAsync(abbruch));
        kontext.EkPositionen.RemoveRange(await kontext.EkPositionen.Where(p => komponenten.Contains(p.PreiskomponenteId)).ToListAsync(abbruch));
    }

    private static void Uebernimm(Preiskomponente komponente, KomponentenStammdaten daten, string bezeichnung)
    {
        komponente.Bezeichnung = bezeichnung;
        komponente.Einheit = daten.Einheit;
        komponente.Abrechnungsart = daten.Abrechnungsart;
        komponente.StaffelBezug = daten.StaffelBezug;
        komponente.NavisionArtikelnummer = Leer(daten.NavisionArtikelnummer);
        komponente.Sortierung = daten.Sortierung;
    }

    private static async Task UebernimmAsync(KalkulatorDbContext kontext, ServiceRegel regel, int serviceId, RegelStammdaten daten, CancellationToken abbruch)
    {
        var meldung = Pflicht(daten.Meldung, "Meldung für den Vertrieb");
        var ziele = daten.ZielServiceIds.Distinct().ToList();
        if (ziele.Count == 0)
        {
            throw new ArgumentException("Eine Regel braucht mindestens einen Ziel-Service.");
        }

        if (ziele.Contains(serviceId))
        {
            throw new ArgumentException("Ein Service kann sich in einer Regel nicht selbst als Ziel haben.");
        }

        if (await kontext.Services.CountAsync(s => ziele.Contains(s.Id), abbruch) != ziele.Count)
        {
            throw new ArgumentException("Mindestens ein Ziel-Service existiert nicht.");
        }

        regel.Typ = daten.Typ;
        regel.Meldung = meldung;
        foreach (var z in regel.Ziele.Where(z => !ziele.Contains(z.ZielServiceId)).ToList())
        {
            regel.Ziele.Remove(z);
        }

        foreach (var z in ziele.Where(z => regel.Ziele.All(v => v.ZielServiceId != z)))
        {
            regel.Ziele.Add(new ServiceRegelZiel { ZielServiceId = z });
        }
    }

    private static async Task UebernimmAsync(KalkulatorDbContext kontext, DokumentVorlage vorlage, VorlagenStammdaten daten, CancellationToken abbruch)
    {
        var code = Pflicht(daten.Code, "Code").ToUpperInvariant();
        if (await kontext.DokumentVorlagen.AnyAsync(d => d.Code == code && d.Id != vorlage.Id, abbruch))
        {
            throw new ArgumentException($"Die Vorlage {code} gibt es schon. Neue Fassungen kommen über den Abgleich mit SharePoint.");
        }

        vorlage.Typ = daten.Typ;
        vorlage.Code = code;
        vorlage.Bezeichnung = Pflicht(daten.Bezeichnung, "Bezeichnung");
    }

    private static string Code(string? code)
    {
        var text = (code ?? "").Trim().ToUpperInvariant();
        return Codeformat().IsMatch(text)
            ? text
            : throw new ArgumentException($"„{code}“ ist kein gültiger Code (Großbuchstaben, Ziffern und Bindestrich, z. B. S34 oder S34-USER).");
    }

    private void PruefeSehen()
    {
        if (!_recht.DarfKatalogSehen)
        {
            throw new KeinZugriffException("Den Katalog sehen nur Produktmanagement und Führung.");
        }
    }

    private void PruefePflegen()
    {
        if (!_recht.DarfKatalogPflegen)
        {
            throw new KeinZugriffException("Den Katalog pflegt nur das Produktmanagement.");
        }
    }

    private static string Pflicht(string? wert, string feld) =>
        string.IsNullOrWhiteSpace(wert) ? throw new ArgumentException($"{feld} fehlt.") : wert.Trim();

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
