# App-Container des Codespaces: .NET 10 und LibreOffice für die PDF-Umwandlung der Vertragsvorlagen.
# Carlito und Liberation sind metrisch gleich zu Calibri bzw. Arial/Times New Roman, damit Umbrüche wie in Word bleiben.
FROM mcr.microsoft.com/devcontainers/dotnet:10.0
RUN apt-get update \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends libreoffice-writer-nogui fonts-crosextra-carlito fonts-liberation fonts-dejavu-core \
    && apt-get clean && rm -rf /var/lib/apt/lists/*
