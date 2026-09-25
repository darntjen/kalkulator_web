# 02 – Offene Fragen

> Antworten bitte direkt hier eintragen (Feld **Antwort**) oder im Chat geben.
> Die Übertragung in die Anforderungen und Entscheidungen erfolgt danach.
>
> Dringlichkeit: 🔴 blockiert die Planung · 🟠 wird bis zum Architekturentscheid gebraucht · 🟢 kann später geklärt werden
> ✅ = beantwortet

## 1. Services und Preislogik

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 1.1 | Welche Managed Services gibt es heute? | ✅ Im SharePoint „Service-Katalog“ vollständig dokumentiert, siehe [06_ist-analyse.md](06_ist-analyse.md), Abschnitt 3 |
| 1.2 | Wie werden die Services heute kalkuliert? | ✅ Excel „Vertriebskalkulator_V1.1.xlsx“ (VK) und „Kalkulation EK und Deckungsbeitrag V1.0.xlsx“ (intern) |
| 1.3 | Nach welchen Einheiten wird abgerechnet? | ✅ pro Kunde, User, Server, Firewall, Tenant, AD-Umgebung, Switch, AP, Netzwerkgerät, Device, NAS; S14 nach Baukasten |
| 1.4 | Gibt es Service-Level-Pakete? | ✅ Connect Standard/Premium/Enterprise; Bundles Standard/Premium |
| 1.5 | Mengenstaffeln, Mindestmengen? | ✅ Keine Mengenstaffeln bei den Monatspreisen; Staffel nur beim Onboarding; S14 mindestens ein Paket |
| 1.6 | Einmalige Kosten? | ✅ Onboarding-Pauschale nach Connect-Stufe und Arbeitsplätzen; S14 F4 auf Anfrage |
| 1.7 | Abhängigkeiten? | ✅ Siehe Ist-Analyse, Abschnitt 7 (Regeln R1–R6) |
| 1.8 | Drittlizenzen? | ✅ Im EK enthalten (Sophos, Hornetsecurity, NinjaOne, TERRA), im VK eingepreist |
| 1.9 | Wer ist für Preise verantwortlich? | 🟢 Offen. Laut Entscheidungslog entscheidet „Kunde“ (vermutlich Geschäftsführung bzw. Dennis Arntjen). Bitte benennen |
| 1.10 | Marge/Deckungsbeitrag? | ✅ Ja, EK je Service liegt vor. Streng vom Vertrieb getrennt (Entscheidung 34) |

## 2. Vertriebsprozess und Rabatte

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 2.1 | Wie viele Personen nutzen den Kalkulator? | 🟢 Offen |
| 2.2 | Dürfen Vertriebler Rabatte geben? | ✅ **Nein.** Kein Rabattfeld, keine Rabattfreigabe |
| 2.3 | Laufzeiten und Zahlungsweisen? | ✅ Laut Grundvertrag: 12 Monate Erstlaufzeit, Verlängerung um 12 Monate, monatlich im Voraus |
| 2.4 | Individuelle Sonderpositionen? | 🟠 Offen, siehe Frage 8.5 |
| 2.5 | Status im Kalkulator oder in HubSpot? | ✅ **Im Kalkulator:** Die Vertriebler setzen je Kundenkalkulation einen Projektstatus. Statuswerte siehe Frage 8.3 |
| 2.6 | HubSpot-Anbindung in v1.0? | ✅ **Nein, später** |

## 3. Angebote (Word)

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 3.1 | Bestehende Angebotsvorlage? | 🔴 **Offen.** Im SharePoint keine eigene Vorlage für Managed-Services-Angebote gefunden, siehe Frage 8.1 |
| 3.2 | Textbausteine je Service? | ✅ Interne Servicebeschreibungen (02) und Leistungsscheine (03) liegen vor. Kurzbeschreibungen im Mastersheet |
| 3.3 | Schema der Angebotsnummer? | 🟠 Offen, siehe Frage 8.6 |
| 3.4 | Angebot nachträglich in Word bearbeitbar? | 🟢 Offen. Annahme: ja (.docx) |
| 3.5 | Zusätzlich PDF? | 🟢 Offen |
| 3.6 | Ablage der Angebote? | 🟢 Offen. Annahme für v1: in der Anwendung, später optional SharePoint |

## 4. Vertragsunterlagen

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 4.1 | Welche Dokumente gehören zum Vertrag? | ✅ AVV, Grundvertrag, AVB, Anlage SLA, S01, Bundle- und Einzel-Leistungsscheine, siehe Ist-Analyse, Abschnitt 5 |
| 4.2 | Was ist je Service unterschiedlich? | ✅ Leistungsscheine je gebuchtem Service/Bundle; Rahmendokumente immer gleich |
| 4.3 | Welche Felder werden befüllt? | ✅ Grundvertrag: Kunde, Anschrift, Vertragsnummer, Vertragsbeginn, § 3 Vergütungstabelle, § 6 Anlagenliste; Leistungsscheine: Datum; S14/S60: Kalkulationswerte |
| 4.4 | Ausgabeformat? | 🟠 Offen, siehe Frage 8.7 |
| 4.5 | Wer pflegt die Vertragsvorlagen? | 🟢 Offen |
| 4.6 | Individuelle Verhandlungen? | 🟢 Offen |

## 5. Statistiken

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 5.1 | Welche Kennzahlen? | 🟠 Offen, Vorschlag siehe Frage 8.8 |
| 5.2 | Wer sieht welche Zahlen? | 🟠 Offen, Vorschlag siehe Frage 8.8 |
| 5.3 | Betriebsrat/Datenschutz? | 🟢 Offen |
| 5.4 | Power BI? | 🟢 Offen. Annahme: v1 nur Dashboard und Excel-Export |

## 6. IT-Betrieb und Infrastruktur

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 6.1 | Betriebssystem Zielserver? | ✅ **Windows Server** |
| 6.2 | Docker erlaubt? | ➖ Hat sich durch Windows Server weitgehend erledigt, siehe Frage 8.2 |
| 6.3 | Anmeldung? | ✅ **Microsoft Entra ID** |
| 6.4 | Interne Zertifizierungsstelle? | 🟢 Offen |
| 6.5 | Bestehende Datenbank (SQL Server)? | 🟠 Offen, siehe Frage 8.2 |
| 6.6 | Datensicherung? | 🟢 Offen (vermutlich Veeam) |
| 6.7 | Zugriff per VPN? | 🟢 Offen |
| 6.8 | Wer betreibt die Anwendung später? | 🟠 Offen, siehe Frage 8.2 |
| 6.9 | Code im GitHub-Repository? Interne Daten erlaubt? | ✅ **Ja**, auch interne Preise und Vorlagen |

## 7. Projektrahmen

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 7.1 | Wunschtermin Go-live? | 🟢 Offen |
| 7.2 | Pilotnutzer? | 🟢 Offen |
| 7.3 | Fachliche Abnahme der Preislogik? | 🟢 Offen |
| 7.4 | Schrittweise Einführung? | 🟢 Offen |

## 8. Neue Fragen aus der SharePoint-Analyse (25.09.2026)

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 8.1 🔴 | **Angebotsvorlage:** Gibt es eine Word-Vorlage mit Briefkopf und Corporate Design für Angebote? | Falls nein: Ich baue eine Vorlage im Nösse-Design, orientiert an bestehenden Angeboten (z. B. Angebot Servertransformation AAV) | |
| 8.2 🔴 | **Technik auf Windows Server:** Gibt es einen SQL Server (auch Express)? Wer betreibt den Server (interne IT)? | Empfehlung: ASP.NET Core auf IIS + SQL Server, siehe [04_architektur.md](04_architektur.md) | |
| 8.3 🔴 | **Projektstatus:** Welche Statuswerte soll der Vertrieb setzen können? | Entwurf → Angebot versendet → In Verhandlung → Gewonnen / Verloren (mit Grund) / Zurückgestellt | |
| 8.4 🟠 | **AVV:** Wo liegt die aktuelle Auftragsverarbeitungsvereinbarung? Sie fehlt im Ordner 03_Vertragswerk | – | |
| 8.5 🟠 | **Sonderfälle in v1:** Welche der folgenden Positionen soll der Kalkulator in Version 1 abbilden? S14 Server Backup nach Baukasten V5.7 (ist voll berechenbar), S25 assetabhängig (12 €/Client, 69 €/Server), S61 Cloud Server, S41 Strategische IT-Begleitung, freie Sonderpositionen | S14 und S25 ja; S61 und S41 später; freie Sonderpositionen nein | |
| 8.6 🟠 | **Nummernkreise:** Wie sehen Angebotsnummer und Vertragsnummer aus? Kommen sie aus dem ERP? | Vorschlag: `MS-A-2026-0001` (Angebot), `MS-V-2026-0001` (Vertrag), vergeben durch die Anwendung | |
| 8.7 🟠 | **Vertragspaket:** Einzelne Word-Dateien als ZIP, ein zusammengeführtes Dokument oder zusätzlich PDF? | v1: ZIP mit befüllten Word-Dateien und Deckblatt/Anlagenverzeichnis; PDF später | |
| 8.8 🟠 | **Statistik-Rechte:** Wer sieht was? | Vertrieb: eigene Kalkulationen und eigene Kennzahlen; Führung: alles inkl. DB/Marge; Produktmanagement: Katalog, Preise, EK | |
| 8.9 🟠 | **Onboarding-Grenze:** Ab wie vielen Arbeitsplätzen ist das Onboarding individuell: ab 70 (Mastersheet) oder ab 501 (Staffel)? Und was zählt als „Arbeitsplatz“: User oder Endgeräte? | – | |
| 8.10 🟠 | **B02 im Vertragspaket:** Wird bei B02 (User Premium) der Leistungsschein B01 mit beigelegt, oder nur B02 plus S02–S07? Analog B06/B05 | – | |
| 8.11 🟢 | **S14-Preisuntergrenze:** 183 € oder 187 €? (Der Baukasten nennt beide Werte) | – | |
| 8.12 🟢 | **Katalogpflege:** Soll die Anwendung nach dem Go-live die maßgebliche Preisquelle sein? Das Mastersheet würde dann aus der Anwendung exportiert | Ja, sonst laufen zwei Preislisten auseinander | |
| 8.13 🟢 | **Vorher/Nachher-Vergleich** für Bestandskunden-Migration in v1? | Ja (geringer Aufwand, hoher Nutzen) | |
