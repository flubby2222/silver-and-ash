# Konzept: erst die Stimmung, dann die Geräusche aus derselben Klangwelt

Stand 2026-11-17. Vorschlag, kein Klang erzeugt. **Anlass** (der Mensch): „Man konnte klar hören, wofür Samples als Grundlage dienten. Es war kein richtiger Style und keine Kohärenz erkennbar. Was glaube ich sinnvoll wäre, ein Soundtrack, der einen generellen Mood setzt – dann einzelne Sounds.“ Mein Fehler war derselbe wie bei Hörprobe 4: Ich habe jedes Geräusch einzeln gebaut und einzeln beurteilen lassen, ohne Maßstab, der sie verbindet.

## 1. Das Stimmungsstück (Maßstab) – zwei Richtungen

Katakomben, Schleife 60–90 s, eigene Komposition per Skript, PS1-nah (22 050 Hz, Dämpfung der Höhen, ein Hall). Vorbilder nur als Stimmung (Gothic-Horror); keine Melodien, keine Samples.

- **A – „Gruft-Orgel“:** Orgel und tiefe Streicher. Langsam (etwa 50 Schläge/min), D-phrygisch (der Halbton über dem Grundton ist das Unheil). Tragend: ein liegender Orgelton im Pedal, darüber langsam wechselnde Akkorde aus dichten, dunklen Streichern; selten ein kurzer, einstimmiger Orgelgang. Wenig Rhythmus, viel Raum.
- **B – „Glocken und Chor“:** karg, kühl, schwebend. Etwa 60 Schläge/min, e-Moll. Tragend: eine tiefe Glocke alle paar Takte, ein leises Chor-Pad (Vokale „u/o“), ein dumpfer Trommelschlag wie ein Herzschlag nur in den dichten Stellen. Weniger Harmonie, mehr Leere.

## 2. Die Klangpalette folgt aus dem Stück

Wenige Klangerzeuger (A: Orgel, Streicher, Trommel; B: Glocke, Chor, Trommel), **ein** gemeinsamer Hall für alles (Musik und Geräusche laufen durch dieselbe Funktion), **ein** Frequenzrahmen (Musik trägt unter etwa 500 Hz und über 3 kHz; die Mitte 300–3 000 Hz bleibt für Ankündigungen frei) und **dieselbe PS1-Kette**. Regel: Jedes Geräusch wird aus dieser Palette gebaut oder durch sie geführt – und so weit geformt, dass der Rohstoff nicht mehr erkennbar ist. Keine Aufnahmen mehr als Baustein, solange nicht jemand zeigt, dass der Rohstoff verschwindet.

## 3. Wie die Geräusche daraus entstehen

- **Schuss:** Trommelkörper der Palette, Sub-Ton **auf dem Grundton** des Stücks, ein Rauschpuls nur als Anschlag, durch den gemeinsamen Hall.
- **Klinge:** ein Glocken-/Metall-Anschlag (unharmonische Teiltöne um den Grundton), sehr kurz gedämpft; eine Geste, keine zweite Schicht.
- **Schritte:** gedämpfte Trommel (Palette), Tiefpass, gleicher Hall.
- **Ghul-Ankündigung:** Chor-/Orgelstimme auf dem Grundton, die in 0,5 s anschwillt und beim Schlag endet (der Ghul ist heute schon ein Vokal-Filter; er liegt der Palette am nächsten).
- **Tropfen:** ein einzelner Glockenteilton auf einem **Ton der Tonleiter** (nicht „irgendeine Höhe“), gedämpft.
- **Prüfung, dass es zusammenpasst (messbar):** tonale Anteile liegen auf Tönen der Tonleiter (Frequenzanalyse); alle Dateien laufen durch denselben Hallaufbau; Pegel relativ zur Musik (LUFS, Spitze); die Mitte 300–3 000 Hz der Musik liegt mindestens 15 dB unter ihrem Gesamtpegel (Vorschlag, noch nicht gehört).

## 4. Was der Mensch wann hört

1. **Nur das Stimmungsstück:** zwei Skizzen (a/b), je etwa 20–30 s, im Spiel als Ambiente. Frage: „a oder b, ein Wort zur Stimmung.“
2. Steht eine Richtung: die Schleife (60–90 s) im Spiel.
3. Erst dann die Geräusche – **im Spiel zusammen mit der Musik**, nicht einzeln.

## 5. Werkzeug

- **Meine Skripte (Python):** Töne, Akkorde, Tonleitern, Hall, PS1-Kette, Messung, byte-gleiche Ausgabe; ein einfacher Notenablauf als Liste. Schwach: Mehrspur-Mischung nach Gehör, Orgel- und Streicherklang ohne Aufnahmen.
- **SoundCraft (Mehrspur, Synth, MIDI, gemeinsamer Hall-Bus):** besser für die Skizze, weil Spuren, Hall-Bus und Mischung fertige Bausteine sind. Der Versuch lautet jetzt: **eine 20-Sekunden-Skizze des Stimmungsstücks mit zwei bis drei Spuren und gemeinsamem Hall bauen und reproduzierbar (byte-gleich) ausgeben** (siehe [plan.md](soundcraft-versuch/plan.md)).
- **FL Studio mit dem Menschen:** für die Musik am wirksamsten, weil er nach Gehör entscheidet. Ich liefere eine **MIDI-Datei** (Noten der Skizze, aus meinem Skript geschrieben) und die **Spuren einzeln als WAV** (siehe Plan); er stellt Klang und Mischung ein und gibt mir die Einstellungen zurück.

## 6. Was ich absichern kann – und was nicht

**Kann ich über Regeln und Messung prüfen:** Tonart und Modus (nur Töne der Tonleiter), Stimmführung (Sprünge, Kreuzungen, Lagen), Tempo, Schleifennaht, Länge, Pegel, Lautheit, Spektrum (Mitte frei), Dichte (Noten je Takt), Wiederholungslänge. **Braucht sein Ohr:** ob es düster klingt statt nur dunkel, ob die Akkordfolge trägt, ob die Orgel nach Orgel und nicht nach Sägezahn klingt, ob die Stimmung zu den Bildern passt und wie die Mischung klingt. Ich kann Musik regelkonform bauen und nicht wissen, ob sie berührt; deshalb stehen die Skizzen zuerst.
