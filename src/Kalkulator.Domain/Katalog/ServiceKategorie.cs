namespace Kalkulator.Domain.Katalog;

/// <summary>Gruppierung im Katalog, z. B. „User as a Service“.</summary>
public class ServiceKategorie
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Sortierung { get; set; }
}
