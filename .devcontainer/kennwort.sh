#!/usr/bin/env bash
# Erzeugt einmalig ein zufälliges SA-Kennwort für den SQL-Server-Container (nur innerhalb des Codespaces gültig).
set -eu
datei="$(dirname "$0")/.env"
if [ ! -s "$datei" ]; then
  zufall=$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 20)
  echo "MSSQL_SA_PASSWORD=Kx9!${zufall}" > "$datei"
fi
