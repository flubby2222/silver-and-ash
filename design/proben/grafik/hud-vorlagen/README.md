# HUD-Vorlagen (Issue #341)

Probestücke zum HUD, alle per Skript erzeugt (Blender 5.3 als Rechenumgebung), nichts heruntergeladen. Entscheidungen stehen in `docs/design/art-bible.md`, Recherche in `docs/design/grundlagen-hud.md`.

| Ordner | Inhalt |
|---|---|
| (Wurzel) | `basis-854x480.png` Foto ohne HUD, Blätter der Runde 1 (`blatt-1A` bis `3C`), Skripte `hud_runde2.py` bis `hud_runde4.py` |
| `runde2/`, `runde3/` | Vergleichsblätter Runde 2 und 3 (Layout A gewählt) |
| `runde4/` | A1 bis A3 in Silber, A3 alt/neu (neutrales Silber), `A3-final-854.png` |
| `runde5/` | A3 ohne Zahlen bei Leben und Ausdauer, drei Verzierungsstufen V1 bis V3, `blatt-runde5.png` |
| `runde6/` | **Endstand:** V3 überarbeitet, `blatt-runde6.png` (alt/neu, 3-fach-Ausschnitte), `r6-V3-final-854.png` |

Neu erzeugen (Beispiel Runde 6):

```powershell
& "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python hud_runde4.py -- --runde6
```

Cinzel (Windows-Schrift) steht nur in den Vorlagen; fürs Spiel kommt eine eigene Pixelschrift (#355).
