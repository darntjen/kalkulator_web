# 02 – Offene Fragen

> Diese Fragen müssen geklärt sein, bevor wir mit der Umsetzung beginnen.
> Antworten bitte direkt hier eintragen (Feld **Antwort**) oder im Chat geben.
> Die Übertragung in die Anforderungen und Entscheidungen erfolgt danach.
>
> Dringlichkeit: 🔴 blockiert die Planung · 🟠 wird bis zum Architekturentscheid gebraucht · 🟢 kann später geklärt werden

## 1. Services und Preislogik 🔴

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 1.1 | Welche Managed Services gibt es heute? Bitte vollständige Liste, gern mit Kategorien (z. B. Managed User, Managed Client, Managed Server, Security, Backup, Netzwerk …). | |
| 1.2 | Wie werden die Services heute kalkuliert (Excel, ERP, Erfahrung)? Kann die bestehende Kalkulationsgrundlage bereitgestellt werden? | |
| 1.3 | Nach welchen Einheiten wird abgerechnet (pro User, pro Gerät, pro Server, pauschal, nach Aufwand)? | |
| 1.4 | Gibt es Service-Level-Pakete (z. B. Basis/Standard/Premium) mit unterschiedlichen Reaktionszeiten? | |
| 1.5 | Gibt es Mengenstaffeln, Mindestmengen oder Mindestvertragswerte? | |
| 1.6 | Gibt es einmalige Kosten (Onboarding, Einrichtung, Transition)? Wie werden sie berechnet? | |
| 1.7 | Gibt es Abhängigkeiten zwischen Services (X nur zusammen mit Y)? | |
| 1.8 | Sind Drittlizenzen enthalten (z. B. Microsoft 365, Security-Lösungen), deren Einkaufspreise schwanken? | |
| 1.9 | Wie oft ändern sich Preise, und wer ist dafür verantwortlich? | |
| 1.10 | Soll die Marge bzw. der Deckungsbeitrag berechnet werden? Liegen die internen Kosten je Service vor? | |

## 2. Vertriebsprozess und Rabatte 🔴

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 2.1 | Wie viele Personen werden den Kalkulator nutzen (Vertrieb, Leitung, Führung, Pflege)? | |
| 2.2 | Dürfen Vertriebler Rabatte geben? Bis zu welcher Grenze ohne Freigabe? Wer gibt darüber hinaus frei? | |
| 2.3 | Welche Vertragslaufzeiten und Zahlungsweisen gibt es? Hängt der Preis von der Laufzeit ab? | |
| 2.4 | Dürfen Vertriebler individuelle Sonderpositionen anlegen, oder nur Katalogservices verwenden? | |
| 2.5 | Soll der Status (gewonnen/verloren) im Kalkulator gepflegt werden, oder ist HubSpot dafür führend? | |
| 2.6 | Soll eine Anbindung an HubSpot bereits in Version 1.0 kommen oder später? | |

## 3. Angebote (Word) 🟠

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 3.1 | Gibt es eine bestehende Angebotsvorlage (Word, Corporate Design)? Bitte als Beispiel bereitstellen, gern anonymisiert. | |
| 3.2 | Gibt es fertige Textbausteine je Service (Leistungsinhalt, Voraussetzungen, Ausschlüsse)? Wo liegen sie? | |
| 3.3 | Wie ist das Schema der Angebotsnummer? Kommt die Nummer aus dem ERP? | |
| 3.4 | Soll das Angebot nach der Erzeugung in Word frei bearbeitet werden dürfen, oder soll es „fertig“ sein? | |
| 3.5 | Wird zusätzlich ein PDF benötigt? | |
| 3.6 | Wo sollen erzeugte Angebote abgelegt werden: nur in der Anwendung, zusätzlich in SharePoint/Teams-Kundenordnern oder im ERP? | |

## 4. Vertragsunterlagen 🟠

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 4.1 | Welche Dokumente gehören zu einem vollständigen Managed-Services-Vertrag? (z. B. Rahmenvertrag, Leistungsscheine, SLA, Preisblatt, AVV, TOMs, AGB) | |
| 4.2 | Welche Dokumente sind je Service unterschiedlich und welche immer gleich? | |
| 4.3 | Welche Felder müssen in den Verträgen befüllt werden? | |
| 4.4 | Gewünschtes Ausgabeformat: einzelne Word-Dateien, ZIP-Paket, ein zusammengeführtes PDF oder mehrere dieser Formate? | |
| 4.5 | Wer pflegt die Vertragsvorlagen (Geschäftsführung, Rechtsberatung)? Wie wird eine neue Version freigegeben? | |
| 4.6 | Werden Verträge mit Kunden individuell verhandelt? Falls ja: Wie sollen Abweichungen dokumentiert werden? | |

## 5. Statistiken 🟠

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 5.1 | Welche Kennzahlen braucht die Führungsebene konkret? (Vorschlag: Anzahl, Volumen MRR/ARR, Abschlussquote, Rabatte, Marge, Top-Services) | |
| 5.2 | Sollen Vertriebler ihre eigenen Zahlen sehen? Sollen sie auch die Zahlen der Kolleginnen und Kollegen sehen? | |
| 5.3 | Gibt es Anforderungen aus Betriebsrat oder Datenschutz zur personenbezogenen Leistungsauswertung? | |
| 5.4 | Reicht ein Dashboard in der Anwendung, oder sollen die Daten auch in ein BI-Werkzeug (z. B. Power BI) fließen? | |

## 6. IT-Betrieb und Infrastruktur 🟠

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 6.1 | Welches Betriebssystem hat der Zielserver? (Linux mit Docker bevorzugt, Windows Server möglich) | |
| 6.2 | Ist Docker bzw. eine Container-Umgebung vorhanden oder erlaubt? | |
| 6.3 | Anmeldung: On-Premises Active Directory, Microsoft Entra ID (Azure AD) oder beides? | |
| 6.4 | Gibt es eine interne Zertifizierungsstelle für HTTPS-Zertifikate? | |
| 6.5 | Gibt es eine bestehende Datenbank-Infrastruktur (z. B. SQL Server, PostgreSQL), die genutzt werden soll? | |
| 6.6 | Wie erfolgt die Datensicherung (bestehende Backup-Lösung)? | |
| 6.7 | Soll der Zugriff auch per VPN von unterwegs möglich sein? | |
| 6.8 | Wer betreibt und aktualisiert die Anwendung später (interne IT, dieses Projekt)? | |
| 6.9 | Wo soll der Code langfristig liegen (dieses GitHub-Repository oder intern)? Ist GitHub für Firmencode freigegeben? | |

## 7. Projektrahmen 🟢

| Nr. | Frage | Antwort |
|-----|-------|---------|
| 7.1 | Gibt es einen Wunschtermin für den ersten produktiven Einsatz? | |
| 7.2 | Wer testet fachlich (Pilotnutzer aus dem Vertrieb)? | |
| 7.3 | Wer gibt die Preislogik fachlich ab (Produktverantwortung)? | |
| 7.4 | Soll eine schrittweise Einführung erfolgen (z. B. zuerst nur Kalkulator und Angebot)? | |
