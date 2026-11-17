# Versuchsplan: SoundCraft für eine Skizze des Stimmungsstücks

Stand 2026-11-17. **Nur gelesen, nichts geladen, nichts installiert.** Quellen: nur das offizielle Repository `github.com/storytold/soundcraft` (README, `docs/cli.md`, `docs/releasing.md`, `docs/control-protocol.md`, `Cargo.lock`, `packaging/windows/package.ps1`, Quelltext der Befehlsliste) und die Release-Seite.

## Die eine Datei (zur Freigabe durch den Menschen)

| | |
|---|---|
| Datei | `soundcraft-0.20.1-windows-x64-portable.zip` |
| Adresse | https://github.com/storytold/soundcraft/releases/download/v0.20.1/soundcraft-0.20.1-windows-x64-portable.zip |
| Größe / Datum | 19 876 437 Byte (etwa 19 MB) · Release v0.20.1 vom 2026-11-12 (Vorabversion: nein, Status des Projekts „pre-alpha“) |
| Prüfsumme | GitHub nennt für die Datei SHA-256 `9769223e26f727866b249fc5176caa8b3791a98c7d89e901b78f5f48aff03b82`; dazu liegen `SHA256SUMS.txt` (alle Dateien) und `SHA256SUMS.txt.asc` (Signatur) sowie `release-signing-key.asc` im Release. Laut README sind Installer und Programme „code-signed“. |
| Inhalt | laut `package.ps1`: `soundcraft.exe` (Fenster), `soundcraft-cli.exe` (Kommandozeile), `portable.txt`. **Ein reines CLI-Zip für Windows gibt es nicht** (nur für macOS); das Portable-Zip ist die kleinste Windows-Datei ohne Installer. Lizenz: MIT oder Apache-2.0. |

Vor dem ersten Start würde ich die SHA-256 gegen `SHA256SUMS.txt` und gegen den GitHub-Wert prüfen. Ob ich die GPG-Signatur prüfen kann, hängt davon ab, ob `gpg` auf dem Rechner ist (nicht geprüft).

## Aktualisiert am 2026-11-17: Das Konzept hat sich geändert

Der Mensch zu den Geräuschen im Spiel: „Man konnte klar hören, wofür Samples als Grundlage dienten. Es war kein richtiger Style und keine Kohärenz erkennbar. Was glaube ich sinnvoll wäre, ein Soundtrack, der einen generellen Mood setzt – dann einzelne Sounds.“ Neue Reihenfolge: **erst die Stimmung (Musik), dann die Geräusche aus derselben Klangwelt** – siehe [konzept-klangwelt.md](../konzept-klangwelt.md). Deshalb fragt der Versuch nicht mehr nach Schuss und Klinge.

## Frage des Versuchs

Kann ich damit **eine 20-Sekunden-Skizze des Stimmungsstücks** (Richtung A oder B des Konzepts) **mit zwei bis drei Spuren und einem gemeinsamen Hall-Bus** bauen und **reproduzierbar (byte-gleich bei zwei Läufen) ausgeben**, einfacher und mit besserer Kontrolle als mit meinen Python-Skripten – und lässt sich die PS1-Kette (`ps1_klang.py`) anhängen? Vergleichsmaßstab: dieselbe Skizze aus meinen Skripten (Töne, Akkorde, eigener Hall).

## Was gebaut wird

Eine Skizze, 20 s, Richtung A („Gruft-Orgel“: Pedal-Orgelton + dichte tiefe Streicher + wenig Hall-Bus) oder B („Glocken und Chor“), 2–3 Spuren aus `subtractive_synth`/`sampler`/`drum_synth` und einem Hall-Bus (`mix.new_bus`, `mix.send`, `room_reverb`/`plate_reverb`). Noten als **Standard-MIDI-Datei**, die mein Python-Skript schreibt (SoundCraft importiert MIDI laut README; `file.import_midi` steht in der Befehlsliste), Ausgabe `file.bounce_mix` und einzeln `file.bounce_stems`, danach durch die PS1-Kette. Dieselben Stems und die MIDI-Datei wären auch das Rohmaterial für den Menschen in FL Studio (siehe unten). Geprüft wird nach Regeln (Töne der Tonleiter, Pegel, Spektrum, Naht), nicht nach Gehör.

## Befehle (aus der Doku und dem Quelltext der Befehlsliste belegt)

`soundcraft-cli run --cmd 'ID={json}' … --bounce datei.wav` und `soundcraft-cli script datei.txt --save sitzung.scraft` (eine Zeile `ID JSON` je Befehl). Gefundene Befehle: `session.new` (Rate, Bit-Tiefe), `track.new`, `mix.instrument`, `mix.instrument_param`, `mix.insert`, `mix.insert_param`, `mix.volume`, `mix.pan`, `mix.new_bus`, `mix.send`, `file.import_audio`, `clip.gain`, `file.bounce_mix` (`wav`, 16/24/32f Bit, Bereich, `normalize`), `file.bounce_stems`, `soundcraft-cli describe <Befehl>`, `soundcraft-cli commands --json`, `soundcraft-cli convert … --rate 44100 --bit-depth 16`. **Noch nicht belegt** (erst mit dem Programm prüfbar): Zeit-Hüllkurven eines Klangs, genaue Parameternamen der Instrumente, Mono-Ausgabe, Bounce ohne Dither (`dither` bei `bounce_stems` ist nachweisbar; bei `bounce_mix` unklar).

## Reproduzierbar im Repository

Im Repository liegen nur: ein Skript je Geräusch (`design/proben/audio/soundcraft-versuch/<name>.txt` mit den Befehlen) und ein kleines Python-Skript, das `soundcraft-cli` aufruft, danach `ps1_klang.py`. Das Programm selbst nicht; das Skript bricht ab, wenn die SHA-256 der `soundcraft-cli.exe` nicht zur dokumentierten Fassung passt. Es meldet die Version (`--version`).

## Grenzen

- Programm und Arbeitsdateien unter `%LOCALAPPDATA%\SilverAndAsh\werkzeuge\soundcraft\`, kein Eintrag im Repository.
- **Nur `soundcraft-cli.exe`**, nie `soundcraft.exe`: Laut `Cargo.lock` hängt nur das Fenster-Programm an der Aktualisierungs-Bibliothek `craft-update` (mit HTTP-Client `ureq`); die Kommandozeile nicht (aus dem Lockfile geschlossen, nicht ausprobiert). Das Fenster prüft laut Quelltext beim Start auf Updates und kann sich selbst aktualisieren – das nutzen wir nicht.
- Schreib- und Lesezugriff auf einen Versuchsordner: Beim MCP-Server gibt es dafür `--automation-read-root`/`--automation-write-root`; für `run`/`script` ist das **nicht** dokumentiert. Dort kann ich den Pfad nur über Windows-Rechte einschränken (Ordner mit eigener Berechtigung) – offen, ob das nötig ist. Einstellungen: `SOUNDCRAFT_NO_PREFS=1`, `SOUNDCRAFT_CONFIG_DIR` leer, damit nichts außerhalb des Ordners geschrieben wird.
- **Kein MCP** (er würde die Claude-Konfiguration ändern), keine Konten, kein Discord.
- Netzzugriffe: Telemetrie habe ich weder in README noch im Lockfile gefunden. Ob die CLI doch eine Verbindung aufbaut, prüfe ich beim Versuch (Windows-Firewall-Protokoll oder `netstat` während eines Laufs), nicht vorher.

## Abbruchkriterien

Ein Befehl für Hüllkurve, Mono-Ausgabe oder Tonhöhenverlauf fehlt; Absturz oder Fehlercode ≠ 0 ohne klare Meldung; zwei Läufe ergeben **nicht** byte-gleiche WAV-Dateien (dann nur noch „gleich nach Messwerten“, was meine Skripte schon leisten); der Aufbau braucht mehr Befehlszeilen als mein jetziges Python-Skript; das Programm verbindet sich ungefragt mit dem Netz; Spitze und Länge lassen sich nicht auf unseren technischen Rahmen bringen.

## Ehrlich: Was ändert das Werkzeug – und was nicht?

**Ändert:** Schichten, Hüllkurven und Effekte sind fertige, benannte Bausteine statt eigener Filter in Python; Ausgabe mit gemeldeter Spitze und Lautheit; echte Hall- und Kompressor-Effekte; mehr Kontrolle für den, der **hört** – der Mensch könnte dieselbe Sitzung später in der Oberfläche öffnen und nach Gehör nachstellen. **Ändert nichts:** Ich kann auch damit nicht hören. Ob ein Schuss „gut“ klingt, bleibt sein Urteil; ich messe weiter nur. Und die Schwäche meiner bisherigen Geräusche lag nicht am Werkzeug, sondern daran, dass ich keine Rückmeldung außer seinen Urteilen habe. Ein besseres Werkzeug ohne Gehör kann nur mehr Auswahl erzeugen, nicht besser auswählen.

## Meine Meinung zu FL Studio (der Mensch stellt nach Gehör ein)

Das halte ich für den wirksameren Weg, wenn er Zeit hat: Er hört, ich messe nur. Rohmaterial für das Stimmungsstück (und später je Geräusch): die **MIDI-Datei** der Skizze plus **3 bis 5 Spuren als einzelne WAV-Dateien**, 44 100 Hz, 24 Bit, mono, alle gleich lang, alle bei Sample 0 beginnend (damit sie ohne Schieben übereinanderliegen), Pegel einzeln auf −6 dBFS normalisiert, Beschriftung `<stueck>_<nr>_<rolle>.wav`, z. B. `skizze-a_1_pedal-orgel.wav`, `_2_streicher.wav`, `_3_hall-bus.wav` (Raum getrennt, damit er ihn weglassen kann), dazu eine `.txt` mit meinem Vorschlag für Lautstärke und Reihenfolge der Spuren, damit er nicht bei null beginnt. Zurück bekomme ich seine fertige WAV und die Einstellungen (Screenshot oder Liste), die ich als Regeln festhalte. Das passt auch zu „der Mensch urteilt am liebsten im Spiel“: Die Ergebnisse würden direkt ins Spiel kommen.
