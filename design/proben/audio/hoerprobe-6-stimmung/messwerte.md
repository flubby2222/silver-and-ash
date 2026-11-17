# Messwerte – Hörprobe 6 (Stimmungsskizzen)

Erzeugt mit `python messen.py`. Geprüft nach Regeln, **nichts gehört**. Spitze und Mittel aus `ffmpeg volumedetect`, Lautheit aus `ffmpeg ebur128`, Tonklassen aus einer eigenen Goertzel-Analyse der fertigen Datei (C1–H6).

## Rahmen

| Datei | Länge | Spitze | Mittel | Lautheit | erstes / letztes Sample | Rest vor Ende | Ergebnis |
|---|---|---|---|---|---|---|---|
| `a-gruft-orgel.wav` | 26.0 s | -9.2 dBFS | -19.4 dBFS | -23.4 LUFS | 0 / 0 | -83 dB | bestanden |
| `b-glocken-chor.wav` | 26.0 s | -3.0 dBFS | -21.2 dBFS | -23.4 LUFS | 0 / 0 | -85 dB | bestanden |

Skizzen sind Hörproben, keine Schleifen; sie enden in Stille (kein Nahtkriterium). 22 050 Hz PS1-Kette, mono.

## Tonleiter und Grundton

**a-gruft-orgel** – Tonleiter D, Es, F, G, A, B, C, Grundton D.

- Notenliste: Pedal-Orgel: 4 Noten, 0 außerhalb; Streicher: 20 Noten, 0 außerhalb; Orgel-Gang: 3 Noten, 0 außerhalb.
- Tonumfang (MIDI): Pedal-Orgel 26–34; Streicher 50–67; Orgel-Gang 74–77.
- Audio: Energie außerhalb der Tonleiter **0.0 %** (Obertöne der Säge- und Impulsklänge zählen mit, null ist darum nicht zu erwarten); stärkste Tonklasse **D** (86 %) – = Grundton.
- Tonklassen (Anteil in %): C 0, Cis 0, D 86, Es 0, E 0, F 0, Fis 0, G 8, As 0, A 1, B 5, H 0

**b-glocken-chor** – Tonleiter E, Fis, G, A, H, C, D, Grundton E.

- Notenliste: Glocke: 3 Noten, 0 außerhalb; Chor: 16 Noten, 0 außerhalb.
- Tonumfang (MIDI): Glocke 47–52; Chor 52–69.
- Audio: Energie außerhalb der Tonleiter **0.1 %** (Obertöne der Säge- und Impulsklänge zählen mit, null ist darum nicht zu erwarten); stärkste Tonklasse **E** (53 %) – = Grundton.
- Tonklassen (Anteil in %): C 0, Cis 0, D 0, Es 0, E 53, F 0, Fis 0, G 12, As 0, A 2, B 0, H 32

## Stimmführung (vierstimmiges Streicher- bzw. Chor-Pad)

| Skizze | Spur | größter Sprung einer Stimme | Stimmkreuzungen | parallele Quinten/Oktaven |
|---|---|---|---|---|
| a-gruft-orgel | Streicher | 4 Halbtöne | 0 | 0 |
| b-glocken-chor | Chor | 3 Halbtöne | 0 | 0 |

## Spektrum und Pegel

Mittlerer Pegel je Band (dBFS, je zwei Filter 2. Ordnung) und Abstand der Mitte (300–3 000 Hz) zum Gesamtpegel. Vorschlag aus dem Konzept: Mitte mindestens 15 dB unter dem Gesamtpegel (damit Ankündigungen frei bleiben).

| Skizze | < 120 Hz | 120–300 | 300–3 000 | > 3 000 | gesamt | Abstand Mitte | Vorschlag erfüllt? |
|---|---|---|---|---|---|---|---|
| a-gruft-orgel | -20 | -33 | -33 | -70 | -19 | -14.0 dB | NEIN |
| b-glocken-chor | -24 | -30 | -34 | -84 | -21 | -12.7 dB | NEIN |

**Der Vorschlag wird verfehlt** (a: -14.0 dB, b: -12.7 dB). Die Mittelspuren (Streicher, Orgel-Gang bzw. Chor, Glocke) habe ich schon abgesenkt; weiter absenken hieße, beide Skizzen auf Bass und Hall zu reduzieren. Ob 15 dB das richtige Maß ist, zeigt erst der Test im Spiel mit den Geräuschen (wird eine Ankündigung verdeckt?); das ist Sache seines Ohres. Beide Skizzen sind auf gleiche Lautheit (LUFS, siehe oben) eingestellt, damit der a/b-Vergleich nicht an der Lautstärke hängt.

## Dichte und Tempo

| Skizze | Tempo | Takte | Noten je Takt |
|---|---|---|---|
| a-gruft-orgel | 50 Schläge/min | 5 | Pedal-Orgel 0.8, Streicher 4.0, Orgel-Gang 0.6 |
| b-glocken-chor | 60 Schläge/min | 6 | Glocke 0.5, Chor 2.7, Herzschlag 3.3 |

## Was ich nicht prüfen konnte

- Ob es **düster** klingt statt nur dunkel, ob die Akkordfolge trägt, ob die Stimmung zu den Katakomben passt.
- Ob die Orgel nach Orgel klingt und die Streicher nicht nach Sägezahn; ob Glocke und Chor glaubhaft sind. Ich habe dafür nur Regeln angewandt (mehrere leicht verstimmte Stimmen, Register in Oktaven und Quinten, langsame Einschwingzeiten, wandernde Filter, unharmonische Glocken-Teiltöne, Vokalformanten) und nichts davon gehört.
- Die Mischung (Pegel der Spuren zueinander und zum Hall) und ob der Hall zu lang oder zu kurz ist.
- Ob die Skizze als Spielmusik funktioniert (Länge der Schleife, Wiederholung, Zusammenspiel mit Geräuschen); sie ist keine Schleife.
