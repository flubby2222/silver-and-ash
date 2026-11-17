# Video: „How to make PS1 samples for FREE!“ (SoundFontGuy, 10:33)

https://www.youtube.com/watch?v=AnHmD2z8zJk – angesehen am 2026-11-17 mit dem Skill `watch` (lokal: Untertitel des Videos, automatisch erzeugt, englisch, und 20 Einzelbilder; keine Sprachausgabe-Erkennung). Zeitmarken stammen aus den Untertiteln. Das Video ist Anschauung, keine Anweisung; ich habe daraus nur Methoden entnommen. Die Einzelbilder sind klein (512 px); was ich nur aus ihnen schließe, ist als „Bild“ markiert und unsicher.

## Wichtig vorab: Das Video ist kein Sounddesign-Kurs

Es zeigt **nicht**, wie man einen guten Schuss oder eine Glocke aus Klangerzeugern, Schichten, Hüllkurven und Effekten baut. Der Vortragende nennt sich selbst einen „Enthusiasten, kein Experte“ (00:45). Er zeigt, **wie man ein beliebiges vorhandenes Sample so aufbereitet, dass es nach PlayStation 1 klingt** und sich auf der Hardware (bzw. im Test-Plugin) abspielen lässt. Über Schichten, Hüllkurven, Effekte oder ein Qualitätsmaß für „gutes“ Sounddesign sagt er nichts. Seine einzige Güteaussage ist die über die **Textur**: Sie wird „offensichtlich“, wenn die Quelle niedrig aufgelöst ist (02:12–02:28) und wenn man das Sample **tief** spielt (09:31–09:42).

## Die Methode, Schritt für Schritt

| Zeit | Schritt |
|---|---|
| 00:21–00:45 | Werkzeug: ein Sample, ein Audio-Programm (DAW, Audacity oder Tracker), **Endless Wave** (Schleifenpunkte), **PSXAVENC** (Kommandozeile, schreibt VAG), das kostenlose **PSX Sampler**-VST zum Testen. |
| 00:49–00:59 | Quelle: selbst aufnehmen, aus einem VST sampeln oder herunterladen (Urheberrecht und Lizenz beachten). |
| 01:12–01:22 | Das Sample **kurz** schneiden. |
| 01:22–01:34 | **Mono** (aus Stereo heruntermischen), **16 Bit**, **auf 22 050 Hz umrechnen**, mit der Einstellung, dass sich die Tonhöhe dabei nicht ändert. |
| 01:43–01:52 | Danach klingt es „viel komprimierter“; dazu noch Anfang kürzen („wir verhunzen den Klang“). |
| 01:58–02:05 | **Nur am Anfang ausblenden** (Einblendung), damit es beim Start nicht knackt; das Ende wird nicht ausgeblendet. |
| 02:07–02:11 | Nach jeder Bearbeitung springt die Bit-Tiefe auf 32 zurück: vor dem Speichern wieder auf 16 stellen. |
| 02:12–02:30 | **Warum niedrige Rate:** Die SPU (Klangchip) spielt mit 44,1 kHz. Ein Sample, das schon 44,1 kHz hat, lässt die Textur weniger hören; eine niedrigere Rate macht sie deutlich. |
| 02:32–02:41 | Einzelschläge (Trommel u. ä.) ohne Schleife: diesen Teil überspringen. |
| 02:41–03:56 | **Schleifenpunkte** in Endless Wave: Die SPU verlangt, dass **Schleifenanfang, Schleifenende und Schleifenlänge je ein Vielfaches von 28** sind (28 = Länge eines ADPCM-Blocks). Grobe Stelle wählen. |
| 04:10–05:40 | **Rechenweg:** Anfang durch 28, auf die ganze Zahl abrunden, mal 28; ebenso das Ende; weil das Ende in Endless Wave **eingeschlossen** ist, danach 1 abziehen. Probe: Länge geteilt durch 28 ergibt eine ganze Zahl (Beispiel 21 252 : 28 = 759). Die Zahlen notieren. |
| 05:47–06:31 | Die Schleife **glätten** mit der Überblendung-Funktion (hier linear, 50 % und dann länger); Kurvenform nach Material ausprobieren. |
| 06:33–06:59 | Speichern; im Audioprogramm prüfen, dass die eingebetteten Schleifenpunkte stimmen, und die Schleife anhören. |
| 07:05–09:05 | **In VAG wandeln** mit PSXAVENC; Befehl nach seinen Worten: `psxavenc -t vag -f 22050 "eingabe.wav" "ausgabe.vag"` (die Rate muss die Rate der WAV-Datei sein). |
| 09:09–09:45 | **Testen** im PSX Sampler: Datei laden. Der PS1-Charakter wird „richtig offensichtlich“, wenn man das Sample **sehr tief** spielt, weil dort die **Gauß-Interpolation** der SPU glättet. |
| 09:52–10:17 | **Gegenprobe:** Original mit linearer Interpolation („im Grunde nichts“) hat viel Knirschen in den Höhen; im PSX Sampler kommt das „typische Dumpfe“. |

Bild (Bildschirmaufnahme bei 09:25, klein): Der PSX Sampler zeigt „Sample Info“ (Länge, Grundton, Schleifenanfang/-ende) und einen Hüllkurvenbereich mit Reglern, die nach SPU-Registern benannt aussehen (Attack/Decay/Sustain/Release als „Shift“/„Step“, Haken Linear/Exponentiell). Das ist der Hinweis, dass die SPU eine **eigene Hüllkurve je Stimme** hat; das Video geht darauf nicht ein.

## Was das Video über „gutes Sounddesign“ sagt

Nur das Folgende (alles zur PS1-Klangfarbe):
1. Die PS1-Textur kommt aus **Quelle bei 22,05 kHz → ADPCM (VAG) → Wiedergabe mit Gauß-Interpolation** (02:12–02:28, 09:31–09:42).
2. Sie wird erst **beim Transponieren** richtig hörbar, vor allem nach unten (09:31).
3. Die Hardware-Zwänge gehören zum Stil („man schätzt die Komponisten der Zeit“, 03:00): kurze Samples, Schleifen auf 28er-Raster.
4. Alles Weitere (Schichtung, Raum, Dynamik) wird nicht behandelt.

## Was davon unserem bisherigen Vorgehen widerspricht oder es ergänzt

| | Bisher bei uns | Video |
|---|---|---|
| Wo die PS1-Kette sitzt | **Nach** der fertigen Synthese: ein fertig gemischter Klang (auch die ganze Musik) läuft einmal bei fester Rate durch `ps1_klang.py` | **Vor** dem Instrument: ein **kurzes Sample je Klang** wird in VAG gewandelt und dann **von der SPU mit wechselnder Tonhöhe gespielt**; die Gauß-Interpolation arbeitet mit dem Transpositionsverhältnis jeder Note |
| Instrument | Dauerklang, in Python je Note frisch gerechnet (Glocke mit neun Teiltönen, Chor aus Säge-Ensemble), jede Tonhöhe voll ausgerechnet | **Ein** Sample (etwa Chor, Glocke) für viele Tonhöhen; tiefe Töne klingen dumpf, hohe körnig, die Klangfarbe wandert mit (der „Mickey-Mouse“-Effekt der Formanten) |
| Schleifen | Nahtlos gemacht, aber nicht auf 28er-Raster und nicht als Sample-Schleife | Schleife als Sample-Merkmal: Länge, Anfang, Ende Vielfache von 28, überblendet |
| Rate | 22 050 Hz ✓ | 22 050 Hz ✓ (dasselbe) |
| ADPCM-Kodierer | eigene Annäherung (`ps1_klang.py`) | PSXAVENC (echter Kodierer) – kein Netzzugriff nötig, aber nicht verfügbar; Unterschied unbekannt |
| Quelle | alles selbst synthetisiert | Aufnahmen, VST-Klänge oder Downloads; für uns bleibt: **nur Eigenes, nichts Fremdes** |
| Einblendung | vorn und hinten gerundet | nur vorn |
| Hall, Hüllkurven, Schichten | eigener Hall, eigene Hüllkurven | nicht behandelt |

**Kern der Lehre:** Unsere Dateien klingen nach „moderner Synthese mit Tiefpass“, weil die PS1-Kette bei **fester Tonhöhe** am Ende wirkt. Der PS1-Klang entsteht aber aus **Samples, die in der Tonhöhe verschoben werden**. Das haben wir bisher nicht gemacht; das ist der einzige Punkt, in dem das Video etwas Neues zeigt. Ob das hörbar „mehr PS1“ ist, entscheidet Hörprobe 9.
