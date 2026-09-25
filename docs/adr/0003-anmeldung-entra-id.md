# ADR-0003: Anmeldung über Microsoft Entra ID

- **Status:** angenommen
- **Datum:** 2026-09-25

## Kontext

Die Nutzer melden sich mit ihren Firmenkonten an. Die Firma nutzt Microsoft Entra ID.

## Entscheidung

- Anmeldung per **OpenID Connect** über eine **App-Registrierung** in Entra ID (Microsoft.Identity.Web)
- Rollen als **App-Rollen** der App-Registrierung: `Vertrieb`, `Vertriebsleitung`, `Produktmanagement`, `Fuehrung`, `Admin`. Die Zuweisung erfolgt über Entra-Gruppen durch die interne IT
- Absenderdaten für Angebote (Name, Funktion, Telefon, E-Mail) werden aus dem Entra-Profil gelesen (vorbehaltlich offener Frage 9.6)
- Kein lokales Benutzerkonto außer einem dokumentierten Notfallzugang für die Administration

## Konsequenzen

- Die interne IT legt die App-Registrierung an und pflegt die Gruppenzuordnung.
- Die Anwendung benötigt vom Server aus Zugriff auf `login.microsoftonline.com`.
- Personalwechsel wirken sich ohne Pflege in der Anwendung direkt auf die Rechte aus.
