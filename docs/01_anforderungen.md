# 01 – Anforderungen

> Status: **Entwurf.** IDs bleiben stabil, damit wir uns in Diskussionen,
> Tickets und Commits darauf beziehen können.
> Priorität: **M** = Muss (v1.0), **S** = Soll (v1.x), **K** = Kann (später), **–** = entfällt
>
> Fachliche Grundlage: [06_ist-analyse.md](06_ist-analyse.md) (Servicekatalog, Preisregeln, Vertragswerk aus dem SharePoint)

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
| A-10 | **Bundles** bestehen aus Einzelservices (B01 = S02+S03+S04 usw.). Die Zusammensetzung ist gepflegt und wird für die Regelprüfung und das Vertragspaket genutzt | M |
| A-11 | Services haben einen Vertriebsstatus: verkaufsfähig, auf Anfrage, geparkt, zukünftig (nicht verkaufen) | M |
| A-12 | Jedem Service bzw. Bundle ist seine Leistungsschein-Vorlage (Code, Version) zugeordnet | M |
| A-13 | Initiale Befüllung aus Mastersheet V1.0, Vertriebskalkulator V1.1, S14-Baukasten V5.7 und EK-Kalkulation V1.0 | M |

## B. Modul 1 – Kalkulator

| ID | Anforderung | Prio |
|----|-------------|------|
| B-01 | Neue Kalkulation anlegen mit Kundendaten (Firma, Ansprechpartner, Anschrift) | M |
| B-02 | Services aus dem Katalog auswählen und Mengen erfassen (User, Clients, Server …) | M |
| B-03 | Geführte Eingabe: Der Kalkulator fragt Mengengerüst und Rahmendaten ab und schlägt passende Services vor | S |
| B-04 | Live-Berechnung: monatliche Kosten, einmalige Kosten, Jahres- und Vertragsgesamtwert | M |
| B-05 | Vertragsparameter: Laufzeit, Zahlungsweise, Startdatum | M |
| B-06 | ~~Rabatte je Position oder auf die Gesamtsumme~~. **Entfällt:** Vertriebler geben keine Rabatte (Entscheidung 25.09.2026) | – |
| B-07 | Prüfung der Regeln R1–R6 (Connect-Pflicht, Abhängigkeiten, keine Doppelberechnung Bundle/Einzelservice, zukünftige Services gesperrt) mit verständlichen Hinweisen | M |
| B-08 | Kalkulation speichern, duplizieren, versionieren (Angebotsstände V1, V2 …) | M |
| B-09 | **Projektstatus je Kundenkalkulation**, vom Vertrieb gesetzt: Entwurf, Angebot versendet, Vertrag erstellt, Gewonnen, Verloren (Grund Pflicht), Zurückgestellt; mit Änderungshistorie | M |
| B-10 | Anzeige von Marge und Deckungsbeitrag nur für berechtigte Rollen | S |
| B-11 | Freitextpositionen bzw. individuelle Sonderleistungen (mit Kennzeichnung) | S |
| B-12 | Übernahme von Kundendaten aus HubSpot | K |
| B-13 | **Onboarding-Pauschale** automatisch aus Connect-Stufe und Anzahl **User** (Staffel XS–L bis 500 User; ab 501 „individuell“) | M |
| B-14 | **Supportkontingent-Rechner** (S60): Anfragen/Monat × Ø AE → Kontingent (2er-Block) × 30,38 € | M |
| B-15 | **Server-Backup-Rechner** (S14 nach Baukasten V5.7): Variante, Serverzahl, Datenmenge, Lizenzherkunft, inkl. Pflicht-Checkliste | M |
| B-18 | **Schwachstellenmanagement** (S25) mit Grundservice und assetabhängigen Preisen je Client und Server; Prüfung der Voraussetzung (Bundle B01–B04) | M |
| B-19 | **Cloud Server** (S61): Erfassung der Buchungsübersicht, Preisbildung aus dem TERRA-Kalkulator (Details offene Frage 9.2), Pflichtkopplung S21 und Backup-Entscheidung | M |
| B-20 | **Strategische IT-Begleitung** (S41) inkl. optionaler Roadmap-Erstellung (einmalig) | M |
| B-21 | **Freie Sonderpositionen** mit Pflichtbegründung und Kennzeichnung (Regeln offene Frage 9.3) | M |
| B-16 | **Vorher/Nachher-Vergleich** für Bestandskunden (alter Monatspreis gegen neues Modell) | S |
| B-17 | Anzeige des Bundle-Vorteils gegenüber Einzelbuchung (Verkaufsargument) | S |

## C. Modul 2 – Angebotserstellung (Word)

| ID | Anforderung | Prio |
|----|-------------|------|
| C-01 | Aus einer Kalkulation wird per Knopfdruck ein Angebot als **.docx** erzeugt | M |
| C-02 | Grundlage ist eine im Corporate Design gestaltete **Word-Vorlage** (Entwurf: [08_angebotsvorlage.md](08_angebotsvorlage.md)). Die Vorlage muss später austauschbar sein, ohne den Code zu ändern | M |
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
| D-01 | Die nötigen Vertragsdokumente werden automatisch passend zu den gewählten Services zusammengestellt. Hinter jeden B-Schein kommen die zugehörigen S-Scheine; verschachtelte Bundles (B01 in B02 usw.) werden in S-Scheine aufgelöst und nicht separat beigelegt; kein S-Schein doppelt (Ist-Analyse 5.1) | M |
| D-02 | Dokumente laut Vertragswerk: AVV, Grundvertrag, AVB, Anlage SLA, S01, Bundle-Leistungsscheine, Einzel-Leistungsscheine (inkl. S14, S60). Reihenfolge gemäß Rangfolge § 1 Abs. 3 Grundvertrag | M |
| D-03 | Grundvertrag: Kunde, Anschrift, Vertragsnummer, Vertragsbeginn, Vergütungstabelle § 3 und Anlagenliste § 6 automatisch befüllt | M |
| D-08 | Die gewählte Connect-Stufe ist im Vertrag sichtbar: § 3 Grundvertrag, Leistungsschein S01 und Anlage SLA | M |
| D-09 | Vorlagen mit Ausfüllfeldern werden befüllt: S14 (Variante, Serverliste, Kalkulation Ziffer 6.1/6.2, Ziffer 1.2 nur bei S61), S61 (Buchungsübersicht 7.1, Backup-Ankreuzfeld 4.4), S60 (Kontingent), B04/B06 (Bundle-Preis) | M |
| D-10 | Erzeugen des Vertragspakets setzt den Projektstatus auf „Vertrag erstellt“ | S |
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
| E-04 | ~~Rabattauswertung~~. **Entfällt** (keine Rabatte) | – |
| E-05 | Margen- und Deckungsbeitragsauswertung auf Basis der internen EK-Kalkulation (nur Führung) | S |
| E-06 | Pipeline-Entwicklung über die Zeit (Trend) | S |
| E-07 | Verlustgründe | S |
| E-08 | Export der Daten nach Excel bzw. CSV | M |

## F. Nicht-fachliche Anforderungen

| ID | Anforderung | Prio |
|----|-------------|------|
| F-01 | Betrieb im internen Netzwerk; kein Zugriff aus dem Internet | M |
| F-02 | Anmeldung mit den Firmenkonten über **Microsoft Entra ID** (SSO); Rollen über Entra-Gruppen bzw. App-Rollen | M |
| F-03 | Rollen- und Rechtekonzept gemäß Projektüberblick, Abschnitt 5 | M |
| F-04 | Verschlüsselte Verbindung (HTTPS mit internem Zertifikat) | M |
| F-05 | Tägliche Datensicherung und dokumentierte Wiederherstellung | M |
| F-06 | DSGVO-konform: Kundenkontaktdaten minimal halten, Lösch- und Aufbewahrungskonzept | M |
| F-07 | Bedienung im Browser (Edge/Chrome), responsiv mindestens bis Tablet | M |
| F-08 | Antwortzeit der Kalkulation unter 1 Sekunde | S |
| F-09 | Nachvollziehbarkeit: Protokoll relevanter Aktionen (Preisänderungen, Statusänderungen, Dokumenterzeugung) | S |
| F-10 | Einfache Installation und Aktualisierung (Container), dokumentierter Betrieb | M |
| F-11 | Automatisierte Tests für die Preisberechnung (Referenzkalkulationen) | M |
| F-12 | Oberfläche vollständig in deutscher Sprache | M |
