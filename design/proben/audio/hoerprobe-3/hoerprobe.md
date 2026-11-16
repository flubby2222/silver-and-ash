# Hörprobe 3 – Abspielliste

Zu Hörprobe 2 hast du gesagt: besser, aber nicht optimal. Ich habe deshalb zwei Dinge getan:

1. **Tutorials ausgewertet** (vor allem Andy Farnells Buch *Designing Sound*) und je Geräusch eine verbesserte Fassung gebaut. Grundlage war jeweils die Variante aus Hörprobe 2, die den Regeln am besten folgt (Ghul d, Pistole d, Klinge d, Schritt d). Was sich geändert hat, steht im Nachtrag der [Grundlagen](../../../grundlagen/grundlagen-audio.md).
2. **Den Klang-Chip der ersten PlayStation nachgebildet** ([`ps1_klang.py`](ps1_klang.py), geprüft in [ps1_pruefung.md](ps1_pruefung.md)). Der Chip hat keine Klänge erzeugt, sondern Samples abgespielt – er färbt also nur: Er nimmt die obersten Höhen weg, dämpft die Höhen darunter leicht und fügt ein feines Rauschen hinzu.

Deshalb gibt es jedes Geräusch **zweimal direkt hintereinander**: `-sauber` und `-ps1`. Es ist derselbe Klang, nur die Färbung unterscheidet sich. Lautstärke beim ersten Stück einstellen und dann nicht mehr ändern. Aufbau und Messwerte stehen in [messwerte.md](messwerte.md).

Wenn etwas nicht stimmt, beschreib bitte mit eigenen Worten, **was du dir stattdessen vorstellst** – zum Beispiel „wie ein nasser Sack, der auf Stein fällt“ oder „kürzer und höher“. Solche Bilder kann ich in Filter, Längen und Tonhöhen übersetzen.

## 1. Katakomben-Ambiente

Die Fläche ist unverändert die aus Hörprobe 1. Die Tropfen liegen anderthalb Oktaven tiefer (die tiefste Stufe aus Hörprobe 2), klingen länger aus und haben einen kleinen Höhlenhall. Bitte mit „Wiederholen“ hören.

1. `ambiente-sauber.wav`
2. `ambiente-ps1.wav`

**Frage:** Sauber oder PS1? Was ist noch nicht richtig? Sind die Tropfen jetzt tief genug – oder zu tief? Was stellst du dir stattdessen vor?

## 2. Ghul-Laut

Eine einzige Bewegung: Das Maul öffnet sich in 0,5 s und schließt sich beim Schlag. Der Schlag liegt bei 0,5 s.

1. `ghul-sauber.wav`
2. `ghul-ps1.wav`

**Frage:** Sauber oder PS1? Was ist noch nicht richtig? Weißt du, wann der Schlag kommt? Was stellst du dir stattdessen vor?

## 3. Pistolenschuss – bitte ausdrücklich urteilen

Dazu gibt es noch kein Urteil von dir.

1. `pistole-sauber.wav`
2. `pistole-ps1.wav`

**Frage:** Sauber oder PS1? Was ist noch nicht richtig? Was stellst du dir stattdessen vor?

## 4. Klingentreffer – bitte ausdrücklich urteilen

Drei leicht verschiedene Treffer, damit Wiederholung nicht auffällt. Bitte jeweils sauber und PS1 hintereinander:

1. `klinge-sauber-1.wav`, dann `klinge-ps1-1.wav`
2. `klinge-sauber-2.wav`, dann `klinge-ps1-2.wav`
3. `klinge-sauber-3.wav`, dann `klinge-ps1-3.wav`

**Frage:** Sauber oder PS1? Was ist noch nicht richtig? Hört man, dass ein Untoter getroffen wird? Was stellst du dir stattdessen vor?

## 5. Schritt auf Stein – bitte ausdrücklich urteilen

Zuerst die Folgen (vier verschiedene Schritte zweimal im Gehtempo):

1. `schritt-sauber-folge.wav`
2. `schritt-ps1-folge.wav`

Einzeln bei Bedarf: `schritt-sauber-1.wav` … `-4.wav` und `schritt-ps1-1.wav` … `-4.wav`

**Frage:** Sauber oder PS1? Was ist noch nicht richtig? Klingt es nach Gehen? Was stellst du dir stattdessen vor?

## Zum Schluss

Soll die PS1-Färbung für **alle** Geräusche gelten („alles im selben Stil“ wie beim Bild), für einige oder für keines? Deine Antworten werden zu Regeln in der [Audio Bible](../../../../docs/design/audio-bible.md).
