# 08 – Angebotsvorlage (Entwurf v0.1)

> Status: **Abgenommen als Arbeitsgrundlage** (25.09.2026). Es gab bisher keine Vorlage.
> Der Entwurf kann später gegen eine intern entwickelte Vorlage ausgetauscht
> werden. Deshalb ist die Vorlage eine austauschbare Datei und nicht im Code
> „eingebaut“ (Anforderung C-02).
>
> Musterangebot: [`templates/angebot/Musterangebot_Entwurf_v0.1.docx`](../templates/angebot/Musterangebot_Entwurf_v0.1.docx)
> (fiktiver Kunde, Zahlen aus Referenzkalkulation RK-02)
>
> **Umgesetzt (02.10.2026):** Aus dem Muster ist die Vorlage
> [`templates/angebot/Angebotsvorlage.docx`](../templates/angebot/Angebotsvorlage.docx) mit Platzhaltern entstanden,
> dazu [`textbausteine.json`](../templates/angebot/textbausteine.json) mit Leistungstexten und SLA-Werten
> (Anlage SLA V2.0). Technik und Regeln für Platzhalter: [ADR-0004](adr/0004-word-erzeugung.md).

## 1. Gestaltung

Grundlage ist der **Corporate-Design-Styleguide** (SharePoint, Marketing/Booklet).

| Element | Umsetzung |
|---------|-----------|
| Hausfarbe | Nösse Petrol `#006C88`: Überschriften, Tabellenköpfe, Kopfzeile |
| Akzent | Orange `#EE7F00`: Linie unter Überschriften, Aufzählungszeichen |
| Nebentext | Grau `#737373`: Hinweise, Fußzeile |
| Schrift | Verdana (Office-Standardschrift laut Styleguide) |
| Logo | Nösse-Logo mit Claim „IHR IT-UMSORGER.“ (`templates/assets/noesse-logo.jpg`, aus dem Intranet-Wiki, 292 × 123 px). Auf dem Deckblatt groß, in der Kopfzeile klein. Eine höher aufgelöste Fassung wäre für den Druck besser (offene Frage 10.3) |
| Format | A4, Ränder 2 cm, Kopfzeile mit Angebotsnummer und Kunde, Fußzeile mit Firmenangaben und „Seite X von Y“ |

## 2. Aufbau

Der Aufbau orientiert sich an bestehenden Nösse-Angeboten (z. B. Angebot AAV):
zuerst das Wichtigste, dann die Details, zuletzt Preise und Rahmen.

| Nr. | Abschnitt | Inhalt | Quelle der Daten |
|-----|-----------|--------|------------------|
| – | Deckblatt | Angebotsnummer, Version, Datum, Gültigkeit, Ansprechpartner | Kalkulation, Entra-Profil |
| – | Anschreiben | Anschrift, Anrede, Standardtext, **optionaler Freitext** (Ausgangssituation) | Kunde, Vertrieb |
| 1 | Das Wichtigste auf einen Blick | Servicebasis, Umfang, laufende Kosten, Einmalkosten, Servicebeginn, Laufzeit | Rechenkern |
| 2 | Ihre Servicebasis: Nösse Connect | Beschreibung und SLA-Tabelle der **gewählten Stufe** | Katalog, Anlage SLA |
| 3 | Die angebotenen Leistungen | Je Bundle/Service: Titel, Preis, Kurzbeschreibung, enthaltene S-Scheine, Ausschlüsse | Katalog (Textbausteine) |
| 4 | Preisübersicht | Positionstabelle, Summe monatlich, Einmalkosten, Kennzahlen (Jahreswert, Erstlaufzeit, Bundle-Vorteil) | Rechenkern |
| 5 | Vertraglicher Rahmen | Laufzeit, Abrechnung, Service Requests (AE-Sätze), Mengenanpassung, **Liste der Vertragsbestandteile** | Parameter, Vertragspaket-Auflösung |
| 6 | Gültigkeit und nächste Schritte | Gültigkeitsdatum, Servicebeginn, Grußformel | Kalkulation |

Optionale Abschnitte, nur wenn gebucht: Server-Backup-Details (S14: Variante,
Server, Datenmenge), Cloud-Server-Übersicht (S61), Vorher/Nachher-Vergleich für
Bestandskunden, Sonderpositionen.

## 3. Platzhalter (für die spätere Vorlagendatei)

Die Vorlage wird in Word gepflegt. Variable Stellen sind Platzhalter bzw.
Inhaltssteuerelemente mit festen Namen. Wiederholte Bereiche (Positionen,
Leistungen, Vertragsbestandteile) sind als Blöcke markiert, die je Eintrag
wiederholt werden.

| Platzhalter | Bedeutung |
|-------------|-----------|
| `angebot.nummer`, `angebot.version`, `angebot.datum`, `angebot.gueltig_bis` | Angebotsdaten |
| `kunde.firma`, `kunde.strasse`, `kunde.plz_ort`, `kunde.ansprechpartner`, `kunde.anrede` | Kundendaten |
| `absender.name`, `absender.funktion`, `absender.telefon`, `absender.email` | aus Entra-Profil |
| `freitext.ausgangssituation` | optionaler Text des Vertriebs |
| `connect.stufe`, `connect.sla.*` | gewählte Stufe und Service Level |
| `servicebeginn`, `laufzeit` | Vertragsdaten |
| Block `leistungen[]`: `code`, `titel`, `preistext`, `beschreibung`, `enthalten[]`, `nicht_enthalten` | Leistungsbeschreibungen |
| Block `positionen[]`: `code`, `bezeichnung`, `einheit`, `menge`, `einzelpreis`, `betrag` | Preistabelle |
| `summe.monatlich`, `summe.einmalig`, `kennzahl.jahreswert`, `kennzahl.erstlaufzeit`, `kennzahl.bundlevorteil` | Summen |
| Block `vertragsbestandteile[]` | aufgelöste Dokumentliste |

### 3.1 Platzhalter der Vorlage (Stand Umsetzung)

Einfache Werte: `angebot.nummer`, `angebot.version`, `angebot.datum`, `angebot.gueltig_bis`, `kunde.firma`,
`kunde.ansprechpartner`, `kunde.strasse`, `kunde.plz_ort`, `kunde.anrede`, `absender.name`, `absender.funktion`,
`absender.telefon`, `absender.email`, `freitext.ausgangssituation`, `servicebeginn`, `servicebeginn.text`,
`summe.monatlich`, `summe.einmalig`, `einmalig.kurz`, `kennzahl.jahreswert`, `kennzahl.erstlaufzeit`,
`satz.ebene1` bis `satz.ebene3` (AE-Sätze aus der Preisliste), `connect.name`, `connect.stufe`, `connect.kurztext`,
`umfang`, `vergleich.bisher`, `vergleich.differenz`.

Blöcke: `connect.vorhanden`, `sla` (`parameter`, `wert`), `leistungen` (`titel`, `preistext`, `beschreibung`,
`hat_enthalten`, `enthalten` mit `text`, `nicht_enthalten`), `positionen` (`code`, `bezeichnung`, `einheit`, `menge`,
`einzelpreis`, `betrag`), `hat_einmalig`, `einmalig` (`bezeichnung`, `betrag`, `hinweis`), `bundlevorteil`
(`bundles`, `betrag`), `vergleich.vorhanden`, `vertragsbestandteile` (`text`).

Sonderpositionen erscheinen in der Preistabelle mit dem Kürzel „SP“ und dem Zusatz „(Sonderposition)“ (B-21).

## 4. Offene Punkte zur Vorlage

- Logo in höherer Auflösung (optional)
- Abnahme von Aufbau und Texten (Anschreiben, Leistungsbeschreibungen)
- Absenderdaten aus Entra (Frage 9.6): bis #4 stehen Anmeldename, Funktion und Telefon aus der Konfiguration (`Angebot:AbsenderFunktion`, `Angebot:AbsenderTelefon`) im Angebot. Standard-Gültigkeit 30 Tage (Frage 9.7) ist umgesetzt und änderbar
- Leistungstexte: Für B02, B03, B05, S31, S32 und S60 stammen sie aus dem Musterangebot, sonst gilt die Kurzbeschreibung aus dem Katalog. Weitere Texte in `textbausteine.json` ergänzen
- Anhang mit vollständigen Leistungsbeschreibungen je Service (wie im AAV-Angebot, Anhang A)? Oder genügt der Verweis auf die Leistungsscheine?
