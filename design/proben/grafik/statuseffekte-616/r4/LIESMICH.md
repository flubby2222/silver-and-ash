# Statuseffekte #616, Runde 4: Cleansing Flame mit vielen kleinen Flammen

Der Effekt heisst ab jetzt Cleansing Flame (frueher Holy Fire; Entscheidung des Menschen). Die Dateien der Runden 1 bis 3 behalten ihre alten Namen. Intern bleibt der Schluessel `holy_fire` in den Skripten stehen.

Nur dieser Effekt ist neu. Marked B, Grave Chill A, Stun A und Leech b1 (Runde 3) stehen fest.

## Aufruf

    blender --background --python design/proben/grafik/statuseffekte-616/r4/blatt_r4.py

## Dateien

- `probenblatt_r4.png`: Aufstellung aller fuenf Effekte (8 m und 12 m, je mit c1 und c2), c1/c2 bei 4/8/12 m, daneben a1 (Runde 3) und die Fackel, vier Phasen.
- `cleansing_flame_c1_c2.gif`: c1 und c2 nebeneinander, 10 Bilder zu 0,1 s.
- `spiel_cleansing_flame_<c1|c2>_<4|8|12>m.png`: Spielbilder 854x480; `spiel_8m_c1/c2.png` und `spiel_12m_c1/c2.png` zeigen alle fuenf Effekte.
- `effekte_r4.py`, `blatt_r4.py`: Skripte.

## Was geliehen ist

Keine Fackelbilder, nichts skaliert. Eigene Flammen in drei Groessen (6x11, 5x8, 4x6 Texel), je 5 Bilder im 0,1-s-Takt, Tropfenform mit Pendeln, abreissende Zunge auf jedem 2. Bild, Messing 7/6/4/3 (Rand 4), drei Funken.

- c1: 9 Flammen, Texel 0,020 m (Flamme 0,12 bis 0,22 m, die Fackel ist 0,42 m hoch).
- c2: 16 Flammen, Texel 0,015 m.
- Die Flammen sitzen auf den Oberkanten der Silhouette (Kopf, Schultern, Arme, Haende, Fuesse) mit Mindestabstand, versetztem Takt und teils gespiegelt. Ueber den Augen steht keine Flamme.

## 12 m

Ab 10 m sind die Flammen nur noch wenige Pixel; dort tragen die kraeftigere Glut-Toenung und der hellere Innenrand. Der Gegner liest als goldener, flackernder Umriss, einzelne Flammen sind nicht mehr zu unterscheiden.

## Grenzen

- Flammen sitzen nur auf Oberkanten, der Rumpf selbst brennt nur ueber die Toenung.
- c1 und c2 unterscheiden sich bei 8 m nur in der Dichte, bei 12 m kaum.

## Regelvorschlag (nicht in der Art Bible)

"Ein Statuseffekt uebernimmt kein fertiges Requisit; er leiht sich Bewegung und Farbe."
