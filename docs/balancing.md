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
  "experience": { "ghoul": 30, "bat": 20, "firstLevel": 100, "growth": 1.25 },
  "boons":  { "offered": 3, "staminaRegen": 0.05, "health": 5, "damage": 0.05, "experience": 0.025 },
  "drops":  { "ghoulChance": 0.3, "batChance": 0.15, "consecratedAmmoShare": 0.3 },   // 0 = nie, 1 = immer
  "silver": { "ghoulChance": 0.5, "batChance": 0.5, "ghoulMin": 1, "ghoulMax": 3, "batMin": 1, "batMax": 3 },   // Chance und Silber pro Kill
  "keys":   { "roomChance": 0.15, "enemyChance": 0.05 },                                // Schlüssel pro geräumtem Raum / Kill
  "chests": { "combatRoomChance": 0.25, "beggar": { ... }, "guard": { ... }, "king": { ... }, "vampire": { ... } },   // Kisten je Stufe
  "bloodVial": { "heal": 50 },
  "consecratedAmmo": { "fireRateMultiplier": 2, "duration": 10 },
  "bloodBolt": { ... }
}
```

- Zeiten in **Sekunden**, Strecken in **Metern**, Geschwindigkeiten in **m/s**.
- **Kommentare** mit `//` sind erlaubt, ebenso ein Komma nach dem letzten Eintrag.
- Jeder Wert ist in der Datei kurz kommentiert; ausführlich in [`src/SilverAndAsh.Core/Balance.cs`](../src/SilverAndAsh.Core/Balance.cs).
- **Nicht** in der Datei: Körpergrößen (müssen zur Level-Geometrie passen) und die Mausempfindlichkeit (eine Einstellung des Spielers).

## Ändern und ausprobieren

1. Wert in der Datei ändern, speichern.
2. Spiel neu starten – oder in der **Testversion** (`dotnet run`) einfach **F5** drücken: Die Datei wird neu gelesen und das Level startet mit den neuen Werten (auf Ebene 0 mit demselben Seed, also demselben Grundriss, solange sich `layout` nicht ändert). Oben links erscheint kurz „Balancing reloaded“.

## Balance-Report: Erwartungswerte ohne Spielen

Der Report baut viele Ebenen genau wie das Spiel (Standard 500 Seeds) und zeigt, was eine Ebene im Schnitt bringt. Für jeden Wert gibt es Durchschnitt, Minimum und Maximum:
- Räume;
- Gegner und Gegner-HP;
- erreichte Stufe;
- Silber;
- Schlüssel gegen Schlösser;
- Kisten je Stufe;
- Heilung;
- eine grobe Zeitschätzung.

Ein Lauf dauert wenige Sekunden. Aufruf aus dem Repository-Ordner:

```bash
dotnet run --project tools/SilverAndAsh.BalanceReport
```

Mehr Ebenen oder eine andere Datei:

```bash
dotnet run --project tools/SilverAndAsh.BalanceReport -- --seeds 1000
```
```bash
dotnet run --project tools/SilverAndAsh.BalanceReport -- meine-werte.json
```

**Vorher/nachher vergleichen:** Datei kopieren, Werte ändern, dann

```bash
dotnet run --project tools/SilverAndAsh.BalanceReport -- --compare meine-werte.json
```

Das zeigt `assets/balance.json` (A) neben der Kopie (B) und den Unterschied B - A.

**So liest man ihn:**
- **Anzahlen** (Räume, Gegner, Kisten, Silber in Kisten, Schlösser) sind exakt.
- **Alles mit Zufall** (Silber und Schlüssel von Gegnern, Phiolen) ist ein **Erwartungswert**: Chance × Anzahl.
- **Die Zeit ist eine grobe Schätzung:** jedes Bodenfeld einmal mit voller Geschwindigkeit durchlaufen, jeden Gegner nur mit der Klinge besiegt. Zum Abgleich dient `runs.log`. Der erste Abgleich (15.11.): Schätzung 6,5 min, gespielt 6:11.

Die Berechnung steht in [`BalanceReport.cs`](../src/SilverAndAsh.Core/Balancing/BalanceReport.cs), das Tool in [`tools/SilverAndAsh.BalanceReport`](../tools/SilverAndAsh.BalanceReport/Program.cs). Das Tool gehört nicht zum Release.

## Balancing-Runden

Vor jedem Release gibt es eine Balancing-Runde (Regel in `CLAUDE.md`):
1. Report laufen lassen, Ausgangswerte festhalten.
2. Neue Werte begründet vorschlagen und mit `--compare` prüfen.
3. Einen ganzen Durchlauf spielen und `runs.log` ansehen.
4. Nachjustieren.

Ergebnis hier eintragen, neueste Runde oben.

### Runde 1 – 15.11.2026 (vor 0.9.0)

**Anlass:** Durchlauf in 6:11, nur 115 Schaden genommen, „zu leicht und zu kurz“. Ghul-Köpfe zu leicht zu treffen; Blutphiolen zu häufig und zu stark.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `layout.combatRoomsMin`/`Max` | 7 / 9 | 12 / 14 | länger: Schritt Richtung 20 Minuten |
| `difficulty.ghoulShareNear` | 0,5 | 0,7 | schon am Anfang mehr Gegner |
| `difficulty.batShareNear` | 0 | 0,25 | Fledermäuse schon früh |
| `ghoul.maxHealth` | 80 | 100 | drei statt zwei Klingenhiebe |
| `ghoul.attackDamage` | 20 | 25 | Fehler kosten mehr |
| `bat.maxHealth` | 30 | 40 | zwei statt ein Pistolenschuss |
| `drops.ghoulChance` / `batChance` | 0,3 / 0,15 | 0,12 / 0,08 | weniger Phiolen |
| `bloodVial.heal` | 50 | 30 | eine Phiole heilt weniger |
| `chests.*.bloodVialChance` | 0,2 / 0,35 / 0,6 / 1 | 0,1 / 0,25 / 0,5 / 1 | weniger Phiolen aus Kisten |
| `keys.roomChance` / `enemyChance` | 0,15 / 0,05 | 0,1 / 0,025 | mehr Räume und Gegner würden sonst 2 Schlüssel pro Schloss bringen |
| Ghul-Kopf (Code, `BodySizes.Ghoul`) | ganze vordere Körperhälfte (80 cm breit) | nur der gezeichnete Kopf (30 cm) | Kopftreffer nur noch auf den Kopf |

**Report (500 Ebenen, Durchschnitt):**

| Kennzahl | vorher | nachher |
|---|---|---|
| Kampfräume | 8,0 | 13,0 |
| Gegner | 50 | 88 |
| Gegner-HP | 3271 | 7223 |
| erreichte Stufe | 7,1 | 9,1 |
| Silber | 254 | 313 |
| Schlüssel pro Schloss | 1,32 | 1,17 |
| Heilung (× maximales Leben) | 5,7 | 2,6 |
| Zeit (grobe Schätzung) | 6,5 min | 9,8 min |

**Noch offen:** Die 20 Minuten aus ADR 0005 sind bewusst noch nicht erreicht. Erst spielen, dann in der nächsten Runde weiter (mehr Räume, Boss in v0.10).

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
