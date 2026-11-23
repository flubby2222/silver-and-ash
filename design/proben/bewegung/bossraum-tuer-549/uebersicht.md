# Bossraum-Tür: Bewegung (Probestück zu #549)

Erzeugt von `bossraum_tuer_bewegung.py` (Blender-Python und ffmpeg, nichts heruntergeladen). Probestück außerhalb des Spiels, kein Spielcode. Grundlage sind die Modelle aus `design/proben/grafik/bossraum-tuer-549/`; gezeigt wird zweiflüglig am Entwurf a und einflüglig am Entwurf b.

## Dateien

- GIFs in Spielauflösung (854 x 480, Augenhöhe 1,6 m, 5 m vor der Wand, Spiel-Licht, Echtzeit mit 50 Bildern je Sekunde): `tor-<flügelart>-<entwurf>-<variante>-<schliessen|oeffnen>.gif`, zwölf Stück. Schließen ist aus dem Bossraum gesehen, Öffnen aus dem Vorraum. Unter dem Bild laufen Tick, Winkel und der vorgeschlagene Zustand des Durchgangs mit.
- `kurven-bossraum-tuer-549.png`: alle Verläufe nebeneinander, mit Sperr-Schwelle und dem heutigen Bosstor als grauer Vergleichslinie.
- `phasen-zweifluegel-a.png`, `phasen-einfluegel-b.png`: je Verlauf sieben Phasen in gleichem Zeitabstand. Nur Vorauswahl; das Urteil fällt am GIF.
- `zeitverlauf.csv`: Winkel je Tick für alle zwölf Verläufe. `pruefung.txt`: Sperr-Schwellen, Kennzahlen, Durchstich-Rechnung.

## Die Verläufe

Winkel in Grad (0 = zu), `t` in Ticks seit dem Auslöser (60 je Sekunde), `offen` = Öffnungswinkel. Beide Flügel laufen gleich. Die Funktionen sind rein und stehen 1:1 übernehmbar im Skript (`close_even`, `close_slam`, `close_two`, `open_even`, `open_slam`, `open_two`).

Hilfsfunktionen:

- `weich(u) = u²·(3 − 2u)`, u auf 0..1 geklemmt.
- `prall(t, t0, dauer, hoehe) = hoehe·4u(1 − u)` mit `u = (t − t0)/dauer`, außerhalb 0.
- `trapez(t, T, an, ab)`: Weganteil 0..1 bei Tempo als Trapez, `v = 1/(T − (an + ab)/2)`; für `t < an`: `v·t²/(2·an)`; bis `T − ab`: `v·(t − an/2)`; danach `1 − v·(T − t)²/(2·ab)`.

| Verlauf | Formel | Ruhe ab Tick |
|---|---|---|
| Schließen 1, gleichmäßig schwer | `offen·(1 − trapez(t, 120, 30, 20))`; kein Prall | 120 |
| Schließen 2, Anlauf und harter Anschlag | bis 78: `offen·(1 − (t/78)^2,2)`; danach `prall(t, 78, 14, 2,5) + prall(t, 92, 8, 0,6)` | 100 |
| Schließen 3, zwei Etappen | bis 36: `offen + (24 − offen)·weich(t/36)`; bis 52: `24 + prall(t, 36, 16, 1,5)`; bis 80: `24·(1 − ((t − 52)/28)²)`; danach `prall(t, 80, 10, 1,5) + prall(t, 90, 6, 0,4)` | 96 |
| Öffnen 1, gleichmäßig schwer | `offen·trapez(t, 132, 30, 40)`; kein Prall | 132 |
| Öffnen 2, Anlauf und Anschlag | bis 84: `offen·(t/84)²`; danach `offen − prall(t, 84, 16, 4) − prall(t, 100, 9, 1)` | 109 |
| Öffnen 3, zwei Etappen | bis 14: `9·(1 − (1 − t/14)²)`; bis 26: `9 − 2·weich((t − 14)/12)`; bis 34: 7; danach `7 + (offen − 7)·weich((t − 34)/90)` | 124 |

Der Prall bleibt immer auf der offenen Seite: der Winkel wird nie negativ und nie größer als `offen`.

Kennzahlen (aus `pruefung.txt`):

| Verlauf | zweiflüglig (offen 90°, Schwelle 63°) | einflüglig (offen 78°, Schwelle 45°) |
|---|---|---|
| Schließen 1 | sperrt ab Tick 44, liegt an bei 120 | sperrt ab 56, liegt an bei 120 |
| Schließen 2 | sperrt ab 46, Anschlag 78, Ruhe 100 | sperrt ab 53, Anschlag 78, Ruhe 100 |
| Schließen 3 | sperrt ab 16, Anschlag 80, Ruhe 96 | sperrt ab 21, Anschlag 80, Ruhe 96 |
| Öffnen 1 | frei ab 83, offen bei 132 | frei ab 71, offen bei 132 |
| Öffnen 2 | frei ab 71, Anschlag 84, Ruhe 109 | frei ab 64, Anschlag 84, Ruhe 109 |
| Öffnen 3 | frei ab 90, offen bei 124 | frei ab 82, offen bei 124 |

Zum Vergleich: Das heutige Bosstor öffnet weich in 132 Ticks, das Fallgitter fällt in 63 und hebt sich in 126 Ticks. Alle Schließ-Verläufe dauern länger als der Fall des Gitters (Gewicht über Zeit, nicht über Winkel), kein Öffnen dauert länger als das heutige Bosstor.

## Angeln, Richtung, Öffnungswinkel

- Angeln: senkrechte Achsen bei x = +1 und x = −1 in der Türfläche (z = 0), wie die Drehpunkte der Flügel im Modell (`leaf_a`, `leaf_b`; einflüglig `leaf` bei x = +1). Der Flügel an der Gegenseite dreht mit umgekehrtem Vorzeichen (wie `DoorPose.Turns`).
- **Richtung (Vorschlag): Die Flügel schwenken immer in den Bossraum.** Beim ersten Öffnen weichen sie vor dem eintretenden Spieler zurück (wie die heutige Regel "weg vom Öffnenden"), und die Schauseite im Vorraum bleibt mit allen Bogenläufen sichtbar. Beim Schließen und beim Öffnen nach dem Kampf bleiben zweiflüglige Flügel (1 m lang) ganz in der 2 m tiefen Türzelle; im Bossraum davor wird niemand berührt.
- Öffnungswinkel: zweiflüglig 90° (lichte Weite 1,70 m). Einflüglig 78° (lichte Weite 1,67 m): ab 80° sticht der 2 m lange Flügel in Sockel und Konsole der Wandseite.

## Ab wann die Tür sperrt

Vorschlag: **Die Tür sperrt, sobald die lichte Weite unter 0,80 m fällt** (Spieler 0,70 m breit plus 0,10 m Luft), und gibt beim Öffnen ab derselben Weite frei. Als Winkel: **zweiflüglig unter 63°** (Spalt zwischen den Flügeln, mit Dicke und Beschlag gerechnet), **einflüglig unter 45°** (Abstand der Flügelspitze zum Gewände der Gegenseite). Wie beim Fallgitter dient dieselbe Zahl Bild und Sperre; der Prall nach dem Anschlag (höchstens 4°) liegt weit von der Schwelle und schaltet nichts um.

Die Schwelle hat Wirkung auf den Kampf und den Spielfluss (wann ist der Raum zu, wie lange wartet man vor der Tür). Sie ist deshalb ein Vorschlag an die Leitung, kein Entwurfswert.

## Niemand wird eingeklemmt (Vorschlag nach dem Muster des Fallgitters)

- Löst das Schließen aus, wird jede Figur in der Türzelle in den Bossraum geschoben, nicht verletzt. Das geschieht in den Ticks bis zur Sperre (je nach Verlauf 16 bis 46 Ticks); so lange passt eine Figur noch durch den Spalt, und die Flügel schwingen nicht durch sie hindurch.
- Ein zweiter Spieler, der noch im Vorraum außerhalb der Türzelle steht, bleibt draußen und unverletzt. Ob er nachgeholt wird oder der Kampf erst beginnt, wenn alle drin sind, ist eine Spielregel für die Leitung.
- Schließen 3 sperrt am frühesten (Tick 16) und lässt damit das kleinste Fenster zum Hinausschlüpfen, aber auch die kürzeste Zeit zum Schieben.

## Befunde am Modell (für den Grafikdesigner)

1. **Spitzbogen-Flügel stechen beim Schwenken in die Bogenläufe** (Entwürfe a und c, beide Schwenkrichtungen, weil das Portal vorn und hinten gleich gebaut ist). Erster Durchstich über 1 cm bei 7,7° (a) und 8,3° (c), bei 90° steckt die Flügelspitze 0,42 m (a) und 0,50 m (c) im Stein. Ursache: Der Flügel füllt den Bogen genau aus; beim Drehen wandert sein hoher Teil zur Seite, wo der Bogen niedriger ist. Kein Winkelverlauf behebt das.
   - Vorschlag: Auf der Schwenkseite (Bossraum) statt der Läufe eine glatte Nische, Halbbreite mindestens 1,2 m, Höhe mindestens 5,3 m, Tiefe von der Türfuge bis zur Wandebene. Die Rechnung ist damit über 0 bis 90° frei. **Die GIFs zeigen diese Nische als Kulisse der Probe**, damit die Bewegung ohne Durchstich beurteilt werden kann; mit `BTB_NISCHE=0` rendert das Skript das Modell, wie es ist.
   - Alternative: rechteckige Flügel bis zum Kämpfer (3,2 m) und ein festes Bogenfeld darüber. Dann bewegt sich aber nur gut die halbe Höhe.
2. **Die Angel liegt in der Mitte der Flügeldicke.** Beim Öffnen taucht die Bandseite bis 15 cm (a) und 18,5 cm (b) in Falz und innere Leibung. Das bleibt im Stein verborgen, schluckt aber die Beschläge an der Bandseite. Mit der Nische (Halbbreite 1,2 m) bleibt nur das Eintauchen in der Türfuge selbst.
3. **Der einflüglige Flügel (2 m) ragt offen 1 m aus der Türzelle in den Bossraum** und überstreicht dort einen Viertelkreis von 2 m. Im Bild steht er als hohe Platte im Raum. Zweiflüglig bleibt alles in der Zelle.

## Geprüft

- Rechnerisch für jeden Winkel in Schritten von 0,1°: Eintauchtiefe der Flügelfläche (Vorder-, Rückseite, Mitte, mit Beschlag) in Leibung, Bogen, Sturz, Sockel und Konsole; Boden und Decke bleiben frei (Flügel bis 5,18 m, Raum 6 m).
- Alle Verläufe in Zehntel-Ticks: bleiben in 0 bis `offen`, ohne Sprung, Anfang und Ende stimmen.
- Im Bild: jedes GIF in voller Länge, dazu die Phasenblätter.
- Größter Schritt je Tick 2,75° (Schließen 3 vor dem Anschlag); bei 1 m Flügellänge sind das 5 cm an der Spitze, kein sichtbares Springen.
- Nicht geprüft: der Blick aus weniger als 5 m und der Blick aus der Türzelle selbst.

## Empfehlung

- **Schließen 2, Anlauf und harter Anschlag.** Es entspricht dem Urteil "schließt sich einfach wieder" am direktesten und zeigt das Gewicht: langsamer Anlauf, voller Anschlag, kurzer Prall. 100 Ticks, gesperrt ab Tick 46.
- **Öffnen 1, gleichmäßig schwer.** Feierlich und ohne Effekt, mit 132 Ticks genau so lang wie das heutige Bosstor. Stört die Wartezeit bis zum Durchgang (Tick 83), ist Öffnen 2 die schnellere Wahl (Tick 71).
- **Zweiflüglig vor einflüglig**, was die Bewegung angeht: Die Flügel bleiben in der Zelle, die Mitte öffnet sich symmetrisch, und nichts ragt in den Kampfraum.

Die Flügel könnten leicht versetzt laufen (einer wenige Ticks später). Das ist hier nicht gezeigt und wäre ein Folgeschritt, falls die Bewegung zu mechanisch wirkt.

## Auswahlfragen an den Menschen

1. Wie schließt die Tür? **Anlauf und harter Anschlag (empfohlen)**, gleichmäßig schwer, oder zwei Etappen.
2. Wie öffnet sie? **Gleichmäßig schwer (empfohlen)**, Anlauf und Anschlag, oder zwei Etappen.
3. Wohin schwenken die Flügel? **In den Bossraum, dort bekommt das Portal eine glatte Nische (empfohlen)**, oder in den Vorraum, dann verliert die Schauseite ihre Bogenläufe.
