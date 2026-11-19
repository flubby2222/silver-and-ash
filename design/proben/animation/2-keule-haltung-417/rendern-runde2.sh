#!/bin/bash
# Runde 2: Probe anwenden (git apply probe-haltung-runde2.patch, Debug bauen), dann je Bild:
# dotnet run --project src/SilverAndAsh.Game --no-build -- --photo club-rest --out <ordner>
# Parameter (Umgebung): SA_CLUB_BEND="Neigung,Auswaerts,px,py,pz"  Drehpunkt im Dateiraum (0,0,-0.06)
#   A "20,6,0,0,-0.06"  B "35,10,0,0,-0.06"  C "50,15,0,0,-0.06"; HEUTE: ohne SA_CLUB_BEND
#   SA_CLUB_BENDHIT="0,0" (Knick im Treffer-Moment), SA_CLUB_BENDWIND="20,-10" (Knick beim vollen Ausholen)
#   SA_CLUB_T (Schlagverlauf 0..1.05), SA_CLUB_W (Ausholen 0..1)
# Schnellschlag q1..q6: T = 0 / 0.553 / 0.852 / 1.0 / 1.05 / 0.631, W=0
# Aufgeladen c1..c6: (T,W) = (0,.25) (0,.6) (0,1) (.553,.447) (.852,.148) (1,0)
# Nahaufnahmen: ffmpeg -vf "crop=300:200:170:230,scale=900:600:flags=neighbor" -> <name>-zoom.png
# Blatt: powershell -ExecutionPolicy Bypass -File blatt-runde2.ps1 -Frames <ordner> -Out keule-haltung-417-runde2.png
