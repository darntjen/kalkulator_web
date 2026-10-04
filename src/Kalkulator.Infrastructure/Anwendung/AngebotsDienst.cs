using System.Text;
using Kalkulator.Documents.Angebot;
using Kalkulator.Domain.Projekte;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure.Anwendung;

/// <summary>Einstellungen der Angebotserzeugung (Abschnitt „Angebot“ der Konfiguration).</summary>
public sealed class AngebotsEinstellungen
{
    /// <summary>Ordner mit <c>Angebotsvorlage.docx</c> und <c>textbausteine.json</c>; austauschbar ohne Codeänderung (C-02).</summary>
    public string Vorlagenordner { get; set; } = "";

    /// <summary>Standard-Gültigkeit ab Angebotsdatum (Frage 9.7); der Vertrieb kann sie ändern.</summary>
    public int GueltigkeitTage { get; set; } = 30;

    /// <summary>Bis zur Übernahme aus dem Entra-Profil (#4, Frage 9.6).</summary>
    public string AbsenderFunktion { get; set; } = "Vertrieb Managed Services";

    public string AbsenderTelefon { get; set; } = "+49 2171 7003-0";
}

public sealed record AngebotsZeile(int Id, string Nummer, string Version, DateOnly Datum, DateOnly GueltigBis, string ErstelltVon, DateOnly? VersendetAm);

/// <summary>
/// Angebot erzeugen (C-01 bis C-07) und Versand vermerken (C-10). Beim Erzeugen wird der Arbeitsstand eingefroren,
/// die Angebotsnummer vergeben, das Dokument aus der Vorlage befüllt und archiviert, alles in einer Transaktion.
/// </summary>
public sealed class AngebotsDienst(
    IDbContextFactory<KalkulatorDbContext> kontexte,
    IBenutzerKontext benutzer,
    TimeProvider zeit,
    IOptions<AngebotsEinstellungen> einstellungen)
{
    public const string Vorlagendatei = "Angebotsvorlage.docx";
    public const string Textbausteindatei = "textbausteine.json";
    private const string Kreis = "MS-A";
    private static readonly TimeZoneInfo Zeitzone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly Berechtigung _recht = new(benutzer);

    public int GueltigkeitTage => einstellungen.Value.GueltigkeitTage;

    public DateOnly Heute => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone).DateTime);

    /// <returns>Id des neuen Angebots.</returns>
    public async Task<int> ErzeugenAsync(int kalkulationId, string? freitext, DateOnly? gueltigBis, CancellationToken abbruch = default)
    {
        var heute = Heute;
        var bis = gueltigBis ?? heute.AddDays(einstellungen.Value.GueltigkeitTage);
        if (bis < heute)
        {
            throw new ArgumentException("Das Gültigkeitsdatum liegt in der Vergangenheit.", nameof(gueltigBis));
        }

        var vorlage = await File.ReadAllBytesAsync(Pfad(Vorlagendatei), abbruch);
        Textbausteine texte;
        await using (var json = File.OpenRead(Pfad(Textbausteindatei)))
        {
            texte = Textbausteine.Lies(json);
        }

        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);

        var kalkulation = await kontext.Kalkulationen
            .Include(k => k.Kundenprojekt).ThenInclude(p => p!.Kunde)
            .Include(k => k.Sonderpositionen)
            .Include(k => k.Vertriebsfreigaben)
            .SingleOrDefaultAsync(k => k.Id == kalkulationId, abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {kalkulationId} gibt es nicht.");
        if (!_recht.DarfBearbeiten(kalkulation.Kundenprojekt!))
        {
            throw new KeinZugriffException("Angebote zu dieser Kalkulation darfst du nicht erzeugen.");
        }

        // Kein Angebot an den Kunden ohne Vertriebsfreigabe (#26); die Freigaben landen in der eingefrorenen Version.
        if (!kalkulation.IstVertriebsfreigegeben)
        {
            throw new InvalidOperationException("Vor dem Angebot braucht die Kalkulation die Freigaben von Vertriebsleitung und Solution Consultant.");
        }

        // Für das Einfrieren immer mit EK, damit die Kosten der Version vollständig im Schema „intern“ landen.
        GeladenerKatalog katalog;
        try
        {
            katalog = await new RechenkernLader(kontext).LadeKatalogAsync(heute, mitEinkauf: true, entwurfZulassen: false, abbruch);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Es gibt noch keine freigegebene Preisliste. Angebote entstehen erst nach der Freigabe durch das Produktmanagement.");
        }

        var version = kalkulation.FriereEin(katalog.ErzeugeRechenkern(), _recht.Name, zeit.GetUtcNow());
        if (kalkulation.Angebotsnummer is null)
        {
            kalkulation.VergebeAngebotsnummer(await NaechsteNummerAsync(kontext, heute.Year, abbruch));
        }

        var nummer = kalkulation.Angebotsnummer!;
        var quelle = new AngebotsQuelle(nummer, version, kalkulation.Kundenprojekt!.Kunde!, heute, bis,
            string.IsNullOrWhiteSpace(freitext) ? null : freitext.Trim(), Absender(), katalog.Services, katalog.Preisliste, texte);
        var dokument = Angebotsdokument.Erzeuge(vorlage, quelle);

        var angebot = new Angebot
        {
            Version = version,
            Nummer = nummer,
            Datum = heute,
            GueltigBis = bis,
            Freitext = quelle.Freitext,
            Dateiname = Dateiname(nummer, version.Nummer, quelle.Kunde.Firma),
            Vorlage = Vorlagendatei,
            ErstelltVon = _recht.Name,
            ErstelltAm = zeit.GetUtcNow(),
            Datei = new AngebotsDatei { Inhalt = dokument },
        };
        kontext.Angebote.Add(angebot);
        await kontext.SaveChangesAsync(abbruch);
        await transaktion.CommitAsync(abbruch);
        return angebot.Id;
    }

    public async Task<IReadOnlyList<AngebotsZeile>> ListeAsync(int kalkulationId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        await PruefeSehenAsync(kontext, kalkulationId, abbruch);
        return await kontext.Angebote.AsNoTracking()
            .Where(a => a.Version!.KalkulationId == kalkulationId)
            .OrderByDescending(a => a.Version!.Nummer)
            .Select(a => new AngebotsZeile(a.Id, a.Nummer, "V" + a.Version!.Nummer, a.Datum, a.GueltigBis, a.ErstelltVon, a.VersendetAm))
            .ToListAsync(abbruch);
    }

    public async Task<(string Dateiname, byte[] Inhalt)> DateiAsync(int angebotId, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var angebot = await kontext.Angebote.AsNoTracking()
            .Include(a => a.Datei)
            .Include(a => a.Version)
            .SingleOrDefaultAsync(a => a.Id == angebotId, abbruch)
            ?? throw new KeyNotFoundException($"Angebot {angebotId} gibt es nicht.");
        await PruefeSehenAsync(kontext, angebot.Version!.KalkulationId, abbruch);
        return (angebot.Dateiname, angebot.Datei!.Inhalt);
    }

    /// <summary>Versandvermerk (C-10); der Projektstatus wechselt auf „Angebot versendet“, wenn der Ablauf es zulässt.</summary>
    public async Task AlsVersendetMarkierenAsync(int angebotId, DateOnly datum, CancellationToken abbruch = default)
    {
        await using var kontext = await kontexte.CreateDbContextAsync(abbruch);
        var angebot = await kontext.Angebote.Include(a => a.Version).SingleOrDefaultAsync(a => a.Id == angebotId, abbruch)
            ?? throw new KeyNotFoundException($"Angebot {angebotId} gibt es nicht.");
        var projekt = await kontext.Kundenprojekte
            .SingleAsync(p => p.Kalkulationen.Any(k => k.Id == angebot.Version!.KalkulationId), abbruch);
        if (!_recht.DarfBearbeiten(projekt))
        {
            throw new KeinZugriffException("Den Versand dieses Angebots darfst du nicht vermerken.");
        }

        angebot.AlsVersendetMarkieren(datum, _recht.Name);
        if (projekt.KannWechselnZu(ProjektStatus.AngebotVersendet))
        {
            projekt.SetzeStatus(ProjektStatus.AngebotVersendet, _recht.Name, zeit.GetUtcNow(), $"Angebot {angebot.Nummer} V{angebot.Version!.Nummer} versendet am {datum:dd.MM.yyyy}");
        }

        await kontext.SaveChangesAsync(abbruch);
    }

    private async Task PruefeSehenAsync(KalkulatorDbContext kontext, int kalkulationId, CancellationToken abbruch)
    {
        var projekt = await kontext.Kundenprojekte.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Kalkulationen.Any(k => k.Id == kalkulationId), abbruch)
            ?? throw new KeyNotFoundException($"Kalkulation {kalkulationId} gibt es nicht.");
        if (!_recht.DarfSehen(projekt))
        {
            throw new KeinZugriffException("Dieses Angebot gehört zu einem Kundenprojekt eines anderen Vertriebsmitarbeiters.");
        }
    }

    /// <summary>
    /// Nächste Nummer im Kreis „MS-A“ des Jahres. MERGE mit HOLDLOCK hält die Zeile bis zum Ende der Transaktion,
    /// sodass parallele Angebote keine Nummer doppelt bekommen und eine abgebrochene Erzeugung keine verbraucht.
    /// </summary>
    private static async Task<string> NaechsteNummerAsync(KalkulatorDbContext kontext, int jahr, CancellationToken abbruch)
    {
        var nummern = await kontext.Database.SqlQuery<int>($"""
            MERGE projekte.Nummernkreise WITH (HOLDLOCK) AS ziel
            USING (SELECT {Kreis} AS Kreis, {jahr} AS Jahr) AS quelle
                ON ziel.Kreis = quelle.Kreis AND ziel.Jahr = quelle.Jahr
            WHEN MATCHED THEN UPDATE SET LetzteNummer = ziel.LetzteNummer + 1
            WHEN NOT MATCHED THEN INSERT (Kreis, Jahr, LetzteNummer) VALUES (quelle.Kreis, quelle.Jahr, 1)
            OUTPUT inserted.LetzteNummer AS [Value];
            """).ToListAsync(abbruch);
        return Nummernkreis.Format(Kreis, jahr, nummern.Single());
    }

    private AngebotsAbsender Absender()
    {
        var name = _recht.Name;
        var email = name.Contains('@', StringComparison.Ordinal) ? name : "";
        return new AngebotsAbsender(name, einstellungen.Value.AbsenderFunktion, einstellungen.Value.AbsenderTelefon, email);
    }

    private string Pfad(string datei)
    {
        var pfad = Path.Combine(einstellungen.Value.Vorlagenordner, datei);
        return File.Exists(pfad)
            ? pfad
            : throw new FileNotFoundException($"Die Angebotsvorlage „{datei}“ fehlt im Vorlagenordner „{einstellungen.Value.Vorlagenordner}“.", pfad);
    }

    /// <summary>„Angebot_MS-A-2026-0001_V1_Muster-GmbH.docx“</summary>
    private static string Dateiname(string nummer, int version, string firma)
    {
        var name = new StringBuilder();
        foreach (var zeichen in firma.Trim())
        {
            name.Append(char.IsLetterOrDigit(zeichen) ? zeichen : '-');
        }

        var kurz = string.Join('-', name.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return $"Angebot_{nummer}_V{version}_{(kurz.Length > 60 ? kurz[..60] : kurz)}.docx";
    }
}
