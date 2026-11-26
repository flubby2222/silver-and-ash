# Stake Crossbow (Issue #203), Probestücke Runde 1

Nichts davon ist im Spiel. Drei Entwürfe, jeder mit eingelegtem Holzpflock (eigener Knoten `stake`, Waffe als Knoten `crossbow`, Drehpunkt am Griff).

| Entwurf | Dreiecke | Farben | Idee |
|---|---|---|---|
| A Eisenwinde (`armbrust_a.py`) | 428 | 20 | Schwere Eisenarmbrust: breiter gerader Eisenbogen, Winde mit Kurbel hinten, Silberspitze am hellen Pflock. |
| B Rippenbogen (`armbrust_b.py`) | 444 | 21 | Helles Holz mit Knochenringen wie Wirbel, sichelförmiger Bogen, Knochenknäufe, verkohlte Pflockspitze. |
| C Schlanke Dohle (`armbrust_c.py`) | 400 | 20 | Schlanke Jagdarmbrust: langer dunkler Schaft mit Messingeinlagen, dünner Recurve nach vorn, Silberbügel, langer Pflock. |

Gemeinsam: `armbrust_gemeinsam.py` (Palette, Atlas, Hilfen), `blatt.py` (Vergleichsblatt, Ansichten, Ich-Sicht, HUD 38x38, Silhouette).

## Maße und Pose
- Modelle sind mit Faktor 1,8 gebacken wie die übrigen Waffen, zusätzlich SHRINK 0,72 (wirksam 1,3), sonst ist die Spannweite (ca. 0,8 m) breiter als das Bild.
- Spannweite A 0,59 m, B 0,56 m, C 0,50 m (Dateimaß).
- Die Hand im Blatt ist der Ärmel aus `pistol_hand.glb`, rechte Hand; nicht animiert.

## Geschlossenheit
Im Skript pro Entwurf gezählt (0 offene, 0 mehrfach belegte, 0 lose Kanten) und mit `tools/models/check_closed.py` bestätigt: alle drei geschlossen.

## Reproduzierbarkeit
Zwei vollständige Läufe: gleiche SHA-256 für alle 20 Dateien.

## Empfehlung
Siehe Bericht der Grafik an die Leitung; kurz: C hebt sich am stärksten von Pistole und Skelett-Armbrust ab, A liest sich im Nebel am robustesten.

## Bekannte Schwächen
- Sehne und Bügel sehr dünn, im HUD nur noch Pixel.
- Bogen kreuzt in der Ich-Sicht unterhalb der Bildmitte.
- Keine Varianten gespannt/entspannt, keine Animation.
- SHRINK 0,72 weicht vom Waffenmaßstab 1,8 ab; bei Integration klären.
