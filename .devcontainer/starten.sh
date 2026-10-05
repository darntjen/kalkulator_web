#!/usr/bin/env bash
# Startet die App im Hintergrund auf Port 5226, falls sie noch nicht läuft. Ausgabe in /tmp/kalkulator.log.
# Wird beim Start des Codespaces und beim Öffnen eines Fensters aufgerufen; ein zweiter Aufruf tut nichts.
set -euo pipefail
cd "$(dirname "$0")/.."
if pgrep -f "src/Kalkulator.Web --no-launch-profile" > /dev/null; then
  echo "Kalkulator läuft bereits auf Port 5226 (Protokoll: /tmp/kalkulator.log)."
  exit 0
fi
source .devcontainer/umgebung.sh
setsid nohup dotnet run --project src/Kalkulator.Web --no-launch-profile --urls http://0.0.0.0:5226 \
  > /tmp/kalkulator.log 2>&1 < /dev/null &
echo "Kalkulator startet auf Port 5226 (Protokoll: /tmp/kalkulator.log)."
