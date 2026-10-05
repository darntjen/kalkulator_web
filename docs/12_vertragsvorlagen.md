# Vertragsvorlagen aus SharePoint

Stand: 04.10.2026 · Issue #26, Teile B, C und D

Die Word-Vorlagen des Vertragswerks liegen zentral in SharePoint. Dazu gehören Grundvertrag, AVB, SLA, AVV,
Leistungsscheine und Bundle-Scheine. Der Kalkulator übernimmt neue Fassungen von dort automatisch, prüft sie und
verwendet sie erst, wenn das Produktmanagement sie freigegeben hat. Aus den freigegebenen Fassungen entsteht in
Teil C das Vertragswerk; Teil D übergibt es mit Unterschriftsfeldern an Paperless (Abschnitt 7).

## 1. Ablage und Dateinamen

Website „Service-Katalog“, Bibliothek „Freigegebene Dokumente“, Ordner
`Allgemein/Nösse MSP Servicekatalog/03_Vertragswerk (EXTERN)`. Unterordner werden mitgelesen, Ordner mit dem Namen
`Archiv` nicht.

Der Kalkulator ordnet eine Datei über ihren Namen zu:

| Dateiname (Beispiel) | Vorlage |
|---|---|
| `Leistungsschein S01 - Noesse Connect V1.0.docx` | S01 |
| `Vorlage Leistungsschein S14 V5.7.docx` | S14 |
| `Bundle B01 - User as a Service Standard V1.0.docx` | B01 |
| `Rahmenvertrag 01 - Grundvertrag V1.0.docx` | Grundvertrag (am Wort „Grundvertrag“ erkannt) |
| `Rahmenvertrag 02 - AVB … V1.0.docx`, `Rahmenvertrag 03 - Anlage SLA V2.0.docx` | AVB, SLA |
| `Rahmenvertrag 04 - AVV V1.0.docx` (Name noch offen, siehe Frage 12.1) | AVV, am Wort „AVV“ oder „Auftragsverarbeitung“ erkannt |

Dateien mit anderem Namensmuster, z. B. „S14 Baukasten und Preisbausteine“, stehen im Abgleichbericht unter
„Nicht zugeordnet“. Taucht ein neuer Code auf, z. B. `Leistungsschein S70 - … V1.0.docx`, legt der Abgleich die
Vorlage selbst an. Den Service ordnet man danach im Katalog zu.

Die Versionsangabe im Dateinamen ist nur ein Hinweis. Eine neue Fassung erkennt der Kalkulator am Inhalt, denn in der
Ablage werden Dateien auch geändert, ohne dass sich der Name ändert: S01 wurde im September geändert und heißt
weiterhin V1.0. Jede Fassung bekommt deshalb eine eigene laufende Nummer (V1, V2 …).

## 2. Ablauf

1. **Abgleich:** jede Nacht um 02:30 Uhr und auf Knopfdruck (Katalog › Vertragsvorlagen › „Jetzt abgleichen“).
   Neue oder geänderte Dateien werden geladen und geprüft. Sie liegen dann als Fassung „zur Prüfung“ vor.
2. **Prüfung:** Jeder Platzhalter muss bekannt sein und jede Liste geschlossen. Beim Grundvertrag sind außerdem die
   Pflicht-Platzhalter nötig (Abschnitt 3). Text in eckigen Klammern und Linien `_____` meldet die Prüfung als
   Hinweis, weil dort oft ein Platzhalter vergessen wurde.
3. **Freigabe:** Das Produktmanagement lädt die Fassung bei Bedarf herunter, sieht Prüfergebnis und Eingaben und
   aktiviert die Fassung oder lehnt sie mit Begründung ab. Eine Fassung mit Fehlern lässt sich nicht aktivieren.
   Eine abgelöste Fassung lässt sich wieder aktivieren, um zur Vorversion zurückzukehren.
4. **Verwendung:** Neue Vertragswerke nutzen immer die aktive Fassung. Fassungen werden nie verändert oder gelöscht.
   So lässt sich zu jedem Vertrag nachweisen, auf welcher Fassung er beruht.

Kommt eine neuere Datei, während eine ältere noch zur Prüfung liegt, wird die ältere als „abgelöst“ markiert. Die
aktive Fassung bleibt aktiv, bis eine neue freigegeben ist.

## 3. Vertragsangaben und Vertragswerk (Teil C)

**Vertragsangaben in der Kalkulation** (Entscheidung 04.10.2026):
- Der Editor zeigt im Bereich „Vertragsangaben“ genau die Eingaben, die die aktiven Vorlagen für die gebuchten
  Leistungen verlangen. Namen gelten über alle Vorlagen hinweg: Fragen zwei Vorlagen nach „Vertreter“, wird das
  einmal erfasst.
- Ein Angebot entsteht erst, wenn alle Angaben vollständig sind. Die Angaben werden mit dem Angebot eingefroren.
- Änderungen an den Angaben heben die Vertriebsfreigaben auf, weil der Solution Consultant auch diese Angaben prüft.

**Vertragswerk im Kundenprojekt:**
- Nach „Gewonnen“ zeigt die Projektansicht den Bereich „Vertragswerk“: die Dokumente in der Rangfolge nach § 1 Abs. 3
  Grundvertrag mit der jeweils aktiven Fassung und gegebenenfalls, was noch fehlt.
- Was zum Erzeugen vorliegen muss:
  - Das angenommene Angebot ist mit beiden Vertriebsfreigaben entstanden.
  - Jedes Dokument hat eine aktive Fassung; fehlt eine, ist die Erzeugung gesperrt (Entscheidung 04.10.2026).
  - Alle Angaben, die die aktiven Fassungen verlangen, stehen im Angebot. Verlangt eine Vorlage nach dem Angebot
    eine neue Angabe, ist ein neues Angebot nötig.
  - Die PDF-Umwandlung ist eingerichtet.
- „Vertragswerk erzeugen“ befüllt jedes Dokument, wandelt es in PDF um und erstellt:
  - die **Gesamtdatei für den Kunden**: Deckblatt mit Vertragsdaten und Verzeichnis
    (`templates/vertrag/Deckblatt.docx`), danach alle Dokumente;
  - ein **ZIP** mit der Gesamtdatei und allen Einzel-PDFs (`Einzeldokumente/01 …`).
- **Vertragsnummer** ist die Angebotsnummer (Entscheidung 04.10.2026).
- **Archiv:** Jede Erzeugung ist eine unveränderliche Ausfertigung (1, 2 …). Festgehalten wird, welche
  Vorlagenfassung je Dokument verwendet wurde.

**PDF-Umwandlung** (Konfiguration `Pdf`):
- `Pdf:Wandler` = `Graph`: Umwandlung über Microsoft 365. Die Datei wird in den Arbeitsordner `Pdf:Ordnerpfad` der
  Vorlagen-Website hochgeladen, als PDF abgerufen und wieder gelöscht. Dafür braucht die App **Schreibrecht** auf die
  Website (Sites.Selected, Rolle `write`).
- `Pdf:Wandler` = `LibreOffice`: für Entwicklung und Test (`Pdf:LibreOffice` = Programmpfad).
- Ohne Eintrag lässt sich kein Vertragswerk erzeugen; die Projektansicht nennt den Grund.

## 4. Platzhalter

Die vollständige, immer aktuelle Liste steht im Kalkulator unter Katalog › Vertragsvorlagen › „Platzhalter für
Vertragsvorlagen“. Die Grundregeln:

- **Text:** `{{kunde.firma}}`. Die Formatierung am Platzhalter gilt für den eingesetzten Wert.
- **Positionen:** `{{#positionen}}` sind die monatlichen Positionen (Vergütungsübersicht), `{{#einmalig}}` die
  einmaligen, z. B. die Onboarding-Pauschale. Beide mit denselben Feldern `{{position.…}}`.
- **Listen:** Ein Absatz, der nur `{{#positionen}}` enthält, bis zu einem Absatz `{{/positionen}}` wird je Eintrag
  wiederholt. In Tabellen beginnt die erste Zelle einer Zeile mit `{{#positionen}}` und die letzte Zelle endet mit
  `{{/positionen}}`.
- **Preise einer Komponente:** `{{preis.S14-SERVER.menge}}`, `.einzelpreis`, `.summe`. Ein Block
  `{{#preis.S14-SERVER}} … {{/preis.S14-SERVER}}` erscheint nur, wenn die Komponente gebucht ist. Damit entfallen nicht
  vereinbarte Preiszeilen von selbst.
- **Eingaben** für alles, was der Kalkulator nicht kennt. Sie werden beim Erstellen des Vertragswerks abgefragt
  (Entscheidung 04.10.2026):
  - `{{eingabe.Vertreter}}`: Freitext
  - `{{eingabe.Sicherungsvariante=Cloud-Backup}}`: Kästchen ☒ oder ☐; alle Optionen einer Gruppe bilden die
    Auswahl. `{{eingabe.Sicherungsvariante}}` setzt die gewählte Option als Text ein.
  - `{{#eingabe.Server}}` mit `{{Server.Servername}}`, `{{Server.Zweck}}` … `{{/eingabe.Server}}`: Tabelle mit
    beliebig vielen Zeilen
  - Namen dürfen Leerzeichen und Umlaute enthalten, aber keinen Punkt.
- **Unterschriftsfelder:** `{{unterschrift.Kunde}}` an der Stelle, an der unterschrieben wird (Abschnitt 7). Im
  Dokument bleibt die Stelle unsichtbar.

Pflicht im Grundvertrag: `{{kunde.firma}}` oder `{{kunde.anschrift}}`, `{{vertrag.nummer}}`, `{{vertrag.beginn}}`,
die Liste `{{#positionen}}` und `{{summe.monatlich}}`.

## 5. Markierungsanleitung für die heutigen Vorlagen

Die Fassungen in SharePoint (Stand 10/2026) kennzeichnen variable Stellen noch mit `[…]` oder `____`. Die Prüfung
meldet sie als Hinweis, beim Grundvertrag als Fehler, weil die Pflicht-Platzhalter fehlen. So werden sie umgestellt:

### Grundvertrag (Rahmenvertrag 01)

| Stelle heute | ersetzen durch |
|---|---|
| `[Firma und Anschrift des Kunden]` | `{{kunde.anschrift}}` |
| `[Vertragsnummer]` | `{{vertrag.nummer}}` |
| § 2 Abs. 1 `[Vertragsbeginn]` | `{{vertrag.beginn}}` |
| § 1 Abs. 2 „in der … vereinbarten Stufe (Standard, Premium oder Enterprise)“ | optional „in der Stufe `{{vertrag.connectstufe}}`“ |
| § 3 Tabelle: die vier Beispielzeilen (S01, B05, S34, B03) | eine Zeile: 1. Zelle `{{#positionen}}{{position.code}}`, dann `{{position.bezeichnung}}`, `{{position.menge}}`, `{{position.einzelpreis}}`, letzte Zelle `{{position.gesamtpreis}}{{/positionen}}` |
| § 3 „Gesamtbetrag (netto, monatlich)“ 1.391,80 € | `{{summe.monatlich}}` |
| § 3 einmalige Leistungen (neu, falls gewünscht) | eigene Tabellenzeile `{{#einmalig}}{{position.code}}` … `{{position.gesamtpreis}}{{/einmalig}}`, Summe `{{summe.einmalig}}` |
| § 6 die beiden Zeilen `[ggf. weitere Bundle-Leistungsscheine …]` und `[ggf. weitere Einzel-Leistungsscheine …]` samt der festen Anlagen davor | Absatz `{{#anlagen}}`, Aufzählungspunkt `{{anlage.code}} — {{anlage.bezeichnung}}`, Absatz `{{/anlagen}}` |
| „Ort, Datum: ____“ | „Ort, Datum: `{{vertrag.datum}}`“ oder unverändert |
| „Unterschrift: ____“ beim Auftraggeber bzw. bei Nösse | `{{unterschrift.Kunde}}____` bzw. `{{unterschrift.Nösse}}____` am Anfang der Linie (Abschnitt 7) |

### Leistungsschein S14 (Vorlage V5.7)

| Stelle heute | ersetzen durch |
|---|---|
| Kopf „Stand: ____“ | `{{vertrag.datum}}` |
| Auftraggeber, Vertragsnummer, Gültig ab | `{{kunde.firma}}`, `{{vertrag.nummer}}`, `{{vertrag.beginn}}` |
| Standort, Freigabe Wiederherstellung, Vertreter | `{{eingabe.Standort}}`, `{{eingabe.Freigabe Wiederherstellung}}`, `{{eingabe.Vertreter}}` |
| 1.1 Kästchen „☐ Cloud-Backup“, „☐ Objektspeicher“ | `{{eingabe.Sicherungsvariante=Cloud-Backup}}`, `{{eingabe.Sicherungsvariante=Objektspeicher}}` |
| 2.2 RPO-Zellen `____` | `{{eingabe.RPO lokal}}`, `{{eingabe.RPO Cloud}}` |
| 6.1 Preiszeilen | je Zeile Block und Felder der Komponente: Grundpauschale `S14-GRUND`, gesicherter Server `S14-SERVER`, Cloud-Paket `S14-PAKET`, Objektspeicher `S14-OBJEKT`, Lizenz `S14-LIZENZ`, z. B. 1. Zelle `{{#preis.S14-SERVER}}Gesicherter Server`, Menge `{{preis.S14-SERVER.menge}}`, letzte Zelle `{{preis.S14-SERVER.summe}}{{/preis.S14-SERVER}}`. Die leere Zusatzzeile entfällt |
| 6.1 „Monatlicher Gesamtpreis netto“ | `{{schein.summe}}` |
| 6.2 Sicherungsvariante, Anzahl gesicherter Server | `{{eingabe.Sicherungsvariante}}`, `{{preis.S14-SERVER.menge}}` |
| 6.2 Datenmenge, Sicherungsziele, Frequenz, Wiederherstellungspunkte, Aufbewahrung, Object Lock, Backup-Software, Wiederanlauf, Wechselmedien | je `{{eingabe.…}}` mit sprechendem Namen, z. B. `{{eingabe.Datenmenge TB}}` |
| 6.2 Tabelle „Erfasste Server“ (sechs leere Zeilen) | eine Zeile: `{{#eingabe.Server}}{{Server.Servername}}`, `{{Server.Zweck}}`, `{{Server.Betriebssystem}}`, `{{Server.Priorität}}{{/eingabe.Server}}` |
| „Leverkusen, [Datum]“ | „Leverkusen, `{{vertrag.datum}}`“ |

Leistungsscheine ohne variable Stellen (z. B. S02) bleiben unverändert. Weitere Stellen mit `____` oder `[…]` zeigt
die Prüfung nach dem ersten Abgleich je Vorlage an.

## 6. Einrichtung (IT)

1. **App-Registrierung** in Entra ID für den Kalkulator, Anwendungsberechtigung **Microsoft Graph `Sites.Selected`**,
   Administratorzustimmung erteilen.
2. **Zugriff auf die Website** „Service-Katalog“ erteilen: Rolle `write`, weil die PDF-Umwandlung Dateien im
   Arbeitsordner `Pdf:Ordnerpfad` ablegt und wieder löscht. Nur für den Abgleich genügt `read`. Das geht per Graph
   (`POST /sites/{site-id}/permissions`) oder PnP PowerShell (`Grant-PnPAzureADAppSitePermission`).
3. **Anmeldung der App:** ein Zertifikat (empfohlen) oder ein Client-Secret. Beides gehört nicht ins Repository,
   sondern in die Konfiguration auf dem Server (Umgebungsvariablen oder geschützte appsettings).
4. **Konfiguration** (Abschnitt `Vorlagen`, Doppelpunkt bzw. `__` als Trenner in Umgebungsvariablen):

   | Schlüssel | Wert |
   |---|---|
   | `Vorlagen:Quelle` | `SharePoint` (oder `Ordner` zum Testen, leer = aus) |
   | `Vorlagen:AbgleichUm` | `02:30` (leer = kein nächtlicher Abgleich) |
   | `Vorlagen:SharePoint:TenantId`, `ClientId` | aus der App-Registrierung |
   | `Vorlagen:SharePoint:ZertifikatPfad`, `ZertifikatKennwort` oder `ClientSecret` | Anmeldung |
   | `Vorlagen:SharePoint:Website` | `https://noessedatentechnik.sharepoint.com/sites/Service-Katalog` |
   | `Vorlagen:SharePoint:Bibliothek` | leer = Standardbibliothek |
   | `Vorlagen:SharePoint:Ordnerpfad` | `Allgemein/Nösse MSP Servicekatalog/03_Vertragswerk (EXTERN)` |
   | `Vorlagen:Ordner` | nur bei Quelle `Ordner`: lokaler Ordner mit derselben Struktur |
   | `Pdf:Wandler` | `Graph` (oder `LibreOffice` zum Testen, leer = keine Vertragswerke) |
   | `Pdf:Ordnerpfad` | Arbeitsordner für die Umwandlung, z. B. `Kalkulator/PDF-Umwandlung` |
   | `Paperless:…` | Übergabe an Paperless, siehe Abschnitt 7 |

Ist die Konfiguration unvollständig, startet der Kalkulator trotzdem. Der Abgleich meldet dann, was fehlt.

## 7. Übergabe an Paperless (Teil D)

Die kaufmännische Freigabe (Vertriebsleitung und Solution Consultant) liegt im Kalkulator, die technische Freigabe in
Paperless. Paperless schickt das Vertragswerk danach zur Unterschrift an den Kunden.

**Ablauf** (Entscheidungen 04.10.2026):
- Die Übergabe geschieht **automatisch beim Erzeugen** des Vertragswerks, sobald Paperless eingerichtet ist.
  Voraussetzungen wie bisher: beide Vertriebsfreigaben und das Projekt mit angenommenem Angebot auf „Gewonnen“.
- Übergeben wird die **Gesamtdatei** (Deckblatt und alle Dokumente) mit den Unterschriftsfeldern.
- **Unterschriftsfelder aus den Vorlagen:** Das Produktmanagement setzt in Word `{{unterschrift.Rolle}}` an die
  Unterschriftslinie, z. B. `{{unterschrift.Kunde}}` beim Auftraggeber. Der Kalkulator schreibt dort eine
  unsichtbare Marke (weiß, 1 pt), findet sie nach der PDF-Umwandlung wieder und gibt Paperless je Marke ein
  Unterschriftsfeld mit Seite und Lage. Die linke untere Ecke des Felds liegt auf der Marke; der Platzhalter gehört
  deshalb an den Anfang der Unterschriftslinie.
- **Rollen und Personen:** Die Rolle ist der Slot in Paperless, sofern `Paperless:Rollen` nichts anderes sagt.
  Rollen, die nicht fest eingestellt sind (in der Regel „Kunde“), fragt die Projektansicht vor dem Erzeugen ab:
  Name und E-Mail-Adresse. Für „Kunde“ ist der Ansprechpartner vorbelegt.
- **Paperless-Vorlage regelt den Ablauf:** Mit `Paperless:VorlageId` wird die Paperless-Vorlage mitgegeben. Rollen
  mit `AusVorlage = true` (z. B. die Geschäftsführung) bekommen nur das Feld; die Person legt die Vorlage fest.
- **Entwurf oder Versand:** Standard ist ein Entwurf in Paperless, den die technische Freigabe prüft und von dort
  versendet. Mit `Paperless:Versenden = true` geht das Dokument sofort in den Ablauf (state `dispatched`).
- **Keine Rückmeldung:** Paperless meldet den Unterschriftsstatus nicht an den Kalkulator zurück.
- **Fehler:** Schlägt die Übergabe fehl (Paperless nicht erreichbar, Feld im PDF nicht gefunden …), bleibt das
  Vertragswerk gespeichert. Die Projektansicht zeigt den Fehler und bietet „Erneut übergeben“. Jede Ausfertigung geht
  höchstens einmal an Paperless.

**Einrichtung bei Nösse** (Entscheidungen 05.10.2026):
- Arbeitsbereich **15114**, Paperless-Vorlage **50379**. Die Vorlage enthält kein Dokument; sie regelt Slots,
  Reihenfolge und die Freigaben vor dem Versand. Das Dokument kommt immer in der aktuellen Fassung aus dem Kalkulator.
- Slots **„Kunde“** und **„Nösse“**, nacheinander: erst unterschreibt der Kunde, dann Nösse.
- **Kunde:** Name und E-Mail kommen je Vertrag aus dem Kalkulator (vorbelegt mit dem Ansprechpartner).
- **Nösse:** Es unterschreibt je Vertrag die Geschäftsführung (André Nösse oder Matthias Erhard) oder der technische
  Leiter (Sascha Manczak). Wer, legt die Person fest, die das Dokument in Paperless freigibt; der Kalkulator setzt nur
  das Feld (`Paperless:Rollen:Nösse:AusVorlage = true`).
- Der Kalkulator übergibt nur Entwürfe (`Versenden = false`). In den Vertragsvorlagen stehen dafür
  `{{unterschrift.Kunde}}` und `{{unterschrift.Nösse}}` am Anfang der Unterschriftslinie (Frage 12.6).
- Offen bis zum Test mit echtem Schlüssel (Frage 12.5): ob Paperless die Vorlage zusammen mit einer eigenen PDF-Datei
  anwendet und ob sich die Person für „Nösse“ auch erst nach der Unterschrift des Kunden zuordnen lässt.

**Konfiguration** (Abschnitt `Paperless`; der Schlüssel gehört nicht ins Repository):

| Schlüssel | Wert |
|---|---|
| `Paperless:ApiSchluessel` | API-Schlüssel aus Paperless (Umgebungsvariable `Paperless__ApiSchluessel`); leer = Übergabe aus |
| `Paperless:ArbeitsbereichId` | Arbeitsbereich (workspace_id); leer = Übergabe aus |
| `Paperless:Adresse` | `https://api.paperless.io/api/v1/` |
| `Paperless:VorlageId` | optionale Paperless-Vorlage (template_id) |
| `Paperless:Versenden` | `false` = Entwurf für die technische Freigabe, `true` = sofort versenden |
| `Paperless:Rollen:<Rolle>:Slot` | Slot-Name in Paperless, falls er vom Rollennamen abweicht |
| `Paperless:Rollen:<Rolle>:AusVorlage` | `true`: Die Person legt die Paperless-Vorlage fest |
| `Paperless:Rollen:<Rolle>:Name`, `EMail` | fest eingestellte Person, z. B. für die Geschäftsführung |
| `Paperless:FeldBreite`, `FeldHoehe` | Größe des Felds in Punkt (Standard 180 × 56) |
| `Paperless:YVonOben` | Koordinaten ab oberem Seitenrand (Standard) oder ab unterem (`false`) |

**Stand der Prüfung gegen die echte API** (05.10.2026, ohne gültigen Schlüssel; Paperless prüft den Aufbau einer
Anfrage vor der Anmeldung):
- `POST blobs`: Name, Größe, MD5-Prüfsumme und Typ stehen **auf oberster Ebene** (`filename`, `byte_size`,
  `checksum`, `content_type`), nicht unter `blob`. Korrigiert; danach folgt der Upload an `direct_upload.url`.
- `POST documents` mit `workspace_id`, `name`, `pdf` (signed_id), `participants` je Slot, `blocks` und `template_id`
  besteht die Prüfung des Aufbaus. Die Blöcke (`Block::Input::SignatureInput` mit `owner_participants_slot_names`,
  `pdf_page_number`, `settings.absolutePosition`, `settings.absoluteSize`) prüft Paperless erst nach der Anmeldung.

**Noch mit echtem Schlüssel zu prüfen:** Einheit und Ursprung der Koordinaten (Punkt, oben oder unten links), ob
`template_id` zusammen mit einer eigenen PDF-Datei wirkt und ob sich „Nösse“ nach der Unterschrift des Kunden zuordnen
lässt. Dafür gibt es den **Testlauf**:

1. API-Schlüssel als Codespaces-Secret `Paperless__ApiSchluessel` für das Repository anlegen (GitHub: Settings →
   Codespaces → Secrets) und den Codespace neu starten. Auf einem Server genügt die Umgebungsvariable.
2. Im Terminal: `dotnet run --project src/Kalkulator.Web -- --paperless-test name@noesse.de` (eigene Adresse; sie ist
   der Test-„Kunde“).
3. Der Testlauf legt zwei **Entwürfe** an (nie Versand): A mit Koordinaten ab oberem Rand, B ab unterem. Im Muster-PDF
   zeigt ein grauer Rahmen, wo das Feld liegen soll. Die Variante, bei der die Felder in den Rahmen fallen, ergibt
   `Paperless:YVonOben` (A = `true`, B = `false`). Danach beide Entwürfe in Paperless löschen.

Alle Annahmen stecken in `PaperlessUebergabe` (`src/Kalkulator.Infrastructure/Paperless`); Abweichungen lassen sich
dort und über die Einstellungen anpassen.
