using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Kalkulator.Documents;

/// <summary>
/// Daten für eine Vorlage. Werte sind Texte, Wahrheitswerte oder Listen weiterer Datensätze.
/// Schlüssel dürfen Punkte enthalten („angebot.nummer“); sie werden nicht weiter zerlegt.
/// </summary>
public sealed class Datensatz : Dictionary<string, object?>
{
    public Datensatz()
        : base(StringComparer.Ordinal)
    {
    }
}

public class VorlagenFehler(string meldung) : InvalidOperationException(meldung);

/// <summary>
/// Befüllt Word-Vorlagen (ADR-0004). Die Vorlage wird in Word gepflegt; variable Stellen schreibt man als Platzhalter:
/// <list type="bullet">
/// <item><c>{{schluessel}}</c> im Fließtext, in Tabellen, Kopf- und Fußzeilen.</item>
/// <item>Ein Absatz, der nur <c>{{#liste}}</c> enthält, und ein späterer Absatz mit <c>{{/liste}}</c> umschließen einen Block,
/// der je Eintrag wiederholt wird. Ist der Wert ein Wahrheitswert oder Text, erscheint der Block einmal oder gar nicht.</item>
/// <item>In Tabellen beginnt die erste Zelle einer Zeile mit <c>{{#liste}}</c> und die letzte Zelle derselben (oder einer
/// späteren) Zeile endet mit <c>{{/liste}}</c>; diese Zeilen werden je Eintrag wiederholt.</item>
/// </list>
/// Innerhalb eines Blocks gelten zuerst die Felder des Eintrags, dann die äußeren Werte. Unbekannte Platzhalter führen
/// zu einem <see cref="VorlagenFehler"/>, damit eine fehlerhafte Vorlage nicht unbemerkt halbe Angebote erzeugt.
/// </summary>
public static partial class WordVorlage
{
    [GeneratedRegex(@"\{\{\s*([^{}#/\s][^{}]*?)\s*\}\}")]
    private static partial Regex Platzhalter();

    /// <summary>Jeder Platzhalter einschließlich Blockmarken, so wie er im Text steht.</summary>
    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex PlatzhalterRoh();

    [GeneratedRegex(@"^\s*\{\{\s*#\s*([^{}]+?)\s*\}\}")]
    private static partial Regex BlockAnfang();

    [GeneratedRegex(@"\{\{\s*/\s*([^{}]+?)\s*\}\}\s*$")]
    private static partial Regex BlockEnde();

    public static byte[] Befuellen(byte[] vorlage, Datensatz daten)
    {
        using var strom = new MemoryStream();
        strom.Write(vorlage);
        var unbekannt = new SortedSet<string>(StringComparer.Ordinal);

        using (var dokument = WordprocessingDocument.Open(strom, isEditable: true))
        {
            var haupt = dokument.MainDocumentPart ?? throw new VorlagenFehler("Die Vorlage hat keinen Dokumentteil.");
            var wurzeln = new List<OpenXmlElement> { haupt.Document?.Body ?? throw new VorlagenFehler("Die Vorlage hat keinen Inhalt.") };
            wurzeln.AddRange(haupt.HeaderParts.Select(h => h.Header).OfType<OpenXmlElement>());
            wurzeln.AddRange(haupt.FooterParts.Select(f => f.Footer).OfType<OpenXmlElement>());

            var kontext = new List<Datensatz> { daten };
            foreach (var wurzel in wurzeln)
            {
                FasseLaeufeZusammen(wurzel);
                Verarbeite(wurzel, kontext, unbekannt);
            }

            if (unbekannt.Count > 0)
            {
                throw new VorlagenFehler("Die Vorlage enthält unbekannte Platzhalter: " + string.Join(", ", unbekannt));
            }

            haupt.Document!.Save();
            foreach (var h in haupt.HeaderParts)
            {
                h.Header?.Save();
            }

            foreach (var f in haupt.FooterParts)
            {
                f.Footer?.Save();
            }
        }

        return strom.ToArray();
    }

    /// <summary>
    /// Word teilt Text oft auf mehrere Läufe auf (Rechtschreibprüfung, Bearbeitungsspuren). Damit ein Platzhalter immer
    /// in einem Textelement steht, wird jeder aufgeteilte Platzhalter in das Textelement verschoben, in dem er beginnt;
    /// dessen Formatierung gilt für den eingesetzten Wert. Alle übrigen Läufe behalten ihre Formatierung.
    /// </summary>
    private static void FasseLaeufeZusammen(OpenXmlElement wurzel)
    {
        foreach (var absatz in wurzel.Descendants<Paragraph>().ToList())
        {
            var texte = absatz.Descendants<Text>().ToList();
            var gesamt = string.Concat(texte.Select(t => t.Text));
            if (texte.Count < 2 || !gesamt.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            var anfaenge = new int[texte.Count];
            for (var k = 1; k < texte.Count; k++)
            {
                anfaenge[k] = anfaenge[k - 1] + texte[k - 1].Text.Length;
            }

            int Index(int position) => Array.FindLastIndex(anfaenge, a => a <= position);

            // Von hinten, damit die Positionen der vorderen Platzhalter gültig bleiben.
            var geaendert = false;
            foreach (var treffer in PlatzhalterRoh().Matches(gesamt).Reverse())
            {
                var von = Index(treffer.Index);
                var bis = Index(treffer.Index + treffer.Length - 1);
                if (von == bis)
                {
                    continue;
                }

                texte[von].Text = texte[von].Text[..(treffer.Index - anfaenge[von])] + treffer.Value;
                for (var k = von + 1; k < bis; k++)
                {
                    texte[k].Text = "";
                }

                texte[bis].Text = texte[bis].Text[(treffer.Index + treffer.Length - anfaenge[bis])..];
                texte[von].Space = SpaceProcessingModeValues.Preserve;
                texte[bis].Space = SpaceProcessingModeValues.Preserve;
                geaendert = true;
            }

            if (!geaendert)
            {
                continue;
            }

            foreach (var t in texte.Where(t => t.Text.Length == 0))
            {
                var lauf = t.Parent;
                t.Remove();
                if (lauf is Run r && !r.ChildElements.Any(c => c is not RunProperties))
                {
                    r.Remove();
                }
            }
        }
    }

    /// <summary>Verarbeitet die Kinder eines Elements: Blöcke wiederholen, Tabellenzeilen wiederholen, Platzhalter ersetzen.</summary>
    private static void Verarbeite(OpenXmlElement eltern, List<Datensatz> kontext, ISet<string> unbekannt)
    {
        var i = 0;
        while (i < eltern.ChildElements.Count)
        {
            var element = eltern.ChildElements[i];
            if (element is Paragraph absatz && BlockAnfang().Match(absatz.InnerText) is { Success: true } anfang
                && absatz.InnerText.Trim() == anfang.Value.Trim())
            {
                var schluessel = anfang.Groups[1].Value;
                var ende = SucheEnde(eltern, i, schluessel)
                    ?? throw new VorlagenFehler($"Zum Block {{{{#{schluessel}}}}} fehlt {{{{/{schluessel}}}}}.");
                var vorlage = Enumerable.Range(i + 1, ende - i - 1).Select(k => eltern.ChildElements[k]).ToList();
                var eintraege = Eintraege(schluessel, kontext, unbekannt);

                var einfuegen = eltern.ChildElements[ende];
                var neu = new List<OpenXmlElement>();
                foreach (var eintrag in eintraege)
                {
                    var innerer = eintrag is null ? kontext : [.. kontext, eintrag];
                    var behaelter = new Body();
                    foreach (var v in vorlage)
                    {
                        behaelter.AppendChild(v.CloneNode(true));
                    }

                    Verarbeite(behaelter, innerer, unbekannt);
                    neu.AddRange(behaelter.ChildElements.Select(c => c.CloneNode(true)));
                }

                foreach (var n in neu)
                {
                    eltern.InsertBefore(n, einfuegen);
                }

                // Vorlage und Markierungen entfernen; weiter hinter dem eingefügten Inhalt.
                einfuegen.Remove();
                foreach (var v in vorlage)
                {
                    v.Remove();
                }

                absatz.Remove();
                i += neu.Count;
                continue;
            }

            if (element is Table tabelle)
            {
                VerarbeiteTabelle(tabelle, kontext, unbekannt);
            }
            else if (element is Paragraph p)
            {
                Ersetze(p, kontext, unbekannt);
            }
            else if (element.HasChildren)
            {
                Verarbeite(element, kontext, unbekannt);
            }

            i++;
        }
    }

    private static int? SucheEnde(OpenXmlElement eltern, int anfang, string schluessel)
    {
        var tiefe = 0;
        for (var k = anfang + 1; k < eltern.ChildElements.Count; k++)
        {
            if (eltern.ChildElements[k] is not Paragraph p)
            {
                continue;
            }

            var text = p.InnerText.Trim();
            if (BlockAnfang().Match(text) is { Success: true } a && a.Groups[1].Value == schluessel && text == a.Value.Trim())
            {
                tiefe++;
            }
            else if (BlockEnde().Match(text) is { Success: true } e && e.Groups[1].Value == schluessel && text == e.Value.Trim())
            {
                if (tiefe == 0)
                {
                    return k;
                }

                tiefe--;
            }
        }

        return null;
    }

    private static void VerarbeiteTabelle(Table tabelle, List<Datensatz> kontext, ISet<string> unbekannt)
    {
        var zeilen = tabelle.Elements<TableRow>().ToList();
        for (var i = 0; i < zeilen.Count; i++)
        {
            var zeile = zeilen[i];
            var erste = zeile.Elements<TableCell>().FirstOrDefault();
            if (erste is not null && BlockAnfang().Match(erste.InnerText) is { Success: true } anfang)
            {
                var schluessel = anfang.Groups[1].Value;
                var ende = Enumerable.Range(i, zeilen.Count - i)
                    .FirstOrDefault(k => BlockEnde().Match(zeilen[k].Elements<TableCell>().Last().InnerText) is { Success: true } e
                        && e.Groups[1].Value == schluessel, -1);
                if (ende < 0)
                {
                    throw new VorlagenFehler($"Zur Tabellenzeile {{{{#{schluessel}}}}} fehlt {{{{/{schluessel}}}}}.");
                }

                var vorlage = zeilen.Skip(i).Take(ende - i + 1).ToList();
                EntferneMarkierung(vorlage[0].Elements<TableCell>().First(), BlockAnfang());
                EntferneMarkierung(vorlage[^1].Elements<TableCell>().Last(), BlockEnde());

                foreach (var eintrag in Eintraege(schluessel, kontext, unbekannt))
                {
                    var innerer = eintrag is null ? kontext : [.. kontext, eintrag];
                    foreach (var v in vorlage)
                    {
                        var kopie = (TableRow)v.CloneNode(true);
                        tabelle.InsertBefore(kopie, vorlage[0]);
                        Verarbeite(kopie, innerer, unbekannt);
                    }
                }

                foreach (var v in vorlage)
                {
                    v.Remove();
                }

                i = ende;
                continue;
            }

            Verarbeite(zeile, kontext, unbekannt);
        }
    }

    private static void EntferneMarkierung(TableCell zelle, Regex markierung)
    {
        foreach (var text in zelle.Descendants<Text>())
        {
            text.Text = markierung.Replace(text.Text, "");
        }
    }

    /// <summary>Liste der Einträge, über die ein Block läuft; <c>null</c> steht für „einmal im äußeren Kontext“.</summary>
    private static IReadOnlyList<Datensatz?> Eintraege(string schluessel, List<Datensatz> kontext, ISet<string> unbekannt)
    {
        if (!Suche(schluessel, kontext, out var wert))
        {
            unbekannt.Add("#" + schluessel);
            return [];
        }

        return wert switch
        {
            null or false => [],
            true => [null],
            string s => string.IsNullOrWhiteSpace(s) ? [] : [null],
            IEnumerable<Datensatz> liste => [.. liste],
            _ => throw new VorlagenFehler($"„{schluessel}“ ist kein Block (Wert vom Typ {wert.GetType().Name})."),
        };
    }

    private static bool Suche(string schluessel, List<Datensatz> kontext, out object? wert)
    {
        for (var k = kontext.Count - 1; k >= 0; k--)
        {
            if (kontext[k].TryGetValue(schluessel, out wert))
            {
                return true;
            }
        }

        wert = null;
        return false;
    }

    private static void Ersetze(Paragraph absatz, List<Datensatz> kontext, ISet<string> unbekannt)
    {
        foreach (var text in absatz.Descendants<Text>().ToList())
        {
            if (!text.Text.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            var neu = Platzhalter().Replace(text.Text, m =>
            {
                var schluessel = m.Groups[1].Value;
                if (!Suche(schluessel, kontext, out var wert))
                {
                    unbekannt.Add(schluessel);
                    return "";
                }

                return wert switch
                {
                    null => "",
                    string s => s,
                    bool b => b ? "ja" : "nein",
                    _ => throw new VorlagenFehler($"„{schluessel}“ ist eine Liste und kann nicht als Text eingesetzt werden."),
                };
            });

            SetzeText(text, neu);
        }
    }

    /// <summary>Setzt Text; Zeilenumbrüche im Wert werden zu Umbrüchen im Lauf.</summary>
    private static void SetzeText(Text text, string wert)
    {
        var zeilen = wert.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        text.Text = zeilen[0];
        text.Space = SpaceProcessingModeValues.Preserve;
        OpenXmlElement letztes = text;
        foreach (var zeile in zeilen.Skip(1))
        {
            var umbruch = new Break();
            letztes.InsertAfterSelf(umbruch);
            var neu = new Text(zeile) { Space = SpaceProcessingModeValues.Preserve };
            umbruch.InsertAfterSelf(neu);
            letztes = neu;
        }
    }
}
