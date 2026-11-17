#!/bin/bash
# Fotografiert die Klinge ueber den ganzen Schlag. LOKALE Hilfsaenderung (nicht committen): WeaponViews.DrawBlade liest die
# Umgebungsvariablen SA_WIND (0..1, Ausholen) und SA_SWING (0..1, Schlagstellung t) und ueberschreibt damit die Werte der Szene
# room-corridor (Ruhe). Danach wird src aus Git wiederhergestellt. Aufruf: phasen.sh <modelle-Ordner> <Ausgabeordner>
cd "$(dirname "$0")/../../../.." || exit 1
M=design/proben/grafik/$1; OUT=design/proben/grafik/$2
for f in $M/*; do cp "$f" assets/models/; done
python tools/models/checksums.py >/dev/null
python - <<'PY'
p="src/SilverAndAsh.Game/Rendering/WeaponViews.cs"
s=open(p,encoding="utf-8").read()
s=s.replace("var t = _swing < 0.4f ? EaseOut(_swing / 0.4f) : 1f - EaseInOut((_swing - 0.4f) / 0.6f);",
 "var t = _swing < 0.4f ? EaseOut(_swing / 0.4f) : 1f - EaseInOut((_swing - 0.4f) / 0.6f);\n        var saW = Environment.GetEnvironmentVariable(\"SA_WIND\"); var saS = Environment.GetEnvironmentVariable(\"SA_SWING\");\n        if (saS is not null) { t = float.Parse(saS, System.Globalization.CultureInfo.InvariantCulture); }\n        var saWind = saW is not null ? float.Parse(saW, System.Globalization.CultureInfo.InvariantCulture) : _windUp;",1)
s=s.replace("var grip = WeaponPose.Blade(t, _windUp, tremble) * modelMotion;","var grip = WeaponPose.Blade(t, saWind, tremble) * modelMotion;",1)
open(p,"w",encoding="utf-8").write(s)
PY
mkdir -p "$OUT"
for ph in "w0 0 0" "w05 0.5 0" "w1 1 0" "s025 0 0.25" "s05 0 0.5" "s075 0 0.75" "s1 0 1"; do
  set -- $ph
  SA_WIND=$2 SA_SWING=$3 dotnet run --project src/SilverAndAsh.Game -- --photo room-corridor --out "$OUT/tmp" >/dev/null 2>&1
  mv "$OUT/tmp/room-corridor.png" "$OUT/blade_$1.png"
done
rm -rf "$OUT/tmp"
git checkout assets/models src
rm -f assets/models/*_hand.png
