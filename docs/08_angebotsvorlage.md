# 08 – Angebotsvorlage (Entwurf v0.1)

> Status: **Entwurf zur Abnahme** (25.09.2026). Es gab bisher keine Vorlage.
> Der Entwurf kann später gegen eine intern entwickelte Vorlage ausgetauscht
> werden. Deshalb ist die Vorlage eine austauschbare Datei und nicht im Code
> „eingebaut“ (Anforderung C-02).
>
> Musterangebot: [`templates/angebot/Musterangebot_Entwurf_v0.1.docx`](../templates/angebot/Musterangebot_Entwurf_v0.1.docx)
> (fiktiver Kunde, Zahlen aus Referenzkalkulation RK-02)

## 1. Gestaltung

Grundlage ist der **Corporate-Design-Styleguide** (SharePoint, Marketing/Booklet).

| Element | Umsetzung |
|---------|-----------|
| Hausfarbe | Nösse Petrol `#006C88`: Überschriften, Tabellenköpfe, Kopfzeile |
| Akzent | Orange `#EE7F00`: Linie unter Überschriften, Aufzählungszeichen |
| Nebentext | Grau `#737373`: Hinweise, Fußzeile |
| Schrift | Verdana (Office-Standardschrift laut Styleguide) |
| Logo | **Platzhalter.** Das Logo liegt im ELO (Marketing › Logo) und muss in die Vorlage eingesetzt werden. Bis dahin steht ein Schriftzug „Nösse“ in Petrol mit Claim „IHR IT-UMSORGER.“ |
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

## 4. Offene Punkte zur Vorlage

- Logo aus ELO bereitstellen
- Abnahme von Aufbau und Texten (Anschreiben, Leistungsbeschreibungen)
- Absenderdaten aus Entra (offene Frage 9.6), Standard-Gültigkeit 30 Tage (offene Frage 9.7)
- Anhang mit vollständigen Leistungsbeschreibungen je Service (wie im AAV-Angebot, Anhang A)? Oder genügt der Verweis auf die Leistungsscheine?
