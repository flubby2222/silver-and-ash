# Balancing-Werte ändern

Alle Werte, die das Spiel leichter oder schwerer machen – Leben, Schaden, Abklingzeiten, Geschwindigkeiten, Zeiten, Reichweiten –, stehen in **[`assets/balance.json`](../assets/balance.json)**. Man kann sie ändern, **ohne das Spiel neu zu bauen**. Hintergrund: [ADR 0004](entscheidungen/0004-fortschritt-stufen-beute-skills.md).

## Wo liegt die Datei?

| Wo | Welche Datei |
|---|---|
| Im Repository | `assets/balance.json` – das ist die **Vorlage** mit den Standardwerten |
| Beim Entwickeln (`dotnet run`) | `src/SilverAndAsh.Game/bin/Debug/net10.0/assets/balance.json` – eine Kopie, die beim Bauen neu geschrieben wird |
| Im Release (`.zip`) | `assets/balance.json` neben `SilverAndAsh.Game.exe` |

Zum **Ausprobieren** die Kopie neben dem Spiel ändern. Ein Wert, der **bleiben** soll, gehört in die Vorlage im Repository – per Pull Request (und dann auch in `Balance.cs`, siehe unten).

## Aufbau

```jsonc
{
  "restartDelay": 2,          // Sekunden bis zum Neustart nach dem Tod
  "player": { "maxHealth": 100, "maxSpeed": 6, ... },
  "blade":  { "damage": 40, "cooldown": 0.4, ... },
  "pistol": { ... },
  "dummy":  { ... },
  "ghoul":  { "speed": 4.5, ... },
  "bat":    { ... },
  "drops":  { "ghoulChance": 0.3, "batChance": 0.15, "consecratedAmmoShare": 0.3 },   // 0 = nie, 1 = immer
  "bloodVial": { "heal": 50 },
  "consecratedAmmo": { "fireRateMultiplier": 2, "duration": 15 },
  "bloodBolt": { ... }
}
```

- Zeiten in **Sekunden**, Strecken in **Metern**, Geschwindigkeiten in **m/s**.
- **Kommentare** mit `//` sind erlaubt, ebenso ein Komma nach dem letzten Eintrag.
- Jeder Wert ist in der Datei kurz kommentiert; ausführlich in [`src/SilverAndAsh.Core/Balance.cs`](../src/SilverAndAsh.Core/Balance.cs).
- **Nicht** in der Datei: Körpergrößen (müssen zur Level-Geometrie passen) und die Mausempfindlichkeit (eine Einstellung des Spielers).

## Ändern und ausprobieren

1. Wert in der Datei ändern, speichern.
2. Spiel neu starten – oder in der **Testversion** (`dotnet run`) einfach **F5** drücken: Die Datei wird neu gelesen und das Level startet mit den neuen Werten. Oben links erscheint kurz „Balancing reloaded“.

## Wenn etwas nicht stimmt

Das Spiel startet **immer**. Was nicht passt, landet als Warnung im Log (`%LOCALAPPDATA%\SilverAndAsh\logs\`):

| Fall | Was passiert |
|---|---|
| Datei fehlt oder ist kaputt (z. B. Klammer vergessen) | alle Standardwerte |
| Ein Wert fehlt | Standardwert für diesen Wert (keine Warnung) |
| Unbekannter Name (Tippfehler, z. B. `"sped"`) | wird ignoriert |
| Falscher Typ (`"maxHealth": "viel"` oder `12.5` bei ganzen Zahlen) | alle Standardwerte (die Datei ist dann „kaputt“) |
| Negativ, oder 0, wo 0 das Spiel kaputt machen würde (z. B. Abklingzeit 0) | Standardwert für diesen Wert |
| Über einer Grenze (Sprung höher als der Raum, Winkel über 180° …) | Standardwert für diesen Wert |
| Minimum größer als Maximum (z. B. `circleRadiusMin` > `circleRadiusMax`) | Standardwerte für beide |

## Standardwerte dauerhaft ändern

Die Standardwerte stehen an **zwei** Stellen: in `Balance.cs` (gilt, wenn ein Wert in der Datei fehlt, und für die Tests) und in `assets/balance.json`. Ein Test prüft, dass beide gleich sind – ändert man nur eine, wird er rot. Also immer beide zusammen ändern.
