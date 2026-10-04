using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>Ergebnis der Prüfung vor der Freigabe; Fehler verhindern die Freigabe, Hinweise nicht.</summary>
public sealed record PruefHinweis(bool IstFehler, string Text);

/// <summary>
/// Prüft einen Preislisten-Entwurf vor der Freigabe gegen den Katalog: Pflichtparameter, Preise aller anbietbaren
/// Komponenten, Staffeln und, als Hinweis, fehlende EK-Werte und rote Margen.
/// </summary>
public static class Freigabepruefung
{
    /// <summary>
    /// Dafür müssen Preise, Staffeln, Parameter und EK-Werte der Preisliste sowie Services mit Komponenten und
    /// Bestandteilen geladen sein.
    /// </summary>
    public static IReadOnlyList<PruefHinweis> Pruefe(Preisliste preisliste, IEnumerable<Service> services)
    {
        var hinweise = new List<PruefHinweis>();
        void Fehler(string text) => hinweise.Add(new PruefHinweis(true, text));
        void Hinweis(string text) => hinweise.Add(new PruefHinweis(false, text));

        var fehlend = ParameterSchluessel.Pflicht.Where(s => preisliste.Parameter.All(p => p.Schluessel != s)).ToList();
        if (fehlend.Count > 0)
        {
            Fehler("Es fehlen Parameter: " + string.Join(", ", fehlend) + ".");
        }

        var teiler = preisliste.ParameterWertOder(ParameterSchluessel.CloudServerMargenteiler, 0.55m);
        if (teiler is <= 0 or >= 1)
        {
            Fehler($"Der Margenteiler für S61 muss zwischen 0 und 1 liegen (ist {teiler}).");
        }

        var schwellen = Margenschwellen.Aus(preisliste);
        if (!(schwellen.RotUnter <= schwellen.GruenAb && (schwellen.GruenBis is null || schwellen.GruenAb <= schwellen.GruenBis)))
        {
            Fehler("Die Margen-Ampel braucht: rot unter ≤ grün ab ≤ grün bis.");
        }

        var ohnePreis = new List<string>();
        var ohneEk = new List<string>();
        var rot = new List<string>();
        foreach (var service in services.Where(s => s.DarfAngebotenWerden).OrderBy(s => s.Sortierung).ThenBy(s => s.Code))
        {
            foreach (var komponente in service.Preiskomponenten.OrderBy(k => k.Sortierung))
            {
                if (komponente.StaffelBezug != StaffelBezug.Keine)
                {
                    var stufen = preisliste.Staffeln
                        .Where(s => ReferenceEquals(s.Preiskomponente, komponente) || (komponente.Id != 0 && s.PreiskomponenteId == komponente.Id))
                        .ToList();
                    if (stufen.Count == 0)
                    {
                        Fehler($"{komponente.Code}: Die Preisstaffel ist leer.");
                    }
                    else if (stufen.Min(s => s.AbMenge) > 1)
                    {
                        Hinweis($"{komponente.Code}: Die Staffel beginnt erst ab {stufen.Min(s => s.AbMenge)}; kleinere Mengen gelten als „individuell“.");
                    }
                }
                else if (!preisliste.Preise.Any(p => ReferenceEquals(p.Preiskomponente, komponente) || (komponente.Id != 0 && p.PreiskomponenteId == komponente.Id)))
                {
                    ohnePreis.Add(komponente.Code);
                }

                if (komponente.Abrechnungsart != Abrechnungsart.Monatlich)
                {
                    continue;
                }

                var pruefung = KomponentenPruefung.Fuer(preisliste, komponente, schwellen);
                if (pruefung.Kosten is null)
                {
                    ohneEk.Add(komponente.Code);
                }
                else if (pruefung.Ampel == Ampel.Rot)
                {
                    rot.Add($"{komponente.Code} ({pruefung.Marge:P0})");
                }
            }
        }

        if (ohnePreis.Count > 0)
        {
            Fehler("Ohne Preiseintrag: " + string.Join(", ", ohnePreis) + ". Im Reiter „Preise“ einen Betrag eintragen oder leer lassen für „auf Anfrage“ und speichern.");
        }

        if (ohneEk.Count > 0)
        {
            Hinweis("Ohne EK, Marge nicht berechenbar: " + string.Join(", ", ohneEk) + ".");
        }

        if (rot.Count > 0)
        {
            Hinweis("Marge im roten Bereich: " + string.Join(", ", rot) + ".");
        }

        return hinweise;
    }
}
