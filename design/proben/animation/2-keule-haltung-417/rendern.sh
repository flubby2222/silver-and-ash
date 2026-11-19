#!/bin/bash
# Erzeugt die Einzelbilder fuer blatt.ps1 (Probe #417). Voraussetzung: probe-haltung.patch ist angewendet
# (git apply design/proben/animation/2-keule-haltung-417/probe-haltung.patch) und das Spiel gebaut (Debug).
# Danach den Patch wieder zuruecknehmen (git checkout src). Aufruf aus der Wurzel: bash rendern.sh <Ausgabeordner>
OUT=${1:?Ausgabeordner}
r() { dotnet run --project src/SilverAndAsh.Game --no-build -- --photo club-rest --out "$OUT/$1" >/dev/null 2>&1; }
HIT="0,45,0,-0.02,-0.09,0.24"; WIND="65,-50,10,-0.18,-0.17,0.20"; B="55,-20,0,-0.13,-0.12,0.21"
export SA_CLUB_HIT=$HIT SA_CLUB_WIND=$WIND
r heute
SA_CLUB_REST="35,-12,0,-0.12,-0.11,0.20" r A
SA_CLUB_REST=$B r B
SA_CLUB_REST="70,-25,0,-0.13,-0.12,0.21" r C
i=0; for t in 0 0.553 0.852 1.0 1.05 0.631; do i=$((i+1)); SA_CLUB_REST=$B SA_CLUB_T=$t SA_CLUB_W=0 r q$i; done
i=0; for tw in "0 0.25" "0 0.6" "0 1.0" "0.553 0.447" "0.852 0.148" "1.0 0"; do i=$((i+1)); set -- $tw; SA_CLUB_REST=$B SA_CLUB_T=$1 SA_CLUB_W=$2 r c$i; done
