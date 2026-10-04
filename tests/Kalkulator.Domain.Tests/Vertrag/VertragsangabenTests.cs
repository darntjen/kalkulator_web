using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Domain.Tests.Vertrag;

/// <summary>Vertragsangaben, die Vorlagen über {{eingabe.…}} verlangen (#26, Teil C).</summary>
public class VertragsangabenTests
{
    private static readonly IReadOnlyList<EingabeDefinition> Definitionen =
    [
        new(EingabeArt.Text, "Vertreter", []),
        new(EingabeArt.Auswahl, "Sicherungsvariante", ["Cloud-Backup", "Objektspeicher"]),
        new(EingabeArt.Liste, "Server", ["Servername", "Zweck"]),
    ];

    [Fact]
    public void Ohne_Angaben_fehlt_alles()
    {
        Assert.Equal(["Vertreter", "Sicherungsvariante", "Server"], new Vertragsangaben().Fehlend(Definitionen));
    }

    [Fact]
    public void Leere_Texte_falsche_Optionen_und_leere_Zeilen_zaehlen_als_fehlend()
    {
        var angaben = new Vertragsangaben
        {
            Werte = new Dictionary<string, string> { ["Vertreter"] = "  ", ["Sicherungsvariante"] = "Band" },
            Listen = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["Server"] = [new Dictionary<string, string> { ["Servername"] = " " }],
            },
        };

        Assert.Equal(["Vertreter", "Sicherungsvariante", "Server"], angaben.Fehlend(Definitionen));
    }

    [Fact]
    public void Vollstaendige_Angaben()
    {
        var angaben = new Vertragsangaben
        {
            Werte = new Dictionary<string, string> { ["Vertreter"] = "Frau Beispiel", ["Sicherungsvariante"] = "Objektspeicher" },
            Listen = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["Server"] = [new Dictionary<string, string> { ["Servername"] = "SRV-DC01" }, new Dictionary<string, string>()],
            },
        };

        Assert.Empty(angaben.Fehlend(Definitionen));
        Assert.Single(angaben.Liste("Server"));
        Assert.Equal("Frau Beispiel", angaben.Wert("Vertreter"));
    }

    [Fact]
    public void Gleichnamige_Eingaben_mehrerer_Vorlagen_werden_zusammengefasst()
    {
        var vereint = Vertragsangaben.Vereinige(
        [
            new(EingabeArt.Text, "Vertreter", []),
            new(EingabeArt.Auswahl, "Variante", ["A"]),
            new(EingabeArt.Text, "Vertreter", []),
            new(EingabeArt.Auswahl, "Variante", ["B", "A"]),
            new(EingabeArt.Liste, "Vertreter", ["Name"]),
        ]);

        Assert.Equal(2, vereint.Count);
        Assert.Equal(["A", "B"], vereint[1].Optionen);
        Assert.Equal(EingabeArt.Text, vereint[0].Art);
    }
}
