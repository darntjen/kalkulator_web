# 07 – Referenzkalkulationen

> Status: **Entwurf. Vom Fachbereich zu bestätigen.**
> Die Fälle sind nach den Regeln aus dem Vertriebskalkulator V1.1 und dem
> S14-Baukasten V5.7 berechnet ([06_ist-analyse.md](06_ist-analyse.md)). Nach der
> Bestätigung werden sie zu automatisierten Tests. Der Kalkulator darf erst
> produktiv gehen, wenn er alle Fälle centgenau trifft.

Alle Beträge netto.

## RK-01 – Kleinkunde, Connect Standard

| Position | Menge | VK | Monatlich |
|----------|------:|---:|----------:|
| S01 Nösse Connect Standard | 1 | 249,00 € | 249,00 € |
| B01 User as a Service Standard | 15 | 31,90 € | 478,50 € |
| B03 Server as a Service Standard | 2 | 49,90 € | 99,80 € |
| B05 Security as a Service Standard | 1 | 199,90 € | 199,90 € |
| **Summe monatlich** | | | **1.027,20 €** |
| Onboarding (Standard, 15 AP → XS) | | | 900,00 € einmalig |
| Jahreswert (12 × monatlich) | | | 12.326,40 € |
| Wert der Erstlaufzeit (12 Monate + Onboarding) | | | 13.226,40 € |

## RK-02 – Mittelstand, Connect Premium mit Add-ons und Supportkontingent

| Position | Menge | VK | Monatlich |
|----------|------:|---:|----------:|
| S01 Nösse Connect Premium | 1 | 699,00 € | 699,00 € |
| B02 User as a Service Premium | 40 | 54,90 € | 2.196,00 € |
| B03 Server as a Service Standard | 4 | 49,90 € | 199,60 € |
| B05 Security as a Service Standard | 1 | 199,90 € | 199,90 € |
| S31 MDR (User) | 40 | 49,00 € | 1.960,00 € |
| S32 Security Awareness Training | 40 | 11,90 € | 476,00 € |
| S60 Supportkontingent (6 Anfragen × 2 AE = 12 AE) | 12 AE | 30,38 € | 364,56 € |
| **Summe monatlich** | | | **6.095,06 €** |
| Onboarding (Premium, 40 AP → S) | | | 2.000,00 € einmalig |

## RK-03 – Connect Enterprise mit Netzwerk

| Position | Menge | VK | Monatlich |
|----------|------:|---:|----------:|
| S01 Nösse Connect Enterprise | 1 | 1.790,00 € | 1.790,00 € |
| B07 Network as a Service (Switch) | 6 | 49,90 € | 299,40 € |
| B07 Network as a Service (AP) | 10 | 41,90 € | 419,00 € |
| S53 Network Monitoring | 3 | 9,90 € | 29,70 € |
| S21 Firewall Betrieb & Regelwerksmanagement | 2 | 129,90 € | 259,80 € |
| **Summe monatlich** | | | **2.797,90 €** |

## RK-04 – Supportkontingent mit Rundung

| Eingabe/Schritt | Wert |
|-----------------|------|
| Geschätzte Anfragen/Monat | 3 |
| Ø AE je Anfrage | 2,5 |
| Rechnerischer Bedarf | 7,5 AE |
| Kontingent (auf 2er-Block aufgerundet) | 8 AE (= 2,0 Std.) |
| Monatspreis (8 × 30,38 €) | **243,04 €** |
| Ad-hoc-Vergleich (8 × 33,75 €) | 270,00 € |
| Kundenvorteil | 26,96 € |

Kontrollfall aus dem Vertriebs-Handbuch: 6 Anfragen × 2 AE = 12 AE → **364,56 €** (ad hoc 405,00 €).

## RK-05 – Onboarding-Grenzfälle

| Connect-Stufe | User | Erwartung |
|---------------|--------------:|-----------|
| Enterprise | 30 | XS → 2.200,00 € |
| Enterprise | 31 | S → 3.000,00 € |
| Premium | 250 | M → 2.800,00 € |
| Standard | 251 | L → 2.800,00 € |
| Standard | 501 | individuell (Projektkalkulation), kein Betrag |

## RK-06 – Server Backup (S14), Beispiele aus dem Baukasten V5.7

| Profil | Variante | Rechnung | Monatlich |
|--------|----------|----------|----------:|
| 1 Server, 200 GB | Cloud | 99 + 15 + 1 × 69 | 183,00 € |
| 3 Server, 400 GB | Cloud | 99 + 3 × 15 + 1 × 69 | 213,00 € |
| 5 Server, 1 TB | Cloud | 99 + 5 × 15 + 2 × 69 | 312,00 € |
| 8 Server, 2 TB | Cloud | 99 + 8 × 15 + 4 × 69 | 495,00 € |
| 12 Server, 4 TB nativ | Cloud | 99 + 12 × 15 + 8 × 69 | 831,00 € |
| 12 Server, 6 TB belegt, Lizenz über uns | Objektspeicher | 99 + 12 × 15 + 6 × 17,90 + 12 × 19,60 | 621,60 € |
| 12 Server, 6 TB belegt, Kundenlizenz | Objektspeicher | 99 + 12 × 15 + 6 × 17,90 | 386,40 € |

## RK-07 – Regelprüfungen (keine Beträge)

| Fall | Erwartete Reaktion des Kalkulators |
|------|------------------------------------|
| Keine Connect-Stufe gewählt | Fehler: Connect ist Pflicht. Kein Angebot möglich |
| Zwei Connect-Stufen gewählt | Fehler: genau eine Stufe |
| B01 × 20 und S03 × 20 gewählt | Hinweis: S03 ist in B01 enthalten, wird nicht zusätzlich berechnet |
| Zukünftigen Service gewählt | nicht auswählbar |
| B04 gewählt | S14-Baukasten muss ausgefüllt werden |

## RK-08 – Auflösung des Vertragspakets

| Gebucht | Erwartetes Paket (Reihenfolge) |
|---------|--------------------------------|
| S01 Premium, B02 × 40, B05, S31 × 40 | AVV · Grundvertrag · AVB · SLA (Premium) · S01 (Premium) · B02 · S02 · S03 · S04 · S05 · S06 · S07 · B05 · S21 · S22 · S23 · S31 |
| S01 Standard, B06, S25-Assets 20 Clients | AVV · Grundvertrag · AVB · SLA (Standard) · S01 (Standard) · B06 · S21 · S22 · S23 · S24 · S25. **Zu klären:** Gilt die Voraussetzung aus LS S25 (aktives Bundle B01–B04) auch, wenn S25 über B06 gebucht wird? Falls ja → Hinweis/Fehler |
| S01 Standard, B01 × 10, S03 × 10 | … · B01 · S02 · S03 · S04 (S03 nicht doppelt; nicht zusätzlich berechnet) |
| S01 Enterprise, S61, S21, B04 × 3, S14 | … · S01 (Enterprise) · B04 · S11 · S12 · S13 · S14 · S21 · S61 |

## RK-09 – Schwachstellenmanagement (S25)

| Gebucht | Rechnung | Monatlich |
|---------|----------|----------:|
| S25 einzeln, 30 Clients, 3 Server (mit B01 und B03) | 499 + 30 × 12 + 3 × 69 | 1.066,00 € |
| B06 + S25-Assets 30 Clients, 3 Server | 1.590 + 30 × 12 + 3 × 69 | 2.157,00 € |
