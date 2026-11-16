# Kiste im neuen Stil – Probestück 4 (2026-11-16)

Ein Kistenmodell für alle vier Stufen wie Probestück 2, jetzt PS1-nah mit Textur. Alles erzeugt [`kiste2.py`](kiste2.py) mit den gemeinsamen Bausteinen aus [`../gemeinsam/psx.py`](../gemeinsam/psx.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/kiste-2/kiste2.py
```

## Bilder

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | King-Kiste, 640 × 640, ohne Nebel |
| `stufen_4m.png`, `stufen_10m.png` | Spielbedingungen im Steingang mit Nebel; von links Beggar, Guard, King, Vampire, 1,6 m von Mitte zu Mitte |
| `stufen_10m_dither.png` | dasselbe aus 10 m mit Dither-Sichtgrenze |
| `king_geschlossen_offen_4m.png` | King aus 4 m, links geschlossen, rechts Deckel 110° offen |
| `vergleich_alt_neu_4m.png` | oben die erste Kiste (Probestück 2), unten die neue; Reihe aus 4 m, doppelt vergrößert |
| `textur_beggar.png`, `textur_guard.png`, `textur_king.png`, `textur_vampire.png` | die vier Texturen, je 64 × 64 |

**Spielbedingungen** wie beim Ghul:

- 854 × 480, 70° vertikal, Augen auf 1,6 m, Blick auf 0,35 m Höhe.
- Laterne und Nebel je Pixel.
- Der Gang ist hier 9 m breit, mit der gemeinsamen Steintextur.

## Aufbau

**Geometrie wie in Probestück 2:**

- Kasten mit dunklem Innenraum, Deckel mit sechseckigem Schnitt und 1,5 cm Überstand.
- Zwei Bänder um Kasten und Deckel.
- Schloss vorn, Scharnierleiste hinten.

**Formmerkmal je Stufe auf dem Deckel:**

| Stufe | Merkmal |
|---|---|
| Beggar | schlicht |
| Guard | Eckbeschläge, eiserner Tragegriff |
| King | Eckbeschläge, Krone mit drei Zacken |
| Vampire | Eckbeschläge, vier Dornen |

**Neu ist die Oberfläche:**

| Bereich (Atlas 64 × 64) | Inhalt |
|---|---|
| Bretter 64 × 32 | vier Bretter je Fläche in Körperfarbe: Maserung aus gestauchtem Rauschen, je Brett leicht anderer Ton, dunkle Fugen, helle Oberkante, Nagelköpfe an den Enden, Schmutz unten |
| Eisenband 16 × 32 | Bandfarbe, Narben, dunkle Kanten, Nietenreihe (hell mit Schatten) |
| Schloss 16 × 16 | Messing mit **dunklem Rand**, Schlüsselloch (Kreis und Schlitz), vier Nieten |
| Innenseite 16 × 16 | dunkles Holz für Innenraum und Deckelunterseite |
| Zierrat 32 × 32 | Körperfarbe mit Glanzstreifen, für Krone und Dornen |

**Wie die Textur entsteht:**

- Gemalt in vierfacher Größe, verkleinert und auf 24 Farben reduziert.
- Je Stufe eine Textur mit gleicher Aufteilung; die Geometrie (UVs) ist für alle gleich.
- UV planar: Jede Fläche wird entlang ihrer Hauptrichtung projiziert und auf die Ausdehnung aller Flächen desselben Bereichs normiert. Deshalb laufen die Bretter auf Vorder-, Seiten- und Oberseite quer.

**Licht und Kanten:** Licht je Fläche mit harten Kanten (Holz und Metall), Helligkeit aus der Normale nach der stufenlosen Regel.

**Farben:** unverändert aus `ChestView.cs`. Für Vampire gilt im Bild die Mitte des Pulsierens (175, 25, 35).

## Zahlen

| Stufe | Dreiecke | davon Kasten / Deckel / Schloss |
|---|---|---|
| Beggar | 120 | 48 / 60 / 12 |
| Guard | 188 | 80 / 96 / 12 |
| King | 176 | 80 / 84 / 12 |
| Vampire | 176 | 80 / 84 / 12 |

Das ist innerhalb meiner neuen Richtwerte für Requisiten (100 bis 400). Das Schloss hat jetzt 12 statt 24 Dreiecke; das Schlüsselloch steht in der Textur.

## Teile und Drehpunkte

Wie Probestück 2:

| Teil | Drehpunkt | Hinweis |
|---|---|---|
| `base` | Mitte Boden | – |
| `lid` | 0, 0,455, −0,365 (Scharnier) | offen 110° nach hinten |
| `lock` | 0, 0,47, 0,39 | – |

Je Stufe eine Datei `chest_<stufe>.glb`, mit eingebetteter Textur und ohne Filterung (NEAREST).

## Angewendete Regeln

- **Textur:** 64 × 64 für eine Requisite, begrenzte Palette, keine Filterung. UVs entstehen beim Bauen.
- **Farbton gegen den Stein:** Holzbraun, Stahlblau, Gold und Blutrot liegen alle im Farbton fern vom rosagrauen Stein (Regel aus Ghul-Runde 1). Das gilt auch gegen den neuen texturierten Gang.
- **Dunkle Formen machen Flächen lesbar:** Schlüsselloch, Fugen und Nieten.
- **Keine Grimasse:** Vorn sitzt nur ein senkrechtes Schlüsselloch.
- **Umriss vor Fläche:** Die Stufe liest sich aus 10 m am Umriss oben (schlicht, Griff, Krone, Dornen) und an der Farbe. Die Textur trägt nur aus der Nähe.
- **Kein Rot als Zierde:** Rot gibt es nur bei der Vampir-Stufe.

## Was mir die Bilder zeigen

- **Aus 4 m** (`vergleich_alt_neu_4m.png`): Die neue Kiste liest sich klar als Holzkiste mit Eisenbändern. Bretter, Nieten und Schloss sind deutlich; die erste wirkt daneben wie ein lackierter Klotz.
- **Aus 10 m** (`stufen_10m.png`): Die vier Stufen sind an Farbe und Umriss sicher zu unterscheiden. Die Textur verschwimmt zu einem leichten Streifenmuster, das ist richtig so.
- **King-Schloss:** Auf Gold hebt sich das Messing jetzt durch den dunklen Rand ab, mein Bedenken aus Probestück 2 ist damit erledigt.

## Wo ich unsicher bin

- **Guard als lackiertes Holz:** Die Stahlblau-Stufe ist hier blau gestrichenes Holz. Ein „Wächter“-Kasten könnte auch ganz aus Eisenblech sein: Blechfelder mit Nietenrändern statt Bretter. Das wäre ein zweiter Bretter-Bereich in der Textur, kein neues Modell.
- **Scharfe Eckbeschläge:** Die dreieckigen Leisten haben ihre Nieten nur auf den Außenflächen sinnvoll. Die schräge Innenseite liegt im Kasten und ist unsichtbar.
- **Moiré auf Distanz:** Ohne Filterung und Mipmaps flimmern die Bretterfugen aus der Ferne leicht. Das ist PS1-typisch und im Standbild kaum zu sehen; in Bewegung sollte der Mensch es beurteilen.
