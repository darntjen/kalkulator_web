# ADR-0001: Architekturentscheidungen als ADR dokumentieren

- **Status:** angenommen
- **Datum:** 2026-09-25

## Kontext

Das Projekt wird über einen längeren Zeitraum und womöglich von wechselnden
Personen weiterentwickelt. Entscheidungen und ihre Begründungen sollen
nachvollziehbar bleiben.

## Entscheidung

Jede wesentliche Architektur- oder Technologieentscheidung wird als kurzes
Dokument im Ordner `docs/adr/` festgehalten. Grundlage ist
[0000-vorlage.md](0000-vorlage.md). Die Nummerierung ist fortlaufend.

## Geplante ADRs

| Nr. | Thema | Status |
|-----|-------|--------|
| 0002 | Technologie-Stack (Vorschlag: ASP.NET Core, SQL Server, IIS auf Windows Server) | offen, siehe [04_architektur.md](../04_architektur.md) |
| 0003 | Anmeldung über Microsoft Entra ID (OIDC) | Rahmen festgelegt (25.09.2026), ADR folgt mit ADR-0002 |
| 0004 | Erzeugung von Word-Dokumenten aus Vorlagen | offen, abhängig von ADR-0002 |
| 0005 | Versionierung von Preislisten und Einfrieren von Kalkulationen | offen |

## Konsequenzen

Geringer Mehraufwand pro Entscheidung, dafür dauerhafte Nachvollziehbarkeit.
