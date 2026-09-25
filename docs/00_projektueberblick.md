# 00 – Projektüberblick

> Status: **Entwurf** – wird in den Planungsrunden fortgeschrieben.

## 1. Ausgangslage

Der neue Managed-Services-Katalog ist fachlich vollständig ausgearbeitet und im
SharePoint dokumentiert (Details in [06_ist-analyse.md](06_ist-analyse.md)):

- Servicemodell: Nösse Connect als Pflichtbasis, dazu Bundles, Einzelservices und Add-ons
- VK-Preisliste (Mastersheet) und interne EK-/Deckungsbeitrags-Kalkulation
- Excel-Vertriebskalkulator V1.1 mit Angebotsrechner, Onboarding, Supportkontingent und Vorher/Nachher
- Modulares Vertragswerk: Grundvertrag, AVB, SLA sowie Leistungsscheine je Service und Bundle

Was fehlt: Die Kalkulationen liegen verstreut in Excel-Dateien. Angebote und
Vertragspakete werden von Hand zusammengestellt. Die Führungsebene hat keinen
zentralen Überblick darüber, was kalkuliert, angeboten und abgeschlossen wurde.

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
| Z3 | Einheitliche, korrekte Preise | Keine Abweichung von der gültigen Preisliste (keine Rabatte, keine Handrechnung) |
| Z4 | Vollständige Vertragsunterlagen | Keine Nachforderungen fehlender Dokumente |
| Z5 | Transparenz für die Führung | Pipeline, Volumen, Deckungsbeitrag und Abschlussquote auf einen Blick |

## 4. Abgrenzung (Scope)

### Im Scope
- Kalkulation aller Managed Services aus einem zentral gepflegten Katalog
- Pflege von Servicekatalog und Preisen durch berechtigte Personen, ohne Programmierung
- Angebotsausgabe als Word-Dokument auf Basis einer Firmenvorlage
- Zusammenstellung der Vertragsunterlagen passend zu den gewählten Services
- Statistiken und Auswertungen für die Führungsebene
- Betrieb auf einem internen Windows Server (IIS, SQL Server) mit Anmeldung über Microsoft Entra ID
- **Alle** Services ab Version 1, einschließlich Server Backup (S14), Schwachstellenmanagement mit Assetpreisen (S25), Cloud Server (S61), Strategische IT-Begleitung (S41) und freier Sonderpositionen

### Nicht im Scope (vorerst, bitte bestätigen)
- Kalkulation von Projekten, Hardware und Lizenzhandel (außer als Bestandteil eines Managed Service)
- Rechnungsstellung und Abrechnung laufender Verträge
- Elektronische Signatur
- Zugriff von außerhalb des Firmennetzes (außer über VPN)
- Kundenportal

### Mögliche spätere Ausbaustufen
- Anbindung an HubSpot (Firma, Kontakt, Deal automatisch übernehmen oder zurückschreiben). **Bewusst nicht in v1.0**
- Anbindung an das ERP-System
- Ablage erzeugter Dokumente in den SharePoint-/Teams-Kundenordnern
- Ausgabe als PDF zusätzlich zu Word

### Bewusst ausgeschlossen
- Rabatte durch den Vertrieb (Entscheidung 25.09.2026)

## 5. Beteiligte und Rollen

| Rolle | Beschreibung | Rechte in der Anwendung (Entwurf) |
|-------|--------------|-----------------------------------|
| **Vertrieb** | Erstellt Kalkulationen, Angebote und Vertragsunterlagen, pflegt den Projektstatus | Eigene Kalkulationen anlegen, bearbeiten und ausgeben. **Keine Rabatte, kein Einblick in EK/Marge** |
| **Vertriebsleitung** | Sieht das Team, gibt Sonderpositionen frei | Wie Vertrieb, zusätzlich alle Kalkulationen des Teams und **Freigabe freier Sonderpositionen** |
| **Produktmanagement / Service-Owner** | Pflegt Services, Preise, EK und Vorlagen | Pflege von Servicekatalog, Preislisten, EK-Kalkulation sowie Angebots- und Vertragsvorlagen |
| **Führungsebene** | Steuert anhand der Kennzahlen | Lesender Zugriff auf alle Daten und Statistiken inkl. Deckungsbeitrag und Marge |
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
