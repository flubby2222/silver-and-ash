#!/bin/bash
# Fotografiert die Szenen im Spiel mit den Texturen a (vorhanden), b, c. Nur fuer Probebilder, aendert nichts am Spiel:
# legt die Variante voruebergehend als catacomb_<flaeche>.png ab, schreibt die Pruefsummen neu und stellt danach alles wieder her.
cd "$(dirname "$0")/../../../.." || exit 1
T=assets/textures
OUT=design/proben/grafik/level-3/fotos
mkdir -p "$OUT" /tmp/tex_orig
cp $T/catacomb_wall.png $T/catacomb_floor.png $T/catacomb_ceiling.png $T/textures.sha256 /tmp/tex_orig/
for v in a b c; do
  for k in wall floor ceiling; do
    if [ $v = a ]; then cp /tmp/tex_orig/catacomb_$k.png $T/catacomb_$k.png; else cp $T/catacomb_${k}_$v.png $T/catacomb_$k.png; fi
  done
  python tools/models/checksums.py >/dev/null
  for scene in room-corridor room-combat room-treasure ghoul-10m chests-4m; do
    dotnet run --project src/SilverAndAsh.Game -- --photo $scene --out "$OUT/$v" || echo "FEHLER $v $scene"
  done
done
cp /tmp/tex_orig/catacomb_*.png /tmp/tex_orig/textures.sha256 $T/
python tools/models/checksums.py >/dev/null   # Pruefsummen jetzt mit den sechs neuen Dateien
