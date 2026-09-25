# Managed-Services-Kalkulator (kalkulator_web)

Interne Webanwendung, mit der Vertriebsmitarbeitende unsere Managed Services
**eigenständig kalkulieren**, daraus **Word-Angebote** erzeugen, die
**Vertragsunterlagen** zusammenstellen und ausgeben können. Die Führungsebene
erhält im Backend **Statistiken** zu allen Kalkulationen.

Die Anwendung läuft auf einem Webserver **innerhalb unseres Netzwerks**
(kein öffentlicher Zugriff).

## Projektstatus

**Phase 1 – Fundament und Servicekatalog.** Die Planung (Phase 0) ist
abgeschlossen. Das technische Projektgerüst steht; als Nächstes folgen
Anmeldung, Datenmodell und Katalogpflege (siehe `docs/05_roadmap.md`).

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
| [docs/06_ist-analyse.md](docs/06_ist-analyse.md) | Auswertung des SharePoint-Servicekatalogs: Services, Preise, Regeln, Vertragswerk |
| [docs/07_referenzkalkulationen.md](docs/07_referenzkalkulationen.md) | Beispielrechnungen mit erwarteten Ergebnissen (spätere Abnahmetests) |
| [docs/08_angebotsvorlage.md](docs/08_angebotsvorlage.md) | Aufbau und Platzhalter der Angebotsvorlage; Musterangebot in `templates/angebot/` |
| [docs/adr/](docs/adr/) | Architekturentscheidungen (Architecture Decision Records) |
| [docs/vorlagen-referenz/](docs/vorlagen-referenz/) | Ablage für Referenzmaterial (Preislisten, Angebots- und Vertragsvorlagen) |

## Lokal starten

Voraussetzung: [.NET SDK 10](https://dotnet.microsoft.com/download) (siehe `global.json`).

```bash
dotnet build Kalkulator.slnx
dotnet test Kalkulator.slnx
dotnet run --project src/Kalkulator.Web
```

Die Anwendung ist danach unter der in der Konsole angezeigten Adresse erreichbar
(Standard: `http://localhost:5226`). Der Health-Check liegt unter `/health`.

Vor einem Push prüfen, ob der Code-Stil passt (das prüft auch die CI):

```bash
dotnet format Kalkulator.slnx --verify-no-changes
```

## Repository-Struktur

```
kalkulator_web/
├── docs/                          Planung, Fachkonzept, Architekturentscheidungen
├── src/
│   ├── Kalkulator.Web/            Blazor-Server-Oberfläche, Einstiegspunkt
│   ├── Kalkulator.Domain/         Fachmodell und Rechenkern (ohne UI- und Datenbankabhängigkeit)
│   ├── Kalkulator.Infrastructure/ Datenbank (EF Core, SQL Server), Dateiablage
│   └── Kalkulator.Documents/      Word-Erzeugung (ab Phase 3)
├── tests/                         Automatisierte Tests (xUnit)
├── templates/                     Angebotsvorlage, Logo
├── deploy/                        Installation auf Windows Server/IIS (Issue #8)
└── .github/workflows/             CI: Code-Stil, Build, Tests, Veröffentlichungspaket
```
