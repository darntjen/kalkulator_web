using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Katalog;

/// <summary>Deutsche Anzeigetexte für die Aufzählungen des Katalogs, gemeinsam für Oberfläche und Excel-Export.</summary>
public static class KatalogTexte
{
    public static string Typ(ServiceTyp typ) => typ switch
    {
        ServiceTyp.Connect => "Nösse Connect",
        ServiceTyp.Bundle => "Bundle",
        ServiceTyp.Einzelservice => "Einzelservice",
        ServiceTyp.AddOn => "Add-on",
        ServiceTyp.Option => "Option",
        _ => typ.ToString(),
    };

    public static string Vertriebsstatus(Vertriebsstatus status) => status switch
    {
        Katalog.Vertriebsstatus.Verkaufsfaehig => "verkaufsfähig",
        Katalog.Vertriebsstatus.AufAnfrage => "auf Anfrage",
        Katalog.Vertriebsstatus.Geparkt => "geparkt",
        Katalog.Vertriebsstatus.Zukuenftig => "zukünftig (nicht anbieten)",
        _ => status.ToString(),
    };

    public static string Einheit(Einheit einheit) => einheit switch
    {
        Katalog.Einheit.Pauschal => "pauschal",
        Katalog.Einheit.Kunde => "je Kunde",
        Katalog.Einheit.AdUmgebung => "je AD-Umgebung",
        Katalog.Einheit.AccessPoint => "je Access Point",
        Katalog.Einheit.Netzwerkgeraet => "je Netzwerkgerät",
        Katalog.Einheit.Nas => "je NAS",
        Katalog.Einheit.Abrechnungseinheit => "je AE",
        Katalog.Einheit.Backupumgebung => "je Backupumgebung",
        Katalog.Einheit.Terabyte => "je TB",
        _ => "je " + einheit,
    };

    public static string Abrechnungsart(Abrechnungsart art) => art == Katalog.Abrechnungsart.Einmalig ? "einmalig" : "monatlich";

    public static string StaffelBezug(StaffelBezug bezug) => bezug switch
    {
        Katalog.StaffelBezug.User => "nach Anzahl User",
        Katalog.StaffelBezug.Mitarbeitende => "nach Anzahl Mitarbeitende",
        _ => "keine Staffel",
    };

    public static string Regel(RegelTyp typ) => typ switch
    {
        RegelTyp.ErfordertAlle => "erfordert alle",
        RegelTyp.ErfordertEinenVon => "erfordert einen von",
        RegelTyp.SchliesstAus => "schließt aus",
        _ => typ.ToString(),
    };

    public static string Dokument(DokumentTyp typ) => typ switch
    {
        DokumentTyp.Grundvertrag => "Grundvertrag",
        DokumentTyp.Avb => "AVB",
        DokumentTyp.Sla => "SLA",
        DokumentTyp.Avv => "AVV",
        DokumentTyp.Leistungsschein => "Leistungsschein",
        DokumentTyp.Angebot => "Angebot",
        _ => typ.ToString(),
    };

    public static string Ampel(Ampel ampel) => ampel switch
    {
        Preise.Ampel.Gruen => "grün",
        Preise.Ampel.Gelb => "gelb",
        Preise.Ampel.Rot => "rot",
        _ => "grau (EK offen)",
    };
}
