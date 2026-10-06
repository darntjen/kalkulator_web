#!/usr/bin/env bash
# Bei jedem Start des Codespaces (postStartCommand): reicht die Codespaces-Secrets an die App weiter.
# GitHub setzt sie nur für Terminals und Startskripte; dienst.sh startet die App aber in einer eigenen Login-Shell.
# Die Werte landen in einer Datei außerhalb des Repositorys (nur für vscode lesbar), umgebung.sh liest sie ein.
# Ändern sie sich, startet die App neu. Die Werte erscheinen nie in einer Ausgabe.
set -eu
ziel=/home/vscode/.kalkulator-geheimnisse
neu=$(mktemp)
chmod 600 "$neu"
for paar in "Paperless__ApiSchluessel PAPERLESS__APISCHLUESSEL" "Ki__ApiSchluessel KI__APISCHLUESSEL"; do
  set -- $paar
  wert=$(printenv "$1" || printenv "$2" || true)
  if [ -n "$wert" ]; then
    printf 'export %s=%q\n' "$1" "$wert" >> "$neu"
    echo "Secret $1 für die App übernommen."
  fi
done
if [ -f "$ziel" ] && cmp -s "$neu" "$ziel"; then
  rm -f "$neu"
  exit 0
fi
mv "$neu" "$ziel"
[ "$(id -u)" = 0 ] && chown vscode:vscode "$ziel"
# App neu starten, damit sie die Secrets kennt (dienst.sh startet sie nach 10 Sekunden wieder).
pkill -f 'bin/Debug/net10.0/Kalkulator.Web' || true
