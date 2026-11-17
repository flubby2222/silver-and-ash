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
