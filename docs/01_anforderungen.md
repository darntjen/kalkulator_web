# 01 – Anforderungen

> Status: **Entwurf.** IDs bleiben stabil, damit wir uns in Diskussionen,
> Tickets und Commits darauf beziehen können.
> Priorität: **M** = Muss (v1.0), **S** = Soll (v1.x), **K** = Kann (später), **–** = entfällt
>
> Fachliche Grundlage: [06_ist-analyse.md](06_ist-analyse.md) (Servicekatalog, Preisregeln, Vertragswerk aus dem SharePoint)
> und [10_gesamtkonzept.md](10_gesamtkonzept.md) (Kundenprojekt, Navision-Import, Gesamtangebot; Abschnitte G–J, ergänzt am 30.09.2026)

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
| A-14 | Jede Preiskomponente trägt ihre **Navision-Artikelnummer** (Platzhalter bis zur Lieferung der Liste, siehe [11_navision-zuordnung.md](11_navision-zuordnung.md)) | M |
| A-15 | **Dienstleistungsrollen** mit Navision-Artikelnummer und internem Stundensatz werden gepflegt (Grundlage für H-06) | M |

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
| B-08 | Kalkulation speichern und duplizieren. Eine Kalkulation gehört immer zu einem Kundenprojekt (G-01); ein Kundenprojekt kann mehrere Kalkulationen haben (z. B. für Varianten). Beim Erzeugen eines Angebots wird ein eingefrorener Stand (Version V1, V2 …) angelegt (C-06, C-07) | M |
| B-09 | **Projektstatus je Kundenprojekt**, vom Vertrieb gesetzt: Entwurf, Angebot versendet, Vertrag erstellt, Gewonnen, Verloren (Grund Pflicht), Zurückgestellt; mit Änderungshistorie. Die Projektliste lässt sich nach einem oder mehreren Status filtern; die Auswahl steht in der Adresse (Entscheidung 08.10.2026) | M |
| B-10 | Anzeige von Marge und Deckungsbeitrag nur für berechtigte Rollen | S |
| B-11 | Freitextpositionen bzw. individuelle Sonderleistungen (mit Kennzeichnung) | S |
| B-12 | Übernahme von Kundendaten aus HubSpot | K |
| B-13 | **Onboarding-Pauschale** automatisch aus Connect-Stufe und Anzahl **User** (Staffel XS–L bis 500 User; ab 501 „individuell“) | M |
| B-14 | **Supportkontingent-Rechner** (S60): Anfragen/Monat × Ø AE → Kontingent (2er-Block) × 30,38 € | M |
| B-15 | **Server-Backup-Rechner** (S14 nach Baukasten V5.7): Variante, Serverzahl, Datenmenge, Lizenzherkunft, inkl. Pflicht-Checkliste | M |
| B-18 | **Schwachstellenmanagement** (S25) mit Grundservice und assetabhängigen Preisen je Client und Server; Prüfung der Voraussetzung (Bundle B01–B04) | M |
| B-19 | **Cloud Server** (S61): Erfassung der Buchungsübersicht (Ziffer 7.1) und des EK aus dem TERRA-Kalkulator; VK = EK ÷ 0,55 (45 % Marge); Pflichtkopplung S21 (zusätzliche Firewall-Instanz) und Backup-Entscheidung (S14 oder Kunde). Der EK ist für den Vertrieb nur als Eingabefeld sichtbar, nicht in Auswertungen | M |
| B-20 | **Strategische IT-Begleitung** (S41): Preis nach Anzahl Mitarbeitende (bis 50: 350 €, ab 51: 550 €), optionale Roadmap-Erstellung 2.400 € einmalig. **Einzige Ausnahme von der Connect-Pflicht:** Kalkulationen nur mit S41 sind ohne S01 zulässig | M |
| B-21 | **Freie Sonderpositionen:** Pflichtfelder Bezeichnung, Einheit, Menge, Preis (nur positiv), Begründung; gekennzeichnet in Angebot und Statistik; kein eigener Leistungsschein | M |
| B-22 | **Freigabe von Sonderpositionen durch die Vertriebsleitung:** Solange eine Sonderposition nicht freigegeben ist, kann weder Angebot noch Vertragspaket erzeugt werden. Freigabe und Ablehnung werden mit Kommentar protokolliert; der Vertrieb wird benachrichtigt | M |
| B-23 | **Vertriebsfreigabe der Kalkulation** durch Vertriebsleitung und Solution Consultant vor dem Angebot (#26): Freigabe mit optionalem Kommentar, zurückziehbar; jede inhaltliche Änderung hebt sie auf; zwei verschiedene Personen, Verantwortliche mit passender Rolle dürfen selbst freigeben. Die Freigaben werden mit der Angebotsversion eingefroren. Bei „Gewonnen“ wird das angenommene Angebot gewählt | M |
| B-24 | **Vertragswerk aus dem angenommenen Angebot** (#26): Vertragsangaben in der Kalkulation (Pflicht vor dem Angebot), Erzeugung nach „Gewonnen“ aus den aktiven Vorlagenfassungen, alle Dokumente als PDF, Gesamt-PDF mit Deckblatt und Verzeichnis, ZIP; Vertragsnummer = Angebotsnummer; Archiv je Ausfertigung | M |
| B-16 | **Vorher/Nachher-Vergleich** für Bestandskunden (alter Monatspreis gegen neues Modell) | S |
| B-17 | Anzeige des Bundle-Vorteils gegenüber Einzelbuchung (Verkaufsargument) | S |

## C. Modul 2 – Angebotserstellung (Word)

| ID | Anforderung | Prio |
|----|-------------|------|
| C-01 | Aus einer Kalkulation (Stufe 1) bzw. einem Kundenprojekt (Stufe 2, Gesamtangebot laut Abschnitt I) wird per Knopfdruck ein Angebot als **.docx** erzeugt | M |
| C-02 | Grundlage ist eine im Corporate Design gestaltete **Word-Vorlage** (Entwurf: [08_angebotsvorlage.md](08_angebotsvorlage.md)). Die Vorlage muss später austauschbar sein, ohne den Code zu ändern | M |
| C-03 | Das Angebot enthält Deckblatt, Anschreiben, Leistungsbeschreibungen der gewählten Services, Preistabelle, Konditionen und Gültigkeit | M |
| C-04 | Textbausteine je Service werden automatisch eingefügt (Leistungsinhalt, Voraussetzungen, Ausschlüsse) | M |
| C-05 | Individuelle Texte (z. B. Ausgangssituation des Kunden) können vor der Erzeugung erfasst werden. In Stufe 2 kommt die Ausgangssituation aus der Kundensituation (G-03) | M |
| C-06 | Fortlaufende Angebotsnummer nach festgelegtem Schema | M |
| C-07 | Jedes erzeugte Dokument wird mit Nummer und Version archiviert. Summen und Inhalte werden eingefroren | M |
| C-10 | **Versandvermerk:** Der Vertrieb markiert eine Angebotsversion als versendet (Datum). Daraus entsteht die Übersicht, welche Version wann an den Kunden ging. Der Projektstatus wechselt auf „Angebot versendet“ | M |
| C-11 | **Angebote im Projekt nach Status filtern** (Entscheidung 08.10.2026): Das Kundenprojekt zeigt alle Angebote aller Kalkulationen. Der Status wird abgeleitet, nicht gespeichert: erzeugt, versendet, angenommen, nicht angenommen (Projekt gewonnen oder verloren), ersetzt (neuere Version derselben Nummer), abgelaufen (versendet, Gültigkeit überschritten). Auch die Angebote zu Analyse und Workshop lassen sich nach Status filtern | M |
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
| E-09 | **Forecast:** Volumen je erwartetem Abschlussmonat, getrennt nach einmalig und laufend, gewichtet mit der Wahrscheinlichkeit des Kundenprojekts (G-05); bei Varianten zählt die als wahrscheinlich markierte | M |
| E-10 | Marge und DB der Transformationsprojekte und des Gesamtangebots; Kennzeichnung bei unvollständigen EK | M |
| E-11 | Analyse- und Workshop-Angebote zählen in Pipeline und Statistik mit (Abschnitt J) | M |
| E-12 | Break-even. **Nicht in v1**, Definition folgt | – |

## F. Nicht-fachliche Anforderungen

| ID | Anforderung | Prio |
|----|-------------|------|
| F-01 | Betrieb im internen Netzwerk; kein Zugriff aus dem Internet | M |
| F-02 | Anmeldung mit den Firmenkonten über **Microsoft Entra ID** (SSO); Rollen über Entra-Gruppen bzw. App-Rollen | M |
| F-03 | Rollen- und Rechtekonzept gemäß Projektüberblick, Abschnitt 5, einschließlich der Rolle **Consultant** | M |
| F-04 | Verschlüsselte Verbindung (HTTPS mit internem Zertifikat) | M |
| F-05 | Tägliche Datensicherung und dokumentierte Wiederherstellung | M |
| F-06 | DSGVO-konform: Kundenkontaktdaten minimal halten, Lösch- und Aufbewahrungskonzept | M |
| F-07 | Bedienung im Browser (Edge/Chrome), responsiv mindestens bis Tablet | M |
| F-08 | Antwortzeit der Kalkulation unter 1 Sekunde | S |
| F-09 | Nachvollziehbarkeit: Protokoll relevanter Aktionen (Preisänderungen, Statusänderungen, Dokumenterzeugung) | S |
| F-10 | Einfache Installation und Aktualisierung (Veröffentlichungspaket für IIS, siehe ADR-0002), dokumentierter Betrieb | M |
| F-11 | Automatisierte Tests für die Preisberechnung (Referenzkalkulationen) | M |
| F-12 | Oberfläche vollständig in deutscher Sprache | M |
| F-13 | Hochgeladene Dateien werden auf dem Server abgelegt, gesichert und nach einem Lösch- und Aufbewahrungskonzept gelöscht | M |
| F-14 | In Version 1 werden keine Kundendaten an externe Dienste übermittelt (kein KI-Dienst, keine Schnittstellen) | M |

## G. Kundenprojekt und Kundensituation (Stufe 2)

| ID | Anforderung | Prio |
|----|-------------|------|
| G-01 | **Kundenprojekt** als oberstes Objekt je Kunde und Vorhaben: Kunde (mit Navision-Kundennummer), Titel, verantwortlicher Vertrieb, Neu-/Bestandskunde. Im Datenmodell ab Stufe 1 angelegt | M |
| G-02 | **Dokumentenablage** je Kundenprojekt: Upload von Analysen, Workshop-Ergebnissen, Gesprächszusammenfassungen, Recherchen und Sonstigem, mit Art, Uploader und Datum. Consultants dürfen hochladen | M |
| G-03 | **Kundensituation:** Herausforderungen je Dimension (kaufmännisch, organisatorisch, technisch) mit Titel, Beschreibung in Kundensprache, Auswirkung, Priorität und Quelle; manuell gepflegt | M |
| G-04 | Jeder Angebotsbaustein (Transformationsprojekt, Managed Service) wird mit den Herausforderungen verknüpft, die er löst | M |
| G-05 | **Forecast-Felder** je Kundenprojekt: Abschlusswahrscheinlichkeit in % und erwarteter Abschlussmonat, manuell | M |
| G-06 | Automatische Übernahme der Analyseergebnisse aus der Kundenakte | K |

## H. Transformationsprojekte und Navision-Import (Stufe 2)

| ID | Anforderung | Prio |
|----|-------------|------|
| H-01 | **Einlesen eines Navision-Angebots als PDF** (festes Layout): Kopf (Angebotsnummer, Belegdatum, Kundennummer, Referenz, Ansprechpartner), Positionen (Pos., Artikelnummer, Bezeichnung, Langtext, Menge, Einheit, VK-Preis, Betrag), Summen. Regelbasiert, ohne KI | M |
| H-02 | Prüfung beim Import: Summe der Beträge = Total netto, sonst Ablehnung mit Hinweis. Warnung bei abweichender Kundennummer | M |
| H-03 | Sonderfälle: mehrzeilige Langtexte über Seitengrenzen, Positionen ohne Preis („inklusive“), Laufzeitangaben („Zeitraum …“), **Alternativpositionen** (nicht summiert, im Angebot als Option darstellbar) | M |
| H-04 | Art der Position: automatisch für Dienstleistung, Managed Services und Onboarding (über die Artikelnummer), sonst manuell (Hardware, Software, Herstellerservice, Sonstiges) | M |
| H-05 | Kapitelstruktur und Kundentexte (Ziel, Nutzen, Kapitelbeschreibung); Sichtbarkeit je Position (einzeln oder zusammengefasst) | M |
| H-06 | **EK manuell** je Position oder als Summe je Kapitel. Dienstleistung wird aus dem Stundensatz der Rolle vorbelegt (EK je AE = Stundensatz ÷ 4), überschreibbar. Fehlende EK werden markiert | M |
| H-07 | Alle Werte sind änderbar. Abweichungen vom importierten Navision-Stand werden mit altem und neuem Wert **gekennzeichnet**; vor dem Erzeugen des Angebots erscheint die Liste der Abweichungen | M |
| H-08 | Erneuter Import derselben Angebotsnummer: Unterschiede werden vorher angezeigt, Zuordnungen (Kapitel, Art, EK) übernommen, soweit möglich; alte Fassung bleibt einsehbar | M |
| H-09 | Navision-Angebote mit Managed Services werden nicht als Transformationsprojekt übernommen; optionaler Abgleich mit der Managed-Services-Kalkulation | K |
| H-10 | Tests gegen synthetische Muster-PDFs im Navision-Layout (keine echten Kundendaten im Repository) | M |
| H-11 | EK-Import aus Navision | K |

## I. Gesamtangebot, Varianten und Finanzierung (Stufe 2)

| ID | Anforderung | Prio |
|----|-------------|------|
| I-01 | Gesamtangebot aus Kundensituation, Transformationsprojekten und Managed Services, Aufbau laut Gesamtkonzept Abschnitt 9; leere Abschnitte entfallen | M |
| I-02 | **Varianten** (A, B …) mit eigenen Bausteinen und Summen; eine Variante ist als wahrscheinlich markiert (Forecast) | M |
| I-03 | Investitionsübersicht je Variante: einmalig, monatlich, ggf. Finanzierungsrate, Wert der Erstlaufzeit | M |
| I-04 | **Finanzierung/Leasing** als Variante: in v1 mit manuell eingetragener Rate, Laufzeit und Partner. Rechenweg mit Faktoren folgt nach Klärung (Gesamtkonzept 13.1–13.4) | M |
| I-05 | **Anlage mit vollständiger Positionsliste** aller Navision-Angebote inklusive Navision-Angebotsnummer | M |
| I-06 | Alternativpositionen erscheinen als Optionen | S |

## J. Analyse- und Workshop-Angebote (Stufe 2)

| ID | Anforderung | Prio |
|----|-------------|------|
| J-01 | Eigene Angebotsart „Analyse und Workshop“: Erfassung von Angebotsnummer, Datum, Paketpreis und Status; Dokument optional in der Ablage | M |
| J-02 | Erstellung des Dokuments weiter per Claude-Skill; Erstellung im Kalkulator mit KI in Version 2 | K |
