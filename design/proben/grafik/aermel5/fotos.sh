#!/bin/bash
# Fotos im Spiel (Probebild, kein Spielcode). Aufruf: fotos.sh <Ordnername> [modelle_x] [Pistolen-Griff x,y,z] [Klingen-Griff x,y,z]
# Mit Modellordner: Dateien voruebergehend nach assets/models, Pruefsummen neu. Mit Griffwerten: WeaponPose.cs lokal
# veraendert. Danach wird alles (src und assets/models) aus Git wiederhergestellt - nichts davon wird committet.
cd "$(dirname "$0")/../../../.." || exit 1
D=design/proben/grafik/aermel5
if [ -n "$2" ]; then
  for f in $D/$2/*; do cp "$f" assets/models/; done
  python tools/models/checksums.py >/dev/null
fi
if [ -n "$3" ]; then
  sed -i "s/PistolGrip { get; } = new(0.095f, -0.08f, 0.18f)/PistolGrip { get; } = new(${3//,/f, }f)/" src/SilverAndAsh.Core/Presentation/WeaponPose.cs
  sed -i "s/BladeGrip { get; } = new(-0.11f, -0.095f, 0.19f)/BladeGrip { get; } = new(${4//,/f, }f)/" src/SilverAndAsh.Core/Presentation/WeaponPose.cs
fi
if [ -n "$5" ]; then
  sed -i "s/WindUpShift { get; } = new(-0.03f, -0.01f, -0.03f)/WindUpShift { get; } = new(${5//,/f, }f)/" src/SilverAndAsh.Core/Presentation/WeaponPose.cs
  sed -i "s/WindUpYawDegrees = 35f/WindUpYawDegrees = ${6}f/" src/SilverAndAsh.Core/Presentation/WeaponPose.cs
fi
for scene in room-corridor pistol-recoil blade-charged; do
  dotnet run --project src/SilverAndAsh.Game -- --photo $scene --out "$D/$1" >/dev/null 2>&1 || echo "FEHLER $scene"
done
rm -f "$D/$1"/*-screen.png
git checkout assets/models src
rm -f assets/models/*_hand.png
