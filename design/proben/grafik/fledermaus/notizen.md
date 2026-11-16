# Vampirfledermaus – Probestück 7 (2026-11-16)

Eine große Vampirfledermaus dieser Welt, gebaut für den Blick von unten: Sie schwebt über Kopfhöhe, der Spieler sieht sie von unten und vorn. PS1-nah nach der neuen Art Bible (zweite Fassung auf `main`).

Alles erzeugt [`fledermaus.py`](fledermaus.py) mit [`../gemeinsam/psx.py`](../gemeinsam/psx.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/fledermaus/fledermaus.py
```

## Das Problem und wie das Stück es löst

**Problem:** Im Spiel ist die Fledermaus aus 10 m praktisch unsichtbar, dunkel vor dunkel. Dazu kommt eine Regel des Spiels, die gegen sie arbeitet: Nach unten zeigende Flächen bekommen nur 55 % Licht (Licht von oben). Genau die sieht der Spieler.

**Lösung, ohne Rot und ohne Leuchten:**

1. **Helle Flughaut-Unterseite:**
   - Die Flughaut ist unten kalt-hellgrau (188, 190, 176), so hell gemalt, dass nach den 55 % Licht noch ein klarer Abstand zum dunklen Gewölbe bleibt.
   - Dunkle Fingerknochen und Adern laufen als Muster darüber: Von unten liest sich das sofort als Fledermausflügel.
   - Die Oberseite ist dunkles Leder (66, 58, 56); die sieht man selten.
   - Der Farbton ist kalt-grau, nicht der rosagraue Stein (Regel 1) und nicht warm wie Beute.
2. **Fahler Bauchpelz** (168, 166, 152) gegen den dunklen Rücken (58, 48, 50).
3. **Umriss:** große, spitze Ohren, nach außen gestellt; Daumenkrallen am Handgelenk; hängende Hinterbeine mit Krallenfüßen; ausgebuchtete Hinterkante der Flügel.
4. **Maul:** lange, weiße obere Fangzähne (222, 218, 196), das Hellste am Tier, dazu eine kleinere Reihe dahinter und untere Fänge. Beim Kreischen ist das Maul weit offen.

**Gemessen aus 10 m**, mit Nebel und Körnung, im selben Gang, Schwebehaltung; vollständig in `messung.txt`:

| | Größe | gedeckte Pixel | mittlere Helligkeit | Umgebung |
|---|---|---|---|---|
| Platzhalter | 28 × 13 Pixel | 76 | 23 | 16 |
| neu | 46 × 12 Pixel | 212 | 38 | 16 |

Die neue Fledermaus deckt fast dreimal so viele Pixel und hebt sich mehr als doppelt so weit vom Hintergrund ab. Der Unterschied ist in `vergleich_platzhalter_10m.png` deutlich: links ein dunkler Fleck mit zwei roten Punkten, rechts eine helle, breite Fledermaus.

**Aus 4 m:** 114 × 35 Pixel, Helligkeit 74 gegen 20.

## Bilder

Alle Spielbilder: 854 × 480, 70° vertikal, Augen auf 1,6 m, Blick auf die Fledermaus. Sie schwebt mit dem Rumpf auf 2,4 m (Schwebehöhe 2,2 m plus halbe Körperhöhe, wie der Platzhalter) und ist zum Spieler gedreht. Der Gang hat die gemeinsame Steintextur.

**Eine Fassung**, nach der Entscheidung des Menschen vom 2026-11-16 („Körnung mit Nebel“):

- Nebel als Sichtgrenze wie bisher.
- Danach wird das ganze Bild auf 15 Bit (5 je Kanal) mit festem 4 × 4-Bayer-Muster an der Bildschirmposition gerastert.

Im gemeinsamen Modul ist das `compose(..., "spiel")`. Die erste Abgabe mit `_nebel`- und `_dither`-Bildern ist durch diese ersetzt; sie liegt in Commit `945ac1a`.

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | vier Ansichten nach der Art Bible, Schwebehaltung |
| `ansicht_unten.png` | zusätzlich von unten, so wie sie der Spieler sieht |
| `spiel_2m_schweben.png`, `spiel_4m_schweben.png`, `spiel_10m_schweben.png` | Ruhehaltung (Mitte des Flügelschlags) aus 2 m, 4 m, 10 m |
| `spiel_4m_schweben_oben.png`, `spiel_4m_schweben_unten.png` | Flügel oben, Flügel unten |
| `spiel_4m_kreischen.png` | Kreischen, die Ankündigung des Sturzflugs |
| `spiel_4m_sturzflug.png` | Sturzflug |
| `platzhalter_10m.png`, `vergleich_platzhalter_10m.png` | Platzhalter aus `BatViews.cs` im selben Gang aus 10 m; Vergleich vierfach vergrößert, links Platzhalter, rechts neu |
| `fledermaus_textur.png` | die Textur, 128 × 128 |

## Haltungen

Winkel in Grad, aus der Tabelle `POSES` im Skript:

| Haltung | Rumpf | Kopf | Kiefer | Innenflügel heben / schwenken | Außenflügel heben / schwenken |
|---|---|---|---|---|---|
| Schweben (Mitte) | 15 Nase hoch | 0 | 12 auf | 8 / 0 | 4 / 0 |
| Flügel oben | 15 | 0 | 10 | 40 / 0 | 20 / 0 |
| Flügel unten | 15 | 0 | 10 | −35 / 0 | −15 / 0 |
| **Kreischen** | 12 | 30 hoch | 55 weit auf | 65 / −15 nach vorn | 25 / −20 |
| **Sturzflug** | 30 Nase runter | −5 | 45 auf | −12 / 32 nach hinten | −5 / 38 |

- **Kreischen:** Der Umriss wechselt von breit-flach zu hoch, die Flügel stehen fast senkrecht, das Maul ist offen und die Fänge sichtbar. Der Platzhalter zittert dabei; das gehört in den Code.
- **Sturzflug:** Kopf mit Ohren und Fängen voran, die Flügel halb nach hinten angelegt. Ganz angelegt war sie im ersten Versuch zu klein und verschwand. Von vorn sieht man vor allem den Kopf, der auf einen zukommt.

## Zahlen

**680 Dreiecke:**

| Teil | Dreiecke |
|---|---|
| Rumpf mit Beinen und Krallen | 172 |
| Kopf mit Ohren und Fängen | 124 |
| Unterkiefer | 44 |
| Innenflügel | je 40 |
| Außenflügel mit Fingern und Daumenkralle | je 130 |

Das liegt unter den 800 bis 1 200 für normale Gegner. Die Fledermaus ist klein und tritt in Gruppen auf; mehr Dreiecke würden vor allem in Ohren und Gesicht gehen.

**Textur:** 128 × 128, 32 Farben, keine Filterung.

| Bereich | Inhalt |
|---|---|
| Flughaut unten 64 × 64 | planar über die ganze Flügelfläche (beide Teile gemeinsam, beide Seiten teilen sich die Textur); Knochen als dunkle Linien genau entlang der Fingerknochen der Geometrie, Adern verzweigt, Faltenhaut aus feinem Rauschen, zum Rumpf hin dunkler |
| Flughaut oben 64 × 64 | dasselbe dunkel |
| Rumpf 64 × 32 | Bauch fahl, Rücken dunkel, Fellsträhnen |
| Kopf 64 × 32 | Kehle fahl, Scheitel dunkel, nackte faltige Schnauze, kleine tiefe Augen mit winzigem Glanzpunkt, Nasenblatt |
| Knochen, Ohren, Kiefer, Zähne, Krallen | je 16 × 16 bis 32 × 16 |

Der Kopf hat ein Achtel der Fläche (64 × 32 von 128 × 128).

## Teile und Drehpunkte

Spiel-Achsen relativ zur Rumpfmitte, x seitlich, y oben, z vorn:

| Teil | Drehpunkt | hängt an | Bewegung |
|---|---|---|---|
| `body` | 0, 0, 0 | – | nicken |
| `head` | 0, 0,03, 0,09 | body | heben und senken |
| `jaw` | 0, 0, 0,13 | head | Maul öffnen |
| `wing_in_px`, `wing_in_nx` | ±0,06, 0,02, 0,05 (Schulter) | body | heben um die Längsachse, schwenken um die Hochachse |
| `wing_out_px`, `wing_out_nx` | ±0,30, 0,04, 0,11 (Handgelenk) | Innenflügel | heben, schwenken |

Der Flügelschlag des Platzhalters (7 Schläge pro Sekunde, ±40°) passt auf die Innenflügel. Die Außenflügel könnten leicht nacheilen, das gäbe einen weicheren Schlag.

## Vorschlag zu den Maßen

`BodySizes.Bat` hat `Radius = 0,35` mit dem Zusatz „wings spread“. Ausgebreitet sind die Flügel bei mir 1,32 m breit, also 0,66 m je Seite; der Platzhalter hat schon 1,2 m. Ich habe die Spannweite nicht an den Radius angepasst: Eine Fledermaus mit 0,7 m Spannweite wäre aus 10 m wieder unsichtbar, und die Größe macht sie zum Fürchten.

- **Vorschlag:** Radius auf etwa 0,5 m, wenn der Trefferkörper zum Bild passen soll.
- **Alternative:** Er bleibt bei 0,35 m für einen kleineren Kern. Dann gehen Schüsse sichtbar durch die Flügelspitzen.

Die Leitung entscheidet; umsetzen müsste es eine Code-Sitzung.

Die Höhe (0,4 m) passt: Der Rumpf ist etwa 0,15 m dick, Ohren und Füße reichen gut 0,35 m.

## Angewendete Regeln

- **Farbton gegen den Stein** (Regel 1): kaltes Grau und Oliv-Grau, nicht rosagrau.
- **Kein Rot als Zierde:** Die roten Augen des Platzhalters sind weg; die Augen sind klein und dunkel. Das rote Aufglühen vor dem Spucken bleibt Sache des Codes (Gefahr).
- **Ankündigung als eigene Haltung** (Regel 3), Umrisswechsel deutlich.
- **Aus der Sicht des Spielers geprüft** (Regel 6), hier von unten.
- **Aus 10 m nachgemessen** (Regel 7).
- **Prüfbild aus 2 m.**

## Wo ich unsicher bin

- **Waagerechte Flügel aus 10 m:** Aus 10 m sieht man die Fledermaus fast auf Augenhöhe (nur 0,8 m höher), die Flügel in der Mitte des Schlags also fast von der Kante: 46 × 12 Pixel. Hell und breit genug, aber flach. Im Spiel schlägt sie siebenmal pro Sekunde, und die Flügel oben und unten geben dem Umriss Höhe. Das sieht man erst in Bewegung.
- **Die helle Flughaut** könnte als „ein heller Vogel“ statt als Vampir gelesen werden. Die dunklen Knochen, Ohren und Fänge sollen das verhindern. Wenn sie zu freundlich wirkt, wäre der nächste Schritt eine fleckigere, kränklichere Haut, nicht eine dunklere.
- **Der Sturzflug von vorn** zeigt vor allem den Kopf. Bedrohlich, aber kleiner als das Schweben; für eine Angriffsbewegung finde ich das richtig, weil sie schnell näher kommt.
- **Licht für Figuren über Kopfhöhe:** Die Lichtregel bestraft Unterseiten. Für alle Figuren über Kopfhöhe (Fledermaus, später Gorm) wäre ein eigener Mindestwert für nach unten zeigende Flächen eine Überlegung, statt die Farben dafür hochzumalen. Das ist eine Entscheidung für das Spiel.

## Nachtrag: Vorschlag für eine größere Fledermaus (nach dem Urteil der Leitung)

Die Leitung merkt an: Aus 4 m wirkt die Fledermaus eher wie ein blasses, kleines Tier als wie eine Bedrohung. Das liegt an der Größe.

**Ich meine, sie sollte größer sein.**

| Was | heute | Vorschlag |
|---|---|---|
| Spannweite | 1,32 m | etwa 1,8 m (alles mit 1,35 skaliert) |
| Rumpf | etwa 0,4 m lang | etwa 0,55 m lang, so groß wie ein Hund |
| Kopf und Fänge | – | mitwachsend; die Fänge werden aus 4 m 3 bis 4 Pixel lang statt 2 bis 3 |

**Aus 10 m** wäre sie nach der Messung etwa 62 statt 46 Pixel breit.

**`BodySizes.Bat`:**

| Wert | heute | Vorschlag |
|---|---|---|
| `Radius` | 0,35 | 0,6 (passt dann zu den Flügeln) |
| `Height` | 0,4 | 0,5 |
| `HoverHeight` | 2,2 | 2,2 |

**Was zu prüfen wäre:** Bei 3,5 m hohen Räumen bleibt mit `CeilingGap` 0,3 m oben genug Luft; die Unterkante läge bei 2,2 m, knapp über Kopfhöhe. Den Sturzflug, die Treffer und die Kollision mit Wänden müsste eine Code-Sitzung prüfen, weil ein größerer Radius sie verändert.

Am Modell ändert sich nur eine Skalierung im Skript. Die Leitung entscheidet.
