# ADR-0004: Word-Erzeugung mit Platzhaltern im Mustache-Stil

- **Status:** angenommen
- **Datum:** 2026-10-02
- **Beteiligte:** Dennis Arntjen, Entwicklung

## Kontext

Angebote (Phase 3, #13) und später das Vertragspaket entstehen als Word-Dokumente. Die Vorlagen sollen in Word
gepflegt und ohne Codeänderung ausgetauscht werden können (C-02). ADR-0002 legt das Open XML SDK fest.

## Entscheidung

- Die Vorlage ist eine normale `.docx`-Datei. Variable Stellen sind Platzhalter `{{schluessel}}` im Text, in Tabellen,
  Kopf- und Fußzeilen.
- Wiederholte und bedingte Bereiche sind Blöcke: ein Absatz nur mit `{{#liste}}`, der Inhalt, ein Absatz mit
  `{{/liste}}`. In Tabellen beginnt die erste Zelle einer Zeile mit `{{#liste}}` und die letzte endet mit `{{/liste}}`.
  Innerhalb eines Blocks gelten zuerst die Felder des Eintrags, dann die äußeren Werte. Ist der Wert ein Wahrheitswert
  oder Text, erscheint der Block einmal oder gar nicht.
- Die Engine (`Kalkulator.Documents.WordVorlage`) fasst vorher den Text betroffener Absätze in einen Lauf zusammen,
  weil Word Platzhalter beim Tippen auf mehrere Läufe verteilt.
- Unbekannte Platzhalter und offene Blöcke brechen die Erzeugung mit einer klaren Meldung ab.
- Leistungstexte und SLA-Werte stehen in `templates/angebot/textbausteine.json` neben der Vorlage. Ohne Eintrag gilt
  die Kurzbeschreibung aus dem Katalog.
- Erzeugte Angebote werden mit Nummer und Version in der Datenbank archiviert (Tabelle `kalkulation.AngebotsDateien`)
  und sind unveränderlich.

## Betrachtete Alternativen

- **Inhaltssteuerelemente (Content Controls):** robust gegen Laufaufteilung, aber in Word nur über die
  Entwicklertools pflegbar; Wiederholungen sind umständlich.
- **Dokument komplett im Code aufbauen:** volle Kontrolle, aber jede Text- oder Layoutänderung bräuchte eine
  neue Programmversion. Widerspricht C-02.
- **Fertige Bibliothek (z. B. docxtpl in Python, kommerzielle .NET-Bibliotheken):** würde eine weitere Laufzeit oder
  Lizenzkosten bringen.

## Konsequenzen

- Vorlagen lassen sich in Word bearbeiten. Ein Test befüllt die echte Vorlage mit einer Referenzkalkulation und prüft
  sie gegen das Word-Schema. Fehler fallen so vor der Auslieferung auf.
- Formatierung innerhalb eines Platzhalter-Absatzes richtet sich nach dem ersten Textlauf.
- Die Dateiablage in der Datenbank vereinfacht die Sicherung. Bei sehr vielen Angeboten ist eine Auslagerung in eine
  Dateiablage später möglich.
