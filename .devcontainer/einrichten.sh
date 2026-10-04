#!/usr/bin/env bash
# Einmalig nach dem Anlegen: bauen, Datenbank anlegen und Musterkatalog einspielen.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build src/Kalkulator.Web --nologo -v q

# SQL Server braucht nach dem ersten Start etwas Zeit; die Erstbefüllung spielt auch die Migrationen ein.
for versuch in $(seq 1 30); do
  if dotnet run --project src/Kalkulator.Web --no-build --no-launch-profile -- --erstbefuellung; then
    exit 0
  fi
  echo "Datenbank noch nicht bereit, neuer Versuch in 5 Sekunden ($versuch/30) …"
  sleep 5
done
echo "Die Erstbefüllung ist fehlgeschlagen." >&2
exit 1
