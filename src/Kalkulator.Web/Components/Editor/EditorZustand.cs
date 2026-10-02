using Kalkulator.Domain.Berechnung;
using Kalkulator.Domain.Katalog;
using Kalkulator.Infrastructure.Berechnung;

namespace Kalkulator.Web.Components.Editor;

/// <summary>Eine wählbare Preiskomponente im Editor, mit Preis aus der aktuellen Preisliste.</summary>
public sealed record KatalogZeile(string Code, string Bezeichnung, string ServiceCode, Einheit Einheit, Abrechnungsart Abrechnungsart, decimal? Preis, string? PreisHinweis);

public sealed record KatalogGruppe(string Name, IReadOnlyList<KatalogZeile> Zeilen);

/// <summary>
/// Bearbeitbarer Zustand des Kalkulationseditors. Hält die Eingaben so, wie die Oberfläche sie braucht,
/// und baut daraus die <see cref="KalkulationsEingabe"/> für den Rechenkern und das Speichern.
/// </summary>
public sealed class EditorZustand
{
    public string Titel { get; set; } = "";
    public DateTime? Vertragsbeginn { get; set; }
    public string? ConnectCode { get; set; }
    public int AnzahlUser { get; set; }
    public int? AnzahlMitarbeitende { get; set; }
    public decimal? BisherigerMonatspreis { get; set; }

    /// <summary>Gebuchte Mengen je Komponenten-Code (ohne Connect).</summary>
    public Dictionary<string, int> Mengen { get; } = new(StringComparer.Ordinal);

    public bool MitSupportkontingent { get; set; }
    public int AnfragenProMonat { get; set; }
    public decimal AeJeAnfrage { get; set; } = 1m;

    public bool MitServerBackup { get; set; }
    public BackupVariante BackupVariante { get; set; } = BackupVariante.Cloud;
    public int BackupServer { get; set; } = 1;
    public int NativGeschuetzteGb { get; set; }
    public decimal BelegteTb { get; set; }
    public int Lizenzinstanzen { get; set; }

    public bool MitCloudServer { get; set; }
    public decimal EkTerraKalkulator { get; set; }
    public CloudBackup CloudBackup { get; set; } = CloudBackup.Offen;

    public static EditorZustand Aus(string titel, DateOnly? vertragsbeginn, KalkulationsEingabe eingabe, IReadOnlySet<string> connectCodes)
    {
        var zustand = new EditorZustand
        {
            Titel = titel,
            Vertragsbeginn = vertragsbeginn?.ToDateTime(TimeOnly.MinValue),
            AnzahlUser = eingabe.AnzahlUser,
            AnzahlMitarbeitende = eingabe.AnzahlMitarbeitende,
            BisherigerMonatspreis = eingabe.BisherigerMonatspreis,
        };

        foreach (var position in eingabe.Positionen)
        {
            if (connectCodes.Contains(position.KomponentenCode))
            {
                zustand.ConnectCode = position.KomponentenCode;
            }
            else
            {
                zustand.Mengen[position.KomponentenCode] = zustand.Mengen.GetValueOrDefault(position.KomponentenCode) + position.Menge;
            }
        }

        if (eingabe.Supportkontingent is { } s60)
        {
            (zustand.MitSupportkontingent, zustand.AnfragenProMonat, zustand.AeJeAnfrage) = (true, s60.AnfragenProMonat, s60.AeJeAnfrage);
        }

        if (eingabe.ServerBackup is { } s14)
        {
            zustand.MitServerBackup = true;
            zustand.BackupVariante = s14.Variante;
            zustand.BackupServer = s14.Server;
            zustand.NativGeschuetzteGb = s14.NativGeschuetzteGb;
            zustand.BelegteTb = s14.BelegteTb;
            zustand.Lizenzinstanzen = s14.Lizenzinstanzen;
        }

        if (eingabe.CloudServer is { } s61)
        {
            (zustand.MitCloudServer, zustand.EkTerraKalkulator, zustand.CloudBackup) = (true, s61.EkTerraKalkulator, s61.Backup);
        }

        return zustand;
    }

    public DateOnly? VertragsbeginnDatum => Vertragsbeginn is { } d ? DateOnly.FromDateTime(d) : null;

    /// <summary>Eingabe ohne Sonderpositionen; diese verwaltet die Kalkulation selbst mit ihrem Freigabestatus.</summary>
    public KalkulationsEingabe AlsEingabe()
    {
        var positionen = new List<PositionsEingabe>();
        if (ConnectCode is not null)
        {
            positionen.Add(new PositionsEingabe(ConnectCode, 1));
        }

        positionen.AddRange(Mengen.Where(m => m.Value != 0).OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new PositionsEingabe(m.Key, m.Value)));

        return new KalkulationsEingabe
        {
            Positionen = positionen,
            AnzahlUser = AnzahlUser,
            AnzahlMitarbeitende = AnzahlMitarbeitende,
            BisherigerMonatspreis = BisherigerMonatspreis,
            Supportkontingent = MitSupportkontingent ? new SupportkontingentEingabe(AnfragenProMonat, AeJeAnfrage) : null,
            ServerBackup = MitServerBackup
                ? new ServerBackupEingabe(BackupVariante, BackupServer,
                    BackupVariante == BackupVariante.Cloud ? NativGeschuetzteGb : 0,
                    BackupVariante == BackupVariante.Objektspeicher ? BelegteTb : 0,
                    BackupVariante == BackupVariante.Objektspeicher ? Lizenzinstanzen : 0)
                : null,
            CloudServer = MitCloudServer ? new CloudServerEingabe(EkTerraKalkulator, CloudBackup) : null,
        };
    }

    public int Menge(string code) => Mengen.GetValueOrDefault(code);

    public void SetzeMenge(string code, int menge)
    {
        if (menge == 0)
        {
            Mengen.Remove(code);
        }
        else
        {
            Mengen[code] = menge;
        }
    }

    /// <summary>
    /// Gliedert den Katalog für den Editor: Connect-Stufen, Gruppen je Kategorie und die Sonderrechner getrennt.
    /// Zukünftige und geparkte Services sowie Onboarding-Pauschalen (automatisch) erscheinen nicht.
    /// </summary>
    public static (IReadOnlyList<KatalogZeile> Connect, IReadOnlyList<KatalogGruppe> Gruppen) Gliedere(GeladenerKatalog katalog)
    {
        var preise = katalog.Preisliste;
        KatalogZeile Zeile(Service s, Preiskomponente k)
        {
            var preis = k.StaffelBezug == StaffelBezug.Keine ? preise.PreisFuer(k) : null;
            var hinweis = k.StaffelBezug == StaffelBezug.Mitarbeitende ? "nach Mitarbeitenden"
                : preis is null ? "auf Anfrage" : null;
            // Komponenten tragen oft schon den Servicenamen („Schwachstellenmanagement je Client“); dann nicht doppeln.
            var name = k.Bezeichnung.StartsWith(s.Bezeichnung, StringComparison.Ordinal) ? k.Bezeichnung : $"{s.Bezeichnung} – {k.Bezeichnung}";
            return new KatalogZeile(k.Code, name, s.Code, k.Einheit, k.Abrechnungsart, preis, hinweis);
        }

        var sichtbar = katalog.Services
            .Where(s => s.DarfAngebotenWerden && !KatalogCodes.Sonderrechner.Contains(s.Code))
            .OrderBy(s => s.Kategorie!.Sortierung).ThenBy(s => s.Sortierung)
            .ToList();

        var connect = sichtbar.Where(s => s.Typ == ServiceTyp.Connect)
            .SelectMany(s => s.Preiskomponenten.Where(k => k.Abrechnungsart == Abrechnungsart.Monatlich).Select(k => Zeile(s, k)))
            .ToList();

        var gruppen = sichtbar.Where(s => s.Typ != ServiceTyp.Connect)
            .GroupBy(s => s.Kategorie!.Name)
            .Select(g => new KatalogGruppe(g.Key, [.. g.SelectMany(s => s.Preiskomponenten
                .Where(k => !(k.Abrechnungsart == Abrechnungsart.Einmalig && k.StaffelBezug == StaffelBezug.User))
                .OrderBy(k => k.Sortierung)
                .Select(k => Zeile(s, k)))]))
            .Where(g => g.Zeilen.Count > 0)
            .ToList();

        return (connect, gruppen);
    }
}
