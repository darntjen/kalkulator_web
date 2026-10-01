# Managed-Services-Kalkulator (kalkulator_web)

Interne Webanwendung, mit der Vertriebsmitarbeitende unsere Managed Services
**eigenständig kalkulieren**, daraus **Word-Angebote** erzeugen, die
**Vertragsunterlagen** zusammenstellen und ausgeben können. Die Führungsebene
erhält im Backend **Statistiken** zu allen Kalkulationen.

Die Anwendung läuft auf einem Webserver **innerhalb unseres Netzwerks**
(kein öffentlicher Zugriff).

## Projektstatus

**Phase 1 – Fundament und Servicekatalog.** Planung und Gesamtkonzept stehen.
Projektgerüst und Datenmodell sind fertig; als Nächstes folgen Erstbefüllung,
Anmeldung und Katalogpflege. Version 1 geht in zwei Stufen in Betrieb:
zuerst die Managed Services, danach Kundenprojekt, Navision-Import und
Gesamtangebot (siehe `docs/05_roadmap.md`).

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
| [docs/09_feedback-clickdummy.md](docs/09_feedback-clickdummy.md) | Auswertung des Feedbacks von Geschäftsführung und Geschäftsleitung zum Clickdummy |
| [docs/10_gesamtkonzept.md](docs/10_gesamtkonzept.md) | Gesamtkonzept: Kundensituation, Navision-Import, Managed Services und Gesamtangebot (freigegebener Arbeitsstand) |
| [docs/11_navision-zuordnung.md](docs/11_navision-zuordnung.md) | Navision-Artikelnummern und Dienstleistungsrollen (vorläufig mit Platzhaltern) |
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

### Anmeldung bis zur Einrichtung von Entra ID

Bis die Anmeldung über Entra ID steht (Issue #4), ist man in der **Entwicklungsumgebung** automatisch
als Testbenutzer angemeldet und wählt die Rolle oben rechts (Vertrieb, Consultant, Vertriebsleitung,
Produktmanagement, Führung, Admin). Die Rechte prüft die Anwendung serverseitig nach der Rechtematrix
in `docs/00_projektueberblick.md`. In allen anderen Umgebungen ist die Anwendung bis dahin gesperrt;
nur der Health-Check antwortet.

Vor einem Push prüfen, ob der Code-Stil passt (das prüft auch die CI):

```bash
dotnet format Kalkulator.slnx --verify-no-changes
```

## Datenbank

Die Anwendung nutzt SQL Server über Entity Framework Core. Die Verbindungszeichenfolge
steht unter `ConnectionStrings:Kalkulator`. Für die lokale Entwicklung ist SQL Server
LocalDB voreingestellt (`appsettings.Development.json`), im Betrieb setzt sie die interne IT.

Migrationen erzeugen und einspielen (Werkzeug über `dotnet tool restore`):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Kalkulator.Infrastructure --output-dir Persistenz/Migrationen
KALKULATOR_DB="<Verbindungszeichenfolge>" dotnet ef database update --project src/Kalkulator.Infrastructure
```

### Erstbefüllung des Katalogs

Ein leerer Katalog wird aus `src/Kalkulator.Infrastructure/Erstbefuellung/katalog.json` befüllt.
Quellen sind Mastersheet, Servicekatalog-Index, EK-Kalkulation und S14-Baukasten. Der Befehl spielt
zuerst die Migrationen ein, legt dann alle Services, Preise, Staffeln, Parameter, EK-Werte, Regeln
und Vorlagen an und beendet sich:

```bash
dotnet run --project src/Kalkulator.Web -- --erstbefuellung
```

Die Preisliste „Preisstand 15.07.2026“ entsteht als **Entwurf** und muss vom Produktmanagement
freigegeben werden. Enthält der Katalog schon Services, passiert nichts. Navision-Artikelnummern
mit `9999…` sind Platzhalter (siehe `docs/11_navision-zuordnung.md`).

Die Datenbanktests (`tests/Kalkulator.Infrastructure.Tests`) starten automatisch einen
SQL Server in Docker (Testcontainers). Dafür muss Docker laufen.

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
