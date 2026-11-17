#!/bin/bash
# Fotos der drei Entwuerfe im Spiel (Probebild, kein Spielcode). Aufruf: fotos.sh a|b|c   (Dateien aus modelle_<x>)
# Legt die Modelle voruebergehend nach assets/models, rechnet die Pruefsummen neu, fotografiert, stellt aus Git her.
cd "$(dirname "$0")/../../../.." || exit 1
D=design/proben/grafik/aermel8
for f in $D/modelle_$1/*; do cp "$f" assets/models/; done
python tools/models/checksums.py >/dev/null
for scene in room-corridor pistol-recoil blade-charged; do
  dotnet run --project src/SilverAndAsh.Game -- --photo $scene --out "$D/fotos_$1" >/dev/null 2>&1 || echo "FEHLER $scene"
done
rm -f "$D/fotos_$1"/*-screen.png
git checkout assets/models
