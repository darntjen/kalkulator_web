using System.Security.Cryptography;
using Kalkulator.Documents.Vertrag;
using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Vertrag;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Eine Vorlage mit ihren Fassungen (neueste zuerst, ohne Dateiinhalt) und den zugeordneten Services.</summary>
public sealed record VorlagenUebersicht(DokumentVorlage Vorlage, IReadOnlyList<Vorlagenversion> Fassungen, IReadOnlyList<string> Services)
{
    public Vorlagenversion? Aktiv => Fassungen.FirstOrDefault(f => f.Status == VorlagenStatus.Aktiv);
    public Vorlagenversion? Offen => Fassungen.FirstOrDefault(f => f.Status == VorlagenStatus.ZurPruefung);
}

public sealed record VorlagenQuellinfo(string Name, string Ort, bool Eingerichtet, Vorlagenabgleich? LetzterAbgleich);

/// <summary>
/// Vertragsvorlagen aus SharePoint (#26, Teil B): Abgleich mit der Quelle, Prüfung jeder neuen Fassung und Freigabe
/// durch das Produktmanagement. Für Vertragswerke zählt nur die aktive Fassung einer Vorlage.
/// </summary>
public sealed class VorlagenDienst(IDbContextFactory<KalkulatorDbContext> kontexte, IBenutzerKontext benutzer, TimeProvider zeit, IVorlagenQuelle quelle)
{
    /// <summary>Es läuft höchstens ein Abgleich zur Zeit, egal ob nächtlich oder auf Knopfdruck.</summary>
    private static readonly SemaphoreSlim Sperre = new(1, 1);

    private readonly Berechtigung _recht = new(benutzer);

    public Berechtigung Recht => _recht;

    public async Task<IReadOnlyList<VorlagenUebersicht>> UebersichtAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var vorlagen = await kontext.DokumentVorlagen.AsNoTracking().Include(d => d.Versionen)
            .Where(d => d.Typ != DokumentTyp.Angebot)
            .OrderBy(d => d.Typ).ThenBy(d => d.Code)
            .ToListAsync(abbruch);
        var zuordnung = await kontext.Services.AsNoTracking()
            .Where(s => s.LeistungsscheinId != null)
            .Select(s => new { s.LeistungsscheinId, s.Code })
            .ToListAsync(abbruch);
        return [.. vorlagen.Select(v => new VorlagenUebersicht(
            v,
            [.. v.Versionen.OrderByDescending(f => f.Nummer)],
            [.. zuordnung.Where(z => z.LeistungsscheinId == v.Id).Select(z => z.Code).Order(StringComparer.Ordinal)]))];
    }

    public async Task<VorlagenQuellinfo> QuellinfoAsync(CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var letzter = await kontext.Vorlagenabgleiche.AsNoTracking().OrderByDescending(a => a.Beginn).FirstOrDefaultAsync(abbruch);
        return new VorlagenQuellinfo(quelle.Name, quelle.Ort, quelle is not KeineVorlagenquelle, letzter);
    }

    public async Task<IReadOnlyList<Vorlagenabgleich>> AbgleicheAsync(int anzahl = 20, CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        return await kontext.Vorlagenabgleiche.AsNoTracking().OrderByDescending(a => a.Beginn).Take(anzahl).ToListAsync(abbruch);
    }

    /// <summary>Word-Datei einer Fassung zum Herunterladen.</summary>
    public async Task<(string Name, byte[] Inhalt)> DateiAsync(int fassungId, CancellationToken abbruch = default)
    {
        PruefeSehen();
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var fassung = await kontext.Vorlagenversionen.AsNoTracking().Include(v => v.Datei).SingleOrDefaultAsync(v => v.Id == fassungId, abbruch)
            ?? throw new KeyNotFoundException($"Fassung {fassungId} gibt es nicht.");
        return (fassung.Dateiname, fassung.Datei!.Inhalt);
    }

    /// <summary>Abgleich auf Knopfdruck durch das Produktmanagement.</summary>
    public Task<Vorlagenabgleich> AbgleichenAsync(CancellationToken abbruch = default)
    {
        PruefePflegen();
        return FuehreAbgleichAusAsync(_recht.Name, abbruch);
    }

    public Task AktivierenAsync(int fassungId, string? kommentar, CancellationToken abbruch = default) =>
        EntscheideAsync(fassungId, (v, f) => v.Aktiviere(f, _recht.Name, zeit.GetUtcNow(), kommentar), abbruch);

    public Task AblehnenAsync(int fassungId, string kommentar, CancellationToken abbruch = default) =>
        EntscheideAsync(fassungId, (v, f) => v.Ablehnen(f, _recht.Name, zeit.GetUtcNow(), kommentar), abbruch);

    private async Task EntscheideAsync(int fassungId, Action<DokumentVorlage, Vorlagenversion> entscheidung, CancellationToken abbruch)
    {
        if (!_recht.DarfKatalogFreigeben)
        {
            throw new KeinZugriffException("Vertragsvorlagen gibt nur das Produktmanagement oder der Admin frei.");
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var vorlage = await kontext.DokumentVorlagen.Include(d => d.Versionen)
            .SingleOrDefaultAsync(d => d.Versionen.Any(v => v.Id == fassungId), abbruch)
            ?? throw new KeyNotFoundException($"Fassung {fassungId} gibt es nicht.");
        entscheidung(vorlage, vorlage.Versionen.Single(v => v.Id == fassungId));
        await kontext.SaveChangesAsync(abbruch);
    }

    /// <summary>
    /// Gleicht die Vorlagen mit der Quelle ab, ohne Rechteprüfung (auch für den nächtlichen Lauf). Neue oder geänderte
    /// Dateien werden geladen, geprüft und als Fassung „zur Prüfung“ abgelegt; unveränderte (gleicher Stand in der Quelle
    /// oder gleicher Inhalt) bleiben unberührt. Fehler beim Zugriff auf die Quelle stehen im Protokoll des Abgleichs.
    /// </summary>
    internal async Task<Vorlagenabgleich> FuehreAbgleichAusAsync(string ausgeloestVon, CancellationToken abbruch)
    {
        if (!await Sperre.WaitAsync(0, abbruch))
        {
            throw new InvalidOperationException("Es läuft gerade ein Abgleich. Bitte in einer Minute noch einmal versuchen.");
        }

        try
        {
            await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
            var abgleich = new Vorlagenabgleich { Quelle = quelle.Name, AusgeloestVon = ausgeloestVon, Beginn = zeit.GetUtcNow() };
            kontext.Vorlagenabgleiche.Add(abgleich);
            await kontext.SaveChangesAsync(abbruch);

            var bericht = new List<string>();
            try
            {
                await GleicheAbAsync(kontext, abgleich, bericht, ausgeloestVon, abbruch);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                abgleich.Fehler = e.Message.Length > 2000 ? e.Message[..2000] : e.Message;
                foreach (var eintrag in kontext.ChangeTracker.Entries().Where(e => e.Entity is not Vorlagenabgleich).ToList())
                {
                    eintrag.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
            }

            abgleich.Ende = zeit.GetUtcNow();
            abgleich.Bericht = string.Join("\n", bericht);
            await kontext.SaveChangesAsync(abbruch);
            return abgleich;
        }
        finally
        {
            Sperre.Release();
        }
    }

    private async Task GleicheAbAsync(KalkulatorDbContext kontext, Vorlagenabgleich abgleich, List<string> bericht, string ausgeloestVon, CancellationToken abbruch)
    {
        var dateien = await quelle.ListeAsync(abbruch);
        abgleich.Dateien = dateien.Count;
        var komponenten = (await kontext.Preiskomponenten.AsNoTracking().Select(k => k.Code).ToListAsync(abbruch)).ToHashSet(StringComparer.Ordinal);
        var vorlagen = await kontext.DokumentVorlagen.Include(d => d.Versionen).ToListAsync(abbruch);

        var zugeordnet = new List<(QuellDatei Datei, VorlagenZuordnung Zuordnung)>();
        foreach (var datei in dateien.Where(d => !Vorlagendateiname.Ueberspringen(d.Pfad)))
        {
            if (Vorlagendateiname.Zuordnen(datei.Name) is { } z)
            {
                zugeordnet.Add((datei, z));
            }
            else
            {
                bericht.Add($"Nicht zugeordnet: {datei.Pfad}");
            }
        }

        foreach (var gruppe in zugeordnet.GroupBy(z => z.Zuordnung.Code, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var (datei, zuordnung) = gruppe.OrderByDescending(z => z.Datei.GeaendertAm).First();
            if (gruppe.Count() > 1)
            {
                bericht.Add($"{gruppe.Key}: mehrere Dateien ({string.Join(", ", gruppe.Select(g => g.Datei.Pfad))}); verwendet wird die zuletzt geänderte, {datei.Name}.");
            }

            var vorlage = vorlagen.SingleOrDefault(v => v.Code == zuordnung.Code);
            if (vorlage is null)
            {
                vorlage = new DokumentVorlage { Typ = zuordnung.Typ, Code = zuordnung.Code, Bezeichnung = zuordnung.Bezeichnung, Version = "" };
                kontext.DokumentVorlagen.Add(vorlage);
                vorlagen.Add(vorlage);
                bericht.Add($"{zuordnung.Code}: neue Vorlage angelegt ({zuordnung.Bezeichnung}). Für Leistungsscheine bitte im Service zuordnen.");
            }

            var letzte = vorlage.Versionen.MaxBy(v => v.Nummer);
            if (letzte is not null && datei.Stand is not null && letzte.QuellId == datei.Id && letzte.QuellStand == datei.Stand)
            {
                continue;
            }

            var inhalt = await quelle.LadeAsync(datei, abbruch);
            var analyse = Vorlagenpruefung.Pruefe(inhalt, vorlage.Typ == DokumentTyp.Grundvertrag ? VorlagenRolle.Grundvertrag : VorlagenRolle.Sonstige, komponenten);
            var neu = vorlage.NimmAuf(new Vorlagenversion
            {
                VersionLaut = zuordnung.Version,
                Dateiname = datei.Name,
                Quelle = quelle.Name,
                Pfad = datei.Pfad,
                QuellId = datei.Id,
                QuellStand = datei.Stand,
                GeaendertInQuelleAm = datei.GeaendertAm,
                GeaendertInQuelleVon = datei.GeaendertVon,
                Sha256 = Convert.ToHexString(SHA256.HashData(inhalt)),
                AbgerufenAm = zeit.GetUtcNow(),
                AbgerufenVon = ausgeloestVon,
                Hinweise = analyse.Hinweise,
                Eingaben = analyse.Eingaben,
                Komponenten = analyse.Komponenten,
                Unterschriften = analyse.Unterschriften,
                Datei = new VorlagenDatei { Inhalt = inhalt },
            }, zeit.GetUtcNow());

            if (neu is not null)
            {
                abgleich.NeueFassungen++;
                var fehler = analyse.Hinweise.Count(h => h.IstFehler);
                bericht.Add($"{zuordnung.Code}: neue Fassung V{neu.Nummer} aus {datei.Name}, zur Prüfung"
                    + (fehler > 0 ? $" – {fehler} Fehler in der Vorlagenprüfung" : "")
                    + (analyse.Eingaben.Count > 0 ? $" – {analyse.Eingaben.Count} Eingabe(n)" : "") + ".");
            }

            await kontext.SaveChangesAsync(abbruch);
        }

        var gefunden = zugeordnet.Select(z => z.Zuordnung.Code).ToHashSet(StringComparer.Ordinal);
        var fehlend = vorlagen.Where(v => v.Typ != DokumentTyp.Angebot && !gefunden.Contains(v.Code)).Select(v => v.Code).Order(StringComparer.Ordinal).ToList();
        if (fehlend.Count > 0)
        {
            bericht.Add($"Ohne Datei in der Quelle: {string.Join(", ", fehlend)}.");
        }
    }

    private void PruefeSehen()
    {
        if (!_recht.DarfKatalogSehen)
        {
            throw new KeinZugriffException("Die Vertragsvorlagen sehen nur Produktmanagement und Führung.");
        }
    }

    private void PruefePflegen()
    {
        if (!_recht.DarfKatalogPflegen)
        {
            throw new KeinZugriffException("Vertragsvorlagen abgleichen und freigeben darf nur das Produktmanagement.");
        }
    }
}

/// <summary>
/// Steht für die Quelle, solange keine (oder eine unvollständige) konfiguriert ist; der Abgleich meldet dann den Grund.
/// </summary>
public sealed class KeineVorlagenquelle(string grund = "Es ist keine Vorlagenquelle eingerichtet. Bitte „Vorlagen:Quelle“ auf „SharePoint“ oder „Ordner“ setzen.") : IVorlagenQuelle
{
    public string Name => "keine";

    public string Ort => grund;

    public Task<IReadOnlyList<QuellDatei>> ListeAsync(CancellationToken abbruch) => throw new InvalidOperationException(grund);

    public Task<byte[]> LadeAsync(QuellDatei datei, CancellationToken abbruch) => throw new NotSupportedException();
}
