namespace Kalkulator.Documents.Vertrag;

/// <summary>Wo ein Platzhalter stehen darf.</summary>
public enum PlatzhalterArt
{
    /// <summary>Text im Fließtext, in Tabellen, Kopf- oder Fußzeile.</summary>
    Text = 1,

    /// <summary>Block, der je Eintrag wiederholt wird (<c>{{#name}}</c> … <c>{{/name}}</c>).</summary>
    Liste = 2,

    /// <summary>Feld innerhalb einer Liste.</summary>
    Listenfeld = 3,
}

/// <summary>Ein Platzhalter, den der Kalkulator aus seinen Daten füllt; bei Listenfeldern mit der Liste, in der er steht.</summary>
public sealed record Platzhalter(string Schluessel, PlatzhalterArt Art, string Beschreibung, string Beispiel, string? Liste = null);

/// <summary>
/// Platzhalter der Vertragsvorlagen (Grundvertrag, AVB, SLA, Leistungsscheine). Die Liste ist zugleich die Referenz
/// für das Produktmanagement (Katalog → Vorlagen → Platzhalter) und docs/12_vertragsvorlagen.md.
/// </summary>
public static class Vertragsplatzhalter
{
    public const string EingabePraefix = "eingabe.";
    public const string PreisPraefix = "preis.";

    /// <summary>Unterschriftsfeld für Paperless: <c>{{unterschrift.Rolle}}</c> (#26, Teil D).</summary>
    public const string UnterschriftPraefix = "unterschrift.";

    /// <summary>Felder je gebuchter Preiskomponente: <c>{{preis.KOMPONENTE.menge}}</c> usw.</summary>
    public static readonly IReadOnlyList<string> PreisFelder = ["menge", "einzelpreis", "summe"];

    public static readonly IReadOnlyList<Platzhalter> Alle =
    [
        new("kunde.firma", PlatzhalterArt.Text, "Firma des Kunden", "Muster Spedition GmbH"),
        new("kunde.strasse", PlatzhalterArt.Text, "Straße und Hausnummer", "Hafenstraße 12"),
        new("kunde.plz", PlatzhalterArt.Text, "Postleitzahl", "26135"),
        new("kunde.ort", PlatzhalterArt.Text, "Ort", "Oldenburg"),
        new("kunde.anschrift", PlatzhalterArt.Text, "Firma und Anschrift, mehrzeilig", "Muster Spedition GmbH\nHafenstraße 12\n26135 Oldenburg"),
        new("kunde.ansprechpartner", PlatzhalterArt.Text, "Ansprechpartner des Kunden", "Frau Beispiel"),
        new("kunde.navision", PlatzhalterArt.Text, "Navision-Kundennummer", "K10042"),
        new("vertrag.nummer", PlatzhalterArt.Text, "Vertragsnummer", "MS-V-2026-0001"),
        new("vertrag.datum", PlatzhalterArt.Text, "Datum der Erstellung", "04.10.2026"),
        new("vertrag.beginn", PlatzhalterArt.Text, "Vertragsbeginn aus der Kalkulation", "01.01.2027"),
        new("vertrag.angebot", PlatzhalterArt.Text, "Angenommenes Angebot mit Version", "MS-A-2026-0001 V2"),
        new("vertrag.connectstufe", PlatzhalterArt.Text, "Connect-Stufe (auch SLA-Stufe)", "Standard"),
        new("summe.monatlich", PlatzhalterArt.Text, "Gesamtbetrag netto monatlich", "1.391,80 €"),
        new("summe.einmalig", PlatzhalterArt.Text, "Einmalige Beträge netto", "900,00 €"),
        new("positionen", PlatzhalterArt.Liste, "Monatliche Positionen des Vertrags (Vergütungsübersicht), je Service eine Zeile", ""),
        new("einmalig", PlatzhalterArt.Liste, "Einmalige Positionen, z. B. Onboarding-Pauschale; Felder wie bei den Positionen", ""),
        new("anlagen", PlatzhalterArt.Liste, "Alle Dokumente des Vertragswerks in Rangfolge", ""),
        new("schein.code", PlatzhalterArt.Text, "Code des Leistungsscheins", "S14"),
        new("schein.bezeichnung", PlatzhalterArt.Text, "Bezeichnung des Leistungsscheins", "Server Backup"),
        new("schein.summe", PlatzhalterArt.Text, "Monatliche Summe der Positionen dieses Leistungsscheins", "438,00 €"),
        new("schein.positionen", PlatzhalterArt.Liste, "Positionen dieses Leistungsscheins", ""),
        new("position.code", PlatzhalterArt.Listenfeld, "Leistungsschein- bzw. Komponentencode", "B05", "positionen"),
        new("position.bezeichnung", PlatzhalterArt.Listenfeld, "Bezeichnung", "Security as a Service Standard", "positionen"),
        new("position.menge", PlatzhalterArt.Listenfeld, "Menge", "4", "positionen"),
        new("position.einheit", PlatzhalterArt.Listenfeld, "Einheit", "User", "positionen"),
        new("position.einzelpreis", PlatzhalterArt.Listenfeld, "Einzelpreis netto", "199,90 €", "positionen"),
        new("position.gesamtpreis", PlatzhalterArt.Listenfeld, "Gesamtpreis netto", "799,60 €", "positionen"),
        new("position.abrechnung", PlatzhalterArt.Listenfeld, "„monatlich“ oder „einmalig“", "monatlich", "positionen"),
        new("anlage.code", PlatzhalterArt.Listenfeld, "Code des Dokuments", "S01", "anlagen"),
        new("anlage.bezeichnung", PlatzhalterArt.Listenfeld, "Bezeichnung des Dokuments", "Nösse Connect", "anlagen"),
        new("anlage.art", PlatzhalterArt.Listenfeld, "Art (Anlage, Bundle, Leistungsschein)", "Leistungsschein", "anlagen"),
    ];

    /// <summary>Platzhalter, ohne die ein Grundvertrag nicht verwendet werden kann; je Eintrag genügt eine der Alternativen.</summary>
    public static readonly IReadOnlyList<string[]> PflichtImGrundvertrag =
        [["kunde.firma", "kunde.anschrift"], ["vertrag.nummer"], ["vertrag.beginn"], ["positionen"], ["summe.monatlich"]];

    /// <summary>Listen, in denen ein Listenfeld stehen darf (<c>position.*</c> auch in <c>schein.positionen</c>).</summary>
    public static IEnumerable<string> ListenFuer(Platzhalter feld) =>
        feld.Liste == "positionen" ? ["positionen", "einmalig", "schein.positionen"] : [feld.Liste!];
}
