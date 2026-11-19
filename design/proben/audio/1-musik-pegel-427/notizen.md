# Hörprobe Musikpegel (Issue #427)

Urteil aus dem Spieltest: Die Atmosphäre ist auch bei vollem Regler zu leise. **Ungehört** – weder Leitung noch Audiodesigner können hören; unten stehen Messwerte, kein Klangurteil. Teil 1: nur Probe, nichts im Spiel geändert.

## So spielt `Game` die Musik heute (nur gelesen)

Bei Regler auf Anschlag (`Core/Audio/SoundCues.cs`, `Game/Audio/SoundPlayer.cs`):

| Was | Abspielfaktor |
|---|---|
| Katakomben-Musik | Datei × `CatacombsMusicVolume` **1,0** × Regler 1,0 × `MasterVolume` **0,7** |
| Hauptmenü-Musik | Datei × `TitleMusicVolume` **0,8** × Regler 1,0 × `MasterVolume` 0,7 |
| Kurze Geräusche (Schuss, Treffer …) | Datei × `SoundVolume` **1,0** × Regler 1,0 × `MasterVolume` 0,7 |

Der Master 0,7 (−3,1 dB) gilt für alles gleich. Die Musik hat über ihren Faktor also **keinen Spielraum nach oben mehr** (1,0), die Katakomben-Datei selbst ist leise.

## Messwerte (ffmpeg `ebur128=peak=true`; „im Spiel“ = Datei + Faktor + Master)

| Datei | Länge | Spitze Datei | RMS Datei | LUFS Datei | Spitze im Spiel | LUFS im Spiel |
|---|---|---|---|---|---|---|
| `musik-katakomben.ogg` | 64,0 s | −20,7 dBFS | −34,0 dBFS | −32,5 | −23,8 | −35,6 |
| `musik-hauptmenue.ogg` | 72,0 s | −7,8 | −23,5 | −22,3 | −12,8 | −27,3 |
| `ambiente-katakomben.ogg` (nicht mehr gespielt) | 30,0 s | −22,6 | −37,9 | −41,0 | −25,7 | −44,1 |
| `p1-schuss-1.wav` | 0,6 s | −1,5 | −19,7 | −19,3 | −4,6 | −22,4 |
| `p1-schuss-geweiht-1.wav` | 0,6 s | −1,5 | −20,4 | −20,4 | −4,6 | −23,5 |
| `p1-treffer-1.wav` | 0,5 s | −4,5 | −27,6 | −24,3 | −7,6 | −27,4 |
| `p1-schmerz.wav` | 0,4 s | −5,0 | −18,9 | −19,8 | −8,1 | −22,9 |
| `pistole.wav` | 0,4 s | −1,5 | −18,2 | −20,0 | −4,6 | −23,1 |

(Sehr kurze Geräusche: LUFS ist dort nur grob, `p1-schritt`/`p1-gorm-treffer` liegen unter dem Messfenster. Die Katakomben-Musik liegt rund 13 dB (LUFS) unter einem Schuss und 10 dB unter dem Hauptmenü-Stück; ihre Spitze hat 19 dB Luft bis 0 dBFS.)

## Proben (je 20 s Ausschnitt ab Sekunde 20 der Katakomben-Datei, 0,5 s ein-/ausgeblendet, Stereo, volle Regler)

Jede Probe = Datei × 1,0 × 0,7 × Stufe, also genau so laut wie im Spiel bei vollem Regler. Der Mensch stellt seine Lautstärke **einmal** ein (am besten mit `bezug-schuss.wav` etwa so, wie ihm ein Schuss im Spiel recht ist) und lässt sie für alle Proben unverändert.

| Datei | Stufe | Spitze | wahre Spitze | RMS | LUFS | begrenzt? |
|---|---|---|---|---|---|---|
| `heute.wav` | ±0 dB (Faktor 1,0) | −24,6 dBFS | −24,6 | −37,9 | −36,4 | nein |
| `plus6.wav` | +6 dB (Faktor ×2,0) | −18,6 | −18,6 | −31,9 | −30,4 | nein |
| `plus10.wav` | +10 dB (Faktor ×3,16) | −14,6 | −14,6 | −27,9 | −26,4 | nein |
| `bezug-schuss.wav` | Schuss 1 im Spiel | −4,6 | – | −22,8 | – | nein |
| `folge-heute-plus6-plus10-mit-schuss.wav` | heute, +6, +10 nacheinander, je 10 s, 1,5 s Pause; ein Schuss 5 s nach jedem Beginn (bei 0 s, 11,5 s, 23,0 s) | −4,0 | – | – | −26,9 | nein |

Keine Probe musste begrenzt werden (Grenze −1 dBFS, größte Spitze −4,0 im Folge-Mix mit Schuss). Das Skript enthielte eine sanfte tanh-Begrenzung, sie hat nicht ausgelöst. Mit +10 dB liegt die Musik (LUFS −26,4) etwa so laut wie das Hauptmenü-Stück im Spiel (−27,3); +14 dB wäre noch möglich (Spitze −10,7), wurde nicht erzeugt.

## Empfehlung (Zahlen, kein Klangurteil)

- Abspielfaktor in `Game` anheben geht **nicht**: Er steht schon bei 1,0, und Raylib/miniaudio über 1,0 zu treiben ist ungeprüft und nicht Sinn des Reglers (der nur bis 1 geht).
- Einbau also: **Datei neu erzeugen**, d. h. `musik-katakomben.ogg` um die gewählte Stufe lauter pegeln (Skript in `tools/`, das den Pegel bitgleich aus dem Original anhebt; Faktor als Konstante, Referenzwerte der alten Datei fest im Skript, nicht aus der überschriebenen Datei lesen). `CatacombsMusicVolume` bleibt 1,0. Die Hauptmenü-Musik (0,8) wäre nur betroffen, wenn der Mensch auch sie lauter will.
- Stufe: Das Hörurteil entscheidet. Als Messanker: +10 dB bringt die Katakomben-Musik auf die Lautheit des Hauptmenü-Stücks; +6 dB ist die Hälfte davon.
- Zu bedenken (Audio Bible, Regel 6 und „Ankündigungen dürfen von nichts überdeckt werden“): Die Musik darf lauter werden, die Mitte soll aber frei bleiben; nach dem Einbau Band 300–3 000 Hz erneut messen. Die vorhandene Datei ist nicht auf −1 dBFS ausgesteuert, eine bloße Verstärkung ändert das Spektrum nicht.

## Wiederholen

`python design/proben/audio/1-musik-pegel-427/bauen.py <repo-wurzel> <ausgabeordner>` (nur Standardbibliothek und `ffmpeg`, ca. 15 s).
