# Versuchsplan: SoundCraft für Pistolenschuss und Klingentreffer

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

## Frage des Versuchs

Kann ich damit ein Geräusch, das ich heute mit Python-Skripten baue, **einfacher, wiederholbar (byte-gleich bei zwei Läufen?) und mit besserer Kontrolle** über Schichten, Hüllkurven und Effekte bauen – und lässt sich die PS1-Kette anhängen oder ersetzen?

## Was gebaut wird

Genau zwei Stücke, je eine Fassung aus SoundCraft, danach durch meine PS1-Kette (`ps1_klang.py`), Vergleich mit dem Stand im Spiel: **Pistolenschuss** (`pistole.wav`) und **Klingentreffer** (`klinge-1.wav`). Aufbau: eine Spur je Schicht (Anschlag, Körper, Ausklang) aus `drum_synth`, `subtractive_synth`, `signal_generator`, Effekte `eq_7band`, `saturator`, `lofi`, `room_reverb`, Ausgabe über `file.bounce_mix`.

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

Das halte ich für den wirksameren Weg, wenn er Zeit hat: Er hört, ich messe nur. Rohmaterial je Geräusch: **3 bis 5 Schichten als einzelne WAV-Dateien**, 44 100 Hz, 24 Bit, mono, alle gleich lang, alle bei Sample 0 beginnend (damit sie ohne Schieben übereinanderliegen), Pegel einzeln auf −6 dBFS normalisiert, Beschriftung `<geraeusch>_<nr>_<rolle>.wav`, z. B. `pistole_1_anschlag.wav`, `_2_koerper.wav`, `_3_sub.wav`, `_4_raum.wav` (Raum getrennt, damit er ihn weglassen kann), dazu eine `.txt` mit meinem Vorschlag für Lautstärke und Reihenfolge der Spuren, damit er nicht bei null beginnt. Zurück bekomme ich seine fertige WAV und die Einstellungen (Screenshot oder Liste), die ich als Regeln festhalte. Das passt auch zu „der Mensch urteilt am liebsten im Spiel“: Die Ergebnisse würden direkt ins Spiel kommen.
