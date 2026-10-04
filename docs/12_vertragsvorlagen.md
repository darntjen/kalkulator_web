# Vertragsvorlagen aus SharePoint

Stand: 04.10.2026 · Issue #26, Teil B

Die Word-Vorlagen des Vertragswerks liegen zentral in SharePoint. Dazu gehören Grundvertrag, AVB, SLA, AVV,
Leistungsscheine und Bundle-Scheine. Der Kalkulator übernimmt neue Fassungen von dort automatisch, prüft sie und
verwendet sie erst, wenn das Produktmanagement sie freigegeben hat. Aus den freigegebenen Fassungen entsteht in
Teil C das Vertragswerk.

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

## 3. Platzhalter

Die vollständige, immer aktuelle Liste steht im Kalkulator unter Katalog › Vertragsvorlagen › „Platzhalter für
Vertragsvorlagen“. Die Grundregeln:

- **Text:** `{{kunde.firma}}`. Die Formatierung am Platzhalter gilt für den eingesetzten Wert.
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

Pflicht im Grundvertrag: `{{kunde.firma}}` oder `{{kunde.anschrift}}`, `{{vertrag.nummer}}`, `{{vertrag.beginn}}`,
die Liste `{{#positionen}}` und `{{summe.monatlich}}`.

## 4. Markierungsanleitung für die heutigen Vorlagen

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
| § 6 die beiden Zeilen `[ggf. weitere Bundle-Leistungsscheine …]` und `[ggf. weitere Einzel-Leistungsscheine …]` samt der festen Anlagen davor | Absatz `{{#anlagen}}`, Aufzählungspunkt `{{anlage.code}} — {{anlage.bezeichnung}}`, Absatz `{{/anlagen}}` |
| „Ort, Datum: ____“, „Unterschrift: ____“ | bleiben; unterschrieben wird in Paperless |

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

## 5. Einrichtung (IT)

1. **App-Registrierung** in Entra ID für den Kalkulator, Anwendungsberechtigung **Microsoft Graph `Sites.Selected`**,
   Administratorzustimmung erteilen.
2. **Zugriff auf die Website** „Service-Katalog“ erteilen, Rolle `read`; für die PDF-Umwandlung in Teil C später
   `write` auf einen Arbeitsordner. Das geht per Graph
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

Ist die Konfiguration unvollständig, startet der Kalkulator trotzdem. Der Abgleich meldet dann, was fehlt.
