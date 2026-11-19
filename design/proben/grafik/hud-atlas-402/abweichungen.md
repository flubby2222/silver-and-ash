# Abweichungen des doppelten Atlas (Issue #402)

Skript: `tools/hud/hud_atlas_doppelt.py`. Vollständige Tabelle alt -> neu: [zuordnung.md](zuordnung.md) (71 Teile, 60 HUD und 11 Menü, alle mit Gegenstück).

| Teil | Abweichung von „genau doppelt“ | Grund |
|---|---|---|
| `icon_*` (alle Zeichen) | gleiche Mitte, Teilbild 2× plus 3 px Rand je Seite | Schatten und Relief wurden sonst abgeschnitten |
| Waffen- und Gegenstandszeichen | etwa 1,15× größer und heller als nur verdoppelt | Vorgabe der Leitung: heller und größer als im Probestück |
| `frame_*`, `panel` | kein Giebel und keine Spitzen, dünnes Metall-Relief | Richtung A wie im Muster aus #393; Zierrat ist ein Folgepunkt |
| `gable` | kleineres Dreieck mit Auge statt der heutigen Form | Das heutige Teil wäre bei doppelter Größe über 126 px breit (abgeschnitten) |
| `knob` | Raute, Radius 5,2, dunkler Rand | heutiger Knopf wäre am Rand abgeschnitten |
| `toast`-Auge | Radius 3,2 statt 4,2 | sonst abgeschnitten |
| `tab_*` | einfache Rechteckrahmen, aktiver Tab mit Raute | Schrift zeichnet das Spiel |

Nicht abweichend: alle anderen Teile (Balken, Ausdauer, Fadenkreuz, Regler, Cursor, Tafel) sind exakt doppelt; Neun- und Dreiteilung (`slice`) doppelt (8 -> 16, 24 -> 48, 20 -> 40).
