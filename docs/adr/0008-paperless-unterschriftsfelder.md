# ADR-0008: Übergabe an Paperless mit Unterschriftsfeldern aus den Word-Vorlagen

- **Status:** angenommen
- **Datum:** 2026-10-04
- **Beteiligte:** Dennis Arntjen, Claude (Umsetzung)

## Kontext

Das Vertragswerk (ADR-0007) geht zur technischen Freigabe und Unterschrift an Paperless (paperless.io). Paperless
braucht neben der PDF-Datei die Teilnehmer und die Stellen, an denen sie unterschreiben. Die Vorlagen pflegt das
Produktmanagement in Word (ADR-0006); die Seiten, auf denen die Unterschriften landen, hängen vom Inhalt ab und sind
erst nach der PDF-Umwandlung bekannt.

## Entscheidung

- **Unterschriftsfelder kommen aus den Vorlagen:** `{{unterschrift.Rolle}}` an der Unterschriftslinie. Der Kalkulator
  schreibt dort eine unsichtbare Marke (weiß, 1 pt, Rolle als Hex-Text) in einen eigenen Lauf und sucht sie nach der
  Umwandlung mit **PdfPig** (Apache-2.0-Lizenz) im Gesamt-PDF. Seite und Lage gehen als Feld
  `Block::Input::SignatureInput` an Paperless.
- **Automatisch beim Erzeugen**, in einem Schritt mit dem Archivieren; schlägt die Übergabe fehl, bleibt das
  Vertragswerk und der Fehler wird vermerkt. „Erneut übergeben“ wiederholt nur die Übergabe.
- **Paperless regelt den weiteren Ablauf** über die Vorlage (`template_id`); standardmäßig entsteht ein Entwurf, den
  die technische Freigabe in Paperless versendet. Rückmeldungen (Webhooks) gibt es nicht.
- **Rollen:** Rollenname = Slot. Feste Personen oder Rollen, die die Paperless-Vorlage besetzt, stehen in der
  Konfiguration; alle anderen fragt die Projektansicht beim Erzeugen ab.
- Die Anfragen sind in einer Klasse gekapselt (`PaperlessUebergabe`), weil das genaue API-Format noch mit einem
  echten Schlüssel zu bestätigen ist.

## Betrachtete Alternativen

- **Felder in Paperless von Hand setzen:** Arbeit bei jedem Vertrag und fehleranfällig.
- **Feste Koordinaten je Vorlage pflegen:** bricht, sobald sich Text oder Seitenumbruch ändern.
- **Textanker von Paperless** (falls angeboten): hängt von einer API-Funktion ab, die nicht bestätigt ist; die
  eigene Marke funktioniert mit jeder Umwandlung.

## Konsequenzen

- Vorlagen ohne `{{unterschrift.…}}` gehen ohne Felder an Paperless; die Projektansicht weist darauf hin.
- Die Marke steht als unsichtbarer Text im PDF (z. B. bei „Alles markieren“ sichtbar); fachlich unbedenklich.
- Der API-Schlüssel ist ein Geheimnis der Serverkonfiguration und nicht Teil des Repositorys.

## Nachtrag 2026-10-05: Vertragsfreigabe im Kalkulator

Die Testläufe mit echtem Schlüssel haben gezeigt: Ein Dokument entsteht in Paperless entweder aus einer Vorlage oder
aus einem PDF. Mit `template_id` fehlt das PDF; beim Anlegen aus einem PDF kennen die Teilnehmer keine Rolle
(`approver`) und keine Reihenfolge. Die beiden Freigaben (AVV, Technik) lassen sich deshalb nicht über Paperless
abbilden.

- Die Übergabe legt das Dokument **ohne Vorlage direkt aus dem PDF** an; Feldlage und -größe werden von Punkt in
  Pixel umgerechnet (`Paperless:Skalierung`).
- **AVV und Technik prüfen im Kalkulator** (App-Rollen `FreigabeAvv`, `FreigabeTechnik`): Sie lesen die Gesamtdatei im
  Browser, geben parallel frei oder lehnen mit Begründung ab. Eine Ablehnung sperrt die Ausfertigung; der Vertrieb
  erzeugt neu.
- Die Übergabe geschieht **nicht mehr beim Erzeugen**, sondern automatisch mit der zweiten Freigabe. „Erneut
  übergeben“ gibt es nur für freigegebene Ausfertigungen.
- Für Nösse unterschreibt fest Sascha Manczak; Kunde zuerst, dann Nösse. Paperless lässt in der Reihenfolge der
  Teilnehmer in der Anfrage unterschreiben; der Kalkulator ordnet sie nach `Paperless:Reihenfolge`. Eine Kopie der
  Ablauf-Vorlage mit eigenem PDF zeigt das PDF nicht (Testlauf D) und entfällt.
- Das Dokument trägt die Sprache `de-DE` (`Paperless:Sprache`).

Einzelheiten: `docs/12_vertragsvorlagen.md`, Abschnitt 7.
