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
| 2.4 | Individuelle Sonderpositionen? | ✅ Ja, ab Version 1. Regeln siehe Frage 9.3 |
| 2.5 | Status im Kalkulator oder in HubSpot? | ✅ **Im Kalkulator:** Die Vertriebler setzen je Kundenkalkulation einen Projektstatus (siehe Frage 8.3) |
| 2.6 | HubSpot-Anbindung in v1.0? | ✅ **Nein, später** |

## 3. Angebote (Word)

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 3.1 | Bestehende Angebotsvorlage? | ✅ Keine vorhanden; Entwurf erstellt, siehe [08_angebotsvorlage.md](08_angebotsvorlage.md) |
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
| 6.5 | Bestehende Datenbank (SQL Server)? | ✅ Ja, SQL Server vorhanden |
| 6.6 | Datensicherung? | 🟢 Offen (vermutlich Veeam) |
| 6.7 | Zugriff per VPN? | 🟢 Offen |
| 6.8 | Wer betreibt die Anwendung später? | ✅ Die interne IT |
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
| 8.1 | **Angebotsvorlage:** Gibt es eine Word-Vorlage mit Briefkopf und Corporate Design für Angebote? | – | ✅ Keine vorhanden. Entwurf erstellen; kann später gegen eine eigene Vorlage ausgetauscht werden → [08_angebotsvorlage.md](08_angebotsvorlage.md) |
| 8.2 | **Technik auf Windows Server:** Gibt es einen SQL Server? Wer betreibt den Server? | – | ✅ SQL Server vorhanden; Betrieb durch die interne IT → [ADR-0002](adr/0002-technologie-stack.md) |
| 8.3 | **Projektstatus:** Welche Statuswerte? | – | ✅ Entwurf, Angebot versendet, Vertrag erstellt, Gewonnen, Verloren (mit Grund), Zurückgestellt |
| 8.4 🟢 | **AVV:** Wo liegt die aktuelle Auftragsverarbeitungsvereinbarung? | – | ⏳ Wird später nachgeliefert. Bis dahin wird ohne AVV geplant und entwickelt; der Platz im Vertragspaket ist vorgesehen |
| 8.5 | **Sonderfälle in v1** (S14, S25 assetabhängig, S61, S41, freie Sonderpositionen) | – | ✅ **Alles ab Version 1.** Detailfragen siehe 9.1–9.3 |
| 8.6 🟠 | **Nummernkreise:** Wie sehen Angebotsnummer und Vertragsnummer aus? Kommen sie aus dem ERP? | Vorschlag: `MS-A-2026-0001` (Angebot), `MS-V-2026-0001` (Vertrag), vergeben durch die Anwendung || ✅ Übernommen (25.09.2026) |
| 8.7 🟠 | **Vertragspaket:** Einzelne Word-Dateien als ZIP, ein zusammengeführtes Dokument oder zusätzlich PDF? | v1: ZIP mit befüllten Word-Dateien und Deckblatt/Anlagenverzeichnis; PDF später || ✅ Übernommen (25.09.2026) |
| 8.8 🟠 | **Statistik-Rechte:** Wer sieht was? | Vertrieb: eigene Kalkulationen und eigene Kennzahlen; Führung: alles inkl. DB/Marge; Produktmanagement: Katalog, Preise, EK || ✅ Übernommen (25.09.2026) |
| 8.9 | **Onboarding-Grenze und Bezugsgröße** | – | ✅ Individuell ab **501**; es zählen die **User** |
| 8.10 | **Bundles im Vertragspaket** | – | ✅ B-Scheine sind Bundles aus S-Scheinen. Hinter jeden B-Schein gehören die in ihm aufgeführten S-Scheine; ein in einem B-Schein genanntes Bundle (z. B. B01 in B02) wird **nicht** als eigener B-Schein beigelegt, sondern in seine S-Scheine aufgelöst. S01 ist immer zu beauftragen; die gewählte Stufe muss im Vertrag sichtbar sein |
| 8.11 | **S14-Preisuntergrenze:** 183 € oder 187 €? (Der Baukasten nennt beide Werte) | – | ✅ **187,00 €** je Monat (bestätigt 25.09.2026); Parameter `S14_PREISUNTERGRENZE` |
| 8.12 🟢 | **Katalogpflege:** Soll die Anwendung nach dem Go-live die maßgebliche Preisquelle sein? Das Mastersheet würde dann aus der Anwendung exportiert | Ja, sonst laufen zwei Preislisten auseinander || ✅ Übernommen (25.09.2026) |
| 8.13 🟢 | **Vorher/Nachher-Vergleich** für Bestandskunden-Migration in v1? | Ja (geringer Aufwand, hoher Nutzen) || ✅ Übernommen (25.09.2026) |

## 9. Neue Fragen (Stand 25.09.2026, nach Durchsicht von S25, S41, S61 und der Bundle-Scheine)

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 9.1 🔴 | **S41 Strategische IT-Begleitung:** Der Leistungsschein sagt „Nösse Connect ist nicht Voraussetzung“ (Ziffer 1.2 b). Du hast gesagt, S01 ist immer zu beauftragen. Gilt: Jeder Vertrag enthält S01, auch wenn nur S41 gebucht wird? Und: Der Monatspreis steht auf „[Preis nach Vereinbarung]“ – im AAV-Angebot waren es 350 €. Ist 350 € der Standardpreis, oder frei durch den Vertrieb? | S01 immer Pflicht (Vertrag geht vor); S41 mit festem Katalogpreis 350 €; Roadmap-Erstellung optional 2.400 € einmalig | ✅ **S41 ist die einzige Ausnahme von der S01-Pflicht.** Offen: Preis (siehe 10.1) und Vertragspaket ohne S01 (siehe 10.2) |
| 9.2 🔴 | **S61 Cloud Server:** Der Preis entsteht im TERRA-Kalkulator. Wie soll der Kalkulator das abbilden? | Vertrieb erfasst die Buchungsübersicht (Ziffer 7.1: Server, vCores, RAM, Speicher, IPs, Lizenzen) und den **EK-Gesamtwert** aus dem TERRA-Kalkulator; der Kalkulator rechnet VK = EK ÷ 0,55 (45 % Marge, Regel aus dem S14-Baukasten). Er erzwingt S21 (zusätzliche Firewall-Instanz) und die Backup-Entscheidung (S14 oder Kunde) | ✅ Ja, wie vorgeschlagen |
| 9.3 🔴 | **Freie Sonderpositionen:** Da es keine Rabatte gibt, könnten freie Preise ein Umweg sein. Welche Regeln gelten? | Pflichtfelder Bezeichnung, Einheit, Menge, Preis und Begründung; deutlich gekennzeichnet in Angebot und Statistik; nur positive Beträge; kein eigener Leistungsschein (erscheinen nur in Angebot und § 3 Grundvertrag). Optional: Freigabe durch Vertriebsleitung | ✅ Ja, wie vorgeschlagen, **zusätzlich Freigabe durch die Vertriebsleitung** |
| 9.4 🟠 | **B04 und B06 im Vertrag:** In beiden Bundle-Scheinen steht „[Preis nach Vereinbarung]“. | B06 = 1.590,00 €/Kunde (Mastersheet) zzgl. S25-Assetpreise. B04 = 49,90 €/Server (B03-Anteil), Server Backup (S14) als eigene Zeile nach Baukasten | ✅ Einverstanden |
| 9.5 🟢 | **Inkonsistenz B05:** Leistungsschein B05 Ziffer 5.1 nennt „199,90 € pro Firewall-Instanz“, Überschrift und Mastersheet sagen „pro Kunde“. | „pro Kunde“ ist richtig; Leistungsschein korrigieren | ✅ Bleibt vorerst so; Dennis nimmt es mit. Der Kalkulator rechnet „pro Kunde“ |
| 9.6 🟢 | **Absenderdaten im Angebot:** Name, Funktion, Telefon und E-Mail des Vertrieblers automatisch aus dem Entra-ID-Profil übernehmen? | Ja || ✅ Übernommen (25.09.2026) |
| 9.7 🟢 | **Angebots-Gültigkeit:** Standardmäßig 30 Tage ab Angebotsdatum? | Ja, vom Vertrieb änderbar || ✅ Übernommen (25.09.2026) |

## 10. Neue Fragen (Stand 25.09.2026, nach den Antworten zu Abschnitt 9)

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 10.1 🟠 | **Preis S41:** Ist 350 € pro Monat der feste Katalogpreis, oder legt der Vertrieb den Preis je Kunde fest (dann wäre es eine freigabepflichtige Position)? | Fester Katalogpreis 350 €; Roadmap-Erstellung 2.400 € einmalig | ✅ Staffel nach Kundengröße: **bis 50 Mitarbeitende 350 €/Monat, ab 51 Mitarbeitenden 550 €/Monat** |
| 10.2 🟠 | **Vertrag ohne S01 (nur S41):** Grundvertrag § 1 Abs. 2 vereinbart S01 automatisch mit. Welche Dokumente bekommt ein Kunde, der nur S41 bucht? | Grundvertrag (mit angepasstem § 1 Abs. 2 bzw. Variante „ohne Connect“), AVB, AVV, S41 | ✅ S41 braucht selbstverständlich einen Grundvertrag (so war der Vorschlag auch gemeint). Offen ist nur § 1 Abs. 2 und die SLA-Stufe, siehe 10.4 und 10.5 |
| 10.3 🟢 | **Logo in höherer Auflösung:** Das verwendete Logo (Intranet-Wiki, 292 × 123 px) ist für den Druck knapp. Gibt es eine größere Fassung oder eine Vektordatei? | Bei Gelegenheit aus dem ELO bereitstellen || ⏳ Kommt noch (04.10.2026) |
| 10.4 🟠 | **§ 1 Abs. 2 Grundvertrag bei reinen S41-Verträgen:** Der Absatz vereinbart S01 automatisch mit. Soll er bei reinen S41-Verträgen entfallen, oder soll der Absatz so umformuliert werden, dass er nur gilt, wenn andere Leistungsscheine als S41 gebucht sind? | Umformulierung im Grundvertrag, damit es nur eine Fassung gibt | ✅ Wie vorgeschlagen: Umformulierung im Grundvertrag; bis dahin legt der Kalkulator den Grundvertrag unverändert bei (04.10.2026) |
| 10.5 🟠 | **SLA bei reinen S41-Verträgen:** S41 verweist auf die Anlage SLA (Ziffer 2.5). Die Reaktionszeiten hängen aber von der Connect-Stufe ab, die es hier nicht gibt. Welche Stufe gilt? | Anlage SLA beilegen; es gelten die Werte der Stufe Standard | ✅ Ja |
| 10.6 🟢 | **Bezugsgröße S41:** „Mitarbeitende“ ist eine eigene Eingabe, nicht die Zahl der User (die für das Onboarding zählt)? | Eigene Eingabe „Anzahl Mitarbeitende“ | ✅ Ja |

## 11. Neue Fragen aus der Pflegeoberfläche (04.10.2026)

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 11.1 🟠 | **Schwellen der Margen-Ampel:** Das Blatt „Preisprüfung“ nennt nur „grün = gesund (55–72 %)“; Gelb und Rot sind dort von Hand gesetzt. Welche Schwellen sollen gelten? | Grün 55–72 %, rot unter 45 %, sonst gelb (auch über 72 %: Preis prüfen). Die Werte sind als Parameter pflegbar. Abweichungen zum Excel: S05 (52 %), S06 (54 %), S11 (52 %) und S01 Enterprise (72,9 %) werden gelb statt grün, S24 (70 %) grün statt gelb | ✅ Managed Services: grün ab 45 %, gelb 38 bis unter 45 %, rot unter 38 %, keine Obergrenze (umgesetzt als Standard und in der Erstbefüllung). Alles andere (Transformationsprojekte: Hardware, Software, Dienstleistung): rot unter 12,5 %, gelb unter 22,5 %, grün darüber; wird mit Phase 5 umgesetzt (04.10.2026) |
| 11.2 🟠 | **Fehlende EK-Werte:** Für S41, S60, S35, B04 und die S14-Bausteine gibt es in der EK-Kalkulation keine Werte; Marge und Ampel bleiben dort offen. Wer liefert sie? | Produktmanagement pflegt sie im nächsten Preislisten-Entwurf. S60: 15 Minuten Aufwand je AE. S61 bleibt ohne Katalog-EK, weil der EK je Kalkulation aus dem TERRA-Kalkulator kommt | ✅ Ja, das Produktmanagement liefert sie (04.10.2026) |

## 12. Fragen zu den Vertragsvorlagen (04.10.2026, #26)

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 12.1 🟠 | **AVV:** Im Ordner `03_Vertragswerk (EXTERN)/Rahmen` liegt keine Auftragsverarbeitungsvereinbarung, laut Grundvertrag § 1 Abs. 3 ist sie aber Teil des Vertragswerks. Wo liegt sie, und wie heißt die Datei? | Ablage als `Rahmen/Rahmenvertrag 04 - AVV V1.0.docx`; der Abgleich erkennt sie dann von selbst | ⏳ Wird mit Matthias geklärt |
| 12.2 🟠 | **Markierung der Vorlagen:** Die Fassungen in SharePoint kennzeichnen variable Stellen mit `[…]` und `____`. Wer stellt Grundvertrag und S14 auf `{{…}}`-Platzhalter um? | Produktmanagement nach der Anleitung in `docs/12_vertragsvorlagen.md`, Abschnitt 4 | |
| 12.3 🟢 | **Zugriff auf SharePoint:** App-Registrierung mit `Sites.Selected` (Lesen auf „Service-Katalog“, später Schreiben auf einen Arbeitsordner für die PDF-Umwandlung). | IT richtet sie nach `docs/12_vertragsvorlagen.md`, Abschnitt 6 ein | |
| 12.4 🟠 | **Paperless einrichten:** API-Schlüssel, Arbeitsbereich und gegebenenfalls die Paperless-Vorlage (ID). Welche Rollen bzw. Slots hat die Vorlage, und wer unterschreibt für Nösse? | Rolle „Kunde“ für den Auftraggeber, die Unterschrift für Nösse regelt die Paperless-Vorlage (`AusVorlage`). Einstellungen nach `docs/12_vertragsvorlagen.md`, Abschnitt 7 | ✅ Arbeitsbereich 15114, Vorlage 50379 ohne Dokument mit den Slots „Kunde“ und „Nösse“, nacheinander (erst Kunde); die Vorlage regelt die Freigaben vor dem Versand. Für Nösse unterschreibt je Vertrag André Nösse, Matthias Erhard oder Sascha Manczak; zugeordnet wird bei der Freigabe in Paperless. Der Schlüssel kommt nur auf den Server (05.10.2026) |
| 12.5 🟠 | **Paperless-API mit echtem Schlüssel prüfen:** Upload, Unterschriftsfelder (Koordinaten) und Vorlage mit eigener PDF-Datei; die Dokumentation war bei der Umsetzung nicht abrufbar | Testdokument in einem Test-Arbeitsbereich übergeben und Lage der Felder ansehen | ⏳ Testlauf `--paperless-test` vorhanden (`docs/12_vertragsvorlagen.md`, Abschnitt 7). Ohne Schlüssel geprüft: Aufbau von `blobs` korrigiert, `documents` passt (05.10.2026); Lage der Felder und Vorlage noch offen |
| 12.6 🟠 | **Unterschriftsfelder markieren:** In welchen Vorlagen wird unterschrieben (Grundvertrag, AVV, Leistungsscheine)? | Produktmanagement setzt `{{unterschrift.Kunde}}` (und gegebenenfalls `{{unterschrift.Nösse}}`) an die Unterschriftslinien | |

## 14. Fragen zur KI-gestützten Angebotserstellung (04.10.2026)

Beantwortet am 04.10.2026: Umfang nur Projekt und Managed Services (Analyse/Workshop bleibt beim Skill), Navision als
PDF-Upload, Gestaltung nach Styleguide, Lesezugriff auf das Team „Kundenprojekte“, Leasingrate von außen, Nutzung der
Claude-API mit Kundendaten freigegeben. Konzept: [13_ki-angebotserstellung.md](13_ki-angebotserstellung.md).

| Nr. | Frage | Vorschlag | Antwort |
|-----|-------|-----------|---------|
| 14.1 🟠 | **Zeitpunkt:** KI-Texte als Phase 6b direkt nach dem Gesamtangebot statt erst in Version 2? | Ja; Phase 6 ist bis dahin mit von Hand geschriebenen Texten nutzbar | ✅ Ja (04.10.2026) |
| 14.2 🟠 | **Schreibanleitung:** Wer pflegt sie künftig, und gelten Skill und Kalkulator parallel weiter, bis Phase 6b läuft? | Vertriebsleitung pflegt; der Skill bleibt bis zur Inbetriebnahme von 6b das Werkzeug | ✅ Ja, wie vorgeschlagen (04.10.2026) |
| 14.3 🟠 | **Rohtranskripte:** Sollen neben den Zusammenfassungen in `80_Protokolle` auch die Rohtranskripte einfließen dürfen? | Nur auf ausdrückliche Auswahl je Angebot | ✅ Ja, wie vorgeschlagen (04.10.2026) |
| 14.4 🟢 | **Lesezugriff Team „Kundenprojekte“:** App-Registrierung mit `Sites.Selected`, Rolle `read` auf die Website des Teams | IT richtet ihn zusammen mit 12.3 ein | ✅ Steht im Dokument „Einrichtung durch die IT“ (Abschnitt 5.2) (04.10.2026) |
| 14.5 🟠 | **Muster für Tests:** Zwei bis drei Navision-PDFs (Projekt mit Dienstleistung, mit Alternativpositionen) für synthetische Testdateien im selben Layout | Dennis stellt sie bereit; im Repository nur synthetische Fassungen | ⏳ Dennis stellt die PDFs bereit, sobald sie für Phase 5 gebraucht werden (04.10.2026) |
