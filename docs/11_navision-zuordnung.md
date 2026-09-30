# 11 – Zuordnung zu Navision (Platzhalter)

> Status: **Arbeitsstand mit Platzhaltern** (30.09.2026)
>
> Die endgültige Liste liefert Dennis Arntjen als Excel (Gesamtkonzept,
> Abschnitte 5.4 und 13). Bis dahin gilt:
>
> - **Bestätigt:** Die Nummer stammt aus echten Navision-Angeboten vom 23. und 25.09.2026.
> - **Platzhalter:** Die Nummer ist erfunden und beginnt immer mit **9999**, damit sie nicht mit echten Artikeln verwechselt werden kann.
>
> Die Werte werden mit der Erstbefüllung (#7) in den Katalog übernommen und
> später über die Pflegeoberfläche ersetzt.

## 1. Managed Services

| Code | Preiskomponente | Navision-Artikel | Quelle |
|------|-----------------|------------------|--------|
| S01 | Nösse Connect Standard | 98252949 | bestätigt |
| S01 | Nösse Connect Premium | 99990002 | Platzhalter |
| S01 | Nösse Connect Enterprise | 99990003 | Platzhalter |
| B01 | User as a Service Standard | 99990011 | Platzhalter |
| B02 | User as a Service Premium | 98252954 | bestätigt |
| S02 | Endpoint Protection (XDR) & Richtlinienmanagement (User) | 98252951 | bestätigt |
| S03 | Patch Management (User) | 98252952 | bestätigt |
| S04 | Monitoring (User) | 98252953 | bestätigt |
| S05 | M365 Backup | 98252955 | bestätigt |
| S06 | E-Mail Security & Archivierung | 98252956 | bestätigt |
| S07 | Sicheres Authentifizieren | 98252957 | bestätigt |
| B03 | Server as a Service Standard | 98252958 | bestätigt |
| B04 | Server as a Service Premium | 99990021 | Platzhalter |
| S11 | Endpoint Protection (XDR) & Richtlinienmanagement (Server) | 98252959 | bestätigt |
| S12 | Patch Management (Server) | 98252960 | bestätigt |
| S13 | Monitoring (Server) | 98252961 | bestätigt |
| S14 | Grundpauschale Backup-Betrieb (S1) | 99990031 | Platzhalter |
| S14 | Gesicherter Server (S2) | 99990032 | Platzhalter |
| S14 | Cloud-Backup-Paket 500 GB (F1) | 99990033 | Platzhalter |
| S14 | Objektspeicher je TB (F2) | 99990034 | Platzhalter |
| S14 | Backup-Software-Lizenz (F3) | 99990035 | Platzhalter |
| B05 | Security as a Service Standard | 98252964 | bestätigt |
| B06 | Security as a Service Premium | 99990041 | Platzhalter |
| S21 | Firewall Betrieb & Regelwerksmanagement | 98252965 | bestätigt |
| S22 | M365 Tenant Baseline Management & Audit | 98252966 | bestätigt |
| S23 | AD Baseline & Security Management | 98252967 | bestätigt |
| S24 | ITSB Security Manager | 99990042 | Platzhalter |
| S25 | Schwachstellenmanagement Grundservice | 99990043 | Platzhalter |
| S25 | Schwachstellenmanagement je Client | 99990044 | Platzhalter |
| S25 | Schwachstellenmanagement je Server | 99990045 | Platzhalter |
| B07 | Network as a Service je Switch | 98252991 | bestätigt |
| B07 | Network as a Service je Access Point | 98252992 | bestätigt |
| S51 | Switch | 99990051 | Platzhalter |
| S52 | WiFi | 99990052 | Platzhalter |
| S53 | Network Monitoring | 99990053 | Platzhalter |
| S31 | MDR je User | 99990061 | Platzhalter |
| S31 | MDR je Server | 99990062 | Platzhalter |
| S32 | Security Awareness Training | 99990063 | Platzhalter |
| S33 | Mobile Device Management | 99990064 | Platzhalter |
| S35 | NAS | 99990065 | Platzhalter |
| S41 | Strategische IT-Begleitung | 99990071 | Platzhalter |
| S41 | Roadmap-Erstellung (einmalig) | 99990072 | Platzhalter |
| S60 | Supportkontingent | 99990081 | Platzhalter |
| S61 | Cloud Server-Bereitstellung | 99990091 | Platzhalter |

## 2. Onboarding

Bestätigt ist nur Standard XS. Navision schreibt dort „bis 30 AP“, der Katalog
zählt User (Gesamtkonzept, Abschnitt 5.4).

| Größe | Standard | Premium | Enterprise |
|-------|----------|---------|------------|
| XS | 98010691 (bestätigt) | 99990102 | 99990103 |
| S | 99990111 | 99990112 | 99990113 |
| M | 99990121 | 99990122 | 99990123 |
| L | 99990131 | 99990132 | 99990133 |

## 3. Dienstleistungsrollen und interne Stundensätze

Grundlage für den automatisch vorbelegten EK der Dienstleistung (Gesamtkonzept,
Abschnitt 6). 1 AE = 15 Minuten, EK je AE = Stundensatz ÷ 4.

| Rolle | Navision-Artikel | VK je AE | EK je Stunde | EK je AE | Quelle |
|-------|------------------|---------:|-------------:|---------:|--------|
| IT Service Techniker | 99990201 | 23,75 € | 45,00 € | 11,25 € | Platzhalter (VK = AE-Satz Ebene 1) |
| IT System Engineer sen. | 98034001 | 33,75 € | 60,75 € | 15,19 € | Artikel und VK bestätigt, EK = bisheriger interner Kostensatz |
| IT Consultant | 98034003 | 42,50 € | 72,00 € | 18,00 € | Artikel und VK bestätigt, EK Platzhalter |
| Projektleitung | 99990202 | 42,50 € | 72,00 € | 18,00 € | Platzhalter |

Die Stundensätze sind **erfunden**. Sie dienen nur der Entwicklung und den Tests
und dürfen nicht für echte Margenaussagen verwendet werden.

## 4. Sonstige Artikel aus den Beispielangeboten

| Navision-Artikel | Bedeutung | Art der Position |
|------------------|-----------|------------------|
| 10200100 | Sammelartikel für Hardware, Software und Herstellerservice | manuell zuordnen |
| 98177653 | Kabel (Beispiel) | Sonstiges |
