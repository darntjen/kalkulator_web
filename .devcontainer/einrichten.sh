#!/usr/bin/env bash
# Einmalig nach dem Anlegen: bauen, Datenbank anlegen und Musterkatalog einspielen. Protokoll: /tmp/einrichten.log
set -euo pipefail
exec > >(tee -a /tmp/einrichten.log) 2>&1
cd "$(dirname "$0")/.."
source .devcontainer/umgebung.sh
dotnet build src/Kalkulator.Web --nologo -v q

# SQL Server braucht nach dem ersten Start etwas Zeit; die Erstbefüllung spielt auch die Migrationen ein.
for versuch in $(seq 1 30); do
  if dotnet run --project src/Kalkulator.Web --no-build --no-launch-profile -- --erstbefuellung; then
    # Ab jetzt startet dienst.sh die App.
    touch .devcontainer/.eingerichtet
    echo "Einrichtung fertig. Der Kalkulator startet gleich auf Port 5226 (Protokoll: /tmp/kalkulator.log)."
    exit 0
  fi
  echo "Datenbank noch nicht bereit, neuer Versuch in 5 Sekunden ($versuch/30) …"
  sleep 5
done
echo "Die Erstbefüllung ist fehlgeschlagen." >&2
exit 1
