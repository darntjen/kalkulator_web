# ADR-0010: Unterlagen am Kundenprojekt aus Teams, ergänzende Uploads in der Datenbank

- **Status:** angenommen
- **Datum:** 2026-10-04
- **Beteiligte:** Dennis Arntjen, Claude (Umsetzung)

## Kontext

Zu jedem Kundenprojekt gehören Analysen, Workshop-Ergebnisse, Gesprächszusammenfassungen, Transkripte und Recherchen
(G-02). Sie entstehen heute im Kanalordner des Kunden im Team „Kundenprojekte“ nach einem festen Standard
(`00_Kundenakte` bis `99_Archiv`). Der Teams-Kanal ist dort die Leitablage. Die Anforderung F-13 verlangt Ablage auf
dem Server, Sicherung und ein Lösch- und Aufbewahrungskonzept. Später sollen die Unterlagen in KI-gestützte Angebote
einfließen (Konzept 13).

## Entscheidung

- **Teams bleibt Leitablage.** Das Kundenprojekt wird einmal mit seinem Kanalordner verknüpft (Vorschlag nach dem
  Firmennamen, Bestätigung durch den Benutzer). Der Kalkulator liest über Microsoft Graph die Ordner `00_Kundenakte`,
  `10_Recherche`, `20_Standortgespraech`, `30_Analyse`, `40_Workshop_Roadmap`, `60_Angebote` und `80_Protokolle`
  mit Unterordnern; `50_Umsetzung`, `70_Vertraege` und `99_Archiv` nicht. Er schreibt nichts nach Teams.
- **Uploads** für alles, was nicht in Teams liegt, werden in der **Datenbank** gespeichert (eigene Tabelle für den
  Inhalt), nicht im Dateisystem. Erlaubt sind gängige Büro-, Text-, Transkript-, Bild- und Mailformate bis 25 MB.
- **Aufbewahrung:** Uploads werden drei Jahre nach „Gewonnen“ oder „Verloren“ automatisch gelöscht (täglich 03:15
  Uhr), von Hand jederzeit. Angebote und Vertragswerke sind davon nicht betroffen.
- **Rechte:** Pflegen (verknüpfen, hochladen, löschen) dürfen der verantwortliche Vertrieb, die Vertriebsleitung und
  die Consultants; sehen darf, wer das Projekt sieht.
- Für Entwicklung und Tests ersetzt ein lokaler Ordner das Team (`Kundenablage:Quelle = Ordner`).

## Betrachtete Alternativen

- **Nur Upload in den Kalkulator:** doppelte Ablage neben Teams, Unterlagen veralten.
- **Upload schreibt nach Teams:** bräuchte Schreibrecht auf das Team; abgelehnt, nur Lesen ist vereinbart.
- **Uploads im Dateisystem des Servers:** zusätzliche Sicherung und Rechte auf Dateiebene; die Datenbank ist ohnehin
  gesichert und hält die übrigen Dokumente bereits.

## Konsequenzen

- Die App-Registrierung braucht Lesezugriff auf die Website des Teams (Sites.Selected, `read`, Frage 14.4).
- Öffnen einer Teams-Datei geht über den SharePoint-Link mit den Rechten des Benutzers; ohne Link (Ordnerquelle) über
  den Kalkulator.
- Die Datenbank wächst um die Uploads; die Löschfrist begrenzt das.
