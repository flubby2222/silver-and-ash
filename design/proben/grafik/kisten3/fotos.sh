#!/bin/bash
# Probefotos der Kisten im Spiel (Probebild, kein Spielcode): Modelle voruebergehend nach assets/models, Pruefsummen
# neu, fotografieren, aus Git wiederherstellen. Aufruf: fotos.sh <Unterordner>
cd "$(dirname "$0")/../../../.." || exit 1
D=design/proben/grafik/kisten3
for f in $D/modelle/*; do cp "$f" assets/models/; done
python tools/models/checksums.py >/dev/null
for scene in chests-4m room-treasure; do
  dotnet run --project src/SilverAndAsh.Game -- --photo $scene --out "$D/$1" >/dev/null 2>&1 || echo "FEHLER $scene"
done
rm -f "$D/$1"/*-screen.png
git checkout assets/models
