namespace Kalkulator.Domain.Katalog;

/// <summary>Zuordnung eines Service zu einem Bundle, wie im Bundle-Leistungsschein unter Ziffer 2 aufgeführt.</summary>
public class BundleBestandteil
{
    public int BundleId { get; set; }
    public Service? Bundle { get; set; }

    public int BestandteilId { get; set; }
    public Service? Bestandteil { get; set; }
}
