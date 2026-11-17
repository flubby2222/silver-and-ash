#!/bin/bash
# Fotografiert die Waffenszenen im Spiel mit den Modellen aus design/proben/grafik/aermel/modelle (Probebild, kein Spielcode).
# Aufruf: fotos.sh <Unterordner, z. B. neu>. Legt die Dateien voruebergehend nach assets/models, schreibt die Pruefsummen
# neu und stellt danach beides aus Git wieder her.
cd "$(dirname "$0")/../../../.." || exit 1
D=design/proben/grafik/aermel
for f in $D/modelle/*; do cp "$f" assets/models/; done
python tools/models/checksums.py >/dev/null
for scene in pistol-recoil blade-charged room-corridor; do
  dotnet run --project src/SilverAndAsh.Game -- --photo $scene --out "$D/$1" >/dev/null 2>&1 || echo "FEHLER $scene"
done
rm -f "$D/$1"/*-screen.png
git checkout assets/models
