# SoundCraft-Versuch – Notizen und ehrliches Urteil

Stand 2026-11-17. Benutzt wurde nur `C:\Program Files\ArtCraft\SoundCraft\soundcraft-cli.exe` (Fassung 0.20.1, SHA-256 `c6fc97e5c33b5e4005b9b0e006f3804211692a63964f0eb8f6227de32ac0de93`), nur die Unterbefehle `commands`, `describe`, `plugins` (über `run --cmd engine.plugins`), `script`, `info`, `--version`. Kein Fenster, kein `app`, kein `mcp`, keine Einstellungen geändert, nichts aus dem Netz, nur eingebaute Plugins. **Alles hier ist gemessen, nichts gehört.**

Wiederholen: `python design/proben/audio/soundcraft-versuch/bauen.py` (nur Standardbibliothek, `ffmpeg`, `soundcraft-cli.exe`; ein Lauf dauert etwa 20 s). Die erzeugten SoundCraft-Skripte liegen in [skripte/](skripte/) (je Zeile `ID JSON`), die Hörprobe in [hoerprobe-8-soundcraft/](hoerprobe-8-soundcraft/hoerprobe.md).

## Korrektur vom 2026-11-17 (nach Hörprobe 8): Die b-Skizze war fehlerhaft

**Die erste Fassung von `4b-skizze.wav` war falsch.** SoundCraft zählt **960 Ticks je Viertel** (gemessen: 1 920 Ticks = 1 s bei 120/min), ich hatte 480 angenommen. Dadurch lief die Skizze mit **doppeltem Tempo**: Die Akkorde wechselten nach 4 statt nach 8 s, und ab etwa 13 s war sie **stumm** (gemessen: Pegel je Sekunde −180 dBFS). Der Mensch hat seine Hörprobe-8-Antwort zur Skizze („klingt wie die letzten Hörproben“) also auf einer kaputten Datei gegeben. Behoben in `bauen.py` (`TICKS_PRO_SCHLAG = 960`); `4b-skizze.wav` und `3b-aufheben.wav` (Notenlänge von 0,25 auf 0,5 s) sind neu gerechnet, die Tabelle unten zeigt die neuen Werte. Schuss und Klinge (je eine Note) waren nicht betroffen. Gefunden habe ich es erst, als ich Notenzeiten in einem neuen Versuch gegen die Uhr prüfte; ich hätte die Skizze nach dem Bau auf Pegel je Sekunde prüfen müssen. Neue Prüfregel für `bauen.py`-Ausgaben: Pegel je Sekunde der Skizze ansehen (keine Stille vor dem Ende).

## Was SoundCraft kann (aus `commands`, `describe`, `plugins`)

- **Sehr großer Umfang:** 484 Befehle einer vollständigen Schnittsoftware (Sitzungen, Spuren, Clips, MIDI-Noten, Mischpult, Automation, Transport). Für uns relevant sind nur wenige: `session.new`, `track.new`, `midi.new_clip`, `midi.note_add`, `mix.instrument`, `mix.insert`, `mix.volume`, `file.bounce_mix`.
- **Klangerzeuger (3):** `subtractive_synth` (zwei Oszillatoren Saw/Square/Sine/Triangle mit Verstimmung und Halbtonabstand, Filter mit Resonanz und eigener Hüllkurve, Hüllkurve für die Lautstärke, Glide), `drum_synth` (Bassdrum, Snare mit Rauschanteil, Hi-Hats, Toms, Becken, je Stimmung und Ausklingzeit), `sampler` (nicht benutzt: keine fremden Samples).
- **Effekte (28):** EQ (1- und 7-Band), Kompressor/Limiter, Gate, De-Esser, Maximizer, Channel Strip, Raum- und Plattenhall, Delay, Auto-Pan, Chorus, Flanger, Phaser, Ringmodulator, Sättigung, Bit-Crusher (`lofi`: Bit-Tiefe, Abtastrate, Rauschen), Gleichrichter, Subharmonische, Tonhöhenverschiebung, Gain/Trim/Invert/DC-Entfernung, Signalgenerator, Dither.
- **Ausgabe:** `file.bounce_mix` als WAV/AIFF/FLAC, 16/24/32 Bit, mit Bereich, Dither an/aus, wahlweise Hauptmischung oder Bus; meldet Spitze, wahre Spitze und Lautheit (LUFS) gleich im Ergebnis.
- **Reproduzierbar:** Jedes der vier Stücke ergab bei zwei Läufen **dieselbe Datei, Byte für Byte** (mit `dither: false`; auch mit eingeschaltetem Dither war ein Probelauf gleich).

## Was es nicht kann (oder was ich nicht fand)

- **Keine Effekte auf Bussen** in der Kommandozeile: Ein Bus (`mix.new_bus`) nimmt Sends an, aber `mix.insert` findet ihn nicht. Der „gemeinsame Raum“ ist deshalb **auf jeder Spur dieselbe Hall-Einstellung** (Hall ist linear, die Summe ist gleichwertig zu einem Bus; die interne Hall-Modulation kann sich aber geringfügig unterscheiden). Eine Hilfsspur („aux“) lässt sich nicht als Send-Ziel verwenden; `track.output` auf ihren Namen legt stattdessen still einen leeren Bus an (Ergebnis: Stille).
- **Kein Rauschen im Synthesizer** (nur im Signalgenerator, der eine Audiospur braucht) und **keine Tonhöhenhüllkurve** für den Synthesizer: Der Schlag mit fallender Tonhöhe (mein Klingen-Körperton) ist nur über die fertigen Stimmen des `drum_synth` möglich.
- **Keine Glocke im eigentlichen Sinn:** Ich kann zwei Sinus mit festem Halbtonabstand (hier 15, ungefähr das Verhältnis 2,38) mischen; mein Python-Glockenprofil hat neun Teiltöne mit eigener Abklingzeit.
- **Keine PS1-Kette** (kein ADPCM, keine Gauß-Interpolation, kein SPU-Hall). `lofi` ist nur ein Bit-Crusher. Ich habe die PS1-Kette deshalb **hinten angehängt**: SoundCraft-Datei → 16 Bit → mono → `ps1_klang.py`. Das funktioniert ohne Umbau, ist aber ein Schritt außerhalb.
- Nicht ausprobiert: Automation (Verläufe von Parametern), `sampler`, `pitch_shifter`, die Fensteroberfläche.

## Wo es ohne Gehör hakt

Gleiche Grundschwäche wie bei meinen Skripten: Ich sehe nur Zahlen. Neu ist nur, dass `bounce_mix` Spitze und Lautheit sofort zurückgibt; alles andere (Spektrum, Schwerpunkt, Bänder) messe ich wie bisher selbst. Hindernisse, die Zeit gekostet haben (jeweils nach höchstens zwei Versuchen gelöst oder als nicht machbar festgehalten):

1. **Einheiten:** Clip- und Bounce-Bereiche sind **Samples**, nicht Sekunden (`end: 1.2` rendert ein Sample, Ergebnis Stille).
2. **Clip-Nummern** vergibt SoundCraft selbst (Spuren, Busse und Clips teilen sich die Zählung); `midi.note_add` braucht sie. Das Skript `bauen.py` ermittelt sie mit einem Probelauf.
3. **Befehlsnamen:** Der Plan nannte `mix.instrument_param(s)`; für mehrere Werte heißt der Befehl `mix.instrument` mit `params`. Auswahlwerte (z. B. Wellenform, EQ-Typ) sind **Zahlen** (Index); die Namen dazu stehen nur in `engine.plugins`, nicht in `describe`.
4. **Hi-Hat und Becken sind sehr leise** (roh −38 dBFS Spitze nach dem Tiefpass); beim Anheben auf gleiche Lautheit wächst der Rauschboden mit. Besser wäre eine eigene Verstärkung in der Kette (nicht getestet).
5. **Ob ein Klang „stimmt“, weiß ich nicht.** Der Tiefpass-Typ (Index 4) und die Tonhöhe sind aus den Messwerten (Bänder, Schwerpunkt) bestätigt, nicht gehört.

**Netz und Dateien:** Während eines Skriptlaufs (alle 30 ms abgefragt) hatte `soundcraft-cli.exe` **keine** TCP-/UDP-Verbindung; das ist eine Stichprobe, kein Beweis. Das separat laufende Programm **„ArtCraft Launcher“** (Prozess `artcraft-launcher`, vom Menschen installiert, nicht von mir gestartet) fragt dagegen im Hintergrund regelmäßig das Netz ab (Protokoll `%LOCALAPPDATA%\ArtCraft Launcher\logs`, vermutlich Update-Prüfung). Das habe ich nicht angefasst; ob der Mensch den Launcher laufen lassen will, ist seine Entscheidung. Eine Einstellungs- oder Konfigurationsdatei des CLI habe ich nicht gefunden.

## Messwerte (je Stück: SoundCraft-Rohdatei, a, b nach der PS1-Kette und gleicher Lautheit)

Pegel in dBFS; „Schwerpunkt“ = Spektrum-Schwerpunkt der ersten 0,37 s; Bänder = Energie je Band minus Gesamtenergie (dB). Gleiche Lautheit: bei den drei Geräuschen gleiche Energie (RMS) über gleiche Länge, bei der Skizze gleiche Lautheit nach `ebur128`; Spitze höchstens −1 dBFS.

| Stück | Datei | Spitze | RMS | LUFS | Schwerpunkt | < 300 Hz | 300–3 000 Hz | > 3 000 Hz |
|---|---|---|---|---|---|---|---|---|
| Schuss | SoundCraft roh | −10,6 | −35,6 | – | 190 Hz | −0,1 | −18,3 | −20,0 |
| | a (Python, PS1) | −6,2 | −25,9 | −24,7 | 92 Hz | −0,1 | −17,1 | −33,9 |
| | b (SoundCraft, PS1) | −1,0 | −25,9 | −23,1 | 167 Hz | −0,1 | −18,0 | −22,1 |
| Klinge | SoundCraft roh | −37,8 | −63,0 | – | 6 882 Hz | −18,6 | −16,2 | −0,2 |
| | a | −1,6 | −25,6 | −22,7 | 157 Hz | −0,1 | −16,8 | −22,8 |
| | b | −1,0 | −25,6 | −19,3 | 5 121 Hz | −12,1 | −11,3 | −0,6 |
| Aufheben (H4 = 494 Hz) | SoundCraft roh | −20,4 | −39,2 | – | 507 Hz | −34,7 | 0,0 | −59,5 |
| | a | −6,6 | −19,8 | −19,9 | 437 Hz | −5,7 | −1,4 | −53,6 |
| | b | −1,0 | −19,8 | −18,3 | 507 Hz | −34,0 | 0,0 | −60,4 |
| Skizze 20 s | SoundCraft roh | −21,8 | −40,3 | – | 166 Hz | 0,0 | −20,5 | −64,0 |
| | a (Anfang Menü-Thema) | −4,1 | −19,5 | −18,1 | 145 Hz | −0,2 | −13,4 | −59,5 |
| | b | −2,4 | −19,9 | −18,1 | 166 Hz | 0,0 | −21,1 | −64,7 |

Lesen: Die Mitte (300–3 000 Hz) liegt bei b in der Skizze 21 dB unter dem Gesamtpegel (Regel „Mitte frei“ ≥ 15 dB: erfüllt), bei a nur 13 dB (das fertige Theme ist dort dichter). Der Schuss ist bei b heller (mehr Energie über 3 kHz). Die Klinge ist bei a ein dumpfer Schlag, bei b helles Rauschen – **zwei verschiedene Klangarten**, nicht zwei Ausführungen derselben. Aufheben: b ist ein einfacherer Ton (ein Teilton über 1 kHz, kaum Tiefe), a hat durch die Glocken-Teiltöne zusätzlich Bass.

## Mein Urteil: lohnt der Umstieg?

**Nein, nicht als Ersatz meines Weges; vielleicht als gemeinsame Werkbank mit dem Menschen.**

- **Was für SoundCraft spricht:** Schneller (vier Stücke in 20 s gegen 5–10 Minuten je Musikstück in reinem Python), fertige Effekte (Hall, Sättigung, Begrenzer, Kompressor, EQ) statt eigener Filter, bitgleiche Ausgabe, Spitze und Lautheit sofort im Ergebnis. Das Wichtigste: **Der Mensch kann dieselbe Sitzung im Fenster öffnen und nach Gehör nachstellen** (mit `--save sitzung.scraft` lässt sich eine Sitzung mitgeben; das habe ich nicht getan und das Fenster nicht gestartet). Das ist die Rückkopplung, die mir fehlt.
- **Was dagegen spricht:** Weniger Kontrolle über den Klang selbst. Ohne Tonhöhenhüllkurve, ohne Rauschquelle im Synthesizer und ohne Glockenprofil bekomme ich weniger Klangfarben als mit meinem Python (Beispiel: Klinge – kein fallender Körperton; Aufheben – zwei statt neun Teiltöne). Effekte auf Bussen fehlen, die PS1-Kette bleibt ein separater Schritt in Python. Das Werkzeug ist laut seiner Projektseite „pre-alpha“; ein Update kann Befehle ändern (ich prüfe deshalb die SHA-256 in `bauen.py` nicht automatisch, sondern halte sie hier fest).
- **Was es nicht ändert:** Ob die Stücke gut klingen, entscheidet das Ohr des Menschen. Meine Auswahl bleibt blind; SoundCraft liefert mir nur schneller mehr Varianten.

**Empfehlung nach dem Urteil des Menschen zu Hörprobe 8:**
- Gewinnt **a** in 1–3: bei meinem Weg bleiben; SoundCraft höchstens für die Musikskizzen mit dem Menschen am Fenster.
- Gewinnt **b** bei Schuss oder Klinge: Für geräuschhafte Treffer (`drum_synth` + Tiefpass + Raum) kann SoundCraft einen eigenen, kohärenten Zweig bilden; Tontreffer wie Aufheben bleiben bei meinem Glockenprofil.
- Gewinnt **b** in der Skizze: Dann nur ein Hinweis, dass der Weg trägt; die Skizze ist ein erster Wurf ohne Abstimmung.

Offen, weil nicht prüfbar: ob die Fassungen für das Ohr kohärent wirken (das ist die Hörprobe), ob der Bit-Crusher oder die Sättigung etwas Brauchbares für den PS1-Charakter bringen (nicht ausprobiert), und ob die Fensteroberfläche das Nachstellen so einfach macht, wie ich vermute.

## Hörprobe 9: dieselben Stücke nach der Methode des Videos

Siehe [video.md](video.md) (Methode mit Zeitmarken, Widersprüche zu unserem Vorgehen). Skripte: `bauen9.py` (erzeugt je Stück `skripte/<name>9.txt` und die Quellen `skripte/q-*.txt`), Ausgabe in [hoerprobe-9-methode/](hoerprobe-9-methode/hoerprobe.md). Alle neun SoundCraft-Läufe (vier Stücke, fünf Quellen) waren bei zwei Läufen **bitgleich**. a = b aus Hörprobe 8 (Skizze: korrigierte Fassung).

**Was ich konkret anders gemacht habe (ein Satz je Stück):**
- **Schuss:** Statt eines synthetisierten Drum-Synth-Schlags jetzt Rauschen aus dem Signalgenerator mit Automation (Tiefpass 9 kHz → 500 Hz in 60 ms, Pegel −45 dB in 0,22 s), auf 22,05 kHz in VAG gewandelt (0,5 s, 11 004 Frames) und im Sampler **eine Oktave tiefer** abgespielt; keine PS1-Kette danach, keine Sättigung.
- **Klinge:** Statt der offenen Hi-Hat jetzt Rauschen mit schnell fallendem Tiefpass (6 kHz → 300 Hz in 15 ms, wie mein Python-Weg, aber als Sample), VAG, im Sampler **eine Quinte höher** gespielt.
- **Aufheben:** Statt eines bei H4 gerechneten Tons jetzt ein Glocken-Sample (zwei Sinus, 15 Halbtöne) **bei H3 aufgenommen** (44 100 Frames), VAG, im Sampler **eine Oktave höher** gespielt (spielt doppelt so schnell, klingt kürzer und heller).
- **Skizze:** Statt zweier Synthesizer-Spuren mit Hall jetzt **zwei Sample-Instrumente** – Glocke (bei E2 aufgenommen, als Einzelschlag, auf E3 und H2 verschoben) und Chor (bei E3 aufgenommen, 2,8 s, **Schleife 0,8–2,8 s auf Vielfache von 28, 0,5 s linear überblendet**, mit Tonhöhenverschiebung auf die Akkordtöne E3–E4) –, dieselben Noten, derselbe Hall wie in Hörprobe 8.

**Messwerte** (dBFS; Bänder = Energie minus Gesamtenergie in dB; Lautheit nach RMS bzw. bei der Skizze nach LUFS gleich gemacht):

| Stück | Datei | Spitze | RMS | LUFS | Schwerpunkt | < 300 Hz | 300–3 000 Hz | > 3 000 Hz |
|---|---|---|---|---|---|---|---|---|
| Schuss | a (Hörprobe 8 b) | −1,0 | −25,9 | −23,1 | 167 Hz | −0,1 | −18,0 | −22,1 |
| | b (Methode) | −5,0 | −25,9 | −24,4 | 640 Hz | −3,0 | −3,2 | −17,4 |
| Klinge | a | −1,9 | −26,5 | −20,2 | 5 121 Hz | −12,1 | −11,3 | −0,6 |
| | b | −1,0 | −26,5 | −21,4 | 1 016 Hz | −4,2 | −2,7 | −10,7 |
| Aufheben | a | −1,0 | −19,8 | −18,3 | 507 Hz | −34,0 | 0,0 | −60,4 |
| | b | −3,1 | −19,8 | −19,1 | 507 Hz | −31,5 | 0,0 | −59,0 |
| Skizze 20 s | a | −1,2 | −18,7 | −16,9 | 166 Hz | 0,0 | −21,2 | −64,6 |
| | b | −1,1 | −18,9 | −16,9 | 166 Hz | 0,0 | −20,0 | −64,7 |

Lesen: Beim Schuss ist b deutlich mittiger (Schwerpunkt 640 statt 167 Hz, Mitte −3 statt −18 dB), das ist die tiefer gespielte Rauschprobe. Bei der Klinge ist b viel **dumpfer** (über 3 kHz nur −10,7 statt −0,6 dB), weil die 22,05-kHz-Probe nichts über 11 kHz enthält und der Tiefpass schnell fällt – sie ähnelt jetzt eher meinem alten Python-Schlag als dem hellen Rauschen aus Hörprobe 8. Aufheben und Skizze sind spektral fast gleich (gleiche Töne); der Unterschied liegt in der Körnung und im zeitlichen Verlauf, nicht im Spektrum. Pegel je Sekunde der Skizze b: −15, −20, −19 … −18 (0–8 s), −15 (Glocke bei 8 s), −26 … −23, −12 (Glocke bei 16 s), −23 … −25; die Glocken sitzen wie bei a bei 0, 8 und 16 s, und die Datei ist durchgehend gefüllt.

**Was sich damit umsetzen ließ und was nicht:**
- **Rauschquelle:** Geht, über `audiosuite.process` mit `signal_generator` auf einer Audiospur (Rauschen, Farbe wählbar).
- **Automation:** Geht für Plugin-Parameter und Lautstärke (`automation.set_point`, `plugin:<Slot>:<Parameter>`): fallender Tiefpass und Abfall der Lautstärke waren messbar (Hochtonanteil −7 → −25 dB).
- **Sampler mit eigenem Sample:** Geht (`mix.sampler_load`); Wurzelton, Transposition, Schleife (in Prozent der Länge), Hüllkurve einstellbar; 22 050-Hz-Dateien spielen in richtiger Tonhöhe (gemessen: 440 Hz bei Wurzelton, 220 Hz eine Oktave tiefer).
- **Schichten mehrerer Spuren:** Geht (Skizze: zwei Spuren). Für die drei Geräusche habe ich bewusst keine Schichten gebaut („ein Klang, eine Geste“).
- **Nicht umgesetzt:**
  - **Tonhöhenhüllkurve** (fallende Tonhöhe innerhalb eines Klangs): nicht gefunden; die Tonhöhe wird je Note festgelegt.
  - **Bus-Effekte:** weiter nicht möglich, der Hall ist je Spur eingefügt.
  - **Gauß-Interpolation und der echte VAG-Kodierer:** Der Sampler spielt mit seiner eigenen Interpolation (welche, ist unbekannt); die VAG-Wandlung ist meine Annäherung aus `ps1_klang.py`, nicht PSXAVENC. Das Video führt gerade die Gauß-Glättung bei tiefen Tönen als Kern der Textur an; ob SoundCraft das ähnlich glättet, habe ich nicht prüfen können (nur gemessen: bei b liegt nichts über 11 kHz).
  - `pitch_shifter` und `audiosuite varispeed` habe ich nicht benutzt; die Transposition des Samplers deckt, was die Methode braucht.

**Ehrliche Einschätzung:** Das Video liefert eine einzige neue Methode, nämlich *Sample-Instrumente mit Tonhöhenverschiebung* statt *Dauerton mit nachgeschalteter Kette*. Ob das für das Ohr mehr nach PS1 und besser zum Menü-Thema klingt, weiß nur der Mensch. Sollte b bei der Skizze nicht überzeugen, liegt es vermutlich weniger an der Methode als daran, dass die Quellen (zwei Sinus als Glocke, zwei Sägen als Chor) einfacher sind als mein Python-Klang aus dem Menü-Thema; das wäre der nächste Hebel (Quellen aus meinem Python-Klang statt aus dem SoundCraft-Synthesizer, danach dieselbe Sample-Methode).
