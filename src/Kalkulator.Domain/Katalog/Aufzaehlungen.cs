namespace Kalkulator.Domain.Katalog;

/// <summary>Art eines verkaufbaren Katalogeintrags.</summary>
public enum ServiceTyp
{
    /// <summary>Nösse Connect in einer Stufe (Pflichtbasis, genau eine je Kalkulation).</summary>
    Connect = 1,
    Bundle = 2,
    Einzelservice = 3,
    AddOn = 4,
    /// <summary>Optionen wie das Supportkontingent (S60).</summary>
    Option = 5,
}

public enum Vertriebsstatus
{
    Verkaufsfaehig = 1,
    AufAnfrage = 2,
    Geparkt = 3,
    /// <summary>Zukünftiger Service: darf nicht angeboten werden (Regel R6).</summary>
    Zukuenftig = 4,
}

public enum Einheit
{
    Pauschal = 1,
    Kunde = 2,
    User = 3,
    Server = 4,
    Client = 5,
    Firewall = 6,
    Tenant = 7,
    AdUmgebung = 8,
    Switch = 9,
    AccessPoint = 10,
    Netzwerkgeraet = 11,
    Device = 12,
    Nas = 13,
    Abrechnungseinheit = 14,
    Backupumgebung = 15,
    Paket = 16,
    Terabyte = 17,
    Instanz = 18,
}

public enum Abrechnungsart
{
    Monatlich = 1,
    Einmalig = 2,
}

/// <summary>Größe, nach der eine Preisstaffel gestaffelt ist.</summary>
public enum StaffelBezug
{
    Keine = 0,
    /// <summary>Anzahl User (z. B. Onboarding-Pauschale).</summary>
    User = 1,
    /// <summary>Anzahl Mitarbeitende (z. B. S41 Strategische IT-Begleitung).</summary>
    Mitarbeitende = 2,
}

public enum RegelTyp
{
    /// <summary>Der Service setzt alle Ziel-Services voraus.</summary>
    ErfordertAlle = 1,
    /// <summary>Der Service setzt mindestens einen der Ziel-Services voraus (z. B. S25 → B01–B04).</summary>
    ErfordertEinenVon = 2,
    /// <summary>Der Service darf nicht zusammen mit den Ziel-Services gebucht werden.</summary>
    SchliesstAus = 3,
}

public enum DokumentTyp
{
    Grundvertrag = 1,
    Avb = 2,
    Sla = 3,
    Avv = 4,
    Leistungsschein = 5,
    Angebot = 6,
}
