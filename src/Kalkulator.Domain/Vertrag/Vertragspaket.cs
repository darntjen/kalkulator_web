using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Vertrag;

public enum VertragsdokumentArt
{
    Avv = 1,
    Grundvertrag = 2,
    Avb = 3,
    Sla = 4,
    Connect = 5,
    Bundle = 6,
    Leistungsschein = 7,
}

/// <summary>
/// Ein Dokument des Vertragspakets. <paramref name="Bundle"/> ist bei Leistungsscheinen gesetzt, die als Anlage hinter
/// einem gebuchten Bundle-Schein liegen.
/// </summary>
public sealed record Vertragsdokument(VertragsdokumentArt Art, string Code, string Bezeichnung, string? Bundle = null);

/// <summary>
/// Löst die gebuchten Services in die Dokumente des Vertragspakets auf (docs/06_ist-analyse.md, Abschnitt 5;
/// Referenzfall RK-08). Rangfolge nach § 1 Abs. 3 Grundvertrag: AVV, Grundvertrag, AVB, Anlage SLA, S01,
/// Bundle-Scheine mit ihren S-Scheinen, Einzel-Leistungsscheine.
/// </summary>
public static class Vertragspaket
{
    /// <summary>Leistungsschein-Code von Nösse Connect; die Stufen heißen im Katalog S01-STD, S01-PRM und S01-ENT.</summary>
    public const string ConnectLeistungsschein = "S01";

    /// <param name="gebuchteServiceCodes">Codes der gebuchten Services, z. B. aus den Positionen einer Kalkulationsversion.</param>
    /// <param name="katalog">Alle Services mit Bundle-Bestandteilen.</param>
    public static IReadOnlyList<Vertragsdokument> Aufloesen(IEnumerable<string> gebuchteServiceCodes, IEnumerable<Service> katalog)
    {
        var services = katalog.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var gebucht = gebuchteServiceCodes
            .Distinct(StringComparer.Ordinal)
            .Select(c => services.TryGetValue(c, out var s) ? s : throw new ArgumentException($"Service „{c}“ ist nicht im Katalog."))
            .ToList();

        var connect = gebucht.SingleOrDefault(s => s.Typ == ServiceTyp.Connect);

        // Bei reinen S41-Verträgen gilt die SLA-Stufe Standard (Frage 10.5).
        var slaStufe = connect is null ? "Standard" : Stufe(connect);
        var dokumente = new List<Vertragsdokument>
        {
            new(VertragsdokumentArt.Avv, "AVV", "Auftragsverarbeitungsvereinbarung"),
            new(VertragsdokumentArt.Grundvertrag, "Grundvertrag", "Managed-Services-Vertrag (Grundvertrag)"),
            new(VertragsdokumentArt.Avb, "AVB", "Allgemeine Vertragsbedingungen Cloud + Managed Services"),
            new(VertragsdokumentArt.Sla, "SLA", $"Service Level Agreement – Stufe {slaStufe}"),
        };

        if (connect is not null)
        {
            dokumente.Add(new Vertragsdokument(VertragsdokumentArt.Connect, ConnectLeistungsschein, connect.Bezeichnung));
        }

        var beigelegt = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bundle in gebucht.Where(s => s.Typ == ServiceTyp.Bundle && !InAnderemBundle(s, gebucht)).OrderBy(s => s.Code, StringComparer.Ordinal))
        {
            dokumente.Add(new Vertragsdokument(VertragsdokumentArt.Bundle, bundle.Code, bundle.Bezeichnung));
            foreach (var schein in Einzelscheine(bundle).OrderBy(s => s.Code, StringComparer.Ordinal))
            {
                if (beigelegt.Add(schein.Code))
                {
                    dokumente.Add(new Vertragsdokument(VertragsdokumentArt.Leistungsschein, schein.Code, schein.Bezeichnung, bundle.Code));
                }
            }
        }

        foreach (var einzel in gebucht.Where(s => s.Typ is not (ServiceTyp.Connect or ServiceTyp.Bundle)).OrderBy(s => s.Code, StringComparer.Ordinal))
        {
            if (beigelegt.Add(einzel.Code))
            {
                dokumente.Add(new Vertragsdokument(VertragsdokumentArt.Leistungsschein, einzel.Code, einzel.Bezeichnung));
            }
        }

        return dokumente;
    }

    /// <summary>„Standard“, „Premium“ oder „Enterprise“ aus der Bezeichnung der Connect-Stufe.</summary>
    public static string Stufe(Service connect) =>
        connect.Bezeichnung.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];

    /// <summary>Ein Bundle, das in einem anderen gebuchten Bundle steckt (B01 in B02), bekommt keinen eigenen Schein.</summary>
    private static bool InAnderemBundle(Service bundle, IEnumerable<Service> gebucht) =>
        gebucht.Any(b => b.Typ == ServiceTyp.Bundle && !ReferenceEquals(b, bundle) && Einzelscheine(b, nurBundles: true).Contains(bundle));

    /// <summary>Alle Einzel-Leistungsscheine eines Bundles; verschachtelte Bundles werden aufgelöst.</summary>
    private static IEnumerable<Service> Einzelscheine(Service bundle, bool nurBundles = false)
    {
        foreach (var bestandteil in bundle.Bestandteile.Select(b => b.Bestandteil
            ?? throw new InvalidOperationException($"Die Bestandteile von „{bundle.Code}“ sind nicht geladen.")))
        {
            if (bestandteil.Typ == ServiceTyp.Bundle)
            {
                if (nurBundles)
                {
                    yield return bestandteil;
                }

                foreach (var s in Einzelscheine(bestandteil, nurBundles))
                {
                    yield return s;
                }
            }
            else if (!nurBundles)
            {
                yield return bestandteil;
            }
        }
    }
}
