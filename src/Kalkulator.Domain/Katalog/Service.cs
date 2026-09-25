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

    public bool DarfAngebotenWerden => Vertriebsstatus is not (Vertriebsstatus.Zukuenftig or Vertriebsstatus.Geparkt);
}
