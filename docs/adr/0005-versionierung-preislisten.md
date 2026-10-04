# ADR-0005: Versionierung von Preislisten

- **Status:** angenommen
- **Datum:** 2026-09-25

## Kontext

Preise ändern sich im Lauf der Zeit. Gespeicherte Kalkulationen und versendete
Angebote müssen trotzdem nachvollziehbar bleiben (Anforderung A-05). Die
Führungsebene wertet Kalkulationen über längere Zeiträume aus. Die Anwendung
wird nach dem Go-live die maßgebliche Preisquelle (offene Frage 8.12).

## Entscheidung

- Alle Verkaufspreise, Preisstaffeln, Parameter (z. B. AE-Sätze, S14-Bausteine) und EK-Werte hängen an einer **Preisliste**.
- Eine Preisliste ist entweder **Entwurf** (änderbar) oder **Freigegeben** (unveränderlich), mit Freigabedatum und freigebender Person.
- Eine Preisänderung erfolgt immer über einen **neuen Entwurf**, der alle Werte der Vorgängerversion kopiert. Nach der Prüfung wird er mit einem Gültigkeitsdatum freigegeben.
- Die Sperre wird doppelt durchgesetzt: im Fachmodell (`Preisliste.Freigeben`) und beim Speichern in der Datenbankschicht. Dort werden Änderungen an einer freigegebenen Preisliste und an allem, was an ihr hängt, abgewiesen.
- Jede Änderung an Katalog und Preisen wird im Änderungsprotokoll mit Benutzer, Zeitpunkt sowie altem und neuem Wert festgehalten.
- Der Katalog selbst (Services, Bundles, Regeln) wird nicht versioniert. Änderungen daran werden protokolliert. Kalkulationen frieren die für sie relevanten Werte ein (Phase 2).

## Konsequenzen

- Alte Angebote und Statistiken bleiben unverändert, auch wenn sich Preise ändern.
- Für eine Preisänderung sind zwei Schritte nötig (Entwurf anlegen, freigeben). Das ist bewusst so gewollt: Es verhindert versehentliche Änderungen an gültigen Preisen.

## Nachtrag 04.10.2026: Pflegeoberfläche (#6)

- Ein Entwurf wird nur freigegeben, wenn die Prüfung keine Fehler meldet: Pflichtparameter vorhanden, jede
  anbietbare Komponente hat einen Preiseintrag oder eine Staffel, und „Gültig ab“ liegt nach der zuletzt
  freigegebenen Preisliste und nicht in der Vergangenheit. Die erste Preisliste nach der Erstbefüllung ist davon
  ausgenommen.
- Weil der Katalog nicht versioniert ist, sind Codes von Services und Preiskomponenten nach dem Anlegen fest
  (Kalkulationen verweisen darauf). Gelöscht wird nur, was in keiner freigegebenen Preisliste, keinem Arbeitsstand
  und keinem eingefrorenen Angebotsstand vorkommt. Alles andere wird über den Vertriebsstatus stillgelegt.
- Beim Anlegen und Löschen von Katalog- und Preisdaten hält das Änderungsprotokoll alle Werte fest, damit
  gelöschte Einträge nachvollziehbar bleiben.
