#!/usr/bin/env bash
# Hauptprozess des App-Containers: startet die App, sobald die Einrichtung (einrichten.sh) fertig ist,
# und startet sie nach einem Absturz oder „pkill -f Kalkulator.Web“ nach 10 Sekunden neu.
# So läuft sie unabhängig davon, ob ein Fenster offen ist. Ausgabe in /tmp/kalkulator.log.
cd "$(dirname "$0")/.."
repo="$PWD"
while true; do
  if [ -f .devcontainer/.eingerichtet ]; then
    echo "$(date '+%F %T') Starte Kalkulator auf Port 5226 …" >> /tmp/kalkulator.log
    runuser -l vscode -c "cd '$repo' && source .devcontainer/umgebung.sh && exec dotnet run --project src/Kalkulator.Web --no-launch-profile --urls http://0.0.0.0:5226" \
      >> /tmp/kalkulator.log 2>&1
    echo "$(date '+%F %T') Kalkulator beendet, Neustart in 10 Sekunden." >> /tmp/kalkulator.log
    sleep 10
  else
    sleep 5
  fi
done
