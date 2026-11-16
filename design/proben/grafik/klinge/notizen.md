# Silberklinge im Bild – Probestück 6 (2026-11-16)

Die Klinge des Jägers in der linken Hand aus der Ich-Sicht: ein gerades, zweischneidiges Jagdschwert aus Silber, kurz und mit breiter Klinge, gehalten in derselben Hand im Handschuh mit Ärmel wie die Pistole. PS1-nah mit Textur.

Alles erzeugt [`klinge.py`](klinge.py) mit [`../gemeinsam/psx.py`](../gemeinsam/psx.py) und [`../gemeinsam/arm.py`](../gemeinsam/arm.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/klinge/klinge.py
```

## Bilder

| Bild | Was |
|---|---|
| `ich_sicht_ruhe_nebel.png`, `ich_sicht_ruhe_dither.png` | Ruhe |
| `ich_sicht_ausholen_nebel.png`, `ich_sicht_ausholen_dither.png` | Ausholen beim Aufladen, voll aufgeladen |
| `ich_sicht_schlag_nebel.png`, `ich_sicht_schlag_dither.png` | Schlag am Höhepunkt |
| `ansicht_links.png`, `ansicht_hinten_oben.png`, `ansicht_oben.png` | freie Ansichten, 640 × 640 |
| `klinge_textur.png` | die Textur, 128 × 128 |

**Ich-Sicht:** 854 × 480, 70° vertikal, Augen auf 1,6 m im Steingang, Blick geradeaus.

## Lage und Bewegung

Übernommen aus `WeaponViews.DrawBlade`:

| Haltung | Neigung | Drehung der Spitze | Hand |
|---|---|---|---|
| **Ruhe** | 20° | – | bei (−0,11, −0,095, 0,19) im Kameraraum |
| **Ausholen** (voll aufgeladen) | 20° | 35° nach links | 3 cm links, 1,5 cm hoch, 4 cm zurück |
| **Schlag** (Höhepunkt, t = 1) | 45° | 95° nach rechts quer durchs Bild | 6 cm rechts, 4 cm hoch |

- **Klinge:** steigt wie im Platzhalter 25° aus dem Griff nach oben. In Ruhe läuft sie von unten links in die Tiefe auf die Bildmitte zu.
- **Größe:** wie bei der Pistole 1,8-mal so groß wie die Platzhalter-Quader. Damit kommt das Schwert auf etwa 41 cm (Klinge 27 cm), ein kurzes Jagdschwert.
- **Neigung und Drehung:** Ich habe sie so gedeutet: Die Oberseite neigt sich zur Bildmitte, positive Drehung bringt die Spitze nach rechts. Die Matrizen im Spiel liegen in einem gespiegelten Kameraraum (siehe Notizen der Pistole); die Vorzeichen muss die Code-Sitzung beim Einbau prüfen.
- **Nicht gezeigt:** Das Zittern bei voller Ladung.

## Aufbau

- **Klinge:** Rautenquerschnitt, zur Spitze schmaler, harte Kanten.
- **Griff:** mit Lederwicklung und Silberdraht.
- **Knauf:** silbern.
- **Parierstange:** dunkles Silber wie im Platzhalter (132, 136, 150), mit nach vorn unten eingerollten Enden.
- **Hand:** die linke Hand aus `arm.py` gespiegelt. Die Faust umschließt den Griff waagerecht, der Daumen liegt oben, der Ärmel läuft zum Ellbogen außerhalb des Bildes.

**Dreiecke:** Schwert 152, Hand und Ärmel 248.

**Textur 128 × 128, 32 Farben:**

| Bereich | Inhalt |
|---|---|
| Klinge 128 × 64 | Silber (206, 210, 222), hell geschliffene Schneiden, dunkle Hohlkehle, Ätzung als Ranke am Heft, ein paar angelaufene Flecken |
| Silber 64 × 16 | Parierstange und Knauf, mit Glanz |
| Griff 64 × 16 | Leder in schräger Wicklung mit Silberdraht |
| Handschuh, Ärmel | wie bei der Pistole |

## Angewendete Regeln

- **Waffe in der Ich-Sicht:** In Ruhe zeigt die Klinge in die Tiefe auf die Bildmitte und wird von der Hand gehalten.
- **Bildanteil:** Hand und Klinge nehmen unten links etwa ein Viertel der Bildbreite ein.
- **Silber gehört dem Jäger.** Kein Rot, kein warmes Licht.
- **Zeichnung von hinten oben:** Aus der Blickrichtung des Spielers sieht man Klingenfläche mit Hohlkehle und Ätzung, Parierstange und Handschuh (`ansicht_hinten_oben.png`).

## Wo ich unsicher bin

- **Der Schlag ist sehr nah:** Bei 95° Drehung und 45° Neigung liegt die Klinge quer durchs halbe Bild, und die Faust wird groß und dunkel. Das ist der Höhepunkt eines schnellen Schlags (40 % von 0,28 s), im Spiel nur einen Augenblick zu sehen. Im Standbild wirkt es wuchtig, vielleicht zu sehr.
- **Ausholen:** Die Spitze dreht nach links und zum Teil aus dem Bild. Das ist die Ankündigung des aufgeladenen Schlags; man sieht vor allem Faust und unteren Teil der Klinge.
- **Klinge von der Seite:** In `ansicht_links.png` ist die Klinge sehr dünn (Rautenquerschnitt 5 mm). Im Spiel sieht man sie nie so, aber eine etwas dickere Rippe würde aus manchen Drehungen besser lesen.
