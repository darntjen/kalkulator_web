#!/usr/bin/env bash
# Gemeinsame Umgebung für einrichten.sh und dienst.sh (wird mit „source“ eingebunden).
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
export ASPNETCORE_ENVIRONMENT=Development
export Kundenablage__Quelle=Ordner
export Kundenablage__Ordner="$PWD/.devcontainer/beispielablage"
# Vertragsvorlagen aus dem Ordner im Repository (statt SharePoint), PDF über LibreOffice. Testbetrieb: Solange nur der
# Rahmenvertrag umgestellt ist, entsteht im Vertragswerk nur der Grundvertrag (Vertragswerk:Umfang).
export Vorlagen__Quelle=Ordner
export Vorlagen__Ordner="$PWD/.devcontainer/vertragsvorlagen"
export Pdf__Wandler=LibreOffice
export Vertragswerk__Umfang__0=GRUNDVERTRAG
