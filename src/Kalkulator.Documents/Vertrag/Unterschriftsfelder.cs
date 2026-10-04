using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Kalkulator.Documents.Vertrag;

/// <summary>
/// Stelle eines Unterschriftsfelds im fertigen PDF (#26, Teil D). <see cref="Seite"/> zählt ab 1; <see cref="X"/> ist
/// der linke Rand, <see cref="Grundlinie"/> die Schriftlinie der Marke, beides in Punkt (1/72 Zoll) vom linken
/// unteren Seitenrand aus, wie im PDF üblich.
/// </summary>
public sealed record Unterschriftsfeld(string Rolle, int Seite, double X, double Grundlinie, double SeitenBreite, double SeitenHoehe);

/// <summary>
/// Unterschriftsfelder der Vertragsvorlagen. Das Produktmanagement setzt in Word <c>{{unterschrift.Rolle}}</c> an die
/// Stelle, an der unterschrieben wird. Der Kalkulator schreibt dort eine unsichtbare Marke, die die Rolle enthält,
/// und findet sie nach der PDF-Umwandlung wieder. Die Rolle wird als Hex-Text kodiert, damit Umlaute und Leerzeichen
/// die Umwandlung unbeschadet überstehen.
/// </summary>
public static partial class Unterschriftsfelder
{
    private const string Anfang = "PLSIG";
    private const string Ende = "Z";

    [GeneratedRegex(Anfang + "((?:[0-9A-F]{2})+)" + Ende)]
    private static partial Regex Muster();

    /// <summary>Text der Marke für eine Rolle, z. B. <c>PLSIG4B756E6465Z</c> für „Kunde“.</summary>
    public static string Marke(string rolle) => Anfang + Convert.ToHexString(Encoding.UTF8.GetBytes(rolle)) + Ende;

    /// <summary>Alle Marken im PDF in Seitenreihenfolge.</summary>
    public static IReadOnlyList<Unterschriftsfeld> Finde(byte[] pdf)
    {
        var felder = new List<Unterschriftsfeld>();
        using var dokument = PdfDocument.Open(pdf);
        foreach (var seite in dokument.GetPages())
        {
            // Buchstaben in Reihenfolge des Inhalts; die Marke steht dort zusammenhängend.
            var buchstaben = seite.Letters;
            var text = new StringBuilder();
            var index = new List<int>();
            for (var i = 0; i < buchstaben.Count; i++)
            {
                foreach (var _ in buchstaben[i].Value)
                {
                    index.Add(i);
                }

                text.Append(buchstaben[i].Value);
            }

            foreach (Match m in Muster().Matches(text.ToString()))
            {
                string rolle;
                try
                {
                    rolle = Encoding.UTF8.GetString(Convert.FromHexString(m.Groups[1].Value));
                }
                catch (FormatException)
                {
                    continue;
                }

                var erster = buchstaben[index[m.Index]];
                felder.Add(new Unterschriftsfeld(rolle, seite.Number, erster.StartBaseLine.X, erster.StartBaseLine.Y, seite.Width, seite.Height));
            }
        }

        return felder;
    }
}
