namespace Kalkulator.Domain.Katalog;

/// <summary>Word-Vorlage aus dem Vertragswerk; die Datei selbst wird ab Phase 3/4 hochgeladen.</summary>
public class DokumentVorlage
{
    public int Id { get; set; }
    public DokumentTyp Typ { get; set; }

    /// <summary>Code im Vertragswerk, z. B. „S02“, „B01“ oder „AVB“.</summary>
    public required string Code { get; set; }

    public required string Bezeichnung { get; set; }
    public required string Version { get; set; }
    public string? Dateiname { get; set; }
}
