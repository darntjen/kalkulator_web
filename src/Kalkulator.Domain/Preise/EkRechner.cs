using Kalkulator.Domain.Katalog;

namespace Kalkulator.Domain.Preise;

/// <summary>Interne Kosten je Preiskomponente, auch für Bundles, die sich aus ihren Bestandteilen zusammensetzen.</summary>
public static class EkRechner
{
    /// <summary>
    /// Kosten einer Preiskomponente je Einheit und Monat; <c>null</c>, wenn der EK (auch eines Bestandteils) fehlt.
    /// Bei Bundles zählt je Bestandteil eine Einheit seiner monatlichen Komponente mit derselben Einheit wie das Bundle,
    /// sonst seiner ersten monatlichen Komponente. So zählt bei B06 (pro Kunde) der Grundservice von S25, nicht dessen
    /// Assetpreise, und bei B05 (pro Kunde) je eine Firewall, ein Tenant und eine AD-Umgebung wie in der EK-Kalkulation.
    /// Dafür müssen Service, Bestandteile und deren Preiskomponenten geladen sein.
    /// </summary>
    public static decimal? KostenFuer(this Preisliste preisliste, Preiskomponente komponente)
    {
        var ek = preisliste.EkPositionen.SingleOrDefault(e =>
            ReferenceEquals(e.Preiskomponente, komponente) || (komponente.Id != 0 && e.PreiskomponenteId == komponente.Id));
        if (ek is null)
        {
            return null;
        }

        var satz = preisliste.ParameterWert(ParameterSchluessel.EkKostensatzProStunde);
        if (!ek.AusBestandteilen)
        {
            return ek.Kosten(satz);
        }

        var bundle = komponente.Service
            ?? throw new InvalidOperationException($"Für „{komponente.Code}“ ist der Service nicht geladen.");
        var summe = 0m;
        foreach (var bestandteil in bundle.Bestandteile)
        {
            var service = bestandteil.Bestandteil
                ?? throw new InvalidOperationException($"Ein Bestandteil von „{bundle.Code}“ ist nicht geladen.");
            var monatlich = service.Preiskomponenten
                .Where(p => p.Abrechnungsart == Abrechnungsart.Monatlich)
                .OrderBy(p => p.Sortierung)
                .ToList();
            var passend = monatlich.FirstOrDefault(p => p.Einheit == komponente.Einheit) ?? monatlich.FirstOrDefault();
            var kosten = passend is null ? null : preisliste.KostenFuer(passend);
            if (kosten is null)
            {
                return null;
            }

            summe += kosten.Value;
        }

        return Math.Round(summe + ek.KostenKorrektur, 2, MidpointRounding.AwayFromZero);
    }
}
