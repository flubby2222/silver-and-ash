# Statuseffekte am Gegner, Runde 2 (#616)

Urteil des Menschen nach Runde 1: Stun A, Grave Chill A, Rest B (Marked, Holy Fire, Leech); detailreicher, vor allem Holy Fire; Leech bekommt eine neue Farbreihe Violett.

## Dateien

- `probenblatt_r2.png`: das Blatt (Farbtabelle, Reihenvergleich, Aufstellung 8 m und 12 m, Kontrolle ohne Effekte, Detailstufen a/b für Holy Fire und Leech, zwei Dreifachfälle).
- `spiel_8m_gewaehlt.png`, `spiel_12m_gewaehlt.png`, `spiel_8m_kontrolle.png`, `spiel_12m_kontrolle.png`: 854x480 in Spielauflösung.
- `spiel_<effekt>_<a|b>_<abstand>m.png` und `spiel_dreifach_<1|2>_<abstand>m.png`: weitere Spielbilder.
- `holy_fire_a_b.gif`, `leech_a_b.gif`: Stufe a und b nebeneinander, 8 Phasen, 4 m.
- `violett-vorschlag.json`: Vorschlag für die neue Reihe. `farbtabelle_r2.csv/.md`: Farben je Effekt. `messwerte_r2.md`.
- Skripte: `effekte_r2.py` (Effekte), `blatt_r2.py` (Blatt), `gifschreiber.py` (GIF ohne Fremdpakete).

Aufruf aus dem Repository-Wurzelordner:

    & "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/statuseffekte-616/r2/blatt_r2.py

## Gewählt

| Effekt | Fassung | Inhalt |
|---|---|---|
| Marked | B | wandernder zweifarbiger Umriss, dunkle Außenkontur, Innenrand |
| Holy Fire | B (Detail-Stufe b) | Tönung in harten Bändern (unten heißer), glühender Innenrand, Flammen mit Kern, Mitte, Rand, Spitze, innere Flammen, Glut |
| Grave Chill | A | Tönung, Reifkruste, Bodenscheibe mit Rissen, Eiszapfen |
| Stun | A | drei kreisende Funkenkreuze mit Schweif |
| Leech | B (Detail-Stufe b) | violette Tönung, Innenrand, gepunkteter Umriss, Faden mit Perlen zur Kamera, Tropfen mit dunklem Rand und Glanzpunkt |

Stufe a ist jeweils die zurückhaltende Fassung (weniger Flammen/Tropfen, kein Umriss). Nur Palettenfarben, kein Rot, Messing/Gold Stufe 5 bleibt gemieden. Die Hauptpalette `assets/palette/hauptpalette.json` ist nicht verändert.

## Neue Reihe Violett (Vorschlag)

| Stufe | RGB |
|---|---|
| 0 | 22,12,34 |
| 1 | 38,20,58 |
| 2 | 58,30,88 |
| 3 | 82,44,120 |
| 4 | 112,62,156 |
| 5 | 150,92,190 |
| 6 | 190,140,220 |
| 7 | 226,200,240 |

Farbton etwa 270 bis 285 Grad: deutlich vom Stoff-Blau (etwa 225 Grad) und vom Blutrot (etwa 350 Grad) abgesetzt. Auf dem Blatt stehen alle drei Reihen untereinander.

## Grenzen

- Flammenspitzen wirken bei kleiner Auflösung noch spitz und gleichförmig.
- Stun-Funken verschwinden bei 12 m im Nebel, besonders über Flammen.
- Marked ist bei 12 m der schwächste Effekt.
- Dreifachfall 2 (Chill, Leech, Marked) ist überladen; Leech-Faden und Bodenscheibe überlagern sich.
- Runde 2 steht die Kamera im Gang wie im Spiel; Runde 1 hatte einen anderen Raum, die Bilder sind nicht 1:1 vergleichbar.

## Vorschlag für die Art Bible (nicht eingetragen)

- Statuseffekte am Gegner bestehen aus mindestens drei Farbstufen und einer Innenform (Rand, Kern, Flamme, Tropfen), nie aus einer flachen Fläche.
- Leech und Verwandtes nutzen die Reihe Violett; Blutrot bleibt der Angriffs-Ankündigung vorbehalten, kein Rot am Gegner.
- Effekte werden immer in Spielauflösung bei 8 m und 12 m im Gang geprüft, nicht nur im Vergrößerungsausschnitt.
