# Gruftwächter, Runde 3 (Issue #335): F weiterentwickelt, Fassungen F1, F2, F3

Richtung des Menschen zu Runde 2: "F, aber bitte überarbeitet (kein Clipping) und detailreicher. Er soll den Eindruck eines alten untoten Kämpfers machen, der die Gräber seiner gefallenen Kameraden bewacht."
Beurteilung anhand von `blatt.png`. Erzeugt von `gruftwaechter_r3.py` (Blender-Python), nichts heruntergeladen.

Aufruf (aus dem Repository-Wurzelordner, TEMP/TMP auf einen Ordner außerhalb von /tmp setzen):
`"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-3/gruftwaechter_r3.py`
`GW_OUT` lenkt die Ausgabe um, `GW_QUICK=F1|F2|F3` rendert nur eine kleine Probe.

## Die drei Fassungen

| | Dreiecke | Breite x Tiefe x Höhe (Ruhe, mit Waffe) | Silhouette und Geschichte |
|---|---|---|---|
| F1 Grabwächter | 2528 | 1,93 x 1,60 x 2,39 m | nah an Runde-2-F: gebückt, Buckel mit Pfeilen, Verbände, Kettenkreuz mit Messingschloss, Stab mit Laterne, Marken, Kerzenstummel an der linken Manschette |
| F2 Veteran | 2438 | 2,05 x 1,48 x 2,59 m | Rüstreste (Rost und Stahl) über Verbänden, Kettengurt, Hellebarde mit Laterne, Pfeil und Bolzen im Rumpf |
| F3 Totenwache | 2422 | 1,95 x 1,19 x 2,46 m | Wappenrock mit Grabkreuz, Umhang, Marken und Schlüsselplättchen, Helm am Riemen, Kerzenbündel, Standarte mit Laterne |

Körper allein 2,0 bis 2,2 m (Kopf bis etwa 2,2 m). Zum Vergleich: Ghul 1024, Gorm 2268. Dreiecke je Teil: `masse.txt`.

## Beachtete Regeln der Art Bible und der Aufgabe

- **Kein Clipping:** Rumpf ist ein einziger verschweißter Körper; Ausrüstung (Gurte, Pfeile, Marken, Nieten, Andenken) wird per Strahl auf die fertige Oberfläche gelegt. Rechnerische Prüfung (`pruefung.txt`): paarweise BVH-Durchdringungstest aller Körper plus Selbstdurchdringung, in Ruhe und Ankündigung. Ergebnis: 0 nicht erlaubte Paare, 0 Selbstdurchdringungen. Erlaubt ist nur Manschette gegen Schaft (Hand hält die Waffe).
- Alle Netze geschlossen: `pruefung.txt` (je Teil offene Kanten 0, mehr als zwei Flächen 0) und `check_closed.txt` (0 offene Kanten).
- Keine modellierten Hände (Griffe in Manschetten). Mindestens zwei Asymmetrien je Fassung (Buckel, Stab rechts, Kerzen links, Helm links, Kerzenbündel rechts, Hellebarde nur rechts, ungleiche Rüstteile).
- Kleiner Kopf. Rot nur in den Augen der Ankündigungs-Textur.
- Nur Hauptpalette, höchstens 32 Farben, keine Rasterung (im Skript geprüft). Keine Mauerwerk-Textur. Farben bewusst weg vom grünen Gorm: Knochenweiß, Rost, verblichenes Tuch.
- Ankündigung: Rumpf verwindet und neigt sich mit (Ausholen aus der Hüfte), Waffe folgt.
- Höhe mit Waffe höchstens 2,6 m (F2 2,59 m, Hellebarde dafür verkürzt).
- Spielachsen x seitlich, y oben, z vorn, Meter, Ursprung zwischen den Füßen.

## Nachweis

- Zwei Läufe in leeren Ordnern: Blatt, drei .glb und sechs Atlanten bitgleich (`pruefsummen.txt`).
- `check_closed.txt`: F1 2528, F2 2438, F3 2422 Dreiecke, 0 offene Kanten.

## Unsicherheiten

- Körper trotz 16 Seiten je Rumpfring und 10 je Glied noch leicht vieleckig; im Nebel aus 10 m lesen sich fast nur Umriss und Waffe.
- Der Rumpf ist weiterhin eine runde Tonne; "Alter Kämpfer" kommt vor allem über Ausrüstung und Haltung, weniger über die Körperform.
- F2 liegt mit 2,59 m knapp unter der Höhengrenze. F2-Rumpffarbe (Blau und Rost) ist die kräftigste, F1 am nächsten an Runde-2-F.
- Nieten und Flicken sind flache Platten mit 1 cm Abstand zur Haut; aus nächster Nähe schweben sie minimal.
- Fleischtextur der Gliedmaßen wirkt etwas weniger tarnmusterartig als in Runde 2, ist aber noch nicht ganz glatt.

## Empfehlung

F3 (Totenwache): trägt die Geschichte am deutlichsten (Wappenrock mit Grabkreuz, Helm eines Gefallenen, Kerzen, Standarte) und hat die klarste Silhouette. Zweite Wahl F1, wenn die Figur näher an Runde-2-F bleiben soll.
