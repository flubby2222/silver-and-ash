# Gruftwächter, Runde 2 (Issue #335): Fassungen D, E, F

Richtung des Menschen: "Weniger Klötzchen, mehr Figur". Keine der Fassungen A bis C aus Runde 1 ist gewählt.
Beurteilung anhand von `blatt.png`. Erzeugt von `gruftwaechter_r2.py` (Blender-Python), nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelordner):
`"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-2/gruftwaechter_r2.py`
Umgebungsvariable `GW_OUT` lenkt die Ausgabe um, `GW_QUICK=1` rendert nur eine kleine Probe.

## Die drei Fassungen

| | Dreiecke | Breite x Tiefe x Höhe (Ruhe, mit Waffe) | Silhouette |
|---|---|---|---|
| D Hammerträger | 2232 | 2,11 x 1,62 x 2,61 m | aufrecht, breit, großer Kreuzhammer, ein Arm eisern gepanzert, einer in Leichentuch |
| E Sargdeckel-Kutte | 1780 | 2,10 x 1,44 x 2,51 m | birnenförmige Kutte mit Spitzkapuze, Sargdeckel als Schild, Kettenflegel mit Totenglocke |
| F Laternenträger | 1932 | 1,91 x 1,53 x 2,89 m (Stab) | gebückt, Buckel, Tuchwülste, Stab mit Haken und Laterne |

Zum Vergleich: Ghul 1024, Gorm 2268. Alle liegen dazwischen. Die Körper allein sind niedriger als die Maße mit Waffe (D Kopf bis etwa 2,33 m, F gebückt etwa 2,2 m).

Dreiecke je Teil: siehe `masse.txt`.

## Beachtete Regeln der Art Bible

- Alle Netze geschlossen: `pruefung.txt` (je Teil offene Kanten 0, Kanten mit mehr als zwei Flächen 0, Rauminhalt > 0) und `check_closed.txt` (`tools/models/check_closed.py`, 0 offene Kanten bei allen drei).
- Keine modellierten Hände: Griffe stecken in Manschetten (Ellipsoide), D mit hängender Kette an der linken Manschette.
- Mindestens zwei Asymmetrien je Fassung (D: Eisenschenkel rechts gegen Tuchbein links, große Schulterplatte rechts gegen kleine Kappe links; E: Schild links, Flegel rechts, Eisenkappe nur rechts; F: Buckel, eine dunkle Schulterplatte nur links, Stab rechts).
- Kleiner Kopf im Verhältnis zum Rumpf.
- Rot nur in den Augen, und nur in der Ankündigungs-Textur (`atlas_ankuendigung.png`); Ruhe-Textur ohne Rot.
- Textur nur aus der Hauptpalette, höchstens 32 Farben, keine Rasterung (im Skript geprüft). Keine Mauerwerk-Textur am Körper; Materialien: totes Fleisch, Leichentuch, Rüstteile, Stoff, Riemen, Ketten, Holz, Messing.
- Ein Grabmerkmal je Fassung als Ausrüstung: Grabkreuz (D), Sargdeckel (E), Grablaterne (F).
- Gerundete, zusammenhängende Formen: Ellipsoid-Lofts für Rumpf, Schultern, Becken, Röhren für Arme und Beine, Drehkörper für Kapuze, Glocke, Laterne. Gouraud-Schattierung.
- Ankündigungs-Haltung je Fassung (Ausholen): D Hammer über die Schulter, E Flegel nach hinten, F Laterne hochgereckt.
- Spielachsen: x seitlich, y oben, z vorn, Meter, Ursprung zwischen den Füßen.

## Nachweis

- Zwei Läufe, Blatt, drei .glb und beide Atlanten bitgleich (SHA-256 in `pruefsummen.txt`).
- `check_closed.txt`: D 2232, E 1780, F 1932 Dreiecke, 0 offene Kanten.

## Unsicherheiten

- Die Körper sind trotz Rundung sichtbar vieleckig (8 Seiten je Ring); bei D wirkt der Rumpf eher wie ein Topf oder eine Glocke als wie ein Brustkorb.
- Die Reihen "aus 10 m" sind sehr dunkel (echter Nebel und echtes Licht); gelesen werden dort fast nur Umriss und Waffe. E hat die klarste Silhouette, D und F verschwimmen mehr.
- F ist im Ton nahe am grünen Gorm (Verbände, aber grünlich-graue Fläche).
- Die Ankündigung kommt nur über Arme und Waffe; der Rumpf neigt sich nicht.
- Waffe oder Stab überragen den Körper (2,61 und 2,89 m); wer 2,2 bis 2,6 m für den Körper meint, sollte F mit kürzerem Stab erwägen.
- Der Texturstil bei Fleisch (D linker Arm, F Beine) bleibt etwas tarnmusterartig.

## Empfehlung

E (Sargdeckel-Kutte): klarste Silhouette auch im Nebel, am weitesten von Ghul und Gorm entfernt, die wenigsten Dreiecke (Raum für Animation), die Ankündigung liest sich auf 4 m eindeutig. Zweite Wahl D, wenn der Mensch einen gepanzerten Körper mit sichtbarer Wucht will.
