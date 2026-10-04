# ADR-0006: Vertragsvorlagen aus SharePoint mit Freigabe im Kalkulator

- **Status:** angenommen
- **Datum:** 2026-10-04
- **Beteiligte:** Dennis Arntjen, Claude (Umsetzung)

## Kontext

Verträge und Leistungsscheine bekommen regelmäßig neue Fassungen. Die gültigen Word-Dateien liegen zentral in
SharePoint (Website „Service-Katalog“, Ordner `03_Vertragswerk (EXTERN)`). Einige brauchen Daten aus dem Kalkulator,
z. B. Kundenname, Mengen und Preise, oder Angaben, die nur der Vertrieb kennt, z. B. die Serverliste in S14. Andere
sind reiner Text. Austauschen soll man sie ohne Programmänderung können (#26).

## Entscheidung

- Der Kalkulator **liest die Dateien per Microsoft Graph** mit einer eigenen App-Registrierung (`Sites.Selected`), jede
  Nacht und auf Knopfdruck. Eine Datei wird über ihren Namen einer Vorlage zugeordnet (Code wie S14, B01, GRUNDVERTRAG).
- Jede neue Fassung wird **gespeichert, geprüft und erst nach Freigabe durch das Produktmanagement aktiv**. Neue
  Fassungen erkennt der Kalkulator am Inhalt (SHA-256), nicht an der Versionsangabe im Namen. Fassungen sind
  unveränderlich und werden nie gelöscht; so bleibt jedes Vertragswerk nachweisbar.
- Variable Stellen sind **`{{…}}`-Platzhalter** derselben Engine wie beim Angebot (ADR-0004). Fehlende Angaben
  beschreibt die Vorlage selbst als `{{eingabe.…}}` (Text, Auswahl, Liste). Der Kalkulator fragt sie ab, ohne dass
  dafür Code geändert werden muss.
- Für Tests und den Übergang gibt es eine **Ordnerquelle** mit derselben Struktur.

## Betrachtete Alternativen

- **Hochladen im Kalkulator:** einfach, aber doppelte Pflege neben SharePoint und Gefahr veralteter Fassungen.
- **Direkt aus SharePoint ohne Freigabe:** jede Änderung in SharePoint ginge sofort in Kundenverträge, auch
  halbfertige oder fehlerhafte.
- **Bestehende Kennzeichnung `[…]` und `____` ersetzen:** keine Änderung an den Vorlagen nötig, aber mehrdeutig, z. B.
  `[ggf. weitere …]`, und nicht prüfbar.

## Konsequenzen

- Die heutigen Vorlagen mit variablen Stellen (Grundvertrag, S14) müssen einmal auf Platzhalter umgestellt werden
  (Anleitung in `docs/12_vertragsvorlagen.md`).
- Die IT richtet App-Registrierung und Website-Berechtigung ein. Geheimnisse liegen nur in der Serverkonfiguration.
- Eine Vorlage hat nun genau einen Datensatz je Code. Version und Dateiname zeigen die aktive Fassung; die bisher von
  Hand gepflegten Werte werden mit der ersten Freigabe überschrieben.
