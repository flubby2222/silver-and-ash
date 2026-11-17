# Geräuschpaket 1 – Übersicht

Alle 31 Dateien aus der neuen Palette ([palette-neu.md](../soundcraft-versuch/palette-neu.md)), nach der Methode aus dem Video ([video.md](../soundcraft-versuch/video.md)): fünf Quell-Samples (Schlag, Hauch, Pfeife, Glocke, Stimme), je einmal in SoundCraft gebaut, auf 22 050 Hz in VAG gewandelt und je Geräusch vom Sampler in der Tonhöhe verschoben gespielt; ein gemeinsamer kurzer Raum (Hall 0,8 s, 22 %). **Ungehört**; alle Zahlen sind gemessen. Erzeugung: `python paket1.py` (zweimal gerechnet, bitgleich); Dateien in `dateien/` und als Übergabe in [design/uebergabe/assets/audio/](../../uebergabe/assets/audio/) (Präfix `p1-`, Prüfsummen in `audio-paket-1.sha256`); Zuordnung zu den Spielereignissen und fehlende Ereignisse: [README-audio-paket-1.md](../../uebergabe/README-audio-paket-1.md).

## Je Geräusch eine Zeile

| Datei | Quelle | gespielt (Halbtöne gegen Aufnahme) | Dauer | Spitze dBFS | Mittel (RMS) dBFS | Kategorie |
|---|---|---|---|---|---|---|
| `p1-schuss-1.wav` | schlag | C3 (-12) | 0.60 s | -1.5 | -19.7 | schuss |
| `p1-schuss-2.wav` | schlag | H2 (-13) | 0.60 s | -1.5 | -19.4 | schuss |
| `p1-schuss-3.wav` | schlag | Cis3 (-11) | 0.60 s | -1.5 | -19.9 | schuss |
| `p1-nachladen-start.wav` | schlag | D4 (+2) | 0.30 s | -8.0 | -31.5 | nachladen |
| `p1-nachladen-bereit.wav` | schlag | G4 (+7) | 0.25 s | -8.0 | -31.9 | nachladen |
| `p1-schwung-1.wav` | hauch | E4 (+4) | 0.50 s | -9.0 | -29.5 | schwung |
| `p1-schwung-2.wav` | hauch | Dis4 (+3) | 0.50 s | -9.0 | -29.4 | schwung |
| `p1-treffer-1.wav` | schlag | C5 (+12) | 0.45 s | -4.5 | -27.6 | treffer |
| `p1-treffer-2.wav` | schlag | H4 (+11) | 0.45 s | -4.5 | -26.5 | treffer |
| `p1-treffer-3.wav` | schlag | Cis5 (+13) | 0.45 s | -4.5 | -27.6 | treffer |
| `p1-schritt-l1.wav` | schlag | A2 (-15) | 0.28 s | -12.0 | -33.0 | schritt |
| `p1-schritt-l2.wav` | schlag | Gis2 (-16) | 0.28 s | -12.0 | -32.8 | schritt |
| `p1-schritt-l3.wav` | schlag | B2 (-14) | 0.28 s | -12.0 | -33.1 | schritt |
| `p1-schritt-r1.wav` | schlag | H2 (-13) | 0.28 s | -12.0 | -33.3 | schritt |
| `p1-schritt-r2.wav` | schlag | B2 (-14) | 0.28 s | -12.0 | -33.1 | schritt |
| `p1-schritt-r3.wav` | schlag | C3 (-12) | 0.28 s | -12.0 | -33.4 | schritt |
| `p1-sprung.wav` | hauch | C5 (+12) | 0.30 s | -11.0 | -30.5 | bewegung |
| `p1-dash.wav` | hauch | G3 (-5) | 0.60 s | -11.0 | -30.1 | bewegung |
| `p1-landung.wav` | schlag | Fis2 (-18) | 0.35 s | -11.0 | -30.6 | bewegung |
| `p1-schmerz.wav` | stimme | G3 (+3) | 0.60 s | -5.0 | -19.1 | schmerz |
| `p1-ghul-entdecken.wav` | stimme | E3 (+0) | 0.90 s | -6.0 | -19.8 | ghul |
| `p1-ghul-ankuendigung.wav` | stimme | E4 (+12) | 1.00 s | -1.5 | -19.3 | ankuendigung |
| `p1-ghul-sterben.wav` | stimme | A2 (-7) | 1.30 s | -6.0 | -21.4 | ghul |
| `p1-tuer-oeffnen.wav` | hauch | G2 (-17) | 1.30 s | -7.0 | -26.6 | tuer_kiste |
| `p1-kiste-oeffnen.wav` | schlag | B2 (-14) | 0.50 s | -7.0 | -28.6 | tuer_kiste |
| `p1-menue-klick.wav` | schlag | C5 (+12) | 0.12 s | -12.0 | -34.5 | menue |
| `p1-aufheben-silber.wav` | glocke | E4 (+12) | 0.60 s | -8.0 | -30.0 | aufheben |
| `p1-aufheben-phiole.wav` | glocke | G3 (+3), H3 (+7) | 0.80 s | -8.0 | -28.3 | aufheben |
| `p1-aufheben-weihmunition.wav` | glocke | E4 (+12), H3 (+7) | 0.80 s | -8.0 | -28.1 | aufheben |
| `p1-aufheben-schluessel.wav` | glocke | E3 (+0), H3 (+7), E4 (+12) | 0.90 s | -8.0 | -27.1 | aufheben |
| `p1-aufheben-ausruestung.wav` | glocke | E2 (-12) | 1.30 s | -8.0 | -28.9 | aufheben |

„gespielt“: Ton und Halbtöne gegen die Aufnahme des Samples (Schlag und Hauch sind bei C4 aufgenommen und tonlos; Pfeife, Glocke und Stimme bei E3). Tonale Geräusche (Aufheben) liegen auf Tönen von e-Moll.

## Lautheit je Kategorie

Die Spitze richtet sich nach den bisherigen Dateien im Spiel (Schuss −1,5, Klinge −4,9, Schritt −12,1, Ghul-Ankündigung −3,0 dBFS) und ist je Kategorie fest. Die Mittelwerte (RMS) liegen bei Schuss, Schritt und Ankündigung 1 bis 2,5 dB unter den alten Dateien (alt: Schuss −18,2, Schritt −30,8, Ankündigung −18,3 dB), beim Treffer etwa 3 bis 4 dB darunter (alt −23,7). Alle Dateien spielt das Spiel mit Lautstärke 1; die Lautheit steckt in den Dateien.

| Kategorie | Dateien | Spitze dBFS | Mittel (RMS) dBFS |
|---|---|---|---|
| ankuendigung | 1 | -1.5 | -19.3 … -19.3 |
| schuss | 3 | -1.5 | -19.9 … -19.4 |
| schmerz | 1 | -5.0 | -19.1 … -19.1 |
| ghul | 2 | -6.0 | -21.4 … -19.8 |
| treffer | 3 | -4.5 | -27.6 … -26.5 |
| nachladen | 2 | -8.0 | -31.9 … -31.5 |
| schwung | 2 | -9.0 | -29.5 … -29.4 |
| bewegung | 3 | -11.0 | -30.6 … -30.1 |
| tuer_kiste | 2 | -7.0 | -28.6 … -26.6 |
| aufheben | 5 | -8.0 | -30.0 … -27.1 |
| schritt | 6 | -12.0 | -33.4 … -32.8 |
| menue | 1 | -12.0 | -34.5 … -34.5 |

Reihenfolge von laut nach leise: Ankündigung, Schuss, Schmerz, Ghul, Treffer, Nachladen, Schwung, Bewegung, Tür/Kiste, Aufheben, Schritte, Menü. Die **Ankündigung** (`p1-ghul-ankuendigung`) hat mit dem Schuss die höchste Spitze (−1,5 dBFS), liegt aber in einem **eigenen Band**: 300 bis 3 000 Hz tragen praktisch die ganze Energie, unter 300 Hz liegt sie 29 dB tiefer (die Katakomben-Musik liegt in dieser Mitte mindestens 15 dB unter ihrem Gesamtpegel). Sie hat genau einen Einsatz (gemessen nach Regel 2 der Audio Bible).

## Wo ich am unsichersten bin

1. **Stimme (Ghul, Schmerz):** Die Stimme ist eine Säge mit zwei Formant-Spitzen (700 und 1 150 Hz), als Sample bei E3; Tonhöhenfahrten macht der SoundCraft-`pitch_shifter` per Automation (Anstieg der Ankündigung, Fall beim Sterben und beim Schmerz). Ob das „menschlich“ oder wie ein Synthesizer klingt und ob der Tonhöhen-Effekt Artefakte hat, kann ich nicht hören. Der Mensch wollte Ghul „menschlich und sehr einfach“; dafür ist eine **vierte und fünfte Quelle** (Hauch, Stimme) nötig geworden, weil Schlag, Pfeife und Glocke keine Stimme und keinen Luftklang liefern.
2. **Klingenschwung (Hauch):** Charakter von 2a (luftig, kein Anschlag) in der neuen Palette gebaut: weißes Rauschen, Bandpass fährt von 900 auf 3 800 Hz (0,35 s), Pegel steigt sanft und fällt (45 dB unter, 0,15 s hoch, 0,5 s Ende), **kein** Tiefpass-Sturz wie beim Rauschschlag. Gemessen sitzt er hell (Schwerpunkt etwa 2,8 kHz) und hat anders als der Treffer keinen Anschlag. Ob er wie ein Schwung klingt und nicht wie ein Zischen, ist offen; ebenso, ob er zu 2b (Treffer) „gleichzeitig“ abgespielt noch trennt.
3. **Gleiche Quelle für Schuss, Treffer, Schritte, Nachladen, Landung, Kiste, Menü-Klick:** Alle sind derselbe Rauschschlag bei anderer Tonhöhe und Länge. Das macht sie zu einer Familie, kann aber heißen, dass man sie bei schnellem Wechsel schwer auseinanderhält (Schritt, Landung und Kiste sind alle dumpfe Schläge, 12 bis 18 Halbtöne unter der Aufnahme). Unterscheidung nur über Tonhöhe und Länge.
4. **Sprung, Dash, Tür:** Dasselbe Hauch-Sample in drei Tonlagen (hoch/kurz, mittel, sehr tief/lang). Als „Tür“ ist ein tief gespieltes Rauschen vielleicht nicht schwer genug; die Messung zeigt bei Dash und Tür mehrere Pegelanstiege (Rauschen mit Bandpassfahrt), was als Zucken zu hören sein kann.
5. **Aufheben je Verbrauchsgut:** Unterscheidung nur durch Zahl, Richtung und Lage der Töne (Silber ein Ton E4; Phiole zwei aufsteigend G3→H3; Weihmunition zwei fallend E4→H3; Schlüssel drei E3→H3→E4; Ausrüstung ein tiefer langer Ton E2). Ob man Phiole und Weihmunition mit Hören auseinanderhält, ist ungeprüft; ob sie bei vielen Pickups hintereinander nerven, auch.
6. **Fehlende Ereignisse im Spiel:** `p1-ghul-entdecken`, Sprung, Dash, Landung, Menü-Klick und das Aufheben von Ausrüstung haben noch kein Ereignis (Liste in der Übergabe); die Dateien sind gebaut, aber nicht auslösbar.
