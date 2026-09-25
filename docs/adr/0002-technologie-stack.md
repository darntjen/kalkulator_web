# ADR-0002: Technologie-Stack

- **Status:** angenommen
- **Datum:** 2026-09-25

## Kontext

Die Anwendung läuft auf einem **Windows Server** im internen Netz. Ein
**SQL Server** ist vorhanden. Betrieb, Updates und Sicherung übernimmt die
**interne IT**. Die Anmeldung erfolgt über Microsoft Entra ID (ADR-0003).
Gebraucht werden ein interaktiver Kalkulator mit Live-Berechnung,
Pflegemasken, die Erzeugung von Word-Dokumenten und Auswertungen.

## Entscheidung

- **ASP.NET Core** auf der aktuellen .NET-LTS-Version
- Oberfläche: **Blazor Server** mit einer Komponentenbibliothek (z. B. MudBlazor, MIT-Lizenz)
- Datenzugriff: **Entity Framework Core** mit Migrationen
- Datenbank: eigene Datenbank auf dem **vorhandenen SQL Server**
- Hosting: **IIS** auf Windows Server, HTTPS mit internem Zertifikat
- Word-Erzeugung: **Open XML SDK** (Details in ADR-0004, Phase 3)
- Excel-Export: ClosedXML
- Tests: xUnit; die Referenzkalkulationen aus `docs/07_referenzkalkulationen.md` sind Pflicht-Testfälle
- CI: GitHub Actions (Build, Tests, Veröffentlichungspaket für IIS)

## Betrachtete Alternativen

- **Python/Django:** fertige Admin-Oberfläche und komfortable Word-Vorlagen (docxtpl). Auf Windows Server aber ungewohnter Betrieb, und die interne IT müsste eine weitere Laufzeitumgebung pflegen.
- **Node.js + React:** getrenntes Frontend und Backend, mehr Aufwand in Entwicklung und Betrieb, kein Vorteil für diesen Anwendungsfall.

## Konsequenzen

- Die Anwendung fügt sich in die bestehende Microsoft-Umgebung ein. Die IT nutzt vertraute Werkzeuge (IIS, SQL Server, vorhandenes Backup).
- Pflegemasken für den Katalog müssen selbst gebaut werden. Der Aufwand ist überschaubar, weil das Datenmodell klar ist.
- Die Befüllung der Word-Vorlagen erfordert etwas mehr Eigenentwicklung als in Python.
