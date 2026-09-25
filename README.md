# Managed-Services-Kalkulator (kalkulator_web)

Interne Webanwendung, mit der Vertriebsmitarbeitende unsere Managed Services
**eigenständig kalkulieren**, daraus **Word-Angebote** erzeugen, die
**Vertragsunterlagen** zusammenstellen und ausgeben können. Die Führungsebene
erhält im Backend **Statistiken** zu allen Kalkulationen.

Die Anwendung läuft auf einem Webserver **innerhalb unseres Netzwerks**
(kein öffentlicher Zugriff).

## Projektstatus

**Phase 0 – Planung.** Es gibt noch keinen Anwendungscode. Aktuell werden
Ziele, Anforderungen und Architektur festgelegt.

## Die vier Kernfunktionen

| Nr. | Modul | Kurzbeschreibung |
|-----|-------|------------------|
| 1 | Kalkulator | Kalkulation aller Managed Services auf Basis eines zentral gepflegten Servicekatalogs |
| 2 | Angebotserstellung | Erzeugung eines Angebots als Word-Dokument (.docx) aus einer Kalkulation |
| 3 | Vertragsunterlagen | Zusammenstellung aller nötigen Vertragsdokumente und Ausgabe als fertiges Paket |
| 4 | Statistik | Auswertungen zu Kalkulationen und Angeboten für die Führungsebene |

## Planungsdokumente

| Dokument | Inhalt |
|----------|--------|
| [docs/00_projektueberblick.md](docs/00_projektueberblick.md) | Vision, Ziele, Abgrenzung, Beteiligte, Rollen |
| [docs/01_anforderungen.md](docs/01_anforderungen.md) | Fachliche und nicht-fachliche Anforderungen je Modul |
| [docs/02_offene-fragen.md](docs/02_offene-fragen.md) | Fragenkatalog, der vor der Umsetzung geklärt sein muss |
| [docs/03_fachmodell.md](docs/03_fachmodell.md) | Erster Entwurf des Datenmodells (Servicekatalog, Kalkulation, Angebot …) |
| [docs/04_architektur.md](docs/04_architektur.md) | Architekturvorschlag und Technologieoptionen |
| [docs/05_roadmap.md](docs/05_roadmap.md) | Phasen, Meilensteine und Arbeitspakete |
| [docs/adr/](docs/adr/) | Architekturentscheidungen (Architecture Decision Records) |
| [docs/vorlagen-referenz/](docs/vorlagen-referenz/) | Ablage für Referenzmaterial (Preislisten, Angebots- und Vertragsvorlagen) |

## Repository-Struktur (geplant)

```
kalkulator_web/
├── docs/            Planung, Fachkonzept, Architekturentscheidungen
├── src/             Anwendungscode (ab Phase 1)
├── templates/       Word-Vorlagen für Angebote und Verträge (ab Phase 2)
├── tests/           Automatisierte Tests
└── deploy/          Container- und Serverkonfiguration
```
