# 06 – Ist-Analyse der SharePoint-Unterlagen

> Stand der Analyse: 25.09.2026 · Status: **Arbeitsgrundlage** (Preise und Regeln
> vom Fachbereich zu bestätigen, siehe Abschnitt 9)
>
> Diese Datei fasst zusammen, was im SharePoint zum Managed-Services-Katalog
> bereits existiert. Sie ist die fachliche Grundlage für Kalkulator, Angebot
> und Vertragsunterlagen.

## 1. Quellen (Single Source of Truth)

SharePoint-Site **„Service-Katalog“** → `Freigegebene Dokumente/Allgemein/Nösse MSP Servicekatalog/`

| Ordner | Inhalt | Relevanz für die Anwendung |
|--------|--------|----------------------------|
| `00_Governance` | EK-Kalkulation & Deckungsbeitrag, Service-EK-Rechner, Master-Backlog & Entscheidungslog, Marktvergleich, Ordner-/Namenskonvention, Ticket-Kategorien | EK/Marge für Statistik (nur Führung), Entscheidungshistorie |
| `01_Kalkulation` | **Mastersheet Servicekatalog V1.0.xlsx** (VK-Preisliste, Preisstand 15.07.2026), **Vertriebskalkulator_V1.1.xlsx** (Angebotsrechner, Onboarding, Supportkontingent, Vorher/Nachher) | **Kern der Preislogik**. Der Web-Kalkulator löst den Excel-Vertriebskalkulator ab |
| `03_Vertragswerk (EXTERN)` | `Rahmen/` (Grundvertrag, AVB, Anlage SLA), `Bundles/` (B01–B07), `Leistungsscheine/` (S01–S61, S14 als Sonderfall mit Baukasten) | **Vorlagen für das Vertragspaket** |
| `04_Handbuecher & Leitfaeden` | Vertriebs-Handbuch, Technik-Handbuch, Leitfaden Abgrenzung, Rollen- und Stundensatzmodell | Regeln, Texte, Argumentation |

Außerdem gibt es den Ordner `Archiv/` mit älteren Fassungen. Er ist **nicht maßgeblich**.
Darüber hinaus liegen im SharePoint weitere Altverträge (z. B. „Managed Service
Infrastruktur Vertrag V1.x“, „Rahmenvertrag Managed Security V1.40“). Sie
gehören zum alten Flatrate-Modell und werden in der Anwendung nicht verwendet.

## 2. Servicemodell

- Jeder Kunde bucht **genau eine Stufe von Nösse Connect** (Pflichtbasis: Standard, Premium oder Enterprise). Premium ist die Vertriebsempfehlung.
- Darauf kommen **Bundles** („X as a Service“: User, Server, Security, Network; jeweils Standard/Premium), **Einzelservices** und **Add-ons**.
- Bundles sind günstiger als die Summe der Einzelservices. Wird ein Bundle gebucht und zusätzlich ein darin enthaltener Einzelservice, **gilt nur der Bundle-Preis** (keine Doppelabrechnung, laut Leistungsschein B01, Ziffer 3 c).
- **Keine Rabatte** durch den Vertrieb (Entscheidung vom 25.09.2026).
- Service Requests werden nach Aufwand in Abrechnungseinheiten abgerechnet (1 AE = 15 Min.; Ebene 1/2/3 = 23,75 / 33,75 / 42,50 € je AE). Das ist für die Kalkulation nur als Information relevant, nicht als Position.

## 3. Servicekatalog mit Verkaufspreisen (Preisstand 15.07.2026)

Alle Preise netto pro Monat, sofern nicht anders angegeben.

### 3.1 Nösse Connect (Pflicht, genau eine Stufe)

| Code | Service-ID | Bezeichnung | VK | Einheit | SLA (P1/P2) |
|------|-----------|-------------|---:|---------|-------------|
| S01 | NOS-CON-BAS-01-STD | Nösse Connect Standard | 249,00 € | pro Kunde | 4 h / 8 h |
| S01 | NOS-CON-BAS-01-PRM | Nösse Connect Premium | 699,00 € | pro Kunde | 2 h / 4 h |
| S01 | NOS-CON-BAS-01-ENT | Nösse Connect Enterprise | 1.790,00 € | pro Kunde | 1 h / 2 h, Notfall 24/7 |

### 3.2 User as a Service (pro Benutzer)

| Code | Service-ID | Bezeichnung | VK | Enthält |
|------|-----------|-------------|---:|---------|
| B01 | NOS-USR-BND-01-STD | User as a Service Standard | 31,90 € | S02, S03, S04 |
| B02 | NOS-USR-BND-02-PRM | User as a Service Premium | 54,90 € | B01 + S05, S06, S07 |
| S02 | NOS-USR-SVC-02-XDR | Endpoint Protection (XDR) & Richtlinienmanagement | 14,90 € | |
| S03 | NOS-USR-SVC-03-PAT | Patch Management | 14,90 € | |
| S04 | NOS-USR-SVC-04-MON | Monitoring | 7,90 € | |
| S05 | NOS-USR-SVC-05-MBU | M365 Backup | 9,90 € | |
| S06 | NOS-USR-SVC-06-MES | E-Mail Security & Archivierung | 12,90 € | |
| S07 | NOS-USR-SVC-07-AUT | Sicheres Authentifizieren | 8,00 € | |

### 3.3 Server as a Service (pro Server)

| Code | Service-ID | Bezeichnung | VK | Enthält |
|------|-----------|-------------|---:|---------|
| B03 | NOS-SRV-BND-03-STD | Server as a Service Standard | 49,90 € | S11, S12, S13 |
| B04 | NOS-SRV-BND-04-PRM | Server as a Service Premium | B03 + individuelles Backup | B03 + S14 |
| S11 | NOS-SRV-SVC-11-XDR | Endpoint Protection (XDR) & Richtlinienmanagement | 19,90 € | |
| S12 | NOS-SRV-SVC-12-PAT | Patch Management | 19,90 € | |
| S13 | NOS-SRV-SVC-13-MON | Monitoring | 19,90 € | |
| S14 | NOS-SRV-SVC-14-BCK | Server Backup | Baukasten, siehe 3.8 | |

### 3.4 Security as a Service

| Code | Service-ID | Bezeichnung | VK | Einheit | Enthält |
|------|-----------|-------------|---:|---------|---------|
| B05 | NOS-SEC-BND-05-STD | Security as a Service Standard | 199,90 € | pro Kunde | S21, S22, S23 |
| B06 | NOS-SEC-BND-06-PRM | Security as a Service Premium | 1.590,00 € | pro Kunde | B05 + S24 + S25 |
| S21 | NOS-SEC-SVC-21-FWL | Firewall Betrieb & Regelwerksmanagement | 129,90 € | pro Firewall | |
| S22 | NOS-SEC-SVC-22-MTS | M365 Tenant Baseline Management & Audit | 49,90 € | pro Tenant | |
| S23 | NOS-SEC-SVC-23-ADS | AD Baseline & Security Management | 49,90 € | pro AD-Umgebung | |
| S24 | NOS-SEC-SVC-24-ISM | ITSB Security Manager | 1.190,00 € | pro Kunde | |
| S25 | NOS-SEC-SVC-25-VUL | Schwachstellenmanagement | 499 € + 12 €/Client + 69 €/Server | pro Kunde + assetabhängig | |

### 3.5 Network as a Service

| Code | Service-ID | Bezeichnung | VK | Einheit | Enthält |
|------|-----------|-------------|---:|---------|---------|
| B07 | NOS-NET-BND-07-SWI | Network as a Service (Switch) | 49,90 € | pro Switch | S51 + S53 |
| B07 | NOS-NET-BND-07-WIF | Network as a Service (Access Point) | 41,90 € | pro AP | S52 + S53 |
| S51 | NOS-NET-SVC-51-SWI | Switch | 44,90 € | pro Switch | |
| S52 | NOS-NET-SVC-52-WIF | WiFi | 35,90 € | pro Access Point | |
| S53 | NOS-NET-SVC-53-MON | Network Monitoring | 9,90 € | pro Netzwerkgerät | |

### 3.6 Add-ons

| Code | Service-ID | Bezeichnung | VK | Einheit |
|------|-----------|-------------|---:|---------|
| S31 | NOS-ADD-SVC-31-MDR | Managed Detection and Response (MDR) | 49,00 € / 79,00 € | pro User / pro Server |
| S32 | NOS-ADD-SVC-32-AWR | Security Awareness Training | 11,90 € | pro Benutzer |
| S33 | NOS-ADD-SVC-33-MDM | Mobile Device Management | 19,90 € | pro Device |
| S35 | NOS-ADD-SVC-35-NAS | NAS | 49,90 € | pro NAS-System |

### 3.7 Optionen und Sonderfälle

| Code | Bezeichnung | Preisbildung |
|------|-------------|--------------|
| S60 | Supportkontingent (NOS-SUP-OPT-60-KON) | Rechner, siehe 4.2. Voraussetzung S01 |
| S41 | Strategische IT-Begleitung | Pauschal/Monat, Preis nach Vereinbarung (Referenz: 350 €); Roadmap-Erstellung 2.400 € einmalig. **In v1 enthalten** |
| S61 | Cloud Server-Bereitstellung (TERRA Cloud) | Nicht im Mastersheet; Preis aus dem TERRA-Kalkulator. **In v1 enthalten**, siehe 5.2 |
| – | Zukünftige Services (IT-Dokumentation, NAC, Pentesting, Backupkonzept, Compliance-Audit) | **Nicht verkaufen**, im Kalkulator nicht auswählbar |

### 3.8 Server Backup (S14): Baukasten V5.7, Stand 23.09.2026

Das Mastersheet (15.07.) führt S14 noch als „auf Anfrage“. Seit Version 5.x gibt es
eine **vollständig berechenbare Preislogik**:

| Pos. | Baustein | Einheit | VK |
|------|----------|---------|---:|
| S1 | Grundpauschale Backup-Betrieb | je Backup-Umgebung/Monat | 99,00 € |
| S2 | Gesicherter Server | je Server/Monat | 15,00 € |
| F1 | Cloud-Backup-Paket (bis 500 GB nativ geschützt) | je Paket/Monat | 69,00 € |
| F2 | Objektspeicher (Object Lock) | je TB belegt/Monat | 17,90 € |
| F3 | Backup-Software-Lizenz als MSP-Miete (nur Objektspeicher, nur wenn über uns) | je Instanz/Monat | 19,60 € |
| F4 | Initialimport/Datenexport | einmalig | auf Anfrage |

- **Variante Cloud-Backup:** 99 € + 15 € × Server + 69 € × ⌈nativ geschützte GB / 500⌉. Mindestens ein Paket.
- **Variante Objektspeicher:** 99 € + 15 € × Server + 17,90 € × belegte TB (+ 19,60 € × Instanzen, wenn Lizenz über uns).
- Server aus S61 → zwingend Cloud-Backup.
- Umfangreiche Checkliste vor jedem Angebot (Abschnitt 10 des Baukastens). Diese Prüfungen bieten sich als Pflichtfragen im Kalkulator an.

## 4. Berechnungsregeln aus dem Vertriebskalkulator

### 4.1 Positionen

- Monatlicher Positionsbetrag = `RUNDEN(VK × Menge; 2)`; Menge 0 → 0.
- Gesamt monatlich = Summe aller Positionen.
- „Auf Anfrage“-Positionen werden manuell eingetragen. **Ziel:** Mit dem S14-Baukasten entfällt das weitgehend.

### 4.2 Supportkontingent (S60)

| Schritt | Formel |
|---------|--------|
| Rechnerischer Bedarf (AE/Monat) | `RUNDEN(Anfragen/Monat × Ø AE je Anfrage; 1)` (Erfahrungswert 2 AE) |
| Empfohlenes Kontingent | `OBERGRENZE(Bedarf; 2)` (auf 2er-Block aufgerundet) |
| Stunden/Monat | Kontingent / 4 |
| Kontingent-Satz | **30,38 € je AE** (33,75 € − 10 % fester Commitment-Rabatt, nicht verhandelbar) |
| Monatspreis | `RUNDEN(Kontingent × 30,38; 2)` |
| Ad-hoc-Vergleich | `RUNDEN(Kontingent × 33,75; 2)` |

### 4.3 Onboarding (einmalig, abhängig von Connect-Stufe und Anzahl **User**; bestätigt 25.09.2026)

| Größe | Arbeitsplätze | Standard | Premium | Enterprise |
|-------|---------------|---------:|--------:|-----------:|
| XS | bis 30 User | 900 € | 1.200 € | 2.200 € |
| S | 31–100 | 1.400 € | 2.000 € | 3.000 € |
| M | 101–250 | 2.000 € | 2.800 € | 4.200 € |
| L | 251–500 | 2.800 € | 3.800 € | 5.500 € |
| – | ab 501 User | individuelle Projektkalkulation | | |

Bundles erheben kein eigenes Onboarding. Das ist in der Connect-Pauschale enthalten (LS B01, Ziffer 5.2).

### 4.4 Vorher/Nachher (Bestandskunden-Migration)

Alter Monatspreis (manuelle Eingabe) gegen neuen Monatspreis aus der Kalkulation,
mit Differenz in € und %, plus Onboarding. Die Argumentationslinie „Wert vor Preis“
steht im Vertriebs-Handbuch, Abschnitt 10.

## 5. Vertragsarchitektur und Dokumentzuordnung

Rangfolge laut Grundvertrag § 1 Abs. 3: **AVV → Grundvertrag → AVB → Anlage SLA → S01 → Bundle-Leistungsscheine → Einzel-Leistungsscheine.**

| Dokument | Datei (03_Vertragswerk) | Wann beilegen | Variable Inhalte |
|----------|-------------------------|---------------|------------------|
| Grundvertrag | `Rahmen/Rahmenvertrag 01 - Grundvertrag V1.0.docx` | immer | Firma und Anschrift, Vertragsnummer, Vertragsbeginn, **§ 3 Vergütungstabelle** (Scheinnummer, Bezeichnung, Menge, Einzelpreis, Gesamtpreis, Summe), Anlagenliste § 6 |
| AVB | `Rahmen/Rahmenvertrag 02 - AVB Cloud und Managed Services V1.0.docx` | immer | keine |
| Anlage SLA | `Rahmen/Rahmenvertrag 03 - Anlage SLA V2.0.docx` | immer | enthält alle drei Stufen; gewählte Stufe ergibt sich aus S01 |
| AVV | **nicht im Ordner gefunden** | immer (Rang 1) | offen, siehe Abschnitt 9 |
| Leistungsschein S01 | `Leistungsscheine/Leistungsschein S01 - Noesse Connect V1.0.docx` | immer | gewählte Stufe |
| Bundle-Leistungsschein | `Bundles/Bundle Bxx - … V1.0.docx` | je gebuchtem Bundle | Datum („Leverkusen, [Datum]“) |
| Einzel-Leistungsschein | `Leistungsscheine/Leistungsschein Sxx - … V1.0.docx` | je gebuchtem Einzelservice **und je Service, der in einem gebuchten Bundle enthalten ist** („Die Einzel-Leistungsscheine sind als Anlage diesem Bundle-Leistungsschein beigefügt“) | ggf. Datum/Kunde |
| S14 | `Leistungsscheine/Leistungsschein S14 - Backup (Sonderfall)/Vorlage Leistungsschein S14 V5.7.docx` | bei Server Backup bzw. B04 | Variante ankreuzen, Ziffer 1.2 nur bei S61, Kalkulation in Ziffer 6.1, Serverliste in Ziffer 6.2 |
| S60 | `Leistungsscheine/Leistungsschein S60 - Supportkontingent V1.0.docx` | bei gebuchtem Supportkontingent | Kontingent (AE), Monatspreis |

### 5.1 Auflösung der Bundles im Vertragspaket (bestätigt 25.09.2026)

- B-Scheine sind Bundles. Sie setzen sich aus S-Scheinen zusammen, die im B-Schein unter Ziffer 2 aufgeführt sind.
- **Hinter jeden B-Schein gehören die zu ihm gehörenden S-Scheine.**
- Nennt ein B-Schein ein anderes Bundle (B02 → B01, B04 → B03, B06 → B05), wird dieses **nicht** als eigener B-Schein beigelegt, sondern direkt in seine S-Scheine aufgelöst.
- **S01 ist immer zu beauftragen.** Die gewählte Stufe (Standard/Premium/Enterprise) muss im Vertrag sichtbar sein: im Grundvertrag § 3, im Leistungsschein S01 und in der Anlage SLA.

| Bundle | Beizulegende S-Scheine |
|--------|------------------------|
| B01 User Standard | S02, S03, S04 |
| B02 User Premium | S02, S03, S04, S05, S06, S07 |
| B03 Server Standard | S11, S12, S13 |
| B04 Server Premium | S11, S12, S13, S14 |
| B05 Security Standard | S21, S22, S23 |
| B06 Security Premium | S21, S22, S23, S24, S25 |
| B07 Network Standard | S51, S52, S53 |

**Beispiel:** Gebucht sind Connect Premium, B02, B05 und S31. Das Vertragspaket hat dann diese Reihenfolge:

1. AVV
2. Grundvertrag
3. AVB
4. Anlage SLA (Stufe Premium)
5. S01 (Premium)
6. B02 → S02, S03, S04, S05, S06, S07
7. B05 → S21, S22, S23
8. S31

Wird ein Einzelservice zusätzlich zu einem Bundle gebucht, das ihn bereits enthält, wird sein S-Schein nur einmal beigelegt (hinter dem Bundle).

### 5.2 Weitere Leistungsscheine mit Besonderheiten

| Schein | Besonderheit |
|--------|--------------|
| S25 Schwachstellenmanagement | Grundservice 499 €/Kundenumgebung + 12 €/Client + 69 €/Server. **Voraussetzung: S01 und ein aktives Bundle B01, B02, B03 oder B04.** In B06 enthalten; die Assetpreise kommen dort zusätzlich hinzu |
| S41 Strategische IT-Begleitung | Pauschal pro Monat, Preis „[nach Vereinbarung]“ (im AAV-Angebot 350 €). Optional Roadmap-Erstellung 2.400 € einmalig. Laut Leistungsschein ohne Connect buchbar, das widerspricht der S01-Pflicht (offene Frage 9.1). Nicht Teil eines Bundles |
| S61 Cloud Server (TERRA Cloud) | Buchungsübersicht Ziffer 7.1 (Server Windows/Linux, vCores, RAM, Speicher, IPs, VLANs, Connectoren, Firewall-Appliance, Lizenzen RDS/Office/Exchange/SQL). Gesamtvergütung aus dem TERRA-Kalkulator. **Voraussetzungen:** S01 und **S21 als zusätzliche Firewall-Instanz**. Datensicherung per Ankreuzen: S14 oder Kunde selbst. S11–S13/B03/B04 nur für Windows-Server |
| B04 / B06 | Bundle-Preis im Schein als „[Preis nach Vereinbarung]“. Wird aus der Kalkulation befüllt (offene Frage 9.4) |

## 6. Interne Kalkulation (EK, Deckungsbeitrag). Nur für Führung und Controlling

Quelle: `00_Governance/Kalkulation EK und Deckungsbeitrag V1.0.xlsx`. Laut
Entscheidung 34 im Entscheidungslog ist der EK **strikt vom Vertrieb getrennt**.
In der Anwendung ist er deshalb nur für die Rollen Führung und Produktmanagement sichtbar.

- Parameter: VK-Verrechnungssatz 135 €/h; EK-Anteil 45 % → EK-Kostensatz 60,75 €/h; Rohmarge Dienstleistung 55 %.
- Kosten je Service = EK/Lizenz + (Aufwand Min./60 × 60,75 €) + Overhead; DB = VK − Kosten; Marge = DB / VK.
- RMM-Regel: Die NinjaOne-Lizenz (1,50 €) wird **einmal je Gerät** gezählt. Im Bundle wird sie dedupliziert (−1,50 €).

| Position | Kosten | VK | DB | Marge |
|----------|-------:|---:|---:|------:|
| Connect Standard / Premium / Enterprise | 75,94 / 248,00 / 485,88 € | 249 / 699 / 1.790 € | 173,06 / 451,00 / 1.304,12 € | 70 / 65 / 73 % |
| B01 / B02 User Standard / Premium | 13,64 / 27,10 € | 31,90 / 54,90 € | 18,26 / 27,80 € | 57 / 51 % |
| B03 Server Standard | 20,08 € | 49,90 € | 29,82 € | 60 % |
| B05 / B06 Security Standard / Premium | 76,74 / 642,37 € | 199,90 / 1.590 € | 123,16 / 947,63 € | 62 / 60 % |
| B07 Switch / AP | 18,18 / 15,14 € | 49,90 / 41,90 € | 31,72 / 26,76 € | 64 / 64 % |
| S31 MDR User / Server | 31,66 / 48,77 € | 49 / 79 € | 17,34 / 30,23 € | 35 / 38 % (bewusst akzeptiert) |

Die vollständige Tabelle aller Einzelservices steht in der Quelldatei. S35 (NAS) hat noch keinen EK.

## 7. Weitere Fachregeln

| Nr. | Regel | Quelle |
|-----|-------|--------|
| R1 | Genau eine Connect-Stufe je Kalkulation (Pflicht) | Vertriebs-Handbuch, Grundvertrag § 1 Abs. 2 |
| R2 | Alle Bundles, Einzelservices und S60 setzen S01 voraus | LS B01 Ziffer 3 a, Entscheidung 30 |
| R3 | Bundle und enthaltener Einzelservice auf derselben Einheit → nur Bundle-Preis | LS B01 Ziffer 3 c |
| R4 | B04 = B03 + S14 (Server Backup nach Baukasten) | Mastersheet, EK-Kalkulation |
| R5 | S61-Server → S14 zwingend in der Variante Cloud-Backup, S21 zwingend | S14 Baukasten 6.3.1, 7.2 |
| R6 | Zukünftige Services dürfen nicht angeboten werden | Mastersheet |
| R6a | S25 setzt ein aktives Bundle B01, B02, B03 oder B04 voraus | LS S25 Ziffer 1.2 b |
| R6b | S61 setzt S21 voraus (zusätzliche Firewall-Instanz); Backup-Entscheidung ist Pflicht (S14 oder Kunde) | LS S61 Ziffer 4.2, 4.4 |
| R7 | Vertragslaufzeit 12 Monate, automatische Verlängerung um 12 Monate, Kündigungsfrist 3 Monate | Grundvertrag § 2 |
| R8 | Abrechnung monatlich im Voraus, Zahlungsziel 14 Tage netto | Grundvertrag § 4 |
| R9 | Mengenreduktion max. 10 % je Quartal | Grundvertrag § 5 |

## 8. Festgestellte Inkonsistenzen in den Quellen

| Nr. | Befund | Auswirkung |
|-----|--------|------------|
| I1 | Das Mastersheet führt S14/B04 als „auf Anfrage“, der S14-Baukasten V5.7 hat eine feste Preislogik | Klären, ob der Baukasten maßgeblich ist |
| I2 | Der Vertriebskalkulator führt S25 nur mit 499 €/Kunde. Das Mastersheet nennt zusätzlich 12 €/Client und 69 €/Server | Assetabhängige Komponente fehlt im Excel |
| I3 | Das Mastersheet sagt „Standard-Onboarding bis ~60–70 AP; größere Umgebungen individuell“. Die Staffel reicht dagegen bis 500 AP | Grenze für „individuell“ klären |
| I4 | Der S14-Baukasten nennt als Preisuntergrenze sowohl 183 € (Abschnitt 5) als auch 187 € (Abschnitte 7.3 und 10 d) | Richtigen Wert bestätigen |
| I5 | Die Beispieltabelle in § 3 des Grundvertrags enthält veraltete Daten (S34 Managed WiFi, B05 mit Menge 4) | Die Vorlage muss vor der Automatisierung auf Platzhalter umgestellt werden |
| I6 | Das Mastersheet nennt für B02 den Preis „54,9“ ohne Formatierung | kosmetisch |
| I7 | Die AVV ist im Vertragswerk-Ordner nicht vorhanden, wird aber als vorrangige Anlage referenziert | Wird nachgereicht |
| I8 | Leistungsschein B05 Ziffer 5.1 nennt „199,90 € pro Firewall-Instanz“, Überschrift und Mastersheet sagen „pro Kunde“ | Leistungsschein korrigieren |
| I9 | Leistungsschein S41 erlaubt die Buchung ohne Connect, laut Vertrieb ist S01 immer Pflicht | Klärung offene Frage 9.1 |
| I10 | Einheitenbezeichnungen weichen ab: S22 „Tenant“ (Mastersheet) vs. „Mandant“ (B05); S23 „AD-Umgebung“ vs. „AD-Forest“ | kosmetisch, vereinheitlichen |

## 9. Offene Punkte aus der Analyse

Übernommen in [02_offene-fragen.md](02_offene-fragen.md), Abschnitt 8.
