namespace Kalkulator.Domain.Katalog;

/// <summary>Verkaufbarer Katalogeintrag: Connect-Stufe, Bundle, Einzelservice, Add-on oder Option.</summary>
public class Service
{
    public int Id { get; set; }

    /// <summary>Kurzcode aus dem Vertragswerk, z. B. „S02“, „B01“ oder „S01-PRM“.</summary>
    public required string Code { get; set; }

    /// <summary>Service-ID aus dem Mastersheet, z. B. „NOS-USR-SVC-02-XDR“.</summary>
    public string? ServiceNummer { get; set; }

    public required string Bezeichnung { get; set; }
    public string? Kurzbeschreibung { get; set; }
    public ServiceTyp Typ { get; set; }
    public Vertriebsstatus Vertriebsstatus { get; set; } = Vertriebsstatus.Verkaufsfaehig;
    public int Sortierung { get; set; }

    public int KategorieId { get; set; }
    public ServiceKategorie? Kategorie { get; set; }

    /// <summary>Leistungsschein, der bei Buchung ins Vertragspaket gehört.</summary>
    public int? LeistungsscheinId { get; set; }
    public DokumentVorlage? Leistungsschein { get; set; }

    public List<Preiskomponente> Preiskomponenten { get; } = [];

    /// <summary>Bei Bundles: die enthaltenen Services (auch verschachtelte Bundles, z. B. B02 → B01).</summary>
    public List<BundleBestandteil> Bestandteile { get; } = [];

    public List<ServiceRegel> Regeln { get; } = [];

    /// <summary>
    /// Ob <paramref name="gesucht"/> dieser Service selbst ist oder direkt bzw. über verschachtelte Bundles in ihm steckt.
    /// Dafür müssen die Bestandteile samt ihrer Bestandteile geladen sein.
    /// </summary>
    public bool Enthaelt(Service gesucht)
    {
        var besucht = new HashSet<Service>(ReferenceEqualityComparer.Instance);
        var offen = new Stack<Service>([this]);
        while (offen.TryPop(out var service))
        {
            if (ReferenceEquals(service, gesucht) || (gesucht.Id != 0 && service.Id == gesucht.Id))
            {
                return true;
            }

            if (besucht.Add(service))
            {
                foreach (var b in service.Bestandteile)
                {
                    offen.Push(b.Bestandteil ?? throw new InvalidOperationException($"Ein Bestandteil von „{service.Code}“ ist nicht geladen."));
                }
            }
        }

        return false;
    }

    public bool DarfAngebotenWerden => Vertriebsstatus is not (Vertriebsstatus.Zukuenftig or Vertriebsstatus.Geparkt);
}
