# ADR-0009: Angebotstexte mit der Claude-API, Zahlen ausschließlich aus dem Kalkulator

- **Status:** vorgeschlagen
- **Datum:** 2026-10-04
- **Beteiligte:** Dennis Arntjen, Claude (Konzept)

## Kontext

Kundenangebote für Projekte und Managed Services entstehen heute außerhalb des Kalkulators. Der Vertrieb verarbeitet
dafür mit Claude-Skills die Navision-Angebote zusammen mit Analyse, Workshop-Ergebnissen, Recherchen und
Transkripten. Das Gesamtkonzept sah KI erst für Version 2 vor und verlangte dafür eine eigene Datenschutz- und
Architekturentscheidung. Die Nutzung der Claude-API mit Kundendaten ist inzwischen freigegeben (04.10.2026).
Konzept: [13_ki-angebotserstellung.md](../13_ki-angebotserstellung.md).

## Entscheidung

- Der Kalkulator ruft die **Claude-API serverseitig** auf und lässt die **Textkapitel** des Angebots entwerfen.
- **Alle Zahlen** (Preise, Mengen, Summen, Stunden, Raten) setzt der Kalkulator aus Navision-Import und Kalkulation
  ein; Claude verweist nur über Platzhalter darauf. Eine automatische Prüfung weist jeden Betrag im Text zurück, der
  keinem berechneten Wert entspricht.
- Vor dem Entwurf steht ein **Abgleich**, den der Vertrieb bestätigt (Haltepunkt des heutigen Skills).
- Jeder Absatz nennt seine **Quelle**; Texte gehören zum Stand der Kalkulation und fallen unter die Vertriebsfreigabe.
- Die Regeln der heutigen Skills werden zur versionierten, austauschbaren **Schreibanleitung**.
- Unterlagen kommen **lesend** aus dem verknüpften Kanalordner im Team „Kundenprojekte“ und per Upload.

## Betrachtete Alternativen

- **Weiter mit Skills außerhalb des Kalkulators:** funktioniert, aber Zahlen werden aus PDFs abgeschrieben, Varianten
  und Freigaben liegen außerhalb, und das Angebot ist nicht mit der Kalkulation verbunden.
- **Claude schreibt das ganze Angebot einschließlich Zahlen:** einfacher, aber jede Zahl müsste von Hand gegen
  Navision geprüft werden; ein Übertragungsfehler im Angebot ist teurer als der Aufwand für Platzhalter.
- **Lokal betriebenes Sprachmodell:** keine Daten nach außen, aber deutlich schwächere Texte und eigener Betrieb
  von Rechenleistung auf dem internen Server.

## Konsequenzen

- Der Kalkulator braucht ausgehenden Zugriff auf die Claude-API und einen API-Schlüssel in der Serverkonfiguration.
- Aufrufe kosten je Angebot Geld; das Protokoll macht die Kosten sichtbar.
- Ohne erreichbare API bleibt das Angebot von Hand schreibbar; Zahlen, Prüfungen und Freigabe hängen nicht an der KI.
- Die IT erteilt der App-Registrierung Lesezugriff auf die Website des Teams „Kundenprojekte“.
