#!/usr/bin/env bash
# Gemeinsame Umgebung für einrichten.sh und starten.sh (wird mit „source“ eingebunden).
# Wartet auf das SA-Kennwort, das der Datenbank-Container beim ersten Start erzeugt.
for _ in $(seq 1 60); do
  [ -s /geheim/sa-kennwort ] && break
  sleep 2
done
if [ ! -s /geheim/sa-kennwort ]; then
  echo "Das Kennwort der Datenbank fehlt (/geheim/sa-kennwort). Läuft der Container „db“?" >&2
  exit 1
fi
export ConnectionStrings__Kalkulator="Server=db,1433;Database=Kalkulator;User Id=sa;Password=$(cat /geheim/sa-kennwort);TrustServerCertificate=True"
export Kundenablage__Quelle=Ordner
export Kundenablage__Ordner="$PWD/.devcontainer/beispielablage"
