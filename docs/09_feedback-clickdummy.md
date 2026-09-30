# 09 – Feedback zum Clickdummy

> Status: **Ausgewertet** (29.09.2026)
>
> Grundlage: Mails im Verlauf „Clickdummy MSP Kalkulator“ vom 25.09. bis 28.09.2026.
> Rückmeldungen kamen von André Nösse (Geschäftsführung), Matthias Erhard
> (Geschäftsleitung) und Olaf Schmidt. Sascha Manczak (IT) ist bis
> 02.10.2026 abwesend.
>
> Den Clickdummy haben alle drei über den Artefakt-Link gesehen. Er nutzt echte
> Katalogpreise und fiktive Kunden.

## 1. Gesamtbild

Alle drei tragen den Ansatz mit und wollen auf dem Clickdummy aufbauen. An
Bedienung und Aufbau gab es keine Kritik. Die Rückmeldungen betreffen vier Themen:

- Betrieb und Anmeldung
- Freigaben und Margen
- das Zusammenspiel mit Navision
- die durchgehende Prozesskette von der Analyse bis zur Abrechnung

## 2. Rückmeldungen im Einzelnen

### André Nösse

- **Lob:** Layout und Bedienung sind einfach. Der Katalog ist übersichtlich, die rollenabhängige Datensicht ist schon vorhanden.
- **Frage: Wo läuft die Anwendung, außerhalb von Claude?** Antwort (Dennis, 28.09.): als Webanwendung auf einem internen Webserver, ohne Zugriff von außen. Das entspricht [ADR-0002](adr/0002-technologie-stack.md).
- **Frage: Anmeldung und Datensicht für Vertriebler und Consultants.** Antwort: Anmeldung per SSO mit dem Nösse-Konto, abgestimmt mit Sascha ([ADR-0003](adr/0003-anmeldung-entra-id.md)). Die Rolle „Consultant“ war im Rollenmodell noch nicht vorgesehen.
- **Frage: Versionierung der Angebote mit einer Übersicht, was wann an den Kunden ging.** Antwort (Dennis, 28.09.): nicht in Version 1.

### Olaf Schmidt

- Der Entwurf soll Grundlage der weiteren Entwicklung und der gemeinsamen Ideen sein.
- **Offen:** Wie spielt der Kalkulator mit der Angebots- und Auftragsanlage in Navision zusammen? Das soll im Montagsmeeting besprochen werden.

### Matthias Erhard

- Der Ansatz ist schlüssig. Die Verknüpfung von Analyse, Kalkulation und Angebot hat großes Potenzial. Gut ist das Vorgehen in kleinen Schritten.
- **Leitbild:** Der Kalkulator wird die zentrale Plattform für den gesamten MSP-Vertrieb. Ziel ist die durchgehende Kette
  **Kundenanalyse → Kalkulation → Angebot → Vertrag → Auftrag → Bereitstellung → Abrechnung**.
- Ideen:
  - Ampelsystem für Margen und Freigabeprozesse (Vertrieb, Vertriebsleitung, Geschäftsleitung)
  - automatische Berechnung von Deckungsbeitrag und Break-even
  - Ergebnisse der Kundenanalyse als Basis für die Erstkalkulation
  - automatische Erstellung von Angebot, Leistungsbeschreibung und Vertrag
  - Dashboard für Vertrieb und Geschäftsleitung mit Abschlussquote und Margen
  - später KI-gestützte Empfehlungen, z. B. „Kunde hat NIS2-Anforderungen → Security-Paket vorschlagen“
  - Übergaben an Navision, DocBee, Paperless und weitere Systeme möglichst automatisieren

### Eigene Ideen aus der Ausgangsmail (Dennis, 25.09.)

- Die Kundenakte aus den Analysen wird hochgeladen. Der Kalkulator übernimmt die Ergebnisse, legt das Projekt an und lädt eine erste, anpassbare Kalkulation.
- Version 2: Export nach Power BI oder ähnlich; eine Datei, mit der der Vertrag in DocBee und/oder Navision angelegt wird.
- Später: Verträge über Paperless zur Unterschrift geben.

## 3. Einordnung

| Nr. | Anforderung | Quelle | Entscheidung (29.09.2026) | Stufe |
|-----|-------------|--------|---------------------------|-------|
| F1 | Betrieb auf internem Webserver, Anmeldung mit Nösse-Konto (SSO) | André | bestätigt, wie geplant | v1 |
| F2 | Rolle **Consultant** | André | Consultants **sehen Kalkulationen und laden Analyseergebnisse hoch. Sie kalkulieren nicht selbst** | v1 |
| F3 | Angebotsversionen mit Versendungsübersicht | André | schlanke Variante: Jedes erzeugte Angebot erhält Nummer und Version, wird archiviert und mit Versanddatum markiert. Keine Versandfunktion | v1 |
| F4 | Zusammenspiel mit Navision | Olaf | v1: Das Navision-Angebot wird **als PDF eingelesen**, das Layout ist fest. Eine Rückgabe an Navision folgt später | v1 / später |
| F5 | Margen-Ampel mit Freigabestufen Vertrieb / VL / GL | Matthias | **zunächst nicht**. Die Freigabe freier Sonderpositionen durch die Vertriebsleitung (B-22) bleibt | später |
| F6 | Deckungsbeitrag und Break-even | Matthias | Deckungsbeitrag und Marge des **Gesamtangebots** (Managed Services und Transformationsprojekt) in v1. Break-even ist noch zu definieren | v1 / offen |
| F7 | Analyseergebnisse als Basis der Erstkalkulation | Matthias, Dennis | v1: Analyseergebnisse als Dokumente hochladen. Die Kundensituation pflegt der Vertrieb **manuell**. Eine automatische Übernahme folgt später | v1 / später |
| F8 | Angebot, Leistungsbeschreibung und Vertrag automatisch | Matthias | wie geplant (Module 2 und 3) | v1 |
| F9 | Dashboard mit Abschlussquote und Margen | Matthias | wie geplant (Modul 4), ergänzt um den **Forecast** der Transformationsprojekte | v1 |
| F10 | KI-Empfehlungen für passende Services | Matthias | später | später |
| F11 | Übergaben an Navision, DocBee, Paperless, Power BI | Matthias, Dennis | später. v1 legt eindeutige Nummern und saubere Datenfelder dafür an | später |

## 4. Folgerungen für das Gesamtkonzept

1. Der Kalkulator wird zur **Angebotszentrale**. Er verbindet Kundensituation,
   Transformationsprojekte (aus Navision) und Managed Services zu einem
   ansehnlichen Gesamtangebot. Siehe [10_gesamtkonzept.md](10_gesamtkonzept.md).
2. Version 1 kommt **ohne Schnittstellen** aus. Eingang sind hochgeladene Dateien
   und manuelle Eingaben, Ausgang ist das Word-Angebot mit den Vertragsunterlagen.
3. Das Rollenmodell wird um **Consultant** erweitert.
4. Die Prozesskette von Matthias ist das Leitbild für den Ausbau. Version 1 deckt
   **Kundenanalyse (Ablage) → Kalkulation → Angebot → Vertrag** ab.
