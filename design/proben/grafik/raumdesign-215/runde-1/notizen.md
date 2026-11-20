# Raumdesign Runde 1 (Issue #215)

Sechs Vorlagen im heutigen Format (`rN-*.txt`). Wo das Format nicht reicht, liegt daneben die beabsichtigte Fassung mit vorgeschlagenen Zeichen (`rN-*.neu.txt`); das Blatt zeigt diese.

```
python pruefen.py   # prueft alle Vorlagen, Rueckgabe 1 bei einem Fehler
python blatt.py     # schreibt blatt.png (zweiter Lauf bitgleich)
```

Alle sechs `rN-*.txt` hat der echte `RoomTemplateParser` gelesen (R1–R5 als `combat`, R6 als `treasure`), ohne Fehler. Dafür lief ein Wegwerf-Skript außerhalb des Repositorys gegen `SilverAndAsh.Core`; am Code wurde nichts geändert.

## Die Vorlagen

| | Raum | Größe | Absicht |
|---|---|---|---|
| R1 | L-Form, Kampf | 16 × 16 | Der Knick bricht die Sichtlinie: Wer von Süden oder Westen kommt, sieht den oberen Arm erst nach der Ecke. Das Podest (0,5 m, Stufe 0,25 m) in der Nordostecke ist ein Schussplatz über den oberen Arm. Gegner stehen im langen Arm, die Kiste im unteren Arm abseits der Türen. |
| R2 | T-Form, Kampf | 18 × 14 | Die Kanzel (0,5 m) am Kopf des T schaut den ganzen Stamm hinunter, eine lange Sichtlinie für das Gewehr. Gegner kommen von drei Türen. Die Kiste liegt in der Nordwestecke, weg von der Kanzel, damit man den sicheren Platz verlassen muss. |
| R3 | Nischen und Alkoven, Kampf (oder Geheim) | 14 × 14 | Sechs Nischen (je 2 Zellen breit) sind Deckung und Hinterhalt für Ghule; die Kiste steht in einer Nische. Die flache Mulde in der Mitte (−0,25 / −0,5 m) bricht die leere Fläche, ohne den Weg zu sperren. Als Geheimraum: eine Tür behalten, Gegner weg. |
| R4 | Säulenhalle, Kampf | 16 × 14 | Acht runde Säulen (Ø ca. 0,9 m, nur im Raum, nie vor einer Tür) teilen ein Mittelschiff (4 Zellen) und zwei Seitenschiffe (2 Zellen). Sie decken nur schmal, man kämpft zwischen ihnen. Der Chor (0,25 m) im Norden hat keine Tür; die Kiste steht dort in der Ecke, Gegner gehören in die Seitenschiffe. |
| R5 | Engstelle, Kampf | 16 × 12 | Zwei Hälften, nur durch einen Steg von einer Zelle über einer Grube (−1 m) verbunden; wer den Steg hält, hält den Raum. Gegner und Kiste stehen auf der Gegenseite der Haupttür. Wer fällt, kommt über eine Rampe aus 0,25-m-Stufen zu Fuß wieder hinaus (oben nach Osten, unten nach Westen), auch ein Ghul. |
| R6 | Empore, Schatz (oder Altar) | 12 × 14 | Die Kiste steht auf einer Empore (1,5 m) und ist von der Tür aus kaum zu sehen (Augenhöhe liegt nur knapp über der Emporenkante). Zwei Treppen an den Seitenwänden (je 6 Stufen à 0,25 m, 2 Zellen breit) führen hinauf. Unten bleibt eine ebene Fläche von 6 × 8 Zellen für Brunnen oder Teleporter. Als Altarraum steht der Altar statt der Kiste oben. |

## Prüfung (pruefen.py)

Das Skript prüft dieselben Regeln wie Parser und `RoomTemplateTests`: Rand, Türen, Zusammenhang, Kampfraum 10–18 Zellen, genau ein `C`, mindestens 30 freie Felder. Dazu kommen die Regeln aus dem Auftrag:

- **Erreichbarkeit:** gerichtete Flutfüllung von der ersten Tür. Hinauf gehen bis 0,4 m, springen bis 1,0 m, hinab immer. Die Füllung läuft hin und zurück.
- **Breite:** Jedes Feld liegt in einem 2 × 2-Block, in dem die Höhen um höchstens 0,4 m abweichen. Die Engstelle ist ausgenommen.

**Ergebnis:**

- Alle zwölf Dateien sind ok.
- **Hinweise:** In R5 ist der Steg schmaler als 2 Zellen (das ist gewollt). In den heutigen Fassungen von R3 und R5 kommt man aus `_` nur mit einem Sprung heraus.
- **Gegenprobe:** In meiner ersten Fassung von R1 lag zwischen Podest und Wand ein Streifen von einer Zelle. Das Skript meldete „6 Felder schmaler als 2 Zellen“, danach habe ich das Podest an die Wand gerückt.

## Format-Lücken und Vorschläge

| Lücke | heute markiert mit | Vorschlag |
|---|---|---|
| Runde Säule (R4) | `#` (sperrt die ganze Zelle von 2 × 2 m) | `O`: Boden auf 0 m, in der Zellmitte ein Kollisionskreis von ca. 0,9 m (Urteil zu #284). Nur in Räumen. |
| Empore höher als 0,75 m (R6) | `3` | `4` bis `8` = 1,0 bis 2,0 m, weiter in Schritten von 0,25 m. Eine Treppe ist eine Ziffer je Zelle. |
| Flache Mulde, Rampe aus einer Grube (R3, R5) | `.` für −0,25 m, `_` für −0,5 und −0,75 m | `a` bis `h` = −0,25 bis −2,0 m. `_` bleibt −1 m (gleich `d`), eine tiefe Grube von −2 m ist `h` und braucht eine Rampe oder Treppe heraus. |
| Kiste auf einer Höhe (R6) | `C` liegt immer auf 0 m, heute steht die Kiste deshalb vor der Empore | Regel: `C` übernimmt die Höhe seiner Nachbarn, wenn alle gleich hoch sind. `pruefen.py` rechnet in `.neu.txt` schon so. |
| Decke je Raum (Kreuzrippen, Kassetten, #244) | – (die Decke kommt aus der Raumart) | Optionale Kopfzeilen mit `;`, die der Parser überspringt, z. B. `; decke kassetten`. |

**Größere Alternative für später:** ein zweites, gleich großes Höhenraster nach einer Leerzeile. Das erste Raster sagt nur noch, *was* an einer Stelle ist (Wand, Boden, Tür, Platz), das zweite, *wie hoch* sie liegt. Dann können auch Gegner, Kisten und Säulen auf jeder Höhe stehen, ohne dass die Zeichen sich vervielfachen.

Mit den Höhen müsste die Doku-Regel „Auf Ebene 0 gibt es noch keine Höhenunterschiede“ fallen.

## Spielbilder: weggelassen

Ein Spielbild ginge nur mit einer Codeänderung:

- `--photo` kennt nur die Szenen in `Core`.
- `--level` lädt handgebaute Level über den `LevelParser`. Der kennt `+` und `C` nicht und baut Decken von 3,5 m statt 6 m, ohne Räume und Türen.

Ein Blick vom Eingang bräuchte also eine neue Foto-Szene in `Core`.

**Vorschlag:** ein Issue für eine Szene, die eine Ebene aus einer einzigen Vorlage baut. Der Spieler steht hinter der ersten Tür und blickt in den Raum. Damit ließe sich jede Vorlage als Bild prüfen.

## Wie das Set für Coop wachsen kann

- **Wege und Plätze:** Wege sind 2 Zellen breit, ebene 3 × 3-Flächen gibt es überall. Zwei Spieler gehen nebeneinander, und Brunnen und Teleporter finden Platz.
- **Türen:** Kampfräume haben 3 bis 4 Türen, Spieler können also getrennt eintreten und flankieren. R5 trennt sie absichtlich: Einer hält den Steg, der andere geht durch die Grube.
- **Gegnerzahl:** Alle Kampfräume haben mindestens 113 freie Felder statt der verlangten 30. Für mehr Gegner bei mehr Spielern braucht es keine neuen Vorlagen.
- **Format:** Die neuen Zeichen kommen nur dazu, alte Vorlagen bleiben gültig. Für einen zweiten Startplatz in Start-Vorlagen könnte `P` zweimal erlaubt werden.

## Vorschläge für Runde 2

- **Raumarten** auf dem Weg zu rund 25 Vorlagen:
  - 2 Boss (einer mit tiefer Grube −2 m und Treppe, einer mit Empore)
  - je 3 Start, Händler und Altar
  - die restlichen Schatz- und Geheimräume (Varianten von R3 und R6)
  - 2 weitere Kampfräume (Kreuzform, Galerie über dem Raum)
- **Prüfregeln** aus `pruefen.py` als Test ins Repository: Breite und Erreichbarkeit mit Höhen. Das wäre ein Issue für eine Code-Sitzung.
- **Spielbilder** über die vorgeschlagene Foto-Szene, dann vom Eingang aus urteilen.

## Offene Fragen

- **Können Ghule springen?** Falls nicht, erreichen sie ein Podest `3` (0,75 m) nicht, und eine Grube `_` ohne Rampe ist für sie eine Falle. Die heutigen Fassungen von R3 und R5 haben dort keine Rampe.
- **Sprunghöhe:** In `balance.json` steht `jumpHeight` 1,3 m, die Doku sagt „ca. 1 m“. Die Prüfung rechnet vorsichtig mit 1,0 m.
- **Kollision runder Säulen:** Bis `O` kommt, sperrt eine Säule die ganze Zelle. R4 funktioniert auch so, wirkt aber klotziger.
