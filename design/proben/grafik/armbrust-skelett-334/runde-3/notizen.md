# Skelett mit Armbrust, Runde 3: der Bogen (Issue #334)

Urteil zu Runde 2: "A2, aber der Bogen von der Armbrust muss nochmal überarbeitet werden (soll auch ein Bogen sein)."
Die Figur A2 ist unverändert (1518 Dreiecke in Runde 2; das Skript führt `armbrust2.py` aus und ersetzt nur die Armbrust).
Blatt: `blatt.png`. Modelle: `modelle/armbrust_skelett_<B1|B2|B3>_<ruhe|ank>.glb`.

## Was war falsch an A2

Gerader dünner Stab mit abgewinkelten Enden, die nach vorn zeigten. Von vorn und aus 10 m ein Stab, kein Bogen.

## Die Lösung, gemeinsam für alle drei

- Ein durchgehendes Rohr von Spitze zu Spitze: Mitte dick, Enden dünn. Die Schenkel biegen sich nach hinten zum Schützen.
- Die Spitzen heben sich zusätzlich nach oben. Ein Bogen liegt waagerecht, von vorn sieht man ihn sonst nur als Balken;
  durch den Anstieg zeigt auch die Vorderansicht (und der Blick aus 10 m) die Kurve.
- Die Sehne ist ein eigenes Teil (`sehne`) von Spitze zu Spitze. In Ruhe läuft sie gerade zwischen den Spitzen, mitten zur
  Schaftachse abgesenkt, der Bolzen liegt hinten an ihr. In der Ankündigung ist sie zur Nuss gezogen, die Schenkel sind
  stärker gebogen (Spitzen 13 bis 14 cm weiter zurück, 3 cm höher), der Bolzen liegt auf der Nuss.

## Die Fassungen

| | Dreiecke gesamt | Armbrust | Sehne | Bogenbreite Ruhe |
|---|---|---|---|---|
| A2 (Runde 2) | 1518 | 198 | in der Armbrust | 1,08 m |
| B1 Holzbogen | 1650 | 298 | 32 | 1,09 m |
| B2 Stahlbogen | 1614 | 262 | 32 | 1,15 m |
| B3 Recurve | 1622 | 270 | 32 | 1,14 m |

- **B1**: Holz, gleichmäßige Parabel, sechseckiger Querschnitt, Lederwicklung in der Mitte und zwei Bänder an den Enden.
- **B2**: Stahl, flacher (Kurve erst zu den Enden hin), breiter und dünner Querschnitt, breite Lederwicklung in der Mitte mit
  Eisenringen, rostige Endkappen.
- **B3**: Hornbogen in dunklem Knochen, S-Kurve: die Enden schwingen wieder nach vorn und steigen steil an. Eisenbeschlag in der Mitte,
  Lederbänder, an jeder Spitze eine Hornspitze aus Eisen und ein Haken für die Sehne.

## Regeln der Art Bible

- Alles per Skript, nichts heruntergeladen; Textur-Atlas unverändert (23 Farben Ruhe, 25 Ankündigung, Grenze 32, nur Hauptpalette).
- Rot nur in den Pupillen der Ankündigung (Atlas "angriff"); die Ruhe-Atlas hat kein Rot.
- Keine modellierten Hände (Fäustlinge wie A2).
- Geschlossene Modelle: `check_closed.py` meldet 0 offene Kanten für alle sechs Dateien.
- Flat Shading, keine Texturen außer dem Atlas.
- Zwei Läufe liefern bitgleiche Dateien (SHA-256 der glb, Atlanten, masse.txt und blatt.png).

## Stolperstein (für das nächste Mal)

`Part.loft` setzt an Rohre mit nur zwei Ringen eine Kappe, die um ein Viertel der Länge über das Ende hinausragt. Bei der
Sehne machte das 14 cm Überstand je Seite und verfälschte die Breite. Lösung im Skript: `inset()` rückt die Ringe je um ein
Sechstel nach innen, dann enden die Spitzen genau am Ziel.

## Unsicher

- Die Hebung der Spitzen ist eine Stilentscheidung (kein echter Bogen hängt so); sie ist der Grund, warum die Kurve von vorn lesbar ist.
- Die Sehne ist bei 10 m kleiner als ein Pixel; dort trägt allein der Schwung der Schenkel.
- Beim B3 liegt die Sehne an den Spitzen minimal hinter dem Schenkel, dadurch ist der Recurve-Schwung im Grundriss dezenter als in der Vorderansicht.
