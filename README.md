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
| [docs/12_vertragsvorlagen.md](docs/12_vertragsvorlagen.md) | Vertragsvorlagen aus SharePoint: Abgleich, Freigabe, Platzhalter, Markierungsanleitung, Einrichtung |
| [docs/13_ki-angebotserstellung.md](docs/13_ki-angebotserstellung.md) | KI-gestützte Angebotserstellung für Projekte und Managed Services (Entwurf) |
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

### Katalog und Preislisten pflegen

Unter „Katalog“ pflegt das Produktmanagement Services, Preiskomponenten, Bundles, Regeln, Kategorien und
Vertragsvorlagen sowie die Preislisten; die Führung sieht alles nur lesend, andere Rollen sehen den Katalog nicht.

- **Preislisten:** Eine Änderung beginnt mit einem Entwurf, der alle Werte einer bestehenden Preisliste kopiert.
  Darin werden Preise, Staffeln, Parameter und EK gepflegt. Vor der Freigabe prüft die Anwendung Pflichtparameter,
  fehlende Preise und das Gültigkeitsdatum; fehlende EK-Werte und rote Margen sind Hinweise. Freigegebene
  Preislisten sind unveränderlich (ADR-0005).
- **Margen-Ampel:** grün ab `MARGE_GRUEN_AB` (45 %), rot unter `MARGE_ROT_UNTER` (38 %), sonst gelb; ohne EK grau.
  `MARGE_GRUEN_BIS` setzt bei Bedarf eine Obergrenze für Grün. Die Schwellen sind Parameter der Preisliste.
- **Katalog:** Codes sind nach dem Anlegen fest. Gelöscht wird nur, was weder in einer freigegebenen Preisliste
  noch in einer Kalkulation vorkommt; sonst den Vertriebsstatus auf „geparkt“ setzen.
- **Excel-Export** je Preisliste unter `/katalog/export/{id}` mit Services, Preisen, Staffeln, Parametern, Regeln
  und EK-Kalkulation.
- **Änderungsprotokoll:** wer wann was geändert hat, mit altem und neuem Wert.

### Angebote

Ein Angebot entsteht im Kalkulationseditor (Bereich „Angebote“). Dabei wird der gespeicherte Stand als Version
eingefroren, die Angebotsnummer `MS-A-JJJJ-NNNN` vergeben und das Word-Dokument aus
`templates/angebot/Angebotsvorlage.docx` und `textbausteine.json` erzeugt und archiviert. Das geht nur mit einer
freigegebenen Preisliste und mit der **Vertriebsfreigabe** von Vertriebsleitung und Solution Consultant (Rolle
„Consultant“): Beide geben im Bereich „Vertriebsfreigabe“ den gespeicherten Stand frei, und zwar zwei verschiedene
Personen; wer das Projekt verantwortet, darf mit passender Rolle selbst freigeben. Jede inhaltliche Änderung hebt die Freigaben auf.
Nimmt der Kunde an, setzt der Vertrieb das Projekt auf „Gewonnen“ und wählt dabei das angenommene Angebot. Die Vorlagen werden mit dem Programm ausgeliefert (`Vorlagen/angebot`); der Ordner lässt
sich über `Angebot:Vorlagenordner` umstellen. Platzhalter: `docs/08_angebotsvorlage.md`, Technik: ADR-0004.

### Vertragsvorlagen

Grundvertrag, AVB, SLA, AVV und Leistungsscheine kommen aus SharePoint (#26). Unter Katalog › Vertragsvorlagen sieht
das Produktmanagement jede neue Fassung mit Prüfergebnis und gibt sie frei; erst dann wird sie verwendet. Abgeglichen
wird jede Nacht und auf Knopfdruck. Zum Ausprobieren ohne SharePoint genügt ein Ordner mit derselben Struktur:

```bash
Vorlagen__Quelle=Ordner Vorlagen__Ordner=/pfad/zum/03_Vertragswerk dotnet run --project src/Kalkulator.Web
```

Ablage, Platzhalter und Einrichtung des Graph-Zugriffs: `docs/12_vertragsvorlagen.md`, Technik: ADR-0006.

### Vertragswerk

Was die Vorlagen an Angaben verlangen (z. B. Serverliste in S14), erfasst der Vertrieb im Kalkulationseditor unter
„Vertragsangaben“; ohne sie entsteht kein Angebot. Nach „Gewonnen“ erzeugt der Vertrieb im Kundenprojekt das
Vertragswerk: alle Dokumente als PDF, eine Gesamtdatei mit Deckblatt und ein ZIP. Die PDF-Umwandlung läuft über
Microsoft 365; lokal geht auch LibreOffice:

```bash
Pdf__Wandler=LibreOffice Vorlagen__Quelle=Ordner Vorlagen__Ordner=/pfad/zum/03_Vertragswerk dotnet run --project src/Kalkulator.Web
```

Technik: ADR-0007.

Ist Paperless eingerichtet (`Paperless__ApiSchluessel`, `Paperless__ArbeitsbereichId`), geht die Gesamtdatei beim
Erzeugen automatisch mit den Unterschriftsfeldern aus `{{unterschrift.Rolle}}` an Paperless zur technischen
Freigabe. Einrichtung: `docs/12_vertragsvorlagen.md`, Abschnitt 7; Technik: ADR-0008.

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
