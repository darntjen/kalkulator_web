# 01 – Anforderungen

> Status: **Entwurf.** IDs bleiben stabil, damit wir uns in Diskussionen,
> Tickets und Commits darauf beziehen können.
> Priorität: **M** = Muss (v1.0), **S** = Soll (v1.x), **K** = Kann (später)

## A. Servicekatalog und Preispflege (Grundlage für alle Module)

| ID | Anforderung | Prio |
|----|-------------|------|
| A-01 | Services werden zentral im Katalog gepflegt: Name, Beschreibung, Kategorie, Leistungsbeschreibung, Voraussetzungen, Ausschlüsse | M |
| A-02 | Jeder Service hat eine oder mehrere **Preiskomponenten**, z. B. pro User, pro Gerät, pro Server, pauschal, einmalig (Onboarding) | M |
| A-03 | Preise können **gestaffelt** sein, z. B. nach Menge oder Service-Level (Bronze/Silber/Gold o. Ä.) | M |
| A-04 | Mindestmengen, Mindestumsatz und Abhängigkeiten zwischen Services sind abbildbar (z. B. „Security erfordert Managed Client“) | M |
| A-05 | Preislisten sind **versioniert** und haben einen Gültigkeitszeitraum. Gespeicherte Kalkulationen behalten ihre ursprünglichen Preise | M |
| A-06 | Die interne Kostenbasis (Einkauf, Aufwand) ist hinterlegt, damit Marge und Deckungsbeitrag berechnet werden können. Sie ist nur für berechtigte Rollen sichtbar | S |
| A-07 | Katalog und Preise werden über eine Oberfläche gepflegt, ohne Programmierung | M |
| A-08 | Änderungen am Katalog werden protokolliert (wer, wann, was) | S |
| A-09 | Import und Export des Katalogs (z. B. aus oder nach Excel) | K |

## B. Modul 1 – Kalkulator

| ID | Anforderung | Prio |
|----|-------------|------|
| B-01 | Neue Kalkulation anlegen mit Kundendaten (Firma, Ansprechpartner, Anschrift) | M |
| B-02 | Services aus dem Katalog auswählen und Mengen erfassen (User, Clients, Server …) | M |
| B-03 | Geführte Eingabe: Der Kalkulator fragt Mengengerüst und Rahmendaten ab und schlägt passende Services vor | S |
| B-04 | Live-Berechnung: monatliche Kosten, einmalige Kosten, Jahres- und Vertragsgesamtwert | M |
| B-05 | Vertragsparameter: Laufzeit, Zahlungsweise, Startdatum | M |
| B-06 | Rabatte je Position oder auf die Gesamtsumme. Oberhalb einer Grenze ist eine Freigabe nötig | M (Rabatt) / S (Freigabe) |
| B-07 | Prüfung von Regeln (Abhängigkeiten, Mindestmengen) mit verständlichen Hinweisen | M |
| B-08 | Kalkulation speichern, duplizieren, versionieren (Angebotsstände V1, V2 …) | M |
| B-09 | Status je Kalkulation: Entwurf → Angebot versendet → gewonnen / verloren / zurückgezogen, inkl. Verlustgrund | M |
| B-10 | Anzeige von Marge und Deckungsbeitrag nur für berechtigte Rollen | S |
| B-11 | Freitextpositionen bzw. individuelle Sonderleistungen (mit Kennzeichnung) | S |
| B-12 | Übernahme von Kundendaten aus HubSpot | K |

## C. Modul 2 – Angebotserstellung (Word)

| ID | Anforderung | Prio |
|----|-------------|------|
| C-01 | Aus einer Kalkulation wird per Knopfdruck ein Angebot als **.docx** erzeugt | M |
| C-02 | Grundlage ist eine im Corporate Design gestaltete **Word-Vorlage** mit Platzhaltern, die ohne Programmierung angepasst werden kann | M |
| C-03 | Das Angebot enthält Deckblatt, Anschreiben, Leistungsbeschreibungen der gewählten Services, Preistabelle, Konditionen und Gültigkeit | M |
| C-04 | Textbausteine je Service werden automatisch eingefügt (Leistungsinhalt, Voraussetzungen, Ausschlüsse) | M |
| C-05 | Individuelle Texte (z. B. Ausgangssituation des Kunden) können vor der Erzeugung erfasst werden | S |
| C-06 | Fortlaufende Angebotsnummer nach festgelegtem Schema | M |
| C-07 | Jedes erzeugte Dokument wird archiviert und ist der Kalkulationsversion zugeordnet | M |
| C-08 | Zusätzliche PDF-Ausgabe | K |
| C-09 | Mehrere Vorlagen (z. B. Kurzangebot, ausführliches Angebot) | K |

## D. Modul 3 – Vertragsunterlagen

| ID | Anforderung | Prio |
|----|-------------|------|
| D-01 | Die nötigen Vertragsdokumente werden automatisch passend zu den gewählten Services zusammengestellt | M |
| D-02 | Dokumenttypen (Entwurf, zu bestätigen): Rahmenvertrag, Leistungsscheine bzw. Leistungsbeschreibungen je Service, SLA, Preisblatt, AVV, AGB, TOMs | M |
| D-03 | Variable Inhalte wie Kunde, Preise, Laufzeit, Startdatum und Mengen werden automatisch befüllt | M |
| D-04 | Ausgabe als fertiges Paket: ZIP mit einzelnen Dokumenten und/oder ein zusammengeführtes Gesamtdokument (Format zu klären) | M |
| D-05 | Die Vertragsvorlagen sind versioniert. Es ist nachvollziehbar, welche Version an welchen Kunden ging | M |
| D-06 | Checkliste der beizulegenden Dokumente, inklusive manuell beizufügender Unterlagen | S |
| D-07 | Unterschriftsfelder und Deckblatt mit Dokumentenübersicht | S |

## E. Modul 4 – Statistiken (Backend für die Führungsebene)

| ID | Anforderung | Prio |
|----|-------------|------|
| E-01 | Dashboard: Anzahl Kalkulationen und Angebote, Angebotsvolumen (MRR/ARR, Vertragswert), Abschlussquote | M |
| E-02 | Filter nach Zeitraum, Vertriebsmitarbeiter, Service, Kundengruppe und Status | M |
| E-03 | Auswertung je Service: Wie oft angeboten, wie oft gewonnen, welches Volumen | M |
| E-04 | Rabattauswertung: durchschnittlicher Rabatt je Vertriebler bzw. Service | S |
| E-05 | Margen- und Deckungsbeitragsauswertung | S |
| E-06 | Pipeline-Entwicklung über die Zeit (Trend) | S |
| E-07 | Verlustgründe | S |
| E-08 | Export der Daten nach Excel bzw. CSV | M |

## F. Nicht-fachliche Anforderungen

| ID | Anforderung | Prio |
|----|-------------|------|
| F-01 | Betrieb im internen Netzwerk; kein Zugriff aus dem Internet | M |
| F-02 | Anmeldung mit den Firmenkonten (Active Directory bzw. Microsoft Entra ID, SSO), Details siehe offene Fragen | M |
| F-03 | Rollen- und Rechtekonzept gemäß Projektüberblick, Abschnitt 5 | M |
| F-04 | Verschlüsselte Verbindung (HTTPS mit internem Zertifikat) | M |
| F-05 | Tägliche Datensicherung und dokumentierte Wiederherstellung | M |
| F-06 | DSGVO-konform: Kundenkontaktdaten minimal halten, Lösch- und Aufbewahrungskonzept | M |
| F-07 | Bedienung im Browser (Edge/Chrome), responsiv mindestens bis Tablet | M |
| F-08 | Antwortzeit der Kalkulation unter 1 Sekunde | S |
| F-09 | Nachvollziehbarkeit: Protokoll relevanter Aktionen (Preisänderungen, Rabattfreigaben, Dokumenterzeugung) | S |
| F-10 | Einfache Installation und Aktualisierung (Container), dokumentierter Betrieb | M |
| F-11 | Automatisierte Tests für die Preisberechnung (Referenzkalkulationen) | M |
| F-12 | Oberfläche vollständig in deutscher Sprache | M |
