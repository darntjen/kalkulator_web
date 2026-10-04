using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Vertrag;

namespace Kalkulator.Domain.Tests.Vertrag;

/// <summary>Zuordnung der Dateien im SharePoint-Ordner 03_Vertragswerk zu Vorlagen (#26, Teil B); Namen wie in der Ablage.</summary>
public class VorlagendateinameTests
{
    [Theory]
    [InlineData("Leistungsschein S01 - Noesse Connect V1.0.docx", DokumentTyp.Leistungsschein, "S01", "Noesse Connect", "1.0")]
    [InlineData("Leistungsschein S11 - Endpoint Protection (Server) V1.0.docx", DokumentTyp.Leistungsschein, "S11", "Endpoint Protection (Server)", "1.0")]
    [InlineData("Leistungsschein S61 - Cloud Server-Bereitstellung V2.1.docx", DokumentTyp.Leistungsschein, "S61", "Cloud Server-Bereitstellung", "2.1")]
    [InlineData("Vorlage Leistungsschein S14 V5.7.docx", DokumentTyp.Leistungsschein, "S14", "Leistungsschein S14", "5.7")]
    [InlineData("Vorlage Leistungsschein S14-C Vollcloud V1.0.docx", DokumentTyp.Leistungsschein, "S14-C", "Vollcloud", "1.0")]
    [InlineData("Bundle B05 - Security as a Service Standard V1.0.docx", DokumentTyp.Leistungsschein, "B05", "Bundle B05 - Security as a Service Standard", "1.0")]
    [InlineData("Rahmenvertrag 01 - Grundvertrag V1.0.docx", DokumentTyp.Grundvertrag, "GRUNDVERTRAG", "Grundvertrag", "1.0")]
    [InlineData("Rahmenvertrag 02 - AVB Cloud und Managed Services V1.0.docx", DokumentTyp.Avb, "AVB", "AVB Cloud und Managed Services", "1.0")]
    [InlineData("Rahmenvertrag 03 - Anlage SLA V2.0.docx", DokumentTyp.Sla, "SLA", "Anlage SLA", "2.0")]
    [InlineData("Rahmenvertrag 04 - Auftragsverarbeitungsvereinbarung V1.0.docx", DokumentTyp.Avv, "AVV", "Auftragsverarbeitungsvereinbarung", "1.0")]
    [InlineData("leistungsschein s02 - endpoint protection (user) v1.0.DOCX", DokumentTyp.Leistungsschein, "S02", "endpoint protection (user)", "1.0")]
    public void Dateinamen_der_Ablage_werden_zugeordnet(string name, DokumentTyp typ, string code, string bezeichnung, string version) =>
        Assert.Equal(new VorlagenZuordnung(typ, code, bezeichnung, version), Vorlagendateiname.Zuordnen(name));

    [Theory]
    [InlineData("S14 Baukasten und Preisbausteine V5.7.docx")]
    [InlineData("Leistungsschein S01 - Noesse Connect.docx")]
    [InlineData("Leistungsschein S01 - Noesse Connect V1.0.pdf")]
    [InlineData("Rahmenvertrag 05 - Preisliste V1.0.docx")]
    [InlineData("Bundle S01 - falsch V1.0.docx")]
    [InlineData("NDT_Rahmen_01_Grundvertrag_v2.docx")]
    public void Andere_Dateien_werden_nicht_zugeordnet(string name) => Assert.Null(Vorlagendateiname.Zuordnen(name));

    [Theory]
    [InlineData("Leistungsscheine/Leistungsschein S02 - Endpoint Protection (User) V1.0.docx", false)]
    [InlineData("Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/Archiv/Vorlage Leistungsschein S14 V5.6.docx", true)]
    [InlineData("archiv/Leistungsschein S02 - X V1.0.docx", true)]
    [InlineData("Rahmen/~$hmenvertrag 01 - Grundvertrag V1.0.docx", true)]
    [InlineData("Archivierung/Leistungsschein S02 - X V1.0.docx", false)]
    public void Archiv_und_Word_Sperrdateien_werden_uebersprungen(string pfad, bool ueberspringen) =>
        Assert.Equal(ueberspringen, Vorlagendateiname.Ueberspringen(pfad));
}
