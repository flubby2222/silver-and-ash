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
  "experience": { "ghoul": 30, "bat": 20, "levelCosts": [ { "fromLevel": 0, "cost": 270 }, { "fromLevel": 10, "cost": 540 }, ... ] },
  "boons":  { "offered": 3, "staminaRegen": 0.05, "health": 5, "damage": 0.05, "experience": 0.025 },
  "drops":  { "ghoulChance": 0.3, "batChance": 0.15, "consecratedAmmoShare": 0.3 },   // 0 = nie, 1 = immer
  "silver": { "ghoulChance": 0.5, "batChance": 0.5, "ghoulMin": 1, "ghoulMax": 3, "batMin": 1, "batMax": 3 },   // Chance und Silber pro Kill
  "keys":   { "roomChance": 0.15, "enemyChance": 0.05 },                                // Schlüssel pro geräumtem Raum / Kill
  "chests": { "combatRoomChance": 0.25, "beggar": { ... }, "guard": { ... }, "king": { ... }, "vampire": { ... } },   // Kisten je Stufe, mit Ausrüstungs-Chance (equipmentChance)
  "loot": { "higherTierChance": 0.1, "itemShare": 0.7 },   // Beute-Topf: Chance auf eine Stufe höher, Anteil Gegenstand statt Waffe
  "doors":  { "normalOpenTime": 0.9, "lockedOpenTime": 1.3, "bossOpenTime": 2.2 },  // Sekunden, bis eine Tür ganz aufgeschwungen ist
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
- Ausrüstung aus Kisten (erwartet) und von Gorm;
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

Musterraum, Prüfstrecke und seit #754 auch die Raum-Vorschau (`--room`) schreiben keine Zeile in `runs.log`. Ältere Vorschau-Läufe lassen sich dort nicht sicher erkennen, weil eine Zeile weder Raum noch Startart nennt; einen Hinweis gibt höchstens die Raumzahl (`rooms 0/0`, `0/1` oder `1/1` statt der Kampfräume einer ganzen Ebene 0, wie sie aber auch ein Lauf mit `--level` zeigt).

Ergebnis hier eintragen, neueste Runde oben.

### Zwischenschritt – 25.11.2026 (#202, Peitsche, vorläufig)

**Anlass:** Issue #202, Teil 1 (Regeln). Neue Nahkampfwaffe der Stufe Royal: weit, mittlerer Schaden, zieht Ghule und Fledermäuse heran (Gorm nicht), aufladbar; voll aufgeladen bricht sie rissige Wände. Neuer Block `whip`, neuer Wert `pullTo` in allen Nahkampf-Blöcken.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `whip.cooldown` | – | 0,7 s (**vorläufig**) | langsamer als die Klinge (0,4), etwas schneller als die Axt (0,8) |
| `whip.damage` | – | 45 (**vorläufig**) | „mittel“ aus dem Issue: zwischen Klinge (40) und Keule (50) |
| `whip.chargeTime` / `chargeMaxMultiplier` / `chargeStaminaSegments` | – | 1 s / 2 / 1 (**vorläufig**) | wie die Keule; voll aufgeladen bricht sie rissige Wände |
| `whip.range` | – | 6 m (**vorläufig**) | „etwa 6 m“ aus dem Issue, mehr als doppelt so weit wie die Klinge (2,5) |
| `whip.angle` | – | 50° (**vorläufig**) | ein schmaler Hieb. 30° verfehlte einen Ghul direkt vor den Füßen (der Kegel misst von 0,6 m hinter den Augen), 50° trifft ihn |
| `whip.knockbackTime` | – | 0,3 s (**vorläufig**) | so lange dauert das Heranziehen |
| `whip.pullTo` | – | 1,5 m (**vorläufig**) | der Gegner landet knapp außerhalb der Klinge vor dem Spieler; wer schon näher steht, bleibt stehen |
| `blade.pullTo`, `club.pullTo`, `huntersAxe.pullTo` | – | 0 | 0 = zieht nicht |

`pullTo` darf 0 sein, aber nicht negativ; `whip.angle` höchstens 180. Außerhalb warnt der Lader und nimmt den Standardwert.

**Vorläufig festgelegt:** Aufgeladen zieht die Peitsche gleich weit wie unaufgeladen. Betäubte und herabstoßende Gegner werden mitgezogen. Fledermäuse werden waagrecht gezogen und behalten ihre Höhe. Ein Ghul bleibt an einer Grubenkante und vor einer Stufe über 0,4 m stehen; eine Wand dazwischen schirmt ihn ab.

**Report und Bot-Läufe:** für diesen Schritt nicht gelaufen. Die Peitsche kommt wie die Jagdaxt in den Beutevorrat (Kisten und Gorm); ob sich das Heranziehen im Lauf bewährt, zeigt erst ein Spieltest (Modell, Klang und HUD folgen in Teil 2).

### Zwischenschritt – 25.11.2026 (#195, Items Teil 1, vorläufig)

**Anlass:** Issue #195. Die ersten vier Items wirken jetzt. Bisher ließen sie sich finden, taten aber nichts. Die Werte stehen im neuen Abschnitt `items`.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `items.huntersBootsSpeed` | – | 0,1 = +10 % Höchstgeschwindigkeit beim Gehen | Wert aus dem Issue. Die Stiefel gelten am Boden und in der Luft, aber nicht für Dash und Ausweichen (**vorläufig**). |
| `items.ironFlaskHeal` | – | 0,5 = Blutfläschchen heilen 50 % mehr (30 → 45; halbe Punkte werden aufgerundet) | Wert aus dem Issue |
| `items.silverRosaryHeal` | – | 3 Leben je Kill | Wert aus dem Issue. Er zählt jeden Gegner, auch Gorm und Kills durch Schaden über Zeit, aber keine Übungspuppe (**vorläufig**). |
| `items.quickdrawHolsterReload` | – | 0,3 = 30 % weniger Nachladezeit (Pistole 90 → 63 Ticks, Gewehr 150 → 105, Donnerbüchse 108 → 76), höchstens unter 1 | Wert aus dem Issue. „30 % schneller“ heißt hier ×0,7, nicht ÷1,3 (**vorläufig**). |

Jeder Wert darf 0 sein (das Item tut dann nichts), aber nicht negativ. `quickdrawHolsterReload` muss unter 1 bleiben. Sonst warnt der Lader und nimmt den Standardwert. Die Texte der Items im Spiel nennen die Standardwerte fest; ein Test meldet, wenn sie nicht mehr passen.

**Report und Bot-Läufe:** für diesen Schritt nicht gelaufen. Der Balance-Report rechnet die Heilung durch Blutfläschchen ohne die Flasche, und kein Bot-Modus nimmt Items.

**Offen:** Ob die Unterschiede im Spiel spürbar sind, urteilt der Mensch im Spieltest.

### Zwischenschritt – 25.11.2026 (#510, Leech, vorläufig)

**Anlass:** Issue #510. Fünfter Statuseffekt: Löst ein Treffer *Leech* aus, heilt der Spieler, der getroffen hat, sofort um einen Anteil des Schadens. Keine Dauer, kein Timer, nie über das Höchstleben; Bruchteile sammelt jeder Spieler für sich. Gefundene Waffen würfeln seither gleich wahrscheinlich zwischen den fünf Effekten.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `statusEffects.leech.share` | – | 0,025 = 2,5 % des Schadens (höchstens 1, **vorläufig**) | Wert aus dem Issue. Ein Gewehrtreffer (80) gibt 2 Leben, eine Keule (50) 1,25, ein Donnerbüchsen-Schuss je nach Kugeln zusammen. |

Der Wert muss größer als 0 sein; liegt er außerhalb, warnt der Lader und nimmt den Standardwert.

**Welcher Schaden zählt (vorläufig):** der, den das Treffer-Ereignis des Gegners meldet. Gegen Gorm mit festsitzender Keule ist das der verdoppelte Schaden. Beim tödlichen Treffer meldet der Ghul heute den ganzen Schaden, Fledermaus und Gorm nur das restliche Leben – Leech zählt jeweils das. Die Meldungen anzugleichen ist eine eigene Aufgabe.

**Report und Bot-Läufe:** für diesen Schritt nicht gelaufen. Wie bei #507 bis #509 rechnet der Report keine Statuseffekte ein, und kein Bot-Modus zählt sie.

**Offen:** Ein Lauf im Spiel steht aus. Leech greift nur, wenn der Treffer den Effekt auslöst (Chance der Seltenheit, 10 bis 25 %); ob das zusammen mit 2,5 % spürbar ist, urteilt der Mensch im Spieltest.

### Zwischenschritt – 25.11.2026 (#509, Stun, vorläufig)

**Anlass:** Issue #509. Vierter Statuseffekt nach *Marked*, *Cleansing Flame* und *Grave Chill*: Ein betäubter Ghul oder eine betäubte Fledermaus steht still und greift nicht an, ein laufendes Ausholen bricht ab. Gorm taumelt nur kurz (bleibt stehen, Ankündigungen laufen weiter) und ist danach eine Weile immun. Gefundene Waffen würfeln seither gleich wahrscheinlich zwischen den vier Effekten.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `statusEffects.stun.seconds` | – | 0,5 s (höchstens 5, **vorläufig**) | Wert aus dem Issue: lang genug, um einen Ghul aus dem Ausholen zu reißen, zu kurz, um einen Gegner wehrlos zu halten. |
| `statusEffects.stun.gormStaggerSeconds` | – | 0,2 s (nie länger als `seconds`, **vorläufig**) | Wert aus dem Issue: Der Boss soll nur kurz stocken. Ist er länger als `seconds`, nimmt der Lader für beide die Standardwerte. |
| `statusEffects.stun.gormImmuneSeconds` | – | 5 s (höchstens 60, **vorläufig**) | Wert aus dem Issue: Gorm lässt sich nicht im Dauertakt festhalten. Die Immunität beginnt schon mit dem Taumeln. |

Alle drei Werte müssen größer als 0 sein; liegt einer außerhalb, warnt der Lader und nimmt den Standardwert.

**Report und Bot-Läufe:** für diesen Schritt nicht gelaufen. Wie bei #507 und #508 rechnet der Report keine Statuseffekte ein, und kein Bot-Modus zählt sie.

**Offen:** Ein Lauf im Spiel steht aus. Ob 0,5 s spürbar sind und ob Gorm mit 0,2 s zu wenig reagiert, urteilt der Mensch im Spieltest.

### Zwischenschritt – 25.11.2026 (#508, Grave Chill, vorläufig)

**Anlass:** Issue #508. Dritter Statuseffekt nach *Marked* und *Cleansing Flame*: Grave Chill verlangsamt Laufen und Fliegen eines Gegners (Ghul kriechen, Fledermaus kreisen und steigen, Gorm gehen, auch in Phase 2). Ankündigungen, Angriffe und der Sturzflug der Fledermaus behalten ihre Länge. Gefundene Waffen würfeln seither gleich wahrscheinlich zwischen den drei Effekten.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `statusEffects.graveChill.slow` | – | 0,2 (unter 1, **vorläufig**) | Wert aus dem Issue: Der Gegner bewegt sich mit 80 % seiner Geschwindigkeit. Unter 1, damit er nie stillsteht. |
| `statusEffects.graveChill.seconds` | – | 2 s (höchstens 30) | Wert aus dem Issue, gleich lang wie die Cleansing Flame. Erneutes Auslösen verlängert auf die volle Dauer, stapelt nicht. |

Liegt ein Wert außerhalb, warnt der Lader und nimmt den Standardwert.

**Report und Bot-Läufe:** für diesen Schritt nicht gelaufen. Wie bei #507 rechnet der Report keine Statuseffekte ein, und kein Bot-Modus zählt sie; ein Vergleich würde nur zeigen, dass Türen, Wege und Stillstand gleich bleiben.

**Offen:** Ein Lauf im Spiel steht aus. Ob 20 % Verlangsamung spürbar sind, urteilt der Mensch im Spieltest.

### Zwischenschritt – 25.11.2026 (#507, Cleansing Flame, vorläufig)

**Anlass:** Issue #507. Zweiter Statuseffekt nach *Marked*: Die Cleansing Flame brennt einen Gegner in Pulsen. Gefundene Waffen würfeln seither zwischen *Marked* und *Cleansing Flame*.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `statusEffects.cleansingFlame.seconds` | – | 2 s (höchstens 30) | Wert aus dem Issue. Erneutes Auslösen verlängert auf die volle Dauer, stapelt nicht. |
| `statusEffects.cleansingFlame.damagePerSecond` | – | 5 (höchstens 100, **vorläufig**) | Wert aus dem Issue: 10 Schaden über die ganze Dauer, ein Viertel eines Klingenschlags. |
| `statusEffects.cleansingFlame.tickSeconds` | – | 0,5 s (höchstens 5, nie länger als `seconds`, **vorläufig**) | Wert aus dem Issue: 4 Pulse mit 2, 3, 2 und 3 Schaden. Die Pulse runden ganzzahlig; die Summe ist genau `damagePerSecond × seconds`, weil die Flamme auf einem Puls enden muss (Regel darunter). |

**Regel:** Die Dauer muss ein ganzes Vielfaches des Pulses sein, gerechnet in Ticks, wie das Spiel sie erzeugt (Sekunden × 60, gerundet; ein Puls kürzer als ein Tick zählt als ein Tick). Sonst brennten die Ticks nach dem letzten Puls umsonst: 2 s mit Puls 0,75 s gäben 7 statt 10 Schaden. Der Lader warnt dann und nimmt für `seconds` und `tickSeconds` die Standardwerte, ebenso, wenn `tickSeconds` länger als `seconds` ist. Beispiele, die gelten: 2 s mit 0,5 s, 0,25 s oder 0,3333 s (20 Ticks); 1 s mit 0,001 s (jeder Tick ein Puls). Die Summe ist dann genau `damagePerSecond × seconds` (ist das keine ganze Zahl, abgerundet). Die Höchstwerte 100 und 5 sind geschätzt, nicht gemessen.

**Report (500 Ebenen), `main` gegen diesen Stand:** zeilengleich bis auf die Laufzeit des Reports. Der Report rechnet Statuseffekte nicht ein; die Cleansing Flame ändert dort nichts.

**Bot-Läufe (`all`, Seeds 1 bis 6), `main` gegen diesen Stand:** Ausgabe zeilengleich (alle Zähler gleich), auf beiden Seiten 0 Befunde, Exit-Code 0. Kein Bot-Modus zählt Statuseffekte; dass die Flamme in seinen Läufen gar nicht vorkommt, ist damit nicht belegt, nur dass sie nichts an Türen, Wegen und Stillstand ändert.

**Offen:** Ein Lauf im Spiel steht aus. Ob 10 Schaden je Flamme spürbar sind, urteilt der Mensch im Spieltest.

### Zwischenschritt – 25.11.2026 (#503, Erfahrungskurve B)

**Anlass:** Issue #503. Der Mensch hat am 20.11.2026 Variante B der Erfahrungskurve gewählt (Skills-Entwurf, Abschnitt 7, #104 Runde 5): Jede Stufe kostet gleich viel, ab Schwellen mehr. Bisher stieg jede Stufe um den Faktor 1,25.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `experience.firstLevel` / `experience.growth` | 100 / 1,25 (100, 125, 156, 195 …) | entfällt | ersetzt durch die Tabelle darunter |
| `experience.levelCosts` | – | ab Stufe 0: 270, ab 10: 540, ab 20: 1080 | Entscheidung des Menschen (Variante B). Eine Zeile gilt ab ihrer Stufe bis zur nächsten Zeile. |

Spiel (`Player.ExperienceToNextLevel`) und Report (`BalanceReport.LevelReached`) rechnen mit derselben Methode `ExperienceBalance.CostFrom`.

**Report (500 Ebenen), `main` gegen diesen Stand:** Nur eine Zeile ändert sich.

| Kennzahl | vorher | nachher |
|---|---|---|
| Player level reached | 8,65 (8 bis 9) | 9,33 (7 bis 10) |

**Begründung:** Das liegt bei der Erwartung des Entwurfs (im Schnitt etwa Stufe 9, 7 bis 10). Bei einem Skillpunkt je drei Stufen bleiben es am Ende der Katakomben 2 oder 3 Punkte. Die Streuung ist größer als vorher, weil jede Stufe bis 10 gleich viel kostet: Mehr Erfahrung in einer Ebene bremst nicht mehr durch steigende Kosten. Der erste Aufstieg kommt nach 270 statt 100 Erfahrung (etwa 10 statt 4 Kills), der erste Skillpunkt nach 810 statt 381 (etwa 29 statt 14 Kills). Die Schwellen bei 10 und 20 erreicht in den Katakomben kaum jemand; für spätere Ebenen sind sie neu abzugleichen (Entwurf: sonst 8 Punkte nach Ebene 3).

**Offen:** Ein Lauf im Spiel steht aus; `runs.log` enthält nur Läufe mit der alten Kurve.

### Runde 4 – 24.11.2026 (QC-Runde vor dem Spieltest zu 0.12.0, ohne Wertänderung)

**Anlass:** QC-Runde (#690) vor dem Spieltest zu 0.12.0, Stand `main` 47862b7.

**Werte:** In dieser Runde **nichts geändert**, `assets/balance.json` bleibt, wie es ist. Die Bot-Läufe geben keinen Anlass dazu.

**Bot-Läufe (Tester):** Alle sieben BotRun-Modi mit den Seeds 1 bis 6 ohne Befund; `stuck` zusätzlich mit den Seeds 7 bis 40 ohne Befund (2632 Ghule beobachtet, 375 Mal „pressed against another enemy“, 0 Befunde). Keine Abbrüche, Ausnahmen oder Exit-Codes ungleich 0.

**Report (500 Ebenen), Tag `v0.11.1` gegen `main`:** Der Vergleich nur mit getauschter `balance.json` ist zeilengleich zu `main` und belegt nichts: Der Report liest einen Teil der geänderten Werte nicht, und neue Schlüssel fehlen in der alten Datei und fallen auf `Balance.Default` zurück. Der echte Vergleich ist daher Code und Datei von `v0.11.1` gegen `main`:

| Kennzahl | v0.11.1 | main | Veränderung |
|---|---|---|---|
| Kampfräume | 13,01 | 13,0 | gleich |
| Gänge | 9,89 | 10,24 | +0,35 |
| Abstand zum Boss (Räume) | 11,5 (7..21) | 12,67 (8..22) | +1,2 |
| Bodenzellen | 1548 | 1585 | +37 |
| Gegner | 96,86 | 96,78 | gleich |
| Ghule / Fledermäuse | 73,33 / 23,53 | 73,27 / 23,51 | gleich |
| Gegner-Leben (mit Gorm) | 11 875 | 11 867 | gleich |
| Erfahrung / Stufe | 2671 / 8,65 | 2668 / 8,65 | gleich |
| Silber gesamt | 324 | 323 | gleich |
| Kisten | 7,18 | 7,18 | gleich |
| Ausrüstung gesamt | 4,76 | 4,76 | gleich |
| Heilung (erwartet) | 286 (2,86-mal Maximum) | 285 (2,85-mal) | gleich |
| Zeit grob: Gehen | 8,6 min | 8,8 min | +0,2 |
| Zeit grob: gesamt | 10,58 min | 10,78 min | +0,2 |

Beute, Gegnerzahl und Leben sind unverändert; die Ebenen sind etwas größer (längerer Weg zum Boss, grob +0,2 min). Als Ursache **vermutet** der Tester Säulen im Bossraum, die Bosstür über zwei Zellen und das Platzieren; der Report erklärt es nicht, belegt ist es nicht.

**Offen und vorläufig:**
- **Ghul-Schwelle in `stuck` ist knapp:** Längster Stillstand eines jagenden Ghuls ohne Angriff, Schwelle 192 Ticks: Seed 11 mit 188, Seed 27 mit 186, Seed 34 mit 184, Seed 40 mit 175, Seed 20 mit 144, alle anderen unter 61. Ein Hinweis, kein Fehler. Wer `WindUpTime`, `RecoverTime` oder `RepathTime` der Ghule ändert, lässt `stuck` mit den Seeds 1 bis 40 neu laufen und nimmt Seed 11, 27 und 34 zuerst.
- **Kein Bot-Modus kämpft einen ganzen Lauf:** `boss` trägt den Spieler zu Gorm und setzt ihn mit einem Schadenstreffer um; das ist ein Pfad-Test. Aussagen zu Gorms Kampf, zum Tod oder zur Heilung sind per Bot nicht möglich. Gorm im `slam` wird nur stehend geprüft (432 Plätze übersprungen).
- **`runs.log`** enthält nur Läufe bis 20.11. (55 Einträge, 24 gewonnen, 31 verloren), also vor Raumkegel, Säulen, Bosstür und Fallgittern; sie beschreiben nicht den Stand von `main`. Die gewonnenen Läufe (etwa 8:40 bis 11:08) passen zur Schätzung von 10,8 min.
- **Urteil des Menschen** aus dem Spieltest zu 0.12.0 (#691) steht aus, auch ob der längere Weg zum Boss gewollt ist.

### Zwischenschritt – 24.11.2026 (#635, ohne Wertänderung)

**Anlass:** Issue #635: Der Abstand, in dem Gorms Blutfläschchen vor ihm fällt, stand fest im Code und steht jetzt in `balance.json`. Der Wert bleibt gleich; nachgetragen mit #642.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `gorm.vialDistance` | 1,2 m (im Code) | 1,2 m | So weit vor ihm fällt das Fläschchen bei seinem Tod. Steht dort Stein, eine Säule, eine Wand oder die Treppe im Weg, wird der Platz um ihn herum gedreht, bis er frei ist. |

### Zwischenschritt – 20.11.2026 (#490, vorläufig)

**Anlass:** Issue #490: Gegner standen seitlich neben der Tür und waren beim Eintreten nicht zu sehen. Urteil des Menschen: Nach vorne reicht der bisherige Türabstand, zu den Seiten nicht.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `difficulty.doorConeAngle` | – (Kreis um die Tür) | 30° (**vorläufig**) | Halber Winkel des Kegels geradeaus in den Raum. Darin sieht man einen Gegner beim Eintreten sofort; dort reicht `doorDistance` (4 m). 45° ließe die Zelle zwei Reihen hinein und zwei zur Seite (5,7 m) zu. |
| `difficulty.sideDoorDistance` | – | 10 m (**vorläufig**) | Abstand außerhalb des Kegels. Der Richtwert 8 m ließ die Zelle zwei Reihen hinein und vier zur Seite zu (8,9 m, 63°); sie liegt außerhalb des Sichtfelds (gut 51° halbe Breite). 10 m schließt sie aus, das Budget wird weiter voll ausgegeben. |

**Messung (250 Seeds, 3254 Kampfräume, 24278 Gegner, Ghule und Fledermäuse; Abstand und Winkel zur nächsten Tür des eigenen Raums):**

Vorher (Kreis von 4 m um jede Tür):

| Abstand \ Winkel | 0–30° | 30–45° | 45–60° | 60–90° | > 90° |
|---|---|---|---|---|---|
| 4–6 m | 2029 | 0 | 697 | 938 | 0 |
| 6–8 m | 1789 | 463 | 748 | 780 | 0 |
| 8–10 m | 2244 | 0 | 365 | 899 | 0 |
| 10–12 m | 2245 | 730 | 493 | 466 | 0 |
| ≥ 12 m | 5778 | 1302 | 1198 | 1114 | 0 |

Seitlich (> 30°) und näher als 8 m: 3626 Gegner = 14,9 %.

Nachher (30°, 10 m):

| Abstand \ Winkel | 0–30° | 30–45° | 45–60° | 60–90° | > 90° |
|---|---|---|---|---|---|
| 4–6 m | 2687 | 0 | 0 | 0 | 0 |
| 6–8 m | 2384 | 0 | 0 | 0 | 0 |
| 8–10 m | 3033 | 0 | 0 | 0 | 0 |
| 10–12 m | 2822 | 963 | 683 | 782 | 0 |
| ≥ 12 m | 6721 | 1486 | 1387 | 1330 | 0 |

Seitlich und näher als 10 m: 0 %. Kein Kampfraum braucht die Notlösung (Gegner auf der am wenigsten schlechten Zelle), das Budget wird in allen Räumen voll ausgegeben, die Zahl der Gegner bleibt gleich.

**Offen:** Bot-Durchläufe des Testers und Spieltest.

### Zwischenschritt – 20.11.2026 (#437, vorläufig)

**Anlass:** Issue #437: Wer lange vor Gorm wegläuft, soll nicht für immer sicher sein. Urteil des Menschen: Vorhalt mit Einrasten (Variante B) und Wurf nach langer Verfolgung mit Warnung (Variante B).

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `gorm.hookLead` | – (zielt auf die Füße) | 1 (**vorläufig**) | Er zielt dorthin, wo das Ziel bei gleichem Tempo ankommt, wenn der Haken es erreicht. |
| `gorm.hookLeadMax` | – | 6 m (**vorläufig**) | Weiter voraus zielt er nie; bei 25 m Abstand läuft ein Spieler sonst über 9 m weit. |
| `gorm.hookLockTime` | – | 0,2 s (**vorläufig**) | 12 Ticks vor dem Wurf rastet das Ziel ein, Gorm erstarrt. Wer dann stehen bleibt oder ausweicht, entkommt. |
| `gorm.chaseThrowTime` | – | 8 s (**vorläufig**) | So lange darf man ihm ohne Angriff davonlaufen, dann wirft er doch – auch direkt nach einem Wurf. |
| `gorm.chaseWarnTime` | – | 2 s (**vorläufig**) | Die letzten 2 s davon schwingt der Haken sichtbar. |

**Bot-Kampf (Zufalls-Bot aus `PlayerTickTraceTests`, 60 Seeds × 4000 Ticks):** Hakentreffer 56 → 65, Seeds mit mindestens einem Treffer 39 → 45. Der Zufalls-Bot wechselt oft das Tempo und entkommt dem Vorhalt manchmal, wird aber im Mittel öfter getroffen.

### Zwischenschritt – 19.11.2026 (#430, vorläufig)

**Anlass:** Issue #430: Gorms Kettenschwung soll weiter reichen, damit ein einzelner Schritt zurück nicht reicht.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `gorm.swingReach` | 8 m | 12 m (**vorläufig** bis zum nächsten Spieltest) | Gorm holt bei 8 m Abstand (`nearRange`) aus. Ein Schritt zurück (6 m/s, 3 m je Schritt) plus Auslaufen bringt den Spieler auf 11,3 m; 12 m trifft ihn dort noch. Wer während der ganzen 0,9 s bis zum Durchzug weiterläuft, kommt auf gut 13 m und entkommt. 12 m ist auch der Radius der Hammer-Druckwelle; die Halle misst 28 × 28 m. |

Die Darstellung liest die Reichweite aus `Gorm.SwingArea`; Kette und Wirbel werden ohne Codeänderung länger.

**Bot-Kampf (50 Seeds × 60 s gegen Gorm, Treffer je Schwung, 8 m → 12 m):** Stehenbleiben 100 % → 100 %; ein Schritt zurück 62,5 % → 100 % (erster Schwung 0/50 → 50/50); Weglaufen 89,5 % → 89,5 % (erster Schwung 0/50 → 0/50, spätere Treffer kommen nach Hakenzügen aus der Nähe); Ausweichen beim Durchzug 0 % → 0 %.

**Report (500 Ebenen):** zeilengleich mit vorher. Der Report rechnet die Kampfzeit aus Schaden und Abklingzeit, nicht aus der Reichweite.

### Zwischenschritt – 19.11.2026 (#425, vorläufig)

**Anlass:** Spieltest des Menschen zu 0.11.0 (19.11.): „Die Silberklinge trifft zu kurz.“

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `blade.range` | 2 m | 2,5 m (**vorläufig**, Wert der Leitung) | ein Viertel mehr Reichweite; die Keule bleibt bei 2 m |

Dieselbe Reichweite gilt für jeden Hieb, für den aufgeladenen Schlag und für das Aufbrechen rissiger Wände. Der Schwung in der Ich-Sicht hängt nicht von der Reichweite ab (er bleibt innerhalb von 30 cm vor den Augen, damit er nicht in Wände ragt).

**Report (500 Ebenen):** zeilengleich mit vorher. Der Report rechnet die Kampfzeit aus Schaden und Abklingzeit, nicht aus der Reichweite.

### Runde 3 – 19.11.2026 (vor 0.11.0, Kurzrunde)

**Anlass:** Release 0.11.0. Seit Runde 2 kamen neue Waffen (Keule, Gewehr, Donnerbüchse), das Nachladen der Pistole und die Grenze für Fledermäuse dazu (siehe Zwischenschritt unten).

**Werte:** In dieser Runde **nichts geändert**. Die Werte der neuen Waffen sind die aus ihren Pull Requests und noch nicht am Spiel geprüft – sie gelten als **vorläufig**.

**Report (500 Ebenen, Durchschnitt):** Silber 324 (97 von Gegnern, 227 aus Kisten), Schlüssel je Schloss 1,24, Kisten 7,2, Ausrüstung 4,8 Stück je Lauf (3,8 aus Kisten, 1 von Gorm), Heilung 286 Leben (2,86-mal das volle Leben), Zeit 10,6 min (8,6 Laufen, 2,0 Kämpfen).

**Durchlauf:** Der Spieltest des Menschen zu 0.11.0 steht noch aus; sein Urteil und die `runs.log` gehen in die nächste Runde ein. Bekannt und offen: Die Ausdauer füllt sich nicht, solange ein Nahkampfschlag aufgeladen wird (#413).

### Zwischenschritt – 19.11.2026 (#391, vorläufig)

**Anlass:** Spieltest vom 18.11.: In einem Raum waren 5 oder 6 Fledermäuse – „ohne genug Schaden für One-Shots nicht machbar“.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `difficulty.maxBatsPerRoom` | – (keine Grenze) | 3 (**vorläufig**, Wert der Leitung) | nie mehr als drei Fledermäuse in einem Raum; das übrige Budget kauft Ghule |

Die Grenze wirkt in der Raumerzeugung: Hat ein Raum so viele Fledermäuse wie erlaubt, kauft sein Budget nur noch Ghule. Der Zufall bleibt beim Seed der Ebene.

**Report (500 Ebenen, Durchschnitt, mit Grenze gegen ohne):** Fledermäuse 23,5 statt 26,5, Ghule 73,3 statt 69,2, Gegner 96,9 statt 95,7, Erfahrung 2671 statt 2606, Kampfzeit 1,98 statt 1,93 min.

**Zählung der Stufe:** Seit #391 beginnt ein Lauf auf Stufe 0 statt 1. Die Erfahrung je Stufe bleibt gleich (`experience.firstLevel` ist jetzt der Weg von Stufe 0 auf 1). Die Zeile „Player level reached“ im Report liegt deshalb um genau 1 tiefer als in den Runden unten.

### Runde 2 – 16.11.2026 (vor 0.10.0)

**Anlass:** Mit Gorm (v0.10) zwei Siege, Gorm nach 44 und 47 s besiegt. Eindruck: Gorm „viel zu leicht“, Phase 2 „zu harmlos“, der Weg „zu leicht, Länge passt“, Heilung „ab und zu knapp“, also richtig.

| Wert | vorher | nachher | warum |
|---|---|---|---|
| `gorm.maxHealth` | 1600 | 3600 | Kampf von 45 s Richtung 1,5–2 min |
| `gorm.pauseTime` | 1,5 s | 1,2 s | weniger freie Zeit zum Zuschlagen |
| `gorm.hammerDamage` / `swingDamage` / `throwDamage` | 35 / 25 / 15 | 40 / 30 / 20 | Fehler kosten mehr |
| `gorm.phase2SpeedUp` | 1,3 | 1,4 | Phase 2 spürbar schneller |
| `gorm.stoneCount` / `stoneSpread` / `stoneDamage` | 4 / 5 m / 20 | 6 / 4 m / 25 | Steine dichter und gefährlicher |
| `ghoul.speed` / `attackDamage` / `recoverTime` | 4,5 / 25 / 0,8 s | 5 / 30 / 0,6 s | gefährlicher, nicht zahlreicher |
| `bat.biteDamage` / `spitInterval`, `bloodBolt.damage` | 15 / 2 s, 15 | 20 / 1,6 s, 20 | dito |
| `difficulty.budgetFar` | 20 | 24 | hinten etwas mehr Gegner |

**Report (500 Ebenen, Durchschnitt):**

| Kennzahl | vorher | nachher |
|---|---|---|
| Gegner | 84 | 96 |
| Gegner-HP (mit Gorm) | 8661 | 11580 |
| erreichte Stufe | 9,0 | 9,5 |
| Silber | 311 | 323 |
| Schlüssel pro Schloss | 1,14 | 1,23 |
| Heilung (× maximales Leben) | 2,6 | 2,8 |
| Zeit (grobe Schätzung) | 10,0 min | 10,5 min |

**Unterwegs geändert, weil das Anspielen es zeigte** (eigene PRs):
- Die Kette zeigt ihre echte Reichweite (#177).
- Der Kettenschwung streicht jetzt als sichtbarer Bogen von links nach rechts statt überall gleichzeitig zu treffen (#181/#182).
- Die Karte hat eigene Symbole und bleibt beim Laufen offen (#178/#179/#180).

**Gespielt (16.11., drei Durchläufe mit allem zusammen):**
- 5:56 verloren (11/15 Räume);
- 3:47 verloren (6/15 Räume);
- 11:55 gewonnen (15/15 Räume, Stufe 10, 290 Schaden genommen, **Gorm in 1:22**).

Eindruck: Weg „hart, aber fair“, Gorm „passt“. Die Werte bleiben so.

**Offene Fragen aus v0.9 und #165 geklärt:**
- **Kisten sollen blockieren:** ja → eigenes Issue.
- **Wandbruch mit Effekt (Staub, Brocken):** ja → eigenes Issue.
- **Fern-Anteile der Kampfraum-Kisten (55/37/8/0):** passen.
- **Schutzräume:** nicht zu sicher.
- **Risse am Geheimraum:** dezent genug.
- **Abstand nach dem Kettenzug (3 m):** bleibt.

**Noch offen:** Ein gewonnener Durchlauf mit allen Räumen dauert jetzt 12 Minuten, die 20 Minuten aus ADR 0005 sind noch nicht erreicht. Weitere Ebenen kommen mit späteren Versionen.

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

**Gespielt (15.11., drei Durchläufe):**
- 4:38 verloren (6/15 Räume);
- 5:21 verloren (8/14 Räume);
- 4:26 gewonnen (9/14 Räume, Stufe 8, 90 Schaden genommen).

Eindruck: „passt“. Man kann Räume überspringen; der Sieg kam schon nach 9 von 14 Räumen, deshalb liegt die gespielte Zeit unter der Schätzung des Reports, die von *allen* Räumen ausgeht.

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
| Tabelle `experience.levelCosts` leer, beginnt nicht bei Stufe 0, steigt nicht von Zeile zu Zeile oder hat eine Zeile mit Kosten 0 oder weniger | die ganze Standardtabelle |

## Standardwerte dauerhaft ändern

Die Standardwerte stehen an **zwei** Stellen: in `Balance.cs` (gilt, wenn ein Wert in der Datei fehlt, und für die Tests) und in `assets/balance.json`. Ein Test prüft, dass beide gleich sind – ändert man nur eine, wird er rot. Also immer beide zusammen ändern.
