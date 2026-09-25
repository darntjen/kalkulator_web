namespace Kalkulator.Domain.Katalog;

/// <summary>Fachliche Buchungsregel eines Service (Ist-Analyse, Abschnitt 7).</summary>
public class ServiceRegel
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public RegelTyp Typ { get; set; }

    /// <summary>Verständliche Meldung für den Vertrieb, wenn die Regel verletzt ist.</summary>
    public required string Meldung { get; set; }

    public List<ServiceRegelZiel> Ziele { get; } = [];
}

public class ServiceRegelZiel
{
    public int RegelId { get; set; }
    public int ZielServiceId { get; set; }
    public Service? ZielService { get; set; }
}
