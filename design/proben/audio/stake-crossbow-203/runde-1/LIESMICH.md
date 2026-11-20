# Hörprobe Stake Crossbow, Runde 1 (Issue #203)

Neue Geräusch-Kategorie (Pflock-Armbrust), ausserhalb des Spiels. Weder die Leitung noch der Audiodesigner können hören: Alles hier ist **gebaut und gemessen, nichts gehört**. Ob eine Variante passt, entscheidet allein der Mensch.

**Zum Anhören: `hoerprobe.wav`** (32,0 s, eine Datei). Reihenfolge und Zeitmarken:

| Start s | Länge s | Inhalt |
|---|---|---|
| 0,00 | 0,53 | Schuss A, Sehnenschlag |
| 1,33 | 0,38 | Schuss B, Holzschlag |
| 2,51 | 0,56 | Schuss C, Stahlbogen |
| 4,67 | 1,45 | Spannen A, Kurbel |
| 6,92 | 1,17 | Spannen B, Hebel |
| 8,89 | 1,20 | Spannen C, zwei Stufen |
| 11,69 | 0,46 | Einschlag Fleisch A, dumpf |
| 12,95 | 0,51 | Einschlag Fleisch B, mit Knochen |
| 14,26 | 0,48 | Einschlag Fleisch C, nass |
| 16,34 | 0,56 | Einschlag Stein/Holz A, Holz steckt |
| 17,70 | 0,34 | Einschlag Stein/Holz B, Stein, Pflock prallt ab |
| 18,84 | 0,26 | Einschlag Stein/Holz C, Holz splittert |
| 20,70 | 3,26 | Zusammenhang A: Spannen A, Schuss A, Fleisch A |
| 24,76 | 2,88 | Zusammenhang B: Spannen B, Schuss B, Fleisch B |
| 28,44 | 3,06 | Zusammenhang C: Spannen C, Schuss C, Fleisch C |

Pausen: 0,8 s zwischen Varianten, 1,6 s zwischen Kategorien. Dieselben Zeitmarken stehen maschinell in `zeitmarken.md`. Die einzelnen Stücke liegen auch als eigene WAV im Ordner.

## Was die Varianten unterscheidet

Alle: mono, 16 Bit, 44 100 Hz, Tiefpass 2,8 kHz (keine hohen, schrillen Töne), Grundtöne unter 300 Hz, kurzer kalter Hall, letzte 50 ms unter -73 dBFS.

**Schuss** (Sehne, Holz oder Stahl, Pflock-Zischen; kein Knall)
- **A Sehnenschlag:** tiefe Sehne (92 Hz, harmonische Teiltöne), hölzerner Anschlag (210 Hz), Zischen um 1,4 kHz. Der Sehnenton trägt.
- **B Holzschlag:** Armschlag aufs Holz (150 Hz) trägt alles, Sehne nur kurz und leise, knappes tiefes Zischen (800 Hz). Kürzeste Fassung (0,38 s), trockenste.
- **C Stahlbogen:** Stahlbogen schwingt metallisch nach (118 Hz, unharmonische Teiltöne bis etwa 780 Hz), härterer Anschlag (260 Hz), längeres Zischen (2 kHz fällt auf 1,2 kHz). Mehr Höhen (siehe Messwerte).

**Spannen und Nachladen** (Länge **vorläufig**, passend zu grob 1 bis 1,5 s Nachladezeit)
- **A Kurbel (1,45 s):** Ratsche mit 11 Klicks, Abstände enger werdend, Tonhöhe steigt (Sehne spannt), schwerer Einrast-Klack, Pflock wird eingelegt (Gleiten, Tock).
- **B Hebel (1,17 s):** ein langer Zug, Holz knarrt, Sehne ächzt tief und wächst an; Einrasten; Pflock einlegen. Wenige Einzelereignisse.
- **C Zwei Stufen (1,20 s):** zwei kurze Züge, leises Rasten nach dem ersten, schwerer Einrast-Klack nach dem zweiten, Pflock einlegen mit Schaben.

**Einschlag in Fleisch** (dumpf)
- **A dumpf:** tiefer Schlag (110 fällt auf 48 Hz) mit nassem, tief gefiltertem Rauschen (350 Hz). Fast keine Höhen.
- **B Knochen:** tiefer Schlag (85 auf 40 Hz), ein kurzes hölzernes Knacken (450 Hz), leises Nachrauschen.
- **C nass:** rundes Schlagen (70 Hz) plus Rauschband mit grobem Schmatzen (Pegel springt unregelmäßig).

**Einschlag in Stein oder Holz**
- **A Holz steckt:** kurzer Tock, danach zittert der Pflock im Holz aus (135 Hz, gleichmäßig abfallend, kein Vibrato). Längste Fassung.
- **B Stein, Pflock prallt ab:** hartes Klack (Körper 320 Hz, Rauschkorn 1,1 kHz), danach kurzes Wegschleifen. Mehr Höhen als alle anderen Einschläge.
- **C Holz splittert:** dumpfer Schlag (120 Hz) mit kurzem Reißen (Rauschband 1,5 kHz auf 600 Hz). Kürzeste Fassung (0,26 s).

## Pegel am Bestand ausgerichtet

Bestand (gemessen mit `volumedetect`): Pistole Spitze -1,5 / Mittel -18,2 dB; `p1-schuss-1` Spitze -1,5 / Mittel -19,7; `p1-nachladen-start` Spitze -8,0 / Mittel -31,5; `p1-treffer-1` Spitze -4,5 / Mittel -27,6.

Hier: Schuss Spitze -1,5 bis -2,0 dBFS, Mittel -17,9 dB (alle drei gleich). Spannen Spitze -8,0 bis -11,8, Mittel -26,8 (gleich); das Mittel liegt über dem Nachladeklick des Bestands, weil die Datei 1,2 bis 1,45 s lang ist und mehr Ereignisse enthält. Einschläge Spitze -4,5 bis -9,6, Mittel -22,6 (Fleisch) und -24,6 (Stein/Holz), also etwas lauter als `p1-treffer-1` im Mittel. Innerhalb einer Kategorie sind A, B und C auf gleiche mittlere Lautstärke gerechnet (nur nach unten, die Spitze bleibt unter dem Ziel); in `hoerprobe.wav` sind die Dateien unverändert eingesetzt, nichts übersteuert (Spitze -1,5 dBFS).

Vollständige Tabelle samt Spektrogrammen: `messwerte.md` und `spektrogramme/`. Anteil über 3 kHz (je kleiner, desto weniger Höhen): Schuss B -26,1 / A -25,6 / C -21,2 dB; Fleisch A -47,9 / B -37,9 / C -28,1; Stein/Holz A -34,3 / C -24,0 / B -19,3.

## Empfehlung (aus Messwerten und Aufbau, ungehört)

Wenn die Linie „trocken und schwer, kein Knall, keine Höhen“ wörtlich genommen wird: **Schuss B** (kürzeste, nur tiefe Schicht plus knappes Zischen), **Fleisch A** (kaum Höhen, einfachster Aufbau), **Stein/Holz A** (der zitternde Pflock sagt „steckt“, klar vom Fleisch unterscheidbar), **Spannen B** (ein Zug, wenige Einzelereignisse, am nächsten an der Regel „eine Geste“). Wer das Spannen als Rückmeldung für den Fortschritt des Nachladens braucht, findet in **Spannen A** (Klicks werden enger und höher) eine Uhr, die man mitzählen kann. Das ist eine Vermutung aus dem Aufbau; der Mensch wählt.

## Schwächen und offene Punkte

- **Spannen ist kein einzelnes Ereignis.** Es ist von Natur aus eine Folge (Zug, Rasten, Einlegen); die Regel 1 der Audio Bible („eine Geste“) trifft es nur bei B einigermassen. Die Einsatzzähler in `messwerte.md` (10-ms-Fenster zählt bei Ratschenklicks mehr) zeigen das; bei den Schüssen und Einschlägen steht 0, weil der Beginn des Lauts in der Zählung nicht mitzählt.
- **Länge des Spannens ist vorläufig**, gewählt für 1 bis 1,5 s Nachladezeit; die echte Dauer kommt aus `balance.json`. Eine Fassung für andere Zeiten wäre durch Dehnen der Abstände leicht zu bauen.
- **Schuss A und C** liegen mit 0,53 und 0,56 s knapp unter der Grenze 0,6 s des technischen Rahmens.
- **Mehr Höhen bei Schuss C und Stein B**, wegen des metallischen Anschlags; beide bleiben unter 2,8 kHz abgeschnitten, trotzdem ist es die Stelle, an der „schrill“ am ehesten auffallen könnte.
- Die Lautheit des Spannens ist durch die Spitzenregel begrenzt (Spitze -8 bis -11,8 dBFS bei Mittel -26,8 dB); im Spiel stellt der Abspielfaktor ein.
- Ob die Pflock-Zisch-Schicht beim Schuss ohne Pflock-Sicht „stimmt“, kann nur das Ohr zeigen.

## Dateien und Prüfung

- `bauen.py`: erzeugt alle WAV und `zeitmarken.md` (`python bauen.py [Zielordner]`, nur Python-Standardbibliothek, fester Seed).
- `messen.py`: schreibt `messwerte.md` und `spektrogramme/*.png` (braucht `ffmpeg`).
- `wav.sha256`: SHA-256 aller WAV. **Zwei Läufe von `bauen.py` in zwei getrennte Ordner ergaben für alle 16 Dateien dieselben Hashes** (`sha256sum`-Vergleich), und beide stimmen mit den eingecheckten Dateien überein.
