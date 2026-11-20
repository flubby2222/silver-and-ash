# Probestück Whip (Issue #202), Runde 1

Drei Entwürfe der Jägerpeitsche, je zwei Posen als eigene Modelle: **Ruhe** (zur Schlaufe gelegt, so wie sie in der Hand hängt) und **Schlag** (gerade Linie). Alles aus einem Skript, `peitsche.py` (Blender-Python und NumPy, nichts heruntergeladen). Es liegt nichts unter `assets/`, im Spiel ist nichts verändert.

Aufruf (Wurzel des Repositorys, TEMP und TMP auf einen Zwischenordner):
`"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/whip-202/runde-1/peitsche.py`

## Die Entwürfe

| | Entwurf | Glieder | Dreiecke (je Pose) | Farben | Erkennbar durch |
|---|---|---|---|---|---|
| A | Kettenpeitsche: Eisenglieder (jedes zweite dunkler, um 90 Grad gedreht), Silbergewicht am Ende | 12 | 1024 | 14 | Hundeknochen-Glieder, helle Spitze |
| B | Geflochtene Lederpeitsche, nach hinten dünner, Silberspitze, Silberzwinge am Griff | 14 | 1200 | 14 | Rautengeflecht, Verjüngung |
| C | Klingenpeitsche: Silberblätter an dunklen Gelenkzapfen, je um 90 Grad verdreht, Speerspitze am Ende | 10 | 1020 | 14 | helle Blätter mit Lücken, Parierstange am Griff |

Dateien: `peitsche-<a-kette|b-leder|c-klingen>-<ruhe|schlag>.glb`. Bilder: `blatt.png` (Ruhe, Ich-Sicht Ruhe und Schlag, Streckung neben 1,8-m-Figur, Silhouetten neben Keule und Klinge), `ansichten.png` (Ruhe vorn, Seite, hinten, schräg oben, Schlag im Nahbild), `ich-<entwurf>-<ruhe|schlag>.png` (Spielauflösung 854 x 480, Katakomben, Nebel, Körnung).

## Gemeinsames

- **Aufbau:** Knoten `handle` (Drehpunkt im Griff) und je Glied ein starrer Knoten `seg01` ... mit Drehpunkt am Gliedanfang; jedes Glied hängt am vorigen (Objekt-Attribut `parent`, wie bei den anderen mehrteiligen Modellen). Die Drehung um die Gelenke ist in den Dateien nicht vorbelegt, die Haltung steckt in den Ecken.
- **Maße (Spielmaß):** Griff 0,29 m, Kette 4,5 m, ganze Waffe 4,7 m. Dateimaß = 1,8 x Spielmaß wie bei allen Waffen (Skalierung eingebacken). Im Issue stehen „ca. 6 m“; `balance.json` hat noch keinen Wert. Länge ist hier nur eine Zahl im Skript (`lens`).
- **Ruhe-Datei:** gleiche Gliederzahl, aber 6- bis 8-mal kürzere Glieder (0,05 bis 0,08 m statt 0,32 bis 0,44 m), sonst läge die hängende Peitsche außerhalb des Bildes. Für die Animation heißt das: Glieder entlang der Achse strecken (oder zwischen beiden Dateien überblenden); starres Weiterdrehen der Ruhe-Glieder allein reicht nicht.
- **Haltung:** Der Griff knickt wie die Keule 35 Grad nach oben und 10 Grad nach außen ab (Regel 23), der Ärmel bleibt stehen und kommt aus `club_hand.glb`. Die Schlaufe ist aus der Lotrechten der Welt gerechnet, sie hängt in der Hand wirklich nach unten.
- **Gold nur klein:** Knauf (A) und Ring (C); B hat kein Gold; sonst Holz, Leder, Eisen, Silber.
- **Hauptpalette, Textur 128 x 128, höchstens 16 Farben (hier 14), kein Rot, keine Mischfarben, flach schattiert, ohne Hand modelliert.**

## Nachweis geschlossen

`python tools/models/check_closed.py design/proben/grafik/whip-202/runde-1/<datei>.glb` meldet für alle sechs Dateien „geschlossen, 0 offene Kanten“. Das Skript zählt vor dem Bauen je Teil ebenfalls (0 offene Kanten, 0 Kanten mit mehr als zwei Flächen). Dreiecke je Datei unter 1500 (größte: 1200).

## Reproduzierbar

Zwei Läufe in leere Zielordner (Umgebungsvariable `PEITSCHE_AUS`) ergeben alle sechs glb und alle Bilder Bit für Bit gleich (SHA-256 verglichen).

## Empfehlung

**B (Leder) für die Peitsche des Jägers**, wenn die Welt eher Handwerk als Eisen zeigen soll: in der Hand die beste Lesbarkeit (große Schlaufe), Silber nur an der Spitze (passt zu „gehört dem Jäger“), ruhigste Silhouette. **C** ist die eigenständigste Silhouette und hebt sich im Nebel am besten ab (helle Blätter). **A** liest sich am schwächsten: dunkles Eisen auf dunklem Grund, in der Ruhe ein Klumpen.

## Bekannte Schwächen

- Im Schlag liegt die Peitsche in der Ich-Sicht fast in Blickrichtung; durch den Nebel (ab 3,25 m) sieht man nur Griff und die ersten Glieder. Das ist die Natur einer gestreckten Peitsche; für die Lesbarkeit braucht der Schlag Bewegung (Bogen quer durchs Bild) und ein Ende, das Licht fängt.
- Die Peitsche im gestreckten Maßstab (Spielmaß neben der Figur) ist dünn: B ist dort nur eine Linie. Dicker machen würde die Silhouette in der Ruhe verklumpen.
- A in Ruhe: die Glieder überlappen stark, es sieht nach Klumpen statt nach Kette aus (ein größerer Schlaufenradius braucht mehr Platz im Bild).
- Länge (4,5 m gegen „ca. 6 m“) und die Dateiskala (Ich-Sicht zeigt 1,8-fach, die Peitsche wirkt dort 8 m lang) sind Entscheidungen für die Leitung.
- Biegung zwischen den Gliedern ist nicht erprobt; die Dateien enthalten nur die zwei Haltungen.
