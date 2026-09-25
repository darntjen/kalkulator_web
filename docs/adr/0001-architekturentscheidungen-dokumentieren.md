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
| [0002](0002-technologie-stack.md) | Technologie-Stack: ASP.NET Core, Blazor Server, SQL Server, IIS | angenommen (25.09.2026) |
| [0003](0003-anmeldung-entra-id.md) | Anmeldung über Microsoft Entra ID (OIDC), Rollen über App-Rollen | angenommen (25.09.2026) |
| 0004 | Erzeugung von Word-Dokumenten aus Vorlagen (Open XML SDK) | offen, Umsetzung Phase 3 |
| 0005 | Versionierung von Preislisten und Einfrieren von Kalkulationen | offen |

## Konsequenzen

Geringer Mehraufwand pro Entscheidung, dafür dauerhafte Nachvollziehbarkeit.
