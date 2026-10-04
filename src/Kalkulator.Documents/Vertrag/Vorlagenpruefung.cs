using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Documents.Vertrag;

/// <summary>Was für ein Dokument geprüft wird; der Grundvertrag hat Pflicht-Platzhalter.</summary>
public enum VorlagenRolle
{
    Grundvertrag = 1,
    Sonstige = 2,
}

/// <summary>
/// Ergebnis der Prüfung einer Vertragsvorlage: Hinweise (Fehler zuerst), erkannte Eingaben, verwendete Platzhalter des
/// Kalkulators (ohne Eingaben) und die Codes der Preiskomponenten aus <c>{{preis.…}}</c>, jeweils sortiert.
/// </summary>
public sealed record Vorlagenanalyse(
    IReadOnlyList<VorlagenHinweis> Hinweise,
    IReadOnlyList<EingabeDefinition> Eingaben,
    IReadOnlyList<string> Platzhalter,
    IReadOnlyList<string> Komponenten)
{
    public bool HatFehler => Hinweise.Any(h => h.IstFehler);
}

/// <summary>
/// Prüft eine Vertragsvorlage, bevor sie verwendet wird (#26, Teil B): Jeder Platzhalter muss bekannt sein, Blöcke
/// müssen sauber geschlossen sein, Eingaben werden erkannt. Hinweise auf Stellen, die wie vergessene Platzhalter
/// aussehen („[Vertragsnummer]“, „______“), sind keine Fehler.
/// </summary>
public static partial class Vorlagenpruefung
{
    [GeneratedRegex(@"\{\{([^{}]*)\}\}")]
    private static partial Regex Roh();

    [GeneratedRegex(@"\[[^\[\]{}]{3,80}\]")]
    private static partial Regex EckigeKlammern();

    [GeneratedRegex(@"_{5,}")]
    private static partial Regex Linie();

    private const int MaxNameLaenge = 60;
    private const int MaxKlammerHinweise = 10;

    /// <summary>Prüft eine Vorlage; <paramref name="komponentenCodes"/> sind alle Preiskomponenten des Katalogs.</summary>
    public static Vorlagenanalyse Pruefe(byte[] datei, VorlagenRolle rolle, IReadOnlySet<string> komponentenCodes)
    {
        var lauf = new Lauf(komponentenCodes);
        try
        {
            using var strom = new MemoryStream(datei, writable: false);
            using var dokument = WordprocessingDocument.Open(strom, isEditable: false);
            var haupt = dokument.MainDocumentPart ?? throw new VorlagenFehler("Die Datei hat keinen Dokumentteil.");
            var wurzeln = new List<OpenXmlElement>();
            wurzeln.AddRange(haupt.HeaderParts.Select(h => h.Header).OfType<OpenXmlElement>());
            wurzeln.Add(haupt.Document?.Body ?? throw new VorlagenFehler("Die Datei hat keinen Inhalt."));
            wurzeln.AddRange(haupt.FooterParts.Select(f => f.Footer).OfType<OpenXmlElement>());

            foreach (var wurzel in wurzeln)
            {
                foreach (var absatz in wurzel.Descendants<Paragraph>())
                {
                    lauf.Absatz(absatz);
                }

                lauf.WurzelEnde();
            }
        }
        catch (Exception e) when (e is OpenXmlPackageException or InvalidDataException or FileFormatException)
        {
            return new Vorlagenanalyse([new VorlagenHinweis(true, "Die Datei ist kein lesbares Word-Dokument (.docx).")], [], [], []);
        }
        catch (VorlagenFehler e)
        {
            return new Vorlagenanalyse([new VorlagenHinweis(true, e.Message)], [], [], []);
        }

        if (rolle == VorlagenRolle.Grundvertrag)
        {
            foreach (var pflicht in Vertragsplatzhalter.PflichtImGrundvertrag.Where(p => !p.Any(lauf.Verwendet.Contains)))
            {
                var texte = pflicht.Select(p => Vertragsplatzhalter.Alle.Single(a => a.Schluessel == p).Art == PlatzhalterArt.Liste
                    ? $"die Liste {{{{#{p}}}}} … {{{{/{p}}}}}"
                    : $"der Platzhalter {{{{{p}}}}}");
                lauf.Fehler($"Im Grundvertrag fehlt {string.Join(" oder ", texte)}.");
            }
        }

        return lauf.Ergebnis();
    }

    private sealed class Lauf(IReadOnlySet<string> komponentenCodes)
    {
        private readonly List<VorlagenHinweis> _hinweise = [];
        private readonly Stack<string> _bloecke = new();
        private readonly Dictionary<string, (EingabeArt Art, List<string> Optionen)> _eingaben = new(StringComparer.Ordinal);
        private readonly List<string> _eingabeReihenfolge = [];
        private readonly SortedSet<string> _komponenten = new(StringComparer.Ordinal);
        private int _klammern, _linien;

        public SortedSet<string> Verwendet { get; } = new(StringComparer.Ordinal);

        public void Fehler(string text)
        {
            if (!_hinweise.Any(h => h.IstFehler && h.Text == text))
            {
                _hinweise.Add(new VorlagenHinweis(true, text));
            }
        }

        public void Absatz(Paragraph absatz)
        {
            var text = absatz.InnerText;
            foreach (Match k in EckigeKlammern().Matches(Roh().Replace(text, "")))
            {
                if (++_klammern <= MaxKlammerHinweise)
                {
                    _hinweise.Add(new VorlagenHinweis(false, $"Text in eckigen Klammern „{k.Value}“: Fehlt hier ein Platzhalter?"));
                }
            }

            _linien += Linie().Matches(text).Count;

            foreach (Match m in Roh().Matches(text))
            {
                var inhalt = m.Groups[1].Value.Trim();
                if (inhalt.StartsWith('#'))
                {
                    var name = inhalt[1..].Trim();
                    PruefeBlockStellung(absatz, text, m, anfang: true);
                    BlockAnfang(name);
                    _bloecke.Push(name);
                }
                else if (inhalt.StartsWith('/'))
                {
                    var name = inhalt[1..].Trim();
                    PruefeBlockStellung(absatz, text, m, anfang: false);
                    if (_bloecke.Count == 0 || _bloecke.Peek() != name)
                    {
                        Fehler(_bloecke.Count == 0
                            ? $"{{{{/{name}}}}} schließt einen Block, der nicht geöffnet wurde."
                            : $"{{{{/{name}}}}} passt nicht zum offenen Block {{{{#{_bloecke.Peek()}}}}}.");
                    }
                    else
                    {
                        _bloecke.Pop();
                    }
                }
                else
                {
                    Feld(inhalt);
                }
            }
        }

        /// <summary>Am Ende von Kopfzeile, Text und Fußzeile müssen alle Blöcke geschlossen sein.</summary>
        public void WurzelEnde()
        {
            while (_bloecke.Count > 0)
            {
                var offen = _bloecke.Pop();
                Fehler($"Zum Block {{{{#{offen}}}}} fehlt {{{{/{offen}}}}}.");
            }
        }

        public Vorlagenanalyse Ergebnis()
        {
            if (_klammern > MaxKlammerHinweise)
            {
                _hinweise.Add(new VorlagenHinweis(false, $"… und {_klammern - MaxKlammerHinweise} weitere Stellen in eckigen Klammern."));
            }

            if (_linien > 0)
            {
                _hinweise.Add(new VorlagenHinweis(false, $"{_linien} Ausfüllstelle(n) mit Linie (_____) ohne Platzhalter: bleiben im Vertrag leer."));
            }

            var eingaben = _eingabeReihenfolge.Select(n => new EingabeDefinition(_eingaben[n].Art, n, _eingaben[n].Optionen)).ToList();
            foreach (var liste in eingaben.Where(e => e.Art == EingabeArt.Liste && e.Optionen.Count == 0))
            {
                Fehler($"Die Liste {{{{#eingabe.{liste.Name}}}}} hat keine Spalten. Spalten schreibt man als {{{{{liste.Name}.Spalte}}}}.");
            }

            return new Vorlagenanalyse(
                [.. _hinweise.OrderByDescending(h => h.IstFehler)],
                eingaben,
                [.. Verwendet],
                [.. _komponenten]);
        }

        private void BlockAnfang(string name)
        {
            if (Vertragsplatzhalter.Alle.SingleOrDefault(p => p.Schluessel == name) is { } bekannt)
            {
                if (bekannt.Art != PlatzhalterArt.Liste)
                {
                    Fehler($"{{{{{name}}}}} ist ein Text und kein Block.");
                }

                Verwendet.Add(name);
            }
            else if (name.StartsWith(Vertragsplatzhalter.PreisPraefix, StringComparison.Ordinal))
            {
                // {{#preis.S14-SRV}}: Zeile erscheint nur, wenn die Komponente gebucht ist.
                Komponente(name[Vertragsplatzhalter.PreisPraefix.Length..]);
            }
            else if (name.StartsWith(Vertragsplatzhalter.EingabePraefix, StringComparison.Ordinal))
            {
                var rest = name[Vertragsplatzhalter.EingabePraefix.Length..];
                if (rest.Contains('=', StringComparison.Ordinal))
                {
                    Auswahl(rest);
                }
                else if (GueltigerName(rest, name))
                {
                    Eingabe(rest, EingabeArt.Liste, null);
                }
            }
            else
            {
                Fehler($"Unbekannter Block {{{{#{name}}}}}.");
            }
        }

        private void Feld(string schluessel)
        {
            if (Vertragsplatzhalter.Alle.SingleOrDefault(p => p.Schluessel == schluessel) is { } bekannt)
            {
                switch (bekannt.Art)
                {
                    case PlatzhalterArt.Liste:
                        Fehler($"{{{{{schluessel}}}}} ist eine Liste und gehört in einen Block {{{{#{schluessel}}}}} … {{{{/{schluessel}}}}}.");
                        break;
                    case PlatzhalterArt.Listenfeld when !Vertragsplatzhalter.ListenFuer(bekannt).Any(_bloecke.Contains):
                        Fehler($"{{{{{schluessel}}}}} steht außerhalb der Liste {{{{#{bekannt.Liste}}}}}.");
                        break;
                    default:
                        Verwendet.Add(schluessel);
                        break;
                }

                return;
            }

            if (schluessel.StartsWith(Vertragsplatzhalter.PreisPraefix, StringComparison.Ordinal))
            {
                var rest = schluessel[Vertragsplatzhalter.PreisPraefix.Length..];
                var punkt = rest.LastIndexOf('.');
                if (punkt <= 0 || !Vertragsplatzhalter.PreisFelder.Contains(rest[(punkt + 1)..]))
                {
                    Fehler($"{{{{{schluessel}}}}}: Preisfelder heißen {{{{preis.KOMPONENTE.menge}}}}, .einzelpreis oder .summe.");
                }
                else
                {
                    Komponente(rest[..punkt]);
                }

                return;
            }

            if (schluessel.StartsWith(Vertragsplatzhalter.EingabePraefix, StringComparison.Ordinal))
            {
                var rest = schluessel[Vertragsplatzhalter.EingabePraefix.Length..];
                if (rest.Contains('=', StringComparison.Ordinal))
                {
                    Auswahl(rest);
                }
                else if (GueltigerName(rest, schluessel))
                {
                    Eingabe(rest, EingabeArt.Text, null);
                }

                return;
            }

            // Spalte einer Eingabeliste: {{Server.Name}} im Block {{#eingabe.Server}}.
            var trenner = schluessel.IndexOf('.', StringComparison.Ordinal);
            if (trenner > 0 && _bloecke.Contains(Vertragsplatzhalter.EingabePraefix + schluessel[..trenner]))
            {
                var spalte = schluessel[(trenner + 1)..].Trim();
                if (spalte.Length == 0)
                {
                    Fehler($"{{{{{schluessel}}}}}: Der Spaltenname fehlt.");
                }
                else
                {
                    Eingabe(schluessel[..trenner], EingabeArt.Liste, spalte);
                }

                return;
            }

            Fehler($"Unbekannter Platzhalter {{{{{schluessel}}}}}.");
        }

        private void Komponente(string code)
        {
            if (komponentenCodes.Contains(code))
            {
                _komponenten.Add(code);
            }
            else
            {
                Fehler($"Die Preiskomponente „{code}“ gibt es im Katalog nicht.");
            }
        }

        private void Auswahl(string rest)
        {
            var teile = rest.Split('=', 2);
            var gruppe = teile[0].Trim();
            var option = teile[1].Trim();
            if (option.Length == 0)
            {
                Fehler($"{{{{eingabe.{rest}}}}}: Nach „=“ fehlt die Option.");
            }
            else if (GueltigerName(gruppe, "eingabe." + rest))
            {
                Eingabe(gruppe, EingabeArt.Auswahl, option);
            }
        }

        private bool GueltigerName(string name, string platzhalter)
        {
            if (name.Length == 0 || name.Length > MaxNameLaenge || name.Contains('.', StringComparison.Ordinal) || name != name.Trim())
            {
                Fehler($"{{{{{platzhalter}}}}}: Der Name einer Eingabe braucht 1 bis {MaxNameLaenge} Zeichen, ohne Punkt und ohne Leerzeichen am Rand.");
                return false;
            }

            return true;
        }

        private void Eingabe(string name, EingabeArt art, string? option)
        {
            if (!_eingaben.TryGetValue(name, out var vorhanden))
            {
                vorhanden = (art, []);
                _eingaben[name] = vorhanden;
                _eingabeReihenfolge.Add(name);
            }
            else if (vorhanden.Art != art)
            {
                // {{eingabe.Gruppe}} neben {{eingabe.Gruppe=Option}} setzt die gewählte Option als Text ein.
                if (vorhanden.Art == EingabeArt.Text && art == EingabeArt.Auswahl)
                {
                    vorhanden = (EingabeArt.Auswahl, vorhanden.Optionen);
                    _eingaben[name] = vorhanden;
                }
                else if (!(vorhanden.Art == EingabeArt.Auswahl && art == EingabeArt.Text))
                {
                    Fehler($"Die Eingabe „{name}“ wird als {Text(vorhanden.Art)} und als {Text(art)} verwendet.");
                    return;
                }
            }

            if (option is not null && !vorhanden.Optionen.Contains(option, StringComparer.Ordinal))
            {
                vorhanden.Optionen.Add(option);
            }
        }

        private static string Text(EingabeArt art) => art switch
        {
            EingabeArt.Text => "Text",
            EingabeArt.Auswahl => "Auswahl",
            _ => "Liste",
        };

        /// <summary>
        /// Blockmarken stehen allein im Absatz oder, für Tabellenzeilen, am Anfang der ersten bzw. am Ende der letzten
        /// Zelle (<see cref="WordVorlage"/>).
        /// </summary>
        private void PruefeBlockStellung(Paragraph absatz, string text, Match marke, bool anfang)
        {
            if (text.Trim() == marke.Value)
            {
                return;
            }

            var zelle = absatz.Ancestors<TableCell>().FirstOrDefault();
            if (zelle?.Parent is TableRow zeile)
            {
                var zellen = zeile.Elements<TableCell>().ToList();
                var zellText = zelle.InnerText;
                if (anfang && zelle == zellen[0] && zellText.TrimStart().StartsWith(marke.Value, StringComparison.Ordinal))
                {
                    return;
                }

                if (!anfang && zelle == zellen[^1] && zellText.TrimEnd().EndsWith(marke.Value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            Fehler($"{marke.Value} muss allein in einem Absatz stehen oder in einer Tabelle am Anfang der ersten (Blockanfang) bzw. am Ende der letzten Zelle (Blockende).");
        }
    }
}
