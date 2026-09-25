# 00 – Projektüberblick

> Status: **Entwurf** – wird in den Planungsrunden fortgeschrieben.

## 1. Ausgangslage

Managed Services werden heute (Annahme, bitte bestätigen) von wenigen
Fachleuten kalkuliert, zum Beispiel in Excel. Vertriebsmitarbeitende sind dafür
auf diese Personen angewiesen. Das kostet Zeit, führt zu uneinheitlichen
Angeboten und liefert der Führungsebene keinen Überblick über die Pipeline.

## 2. Vision

> Jede Vertriebsmitarbeiterin und jeder Vertriebsmitarbeiter kann einen
> Managed-Services-Vertrag selbst korrekt kalkulieren. Er oder sie erzeugt
> daraus in wenigen Minuten ein einheitliches Angebot und die vollständigen
> Vertragsunterlagen. Die Führungsebene sieht jederzeit, was kalkuliert,
> angeboten und abgeschlossen wurde.

## 3. Ziele (messbar, Zielwerte noch festzulegen)

| Nr. | Ziel | Mögliche Kennzahl |
|-----|------|-------------------|
| Z1 | Vertrieb kalkuliert selbst | Anteil der Kalkulationen ohne Rückfrage bei Technik oder Produktmanagement |
| Z2 | Schnellere Angebotserstellung | Zeit von der Kalkulation bis zum fertigen Angebot |
| Z3 | Einheitliche, korrekte Preise | Keine Abweichung von der gültigen Preisliste außer durch freigegebene Rabatte |
| Z4 | Vollständige Vertragsunterlagen | Keine Nachforderungen fehlender Dokumente |
| Z5 | Transparenz für die Führung | Pipeline, Volumen, Rabatte und Abschlussquote auf einen Blick |

## 4. Abgrenzung (Scope)

### Im Scope
- Kalkulation aller Managed Services aus einem zentral gepflegten Katalog
- Pflege von Servicekatalog und Preisen durch berechtigte Personen, ohne Programmierung
- Angebotsausgabe als Word-Dokument auf Basis einer Firmenvorlage
- Zusammenstellung der Vertragsunterlagen passend zu den gewählten Services
- Statistiken und Auswertungen für die Führungsebene
- Betrieb auf einem internen Server mit Anmeldung über die Firmenkonten (Ziel)

### Nicht im Scope (vorerst, bitte bestätigen)
- Kalkulation von Projekten, Hardware und Lizenzhandel (außer als Bestandteil eines Managed Service)
- Rechnungsstellung und Abrechnung laufender Verträge
- Elektronische Signatur
- Zugriff von außerhalb des Firmennetzes (außer über VPN)
- Kundenportal

### Mögliche spätere Ausbaustufen
- Anbindung an HubSpot (Firma, Kontakt, Deal automatisch übernehmen oder zurückschreiben)
- Anbindung an das ERP-System
- Freigabe-Workflow für Rabatte
- Ausgabe als PDF zusätzlich zu Word

## 5. Beteiligte und Rollen

| Rolle | Beschreibung | Rechte in der Anwendung (Entwurf) |
|-------|--------------|-----------------------------------|
| **Vertrieb** | Erstellt Kalkulationen, Angebote und Vertragsunterlagen | Eigene Kalkulationen anlegen, bearbeiten und ausgeben; Rabatt bis zu einer festgelegten Grenze |
| **Vertriebsleitung** | Gibt höhere Rabatte frei, sieht das Team | Wie Vertrieb, zusätzlich alle Kalkulationen des Teams und Rabattfreigabe |
| **Produktmanagement / Service-Owner** | Pflegt Services, Preise und Vorlagen | Pflege von Servicekatalog, Preislisten sowie Angebots- und Vertragsvorlagen |
| **Führungsebene** | Steuert anhand der Kennzahlen | Lesender Zugriff auf alle Daten und Statistiken |
| **Administration (IT)** | Betreibt die Anwendung | Benutzer und Rollen, Systemeinstellungen, Backups |

## 6. Erfolgskriterien für Version 1.0

1. Alle aktuell angebotenen Managed Services sind im Katalog abgebildet.
   Ihre Preise stimmen mit der bisherigen Kalkulation überein.
   Das wird über Referenzkalkulationen geprüft.
2. Ein Vertriebsmitarbeiter ohne Vorwissen erstellt nach einer kurzen
   Einweisung eine vollständige Kalkulation mit Angebot.
3. Das erzeugte Word-Angebot entspricht dem Corporate Design und muss nicht
   nachbearbeitet werden. Freitext-Anpassungen sind ausdrücklich erlaubt.
4. Die Vertragsunterlagen werden vollständig und passend zu den gewählten
   Services ausgegeben.
5. Die Führungsebene kann die vereinbarten Kennzahlen selbst abrufen.
