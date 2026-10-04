# ADR-0007: Vertragswerk als PDF über Microsoft 365, zusammengeführt mit PDFsharp

- **Status:** angenommen
- **Datum:** 2026-10-04
- **Beteiligte:** Dennis Arntjen, Claude (Umsetzung)

## Kontext

Aus den Word-Vorlagen (ADR-0006) entsteht nach der Zusage des Kunden das Vertragswerk. Gebraucht werden alle Dokumente
als PDF und eine vollständige Datei, die über Paperless zur Unterschrift geht (#26). Der Kalkulator läuft auf
Windows/IIS ohne Office-Installation; die Vorlagen sind gestaltete Word-Dokumente mit Kopf- und Fußzeilen.

## Entscheidung

- **Umwandlung Word → PDF über Microsoft 365** (Graph): hochladen, `?format=pdf` abrufen, löschen. Das Ergebnis
  entspricht der Word-Darstellung, und es gibt keine weitere Software auf dem Server.
- **LibreOffice** als zweite Umwandlung für Entwicklung, Tests und als Notlösung, austauschbar über `Pdf:Wandler`.
- **Zusammenführen mit PDFsharp** (MIT-Lizenz): Deckblatt und Dokumente werden seitenweise aneinandergehängt; dafür
  braucht es keine Schriftarten.
- Das **Deckblatt** ist eine Word-Vorlage im Repository (`templates/vertrag/Deckblatt.docx`) im Erscheinungsbild der
  Angebotsvorlage.
- Gesamt-PDF und ZIP werden mit den verwendeten Vorlagenfassungen **unveränderlich archiviert**.

## Betrachtete Alternativen

- **Word auf dem Server (Interop):** von Microsoft für Server nicht unterstützt.
- **Kommerzielle Bibliotheken** (z. B. Aspose): Lizenzkosten; die Darstellung weicht trotzdem von Word ab.
- **Nur LibreOffice:** zusätzliche Installation auf dem IIS-Server und Abweichungen bei komplexen Layouts.

## Konsequenzen

- Die App-Registrierung braucht Schreibrecht auf einen Arbeitsordner der Website (Sites.Selected, `write`).
- Die Erzeugung dauert je Dokument einige Sekunden; bei rund 15 Dokumenten ist das für V1 vertretbar.
