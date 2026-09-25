# 05 – Roadmap

> Status: **Entwurf.** Zeitangaben werden festgelegt, sobald Umfang (Anzahl
> Services, Komplexität der Preislogik) und Ressourcen bekannt sind.

## Übersicht

```mermaid
flowchart LR
    P0[Phase 0<br/>Planung] --> P1[Phase 1<br/>Fundament & Katalog]
    P1 --> P2[Phase 2<br/>Kalkulator]
    P2 --> P3[Phase 3<br/>Word-Angebot]
    P3 --> P4[Phase 4<br/>Vertragsunterlagen]
    P4 --> P5[Phase 5<br/>Statistik]
    P5 --> P6[Phase 6<br/>Pilot & Go-live]
    P6 --> P7[Phase 7<br/>Ausbau]
```

Ab Phase 2 entsteht nach jeder Phase ein **lauffähiger, vorführbarer Stand**.
So kann der Vertrieb früh Rückmeldung geben.

## Phase 0 – Planung (aktuell)

**Ziel:** Klarheit über Umfang, Preislogik, Dokumente und Technik.

- [x] Repository und Planungsstruktur anlegen
- [x] Referenzmaterial im SharePoint gesichtet und ausgewertet ([06_ist-analyse.md](06_ist-analyse.md))
- [x] Preislogik beschrieben und Referenzkalkulationen entworfen ([07_referenzkalkulationen.md](07_referenzkalkulationen.md))
- [x] Grundsatzentscheidungen: keine Rabatte, Projektstatus im Kalkulator, HubSpot später, Windows Server, Entra ID
- [ ] Offene Fragen aus Abschnitt 8 klären ([02_offene-fragen.md](02_offene-fragen.md))
- [ ] Referenzkalkulationen durch den Fachbereich bestätigen lassen
- [ ] Angebotsvorlage klären bzw. entwerfen
- [ ] Rollen, Rechte und Kennzahlen final festlegen
- [ ] Architekturentscheidung treffen (ADR-0002 Technologie-Stack)
- [ ] Umfang von Version 1.0 festschreiben

**Ergebnis:** Freigegebenes Fachkonzept, Architekturentscheidung, Umfang v1.0.

## Phase 1 – Fundament und Servicekatalog

- Projektgerüst, automatisierte Tests und Code-Prüfung (CI), Installationspaket für IIS
- Entra-ID-Anmeldung (App-Registrierung), Rollen
- Datenmodell für Katalog, Bundles, Preislisten, Parameter, Regeln, EK-Kalkulation
- Pflegeoberfläche für den Katalog
- Erstbefüllung aus Mastersheet, Vertriebskalkulator, S14-Baukasten und EK-Kalkulation

**Ergebnis:** Katalog ist vollständig gepflegt und von der Produktverantwortung abgenommen.

## Phase 2 – Kalkulator (Modul 1)

- Rechenkern mit Tests gegen die Referenzkalkulationen
- Kalkulationsoberfläche mit Live-Berechnung und Regelprüfung (Connect-Pflicht, Bundle-Deduplizierung)
- Sonderrechner: Onboarding, Supportkontingent, Server Backup (S14)
- Vorher/Nachher-Vergleich für Bestandskunden
- Speichern, Versionieren, Projektstatus

**Ergebnis:** Vertrieb kann kalkulieren. Die Ergebnisse stimmen mit den Referenzkalkulationen überein.

## Phase 3 – Angebotserstellung in Word (Modul 2)

- Angebotsvorlage mit Platzhaltern auf Basis des Corporate Designs
- Erzeugung, Nummernkreis, Archivierung

**Ergebnis:** Fertiges Word-Angebot per Knopfdruck.

## Phase 4 – Vertragsunterlagen (Modul 3)

- Vertragsvorlagen aus `03_Vertragswerk` mit Platzhaltern versehen (Grundvertrag § 3/§ 6, S14, S60)
- Automatische Auflösung: Bundle → enthaltene Einzel-Leistungsscheine; Rangfolge nach § 1 Abs. 3
- Paketausgabe (ZIP mit Anlagenverzeichnis)
- Versionierung der Vorlagen

**Ergebnis:** Vollständige Vertragsunterlagen per Knopfdruck.

## Phase 5 – Statistik (Modul 4)

- Dashboard mit den vereinbarten Kennzahlen, Filter, Excel-Export
- Rechteprüfung: Wer sieht welche Zahlen

**Ergebnis:** Die Führungsebene ruft Kennzahlen selbst ab.

## Phase 6 – Pilot und Go-live

- Installation auf dem internen Server, Backup und Wiederherstellung testen
- Pilotbetrieb mit ausgewählten Vertriebsmitarbeitenden
- Kurze Anleitung bzw. Schulung, Fehlerbehebung
- Produktivsetzung

## Phase 7 – Ausbau (nach Bedarf)

- HubSpot-Anbindung
- Ablage der Dokumente in SharePoint/Teams-Kundenordnern; Vorlagen-Sync aus SharePoint
- PDF-Ausgabe, weitere Vorlagen
- S61 Cloud Server, S41 Strategische IT-Begleitung
- Anbindung an das ERP-System

## Arbeitsweise

- Anforderungen und Aufgaben als **GitHub-Issues**, gruppiert nach Phase (Meilensteine)
- Entwicklung in Feature-Branches, Übernahme per Pull Request
- Jede Architekturentscheidung als ADR in `docs/adr/`
- Preislogik-Änderungen immer mit Referenzkalkulation als Test
