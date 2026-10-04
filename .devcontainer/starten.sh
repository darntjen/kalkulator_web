#!/usr/bin/env bash
# Bei jedem Start des Codespaces: App im Hintergrund auf Port 5226 starten. Ausgabe in /tmp/kalkulator.log.
set -euo pipefail
cd "$(dirname "$0")/.."
export Kundenablage__Quelle=Ordner
export Kundenablage__Ordner="$PWD/.devcontainer/beispielablage"
setsid nohup dotnet run --project src/Kalkulator.Web --no-launch-profile --urls http://0.0.0.0:5226 \
  > /tmp/kalkulator.log 2>&1 < /dev/null &
echo "Kalkulator startet auf Port 5226 (Protokoll: /tmp/kalkulator.log)."
