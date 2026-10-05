# 00 – Projektüberblick

> Status: **Entwurf**, fortgeschrieben am 30.09.2026 nach dem Gesamtkonzept
> ([10_gesamtkonzept.md](10_gesamtkonzept.md)).

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

Dazu kommt der übrige Verkaufsprozess: Infrastrukturanalysen, Roadmap-Workshops,
Standortgespräche und Recherchen liegen verteilt in Teams, SharePoint und Outlook.
Transformationsprojekte werden in Navision kalkuliert. Das Navision-Angebot ist
für Kunden aber schwer lesbar. Das Kundenangebot wird deshalb jedes Mal von Hand
in Word zusammengesetzt.

## 2. Vision

> Jede Vertriebsmitarbeiterin und jeder Vertriebsmitarbeiter kann einen
> Managed-Services-Vertrag selbst korrekt kalkulieren. Er oder sie verbindet ihn
> mit den Transformationsprojekten aus Navision und der Situation des Kunden zu
> **einem** ansehnlichen, gut verständlichen Angebot und erzeugt die vollständigen
> Vertragsunterlagen. Die Führungsebene sieht jederzeit, was kalkuliert,
> angeboten und abgeschlossen wurde und was zu erwarten ist.
>
> Langfristig wird der Kalkulator die zentrale Plattform für den MSP-Vertrieb:
> Kundenanalyse → Kalkulation → Angebot → Vertrag → Auftrag → Bereitstellung → Abrechnung.

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
- **Kundenprojekt** als Klammer je Kunde: Dokumentenablage für Analysen, Workshops und Gespräche; **Kundensituation** mit kaufmännischen, organisatorischen und technischen Herausforderungen (manuell gepflegt)
- **Transformationsprojekte:** Einlesen von Navision-Angeboten (PDF), Aufbereitung für den Kunden, Einkaufspreise manuell, Marge
- **Gesamtangebot** mit Varianten, Finanzierung (Leasing) und vollständiger Positionsliste als Anlage
- **Analyse- und Workshop-Angebote** als eigene Angebotsart (nur Eckdaten; das Dokument entsteht weiter per Claude-Skill)
- **Forecast** je Kundenprojekt (Wahrscheinlichkeit und erwarteter Abschlussmonat, manuell)

### Nicht im Scope von Version 1
- Kalkulation von Hardware, Software und Projektdienstleistung **im Kalkulator**. Sie bleibt in Navision; der Kalkulator liest das Navision-Angebot ein
- Schnittstellen zu anderen Systemen (Navision, HubSpot, DocBee, Paperless, Power BI). Version 1 arbeitet mit Upload und Word-Ausgabe
- KI-Funktionen, z. B. Angebotserstellung aus Transkripten oder Serviceempfehlungen (geplant für Version 2)
- Freigabestufen und Margen-Ampel (außer der Freigabe freier Sonderpositionen)
- Rechnungsstellung und Abrechnung laufender Verträge
- Elektronische Signatur
- Zugriff von außerhalb des Firmennetzes (außer über VPN)
- Kundenportal

### Mögliche spätere Ausbaustufen
- **Version 2:** KI-gestützte Angebotserstellung aus erhobenen Daten und Transkripten, auch für Analyse- und Workshop-Angebote; Serviceempfehlungen (z. B. NIS2 → Security-Paket)
- Anbindung an HubSpot (Firma, Kontakt, Deal automatisch übernehmen oder zurückschreiben). **Bewusst nicht in v1.0**
- Anbindung an Navision: Einkaufspreise importieren, Angebot oder Auftrag übergeben
- Automatische Übernahme der Analyseergebnisse aus der Kundenakte
- DocBee, Paperless (Signatur), Power BI
- Margen-Ampel mit Freigabestufen Vertrieb / Vertriebsleitung / Geschäftsleitung
- Ablage erzeugter Dokumente in den SharePoint-/Teams-Kundenordnern
- Ausgabe als PDF zusätzlich zu Word

### Bewusst ausgeschlossen
- Rabatte durch den Vertrieb (Entscheidung 25.09.2026)

## 5. Beteiligte und Rollen

| Rolle | Beschreibung | Rechte in der Anwendung (Entwurf) |
|-------|--------------|-----------------------------------|
| **Vertrieb** | Erstellt Kundenprojekte, Kalkulationen, Angebote und Vertragsunterlagen, pflegt Projektstatus und Forecast | Eigene Kundenprojekte anlegen, bearbeiten und ausgeben. **Keine Rabatte.** Kein Einblick in EK und Marge der Managed Services; EK und Marge **seiner Transformationsprojekte** pflegt und sieht er |
| **Consultant** (Solution Consultant) | Führt Analysen durch, prüft die Lösung | Sieht Kundenprojekte und Kalkulationen, lädt Analyseergebnisse hoch. **Erteilt die Vertriebsfreigabe aus Lösungssicht** (#26). Kalkuliert nicht, erzeugt keine Angebote, sieht keine Einkaufspreise |
| **Vertriebsleitung** | Sieht das Team, gibt Sonderpositionen und Kalkulationen frei | Wie Vertrieb, zusätzlich alle Kalkulationen des Teams, **Freigabe freier Sonderpositionen** und **Vertriebsfreigabe der Kalkulation** (#26) |
| **Freigabe AVV** (`FreigabeAvv`, zusätzliche Rolle) | Prüft im Vertragswerk die Auftragsverarbeitung | Sieht alle Kundenprojekte lesend, öffnet die Gesamtdatei und **gibt das Vertragswerk aus AVV-Sicht frei oder lehnt es mit Begründung ab** |
| **Freigabe Technik** (`FreigabeTechnik`, zusätzliche Rolle) | Prüft das Vertragswerk aus technischer Sicht | Wie Freigabe AVV, für die technische Prüfung. Erst nach beiden Freigaben geht das Vertragswerk an Paperless |
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

Version 1 geht **stufenweise** in Betrieb (Entscheidung 29.09.2026):

- **Stufe 1:** Managed-Services-Kalkulator mit Angebot und Vertragspaket (Kriterien 1–4)
- **Stufe 2:** Kundenprojekt, Navision-Import und Gesamtangebot. Zusätzliches Kriterium: Ein Gesamtangebot aus Kundensituation, eingelesenem Navision-Angebot und Managed Services entsteht ohne Nacharbeit in Word.
