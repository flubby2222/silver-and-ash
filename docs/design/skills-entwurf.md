# Entwurf: Skills, Statuseffekte und Altar (#104)

- **Stand:** 4. Fassung, 2026-11-20, nach Runde 5 der Urteile des Menschen in [#104](https://github.com/flubby2222/silver-and-ash/issues/104): Erfahrungskurve B gewählt, Schnitt in Issues angenommen. Noch nichts davon ist gebaut; die Issues stehen im Meilenstein „v0.13 – Skills“.
- **Grundlagen:** [ADR 0005](../entscheidungen/0005-ebenen-raeume-durchlauf.md) (jede 3. Stufe ein Skillpunkt, Altarraum, Fenster halten nie an), [ADR 0003](../entscheidungen/0003-online-coop-offenhalten.md) (Coop offen), [ADR 0006](../entscheidungen/0006-kisten-beute-rarity.md) und [0008](../entscheidungen/0008-ausruestung-haendler-waffenkammer.md) (Seltenheit), ADR 0011 (PR #481, noch offen: Skills gehören in 1.0).
- **Alle Zahlen sind vorläufige Startwerte** in `assets/balance.json` (neue Abschnitte `skills` und `statusEffects`), angepasst in der Balancing-Runde.
- **Skillpunkte:** Es bleibt bei 1 Skillpunkt alle 3 Stufen. Die Katakomben geben nicht mehr Punkte als heute (2 bis 3). Ausbau II und III bleiben im Entwurf und werden mit späteren Ebenen erreichbar (Label `später`).

## Zusammenfassung

1. **Ein Skillpunkt alle 3 Stufen.** Ein voller Lauf durch die Katakomben (Ebene 0) bringt heute **2 oder 3 Punkte**, wer Räume auslässt **2**.
2. **Punkte zahlen alles:** 1. Punkt = aktiver Skill, 2. Punkt = sein Statuseffekt, jeder weitere = eine Ausbaustufe. **Fünf Punkte** bauen einen Skill ganz aus; mehr haben keine Verwendung.
3. **Ein aktiver Skill je Lauf** (Taste **Q**). Der erste Altarbesuch bietet **drei zufällige** von fünf Skills, der zweite **drei zufällige** von fünf Statuseffekten.
4. **Fünf Statuseffekte:** Leech, Stun, Holy Fire (Weihfeuer), Grave Chill (Grabeskälte), Marked (Gezeichnet). Startwerte halb so stark wie zuerst vorgeschlagen.
5. **Waffen:** **30 %** der gefundenen Waffen tragen einen Effekt. Er löst je Treffer mit **10 bis 25 %** aus, je nach Seltenheit.
6. **Ausbau** ab dem dritten Punkt in **fester Reihenfolge je Skill** (I, II, III).
7. **Synergie:** Tragen Waffe und Skill denselben Effekt, hält er **1,5-mal so lange** (Leech: 1,5-mal so stark).
8. **Erneut ausgelöst** frischt die Dauer auf, stärker wird nichts. Verschiedene Effekte liegen gleichzeitig.
9. **Bosse taumeln** bei Stun nur kurz und sind danach einige Sekunden immun.
10. **Am Gegner:** farbiger Schimmer, ein Effekt passend zum Status, darüber die Restzeit als kleine Zahl. Kein Rot.
11. **Altar** im eigenen Raum mit eigenem Teleporter. Kein Skill erhöht das maximale Leben.
12. **Erfahrungskurve B, schon in 1.0:** jede Stufe kostet gleich viel (270), ab Stufe 10 das Doppelte, ab Stufe 20 das Vierfache (Abschnitt 7). Der erste Skillpunkt kommt nach etwa 29 statt 14 Kills.

## 1. Ablauf eines Laufs

**Erfahrung heute** (`assets/balance.json`, `experience`; Rechnung in `Player.ExperienceToNextLevel`, `src/SilverAndAsh.Core/Gameplay/Player.cs`): Stufe 0 → 1 kostet 100, jede weitere Stufe 1,25-mal so viel (gerundet). Ein Ghul gibt 30, eine Fledermaus 20; **Gorm gibt keine Erfahrung** (`GameWorld.Damage`, `src/SilverAndAsh.Core/Gameplay/GameWorld.cs`, nur Ghul und Fledermaus). Erfahrung bekommt nur, wer den tödlichen Treffer setzt. Tod setzt die Stufe auf 0 zurück.

| Stufe | 1 | 2 | **3** | 4 | 5 | **6** | 7 | 8 | **9** | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Erfahrung gesamt | 100 | 225 | **381** | 576 | 820 | **1125** | 1506 | 1983 | **2579** | 3324 |

**Balance-Report** (Stand `main` vom 20.11., 500 Ebenen, alle Räume geräumt): 96,9 Gegner (73,3 Ghule, 23,5 Fledermäuse), **2671 Erfahrung** (kleinste Ebene 2140, größte 3230), also **27,6 je Kill**. „Player level reached“: im Schnitt **8,65**, nie unter 8, nie über 9.

| Punkt | bei Stufe | nach etwa | im Lauf (13 Kampfräume, rund 7,5 Gegner je Raum) | wofür |
|---|---|---|---|---|
| 1 | 3 | 14 Kills | Kampfraum 2 | aktiver Skill |
| 2 | 6 | 41 Kills | Kampfraum 5 bis 6 | Statuseffekt |
| 3 | 9 | 94 Kills | erst fast am Ende, vor dem Bosstor – und nur in etwa zwei von drei Ebenen | Ausbaustufe I |

- **Mit Kurve B** (entschieden, Abschnitt 7): Punkt 1 nach etwa 29 Kills (Kampfraum 4), Punkt 2 nach 59 (Kampfraum 8), Punkt 3 nach 88 (kurz vor dem Bosstor). Voller Lauf Stufe 7 bis 10, im Schnitt 9, also weiter 2 oder 3 Punkte; mit ausgelassenen Räumen Stufe 6, 2 Punkte. Der Altar auf einem Drittel des Wegs passt weiter.
- **Voller Lauf heute: 2 oder 3 Punkte** (Stufe 8 oder 9); der dritte kommt erst kurz vor Gorm und fehlt bei Ebenen mit wenig Gegnern. Erfahrungs-Boni (+2,5 % je Wahl) ziehen das nur wenig vor.
- **Lauf mit ausgelassenen Räumen** (Sieg am 15.11. nach 9 von 14 Räumen): etwa 60 Kills, rund 1650 Erfahrung, Stufe 7, **2 Punkte** – Skill und Effekt, kein Ausbau.
- Der Altar steht deshalb **auf etwa einem Drittel des Wegs** (Punkt 1 kommt früh), sein **Teleporter** macht Punkt 3 einlösbar: vom Bosstor zum Altar und zurück.
- **Ausbaustufen II und III** sind in den Katakomben nicht erreichbar. Sie bleiben im Entwurf und kommen mit den späteren Ebenen (Abschnitt 7).

## 2. Altar

- **Raum:** eigene Raumart `Altar` (ADR 0005): ein Eingang, ohne Schlüssel erreichbar, gegnerfrei, nie hinter verschlossener Tür, kein Brunnen darin. Genau einer je Ebene, etwa auf einem Drittel des Wegs zum Boss.
- **Teleporter:** dritter Ort neben Start und Bosstor (`TeleporterSite` in `src/SilverAndAsh.Core/World/TeleporterPlan.cs` bekommt `Altar`, Platzierung in `TeleporterPlacement.cs`, Freischalten wie heute in `src/SilverAndAsh.Core/Gameplay/Teleporter.cs`).
- **Bedienen:** **F** öffnet das Fenster „Ash Altar“. Was es zeigt, hängt vom Stand des Spielers ab:

| Stand | Angebot | kostet |
|---|---|---|
| noch kein Skill | **drei zufällige** der fünf aktiven Skills | 1 Punkt |
| Skill, kein Effekt | **drei zufällige** der fünf Statuseffekte | 1 Punkt |
| Skill und Effekt | die nächste Ausbaustufe, **feste Reihenfolge je Skill** (I, dann II, dann III) | 1 Punkt |
| ganz ausgebaut | nichts mehr; Punkte bleiben liegen | – |

- **Zufall** für beide Angebote aus einem eigenen Generator, gestartet aus dem Seed der Welt: gleicher Seed, gleiches Angebot; keine anderen Würfe verschieben sich.
- Ohne Punkt ist alles grau, lesbar bleibt es. Bewegen oder ein Treffer schließt das Fenster; die Welt läuft weiter.
- Ein Angebot bleibt bis zur Wahl gleich (erneutes Öffnen würfelt nicht neu).
- **Coop:** Jeder Spieler hat eigene Punkte, eigenen Skill, eigene Angebote; der Altar verbraucht sich nicht.
- **Aussehen:** hüfthoher, verwitterter Steinblock mit Silberschale und zwei Kerzen; die Kerzen brennen (Messing), solange ein Punkt offen ist. Kartensymbol Kerze. Name „Ash Altar“ (vorläufig).
- **Kein HP-Ausbau:** Kein Skill, Effekt oder Ausbau hebt das maximale Leben. Der Deckel bleibt `boons.healthCap` (235); am Deckel kommen wie heute keine Leben-Boni mehr (`Player.cs`, Bonus-Angebot). Leech heilt nur bis zum Maximum.

## 3. Aktive Skills

Gemeinsam: Taste **Q** (neues Feld `UseSkill` in `PlayerInput`), Werte unter `skills.<name>`, Spieltexte englisch. **Der Statuseffekt des Skills löst bei jedem Treffer des Skills sicher aus** (keine Chance); bei Bloodlust bei jedem eigenen Treffer während des Rauschs. Die Ausbaustufen stehen je Skill in fester Reihenfolge.

| Skill | Wirkung (vorläufig) | Effekt geht auf | Ausbau I | Ausbau II | Ausbau III |
|---|---|---|---|---|---|
| **Bloodlust** | Nahkampftreffer füllen eine Leiste (8 Treffer). Volle Leiste, Q: 6 s lang Fernwaffen 1,5-mal so schnell, 20 % schneller laufen. | jeden Treffer im Rausch | Rausch 8 s | auch Kopftreffer füllen die Leiste | Kills im Rausch heilen 5 |
| **Holy Water** | Q wirft eine Phiole; eine Pfütze (Radius 3 m, 4 s) brennt Untote mit 10 Schaden/s. Abklingzeit 12 s. | jeden Gegner in der Pfütze (aufgefrischt) | Radius 3,5 m | Abklingzeit 8 s | zwei Ladungen |
| **Ash Nova** | Q: Aschering um den Spieler, 4 m, 30 Schaden, stößt 2 m zurück. Abklingzeit 10 s. | jeden Getroffenen | Radius 5 m | 45 Schaden | zweiter Ring 1 s später |
| **Phantom Step** | Q: 6 m in Blickrichtung durch Gegner hindurch, dabei unverwundbar, je durchquertem Gegner 25 Schaden. Ohne Ausdauer. Abklingzeit 8 s. | jeden Durchquerten | Abklingzeit 6 s | zwei Ladungen | nächster Nahkampftreffer danach +30 % |
| **Silver Volley** | Q: Fächer aus 5 Silbernägeln, je 15 Schaden, 15 m. Abklingzeit 9 s. | jeden getroffenen Gegner (einmal je Wurf) | 7 Nägel | Nägel durchschlagen einen Gegner | Abklingzeit 6 s |

**Aus der 1. Fassung übernommen:** Bloodlust und Holy Water samt Werten; Frenzied Blood und Crimson Feast sind jetzt Ausbau II und III von Bloodlust, Executioner steckt in Phantom Step III, die Rückstoß-Idee aus Chain Breaker in Ash Nova. Die passiven Skills (Second Wind, Grave Tithe …) entfallen: Ein Lauf trägt nur den einen aktiven Skill.

**Anbindung im Code:** Bloodlust nach dem Muster der Geweihten Munition (`_consecratedTicksLeft` in `Player.cs`, Feuerrate über den Cooldown der Fernwaffe); Phantom Step über die vorhandene Dash-Bewegung (`player.dashDistance`, Wände wie bei `MoveThroughLevel`); Silver Volley über Schrot-Logik wie die Blunderbuss (`pellets`, `spread`, `pierce`); Holy Water als neues Objekt in `GameWorld` (Position, Radius, Restzeit in Ticks); alle Treffer laufen durch `GameWorld.Damage`, damit Kill und Erfahrung beim richtigen Spieler landen.

## 4. Statuseffekte

Startwerte = etwa die Hälfte des ersten Vorschlags (5 %, 1 s, 4 s, 40 %/4 s, 25 %/6 s). **Synergie** (Skill und eine ausgerüstete Waffe tragen denselben Effekt): jede Auslösung durch diesen Spieler hält 1,5-mal so lange (`statusEffects.synergyFactor`); Leech hat keine Dauer und wird 1,5-mal so stark.

| Effekt (im Spiel) | Wirkung | Startwert | mit Synergie | gegen Gorm | Schimmer und Effekt | Timer |
|---|---|---|---|---|---|---|
| **Leech** (Lifedrain) | Anteil des Schadens als Leben für den Angreifer | 2,5 % je Treffer | 3,75 % | voll | fahles Violett, dünner Faden zum Spieler | keiner (sofort) |
| **Stun** | Gegner betäubt: kein Gehen, kein Angriff; ein ausholender Ghul bricht ab | 0,5 s | 0,75 s | **Taumeln 0,2 s**, bricht keine Ankündigung ab, danach **5 s immun** | Silbergrau, kreisende Funken über dem Kopf | ja |
| **Holy Fire** (Weihfeuer) | Schaden über Zeit, alle 0,5 s | 2 s, 5 Schaden/s | 3 s | voll | weißgold, kleine Flammen am Körper | ja |
| **Grave Chill** (Grabeskälte) | langsamer (nur Bewegung, Ankündigungen bleiben gleich lang) | 20 % für 2 s | 3 s | voll | blassblau, Reif und kalter Hauch | ja |
| **Marked** (Gezeichnet) | Gegner nimmt mehr Schaden, von allen Spielern | 12,5 % für 3 s | 4,5 s | voll | Aschweiß, ein Zeichen auf der Brust | ja |

- **Auffrischen:** Neue Dauer = die längere von Rest und neuer Dauer; die Stärke bleibt. Verschiedene Effekte liegen nebeneinander.
- **Timer, wörtlich gewünscht** („Überm Kopf ein kleiner Timer“): Restzeit je Effekt als kleine Zahl in seiner Farbe, Spielschrift englisch, also „0.4s“. Mehrere Effekte stehen untereinander, höchstens drei.
- **Rot bleibt frei** für die Angriffs-Ankündigung (Ghul-Augen, Art-Bible). Farben kommen aus der Hauptpalette; Weihfeuer aus der Reihe Messing/Gold, aber klar weg vom Gold der Stufe *Royal*.
- **Kills durch Holy Fire** zählen für den Spieler, der es ausgelöst hat.

## 5. Waffen mit Statuseffekt

- **Beim Fund** (Kiste, Gorm, Händler) würfelt die Waffe, ob sie einen Effekt trägt: **30 %**, für alle Seltenheiten gleich (`statusEffects.weaponShare`). Welcher Effekt, ist gleich verteilt. Die Startwaffen (Silver Blade, Pistol) tragen keinen. Der Anteil ist bewusst niedrig, weil später weitere Ebenen mehr Waffen bringen.
- **Auslöse-Chance je Treffer** nach Seltenheit (`ItemTier` in `src/SilverAndAsh.Core/Gameplay/Weapons.cs`):

| Seltenheit | Worn | Forged | Royal | Vampiric |
|---|---|---|---|---|
| Chance je Treffer | 10 % | 15 % | 20 % | 25 % |

- Heute sind alle Waffen Worn oder Forged (`Weapons.Info`); Royal und Vampiric gelten für spätere Waffen.
- **Blunderbuss:** eine Auslösung je Schuss und Gegner, nicht je Kugel (sonst neunfache Chance).
- **Zufall** aus einem eigenen Generator, gestartet aus dem Seed der Welt (wie `LootPool`, `_lootRandom`, `_shotRandom` in `GameWorld`): gleicher Seed, gleiche Würfe; keine anderen Würfe verschieben sich.
- Inventar und Fundmeldung zeigen den Effekt (z. B. „Club – Grave Chill“).

## 6. Architektur

- **Core ohne Raylib:** Effekte, Skills und Altar liegen ganz in `SilverAndAsh.Core`.
- **Effekte als Zustand am Gegner:** eine kleine Klasse (etwa `StatusEffects`) je Gegner mit Restzeit in Ticks je Art, Quelle (Spieler-Id) und Synergie-Merker; `GameWorld.Tick` zählt sie herunter, als eigene Methode, nicht als weiterer Zweig in `Tick`.
- **Wirkung über vorhandene Wege:** Marked als Schadensregel nach `IDamageRule` (`src/SilverAndAsh.Core/Gameplay/Damage.cs`; `Hit` braucht dazu den Zustand des Ziels); Holy Fire als Schaden durch `GameWorld.Damage`; Grave Chill als Tempo-Faktor und Stun als Pause in `Ghoul.cs`, `Bat.cs`, `Gorm.cs`; Leech über `Player.Heal` mit gesammelten Bruchteilen (Leben ist ganzzahlig).
- **Ereignisse für Klang und Anzeige:** `StatusApplied`, `StatusEnded`, `SkillUsed`, `SkillPointGained`, `AltarChoice` in `GameEvent.cs`; Klänge als Tabelleneinträge in `SoundCues.cs`; die Anzeige liest einen Zustand aus Core (Muster `HudReadout`).
- **Online-Coop offen:** Q und die Altar-Wahl sind Eingaben im Tick; Punkte, Skill und Angebote je Spieler; Effekte merken ihren Auslöser; Zufall nur mit Seed im Spielzustand; kein veränderlicher `static`-Zustand.

## 7. Erfahrungskurve

**Erfahrungskurve** = was jede Stufe an Erfahrung kostet. Heute ist sie **exponentiell** (jede Stufe kostet einen festen Faktor mehr als die vorige). Der Mensch schlägt vor: gleiche Kosten je Stufe, ab **Schwellen** (englisch *Thresholds*, z. B. Stufe 20) höher. Jeder Stufenaufstieg bringt außerdem eine **Bonus-Wahl** (Boon, ADR 0008); die Kurve bestimmt also auch, wie viele Boni ein Lauf sammelt.

**Rechenweg** (wie `Player.AddExperience` und `BalanceReport.LevelReached`):

- Kosten heute: `kosten(n) = round(100 · 1,25^n)` für den Schritt von Stufe n auf n + 1.
- Erfahrung bis Stufe L: `summe(L) = kosten(0) + … + kosten(L − 1)`.
- Erreichte Stufe: das größte L mit `summe(L) ≤ Erfahrung`. Skillpunkte: `L / 3`, abgerundet.

**Annahmen** (nachgerechnet, nicht gemessen – außer Ebene 1):

| | Ebene 1 Katakomben | Ebene 2 Kerker | Ebene 3 Schlosshalle | Ebene 4 (nur Annahme) |
|---|---|---|---|---|
| Erfahrung der Ebene | **2671** (Report) | 4006 | 6010 | 9015 |
| Erfahrung gesamt | 2671 | 6678 | 12687 | 21702 |

- Jede weitere Ebene bringt **1,5-mal so viel** Erfahrung wie die vorige: `erfahrung(n) = 2671 · 1,5^(n − 1)` (stärkere Gegner geben mehr, dazu etwas mehr Gegner). Ebenen hier ab 1 gezählt; im Code heißen die Katakomben Ebene 0.
- Alle Räume geräumt, ein Spieler allein, keine Erfahrungs-Boni. Im Coop teilen sich die Spieler die Kills und kommen je weniger weit.
- Ein Skill braucht **5 Punkte** bis ganz ausgebaut (Stufe 15); was darüber liegt, hat keine Verwendung.

**Drei Varianten** (Stufe / Skillpunkte am Ende der Ebene; in Klammern kleinste und größte Katakomben-Ebene des Reports):

| Variante | Kosten je Stufe | 1. Punkt nach | nach Ebene 1 | nach Ebene 2 | nach Ebene 3 | nach Ebene 4 | Boni bis Ebene 3 |
|---|---|---|---|---|---|---|---|
| **A – heute** | `round(100 · 1,25^n)`: 100, 125 … 745 (9 → 10), 931 … 2274 (14 → 15) | 381 Erf. ≈ **14 Kills** | **9 / 3** (8–9 / 2–3) | 12 / 4 | **15 / 5** | 17 / 5 | 15 |
| **B – gleich mit Schwellen** | 270 bis Stufe 10, ab Stufe 10 → 11: 540, ab 20 → 21: 1080 | 810 Erf. ≈ **29 Kills** | **9 / 3** (7–10 / 2–3) | 17 / 5 | 24 / 8 | 32 / 10 | 24 |
| **C – heute bis Stufe 10, dann gleich** | wie A bis 9 → 10 (745), ab 10 → 11: 1200, ab 20 → 21: 2400 | 381 Erf. ≈ **14 Kills** | **9 / 3** (8–9 / 2–3) | 12 / 4 | **17 / 5** | 22 / 7 | 17 |

**Gegenprobe der Annahme:** Punkte nach Ebene 3 bzw. 4, wenn jede Ebene 1,25-, 1,5- oder 2-mal so viel bringt wie die vorige.

| Variante | ×1,25 | ×1,5 | ×2 |
|---|---|---|---|
| A | 4 / 5 | 5 / 5 | 5 / 6 |
| B | 7 / 8 | 8 / 10 | 9 / 16 |
| C | 5 / 6 | 5 / 7 | 7 / 10 |

**Was die Zahlen zeigen:**

- **Die Katakomben bleiben in allen drei Varianten bei 2 bis 3 Punkten.** Die Varianten unterscheiden sich erst ab Ebene 2 – und die gibt es in 1.0 nicht.
- **A** bremst sich selbst: Mehr Erfahrung je Ebene wird durch die steigenden Kosten aufgefangen; Ebene 3 endet bei 4 bis 5 Punkten, ob ×1,25 oder ×2. Dafür bringt jede spätere Ebene nur 2 bis 3 Stufen, also nur 2 bis 3 Boni in 20 Minuten.
- **B** setzt die Idee wörtlich um, kostet aber zweimal: Der erste Punkt kommt doppelt so spät (29 statt 14 Kills), und nach Ebene 3 liegen 8 Punkte da, 3 davon ohne Verwendung, dazu 24 Boni. Das ist das schnelle Skalieren, das der Mensch vermeiden will.
- **C** behält den schnellen Anfang von A und macht ab Stufe 10 jeden Aufstieg gleich teuer, mit Schwelle bei 20. Ebene 3 endet bei 5 Punkten (ganz ausgebaut), jede spätere Ebene bringt 3 bis 5 Stufen statt 2 bis 3. Die Schwellen-Werte hängen aber stärker an der Annahme (×2 ergäbe 7 Punkte).

**Entscheidung des Menschen (20.11.2026, Runde 5): Variante B, schon für 1.0.** Jede Stufe kostet 270, ab Stufe 10 → 11 das Doppelte (540), ab Stufe 20 → 21 das Vierfache (1080). Die Werte stehen in `assets/balance.json` als Schwellen-Tabelle, nicht als Formel im Code; die Zahlen gleicht die Balancing-Runde des Issues „Erfahrungskurve B“ ab.

*Nicht gewählt:* Die Leitung hatte empfohlen, in 1.0 Variante A zu lassen und Variante C für den Kerker zu planen.

**Was sich für den Spieler ändert:** Der erste Skillpunkt kommt nach etwa 29 statt 14 Kills, am Ende der Katakomben hat er weiter 2 oder 3; jede Stufe kostet bis Stufe 10 gleich viel. Für spätere Ebenen sind die Schwellen neu abzugleichen, damit nicht mehr als 5 Punkte zusammenkommen (Gegenprobe oben: 8 nach Ebene 3).

## Noch offen

1. **Farben für Leech, Stun und Marked** stehen noch nicht in der Art-Bible. *Empfehlung:* Probenblatt des Grafikdesigners mit den Vorschlägen aus Abschnitt 4, Urteil beim nächsten Spieltest.

## 8. Schnitt in Issues

Je Zeile ein Issue und ein kleiner PR, Code-PRs nacheinander. Jedes Issue ist für sich spielbar und testbar: Zuerst tragen **Waffen** die Effekte, dann erst kommt der Altar. **Größe:** S bis etwa 200 geänderte Zeilen, M bis etwa 400, L bis etwa 800 (mit Tests). Für alle gilt: Logik in `Core` ohne Raylib, Werte in `balance.json` (mit Obergrenzen in `BalanceFile.cs`), Klang und Anzeige über Ereignisse aus `Core`, alles je Spieler („ein Spieler“), Zufall aus dem Seed.

**Code**

| # | Issue | Ziel (ein Satz) | hängt ab von | Größe |
|---|---|---|---|---|
| 1 | Skillpunkte zählen | Stufe 3, 6, 9 … gibt je Spieler einen Punkt (Ereignis `SkillPointGained`, Tod setzt zurück), das HUD zeigt die Zahl als Platzhalter. | – | S |
| 2 | Erfahrungskurve B | Stufenkosten als Schwellen-Tabelle in `balance.json` (270, ab Stufe 10: 540, ab 20: 1080), eine Rechnung für Spiel und Report, Balancing-Runde mit Vorher/Nachher. | – | M |
| 3 | Statuseffekte: Grundgerüst und Marked | Gegner tragen Effekte mit Restzeit in Ticks und Quelle; Marked erhöht den Schaden über eine `IDamageRule`; Ereignisse `StatusApplied`/`StatusEnded`. | – | M |
| 4 | Waffen mit Effekt | 30 % der gefundenen Waffen tragen einen Effekt aus der Liste der gebauten (anfangs nur Marked), Chance je Treffer nach Seltenheit, Anzeige in Inventar und Fundmeldung. | 3 | M |
| 5 | Anzeige am Gegner | Platzhalter-Schimmer in der Effektfarbe und Timer über dem Kopf, gelesen aus einem Zustand in `Core`. | 3 | M |
| 6 | Holy Fire | Schaden über Zeit durch `GameWorld.Damage`, der Kill zählt für den Auslöser; Eintrag in der Waffenliste. | 4 | S |
| 7 | Grave Chill | Ghul, Fledermaus und Gorm gehen langsamer, Ankündigungen bleiben gleich lang; Eintrag in der Waffenliste. | 4 | S |
| 8 | Stun | Gegner stehen still, ein ausholender Ghul bricht ab; Gorm taumelt nur kurz und ist danach immun. | 4 | M |
| 9 | Leech | Anteil des Schadens heilt den Angreifer, Bruchteile gesammelt, nie über das Maximum. | 4 | S |
| 10 | Altarraum im Generator | Raumart `Altar` mit den Regeln aus Abschnitt 2, einer je Ebene; Test über 100 Ebenen, gleicher Seed = gleicher Grundriss. | – | M |
| 11 | Teleporter am Altar | `TeleporterSite.Altar` wird platziert und freigeschaltet wie Start und Bosstor. | 10 | S |
| 12 | Altar bedienen und erster Skill | F öffnet „Ash Altar“, drei zufällige Skills aus den gebauten (anfangs nur Ash Nova), Q löst ihn aus; Fehlerzweige: kein Punkt, tot, Fenster schließt bei Bewegung. | 1, 11 | L |
| 13 | Effektwahl am Altar | Zweiter Punkt: drei zufällige Effekte, der gewählte löst bei jedem Skilltreffer sicher aus. | 12, 6–9 | M |
| 14 | Ausbau I | Dritter Punkt: nächste Ausbaustufe in fester Reihenfolge, zuerst Ash Nova I; „ganz ausgebaut“ als eigener Fehlerzweig. | 13 | S |
| 15 | Skill Bloodlust | Leiste, Rausch, Ausbau I; kommt ins Altar-Angebot. | 14 | M |
| 16 | Skill Holy Water | Phiole, Pfütze, Ausbau I; kommt ins Altar-Angebot. | 14 | M |
| 17 | Skill Phantom Step | Durchschreiten mit Schaden, Ausbau I; kommt ins Altar-Angebot. | 14 | M |
| 18 | Skill Silver Volley | Nagelfächer, Ausbau I; kommt ins Altar-Angebot. | 14 | M |
| 19 | Synergie | Gleicher Effekt an Waffe und Skill: Dauer ×1,5 (Leech: Stärke ×1,5). | 13 | S |
| 20 | Balance-Report und Bot | Report zeigt Stufe und Punkte je Ebene nach der Kurve aus `balance.json`, Bot wählt Skills und Effekte. | 2, 12 | M |

**Mit Label `später`** (kommen mit Ebene 1): Ausbau II und III aller Skills.

**Bild:** Altar-Probestück und Einbau; Probenblatt der fünf Effekte am Ghul (Schimmer, Flammen, Reif …, mit Timer; klärt die offenen Farben); HUD-Feld für Q mit Leiste bzw. Abklingzeit; je Skill ein kleines Probenblatt.

**Klang** (neue Kategorien, also Hörprobe vor dem Einbau): Altar und Skillpunkt; je Effekt ein Auslöse-Klang; je Skill ein Klang beim Auslösen.
