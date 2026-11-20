# Entwurf: Skills, Statuseffekte und Altar (#104)

- **Stand:** 2. Fassung, 2026-11-20, nach den Urteilen des Menschen in [#104](https://github.com/flubby2222/silver-and-ash/issues/104) (Kommentare vom 20.11.). Noch nichts davon ist gebaut.
- **Grundlagen:** [ADR 0005](../entscheidungen/0005-ebenen-raeume-durchlauf.md) (jede 3. Stufe ein Skillpunkt, Altarraum, Fenster halten nie an), [ADR 0003](../entscheidungen/0003-online-coop-offenhalten.md) (Coop offen), [ADR 0006](../entscheidungen/0006-kisten-beute-rarity.md) und [0008](../entscheidungen/0008-ausruestung-haendler-waffenkammer.md) (Seltenheit), ADR 0011 (PR #481, noch offen: Skills gehören in 1.0).
- **Alle Zahlen sind vorläufige Startwerte** in `assets/balance.json` (neue Abschnitte `skills` und `statusEffects`), angepasst in der Balancing-Runde.

## Zusammenfassung

1. **Ein Skillpunkt alle 3 Stufen.** Ein voller Lauf durch Ebene 0 bringt heute **3 Punkte**, wer Räume auslässt **2**.
2. **Punkte zahlen alles:** 1. Punkt = aktiver Skill, 2. Punkt = sein Statuseffekt, jeder weitere = eine Ausbaustufe.
3. **Ein aktiver Skill je Lauf** (Taste **Q**). Der erste Altarbesuch bietet **drei zufällige** von fünf.
4. **Fünf Statuseffekte:** Leech, Stun, Holy Fire (Weihfeuer), Grave Chill (Grabeskälte), Marked (Gezeichnet). Startwerte halb so stark wie zuerst vorgeschlagen.
5. **Waffen** würfeln beim Fund, ob sie einen Effekt tragen. Er löst je Treffer mit **10 bis 25 %** aus, je nach Seltenheit.
6. **Synergie:** Tragen Waffe und Skill denselben Effekt, hält er **1,5-mal so lange** (Leech: 1,5-mal so stark).
7. **Erneut ausgelöst** frischt die Dauer auf, stärker wird nichts. Verschiedene Effekte liegen gleichzeitig.
8. **Bosse taumeln** bei Stun nur kurz und sind danach einige Sekunden immun.
9. **Am Gegner:** farbiger Schimmer, ein Effekt passend zum Status, darüber die Restzeit als kleine Zahl. Kein Rot.
10. **Altar** im eigenen Raum mit eigenem Teleporter. Kein Skill erhöht das maximale Leben.

## 1. Ablauf eines Laufs

**Erfahrung heute** (`assets/balance.json`, `experience`; Rechnung in `Player.ExperienceToNextLevel`, `src/SilverAndAsh.Core/Gameplay/Player.cs`): Stufe 0 → 1 kostet 100, jede weitere Stufe 1,25-mal so viel (gerundet). Ein Ghul gibt 30, eine Fledermaus 20; **Gorm gibt keine Erfahrung** (`GameWorld.Damage`, `src/SilverAndAsh.Core/Gameplay/GameWorld.cs`, nur Ghul und Fledermaus).

| Stufe | 1 | 2 | **3** | 4 | 5 | **6** | 7 | 8 | **9** | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Erfahrung gesamt | 100 | 225 | **381** | 576 | 820 | **1125** | 1506 | 1983 | **2579** | 3324 |

**Report vom 19.11.** (`docs/balancing.md`, 500 Ebenen): 96,9 Gegner, 2671 Erfahrung bei vollem Räumen, also **27,6 je Kill**.

| Punkt | bei Stufe | nach etwa | im Lauf (13 Kampfräume, rund 7,5 Gegner je Raum) | wofür |
|---|---|---|---|---|
| 1 | 3 | 14 Kills | Kampfraum 2 bis 3 | aktiver Skill |
| 2 | 6 | 41 Kills | Kampfraum 5 bis 6 | Statuseffekt |
| 3 | 9 | 93 Kills | erst fast am Ende, vor dem Bosstor | Ausbaustufe I |

- **Voller Lauf: 3 Punkte**, der dritte aber erst kurz vor Gorm. Erfahrungs-Boni (+2,5 % je Wahl) ziehen das nur wenig vor.
- **Lauf mit ausgelassenen Räumen** (Sieg am 15.11. nach 9 von 14 Räumen): etwa 60 Kills, Stufe 7, **2 Punkte** – Skill und Effekt, kein Ausbau.
- Der Altar steht deshalb **auf etwa einem Drittel des Wegs** (Punkt 1 kommt früh), sein **Teleporter** macht Punkt 3 einlösbar: vom Bosstor zum Altar und zurück.
- **Ausbaustufen II und III erreicht in 1.0 (nur Ebene 0) kaum jemand.** Sie sind für spätere Ebenen gedacht (siehe „Noch offen“).

## 2. Altar

- **Raum:** eigene Raumart `Altar` (ADR 0005): ein Eingang, ohne Schlüssel erreichbar, gegnerfrei, nie hinter verschlossener Tür, kein Brunnen darin. Genau einer je Ebene, etwa auf einem Drittel des Wegs zum Boss.
- **Teleporter:** dritter Ort neben Start und Bosstor (`TeleporterSite` in `src/SilverAndAsh.Core/World/TeleporterPlan.cs` bekommt `Altar`, Platzierung in `TeleporterPlacement.cs`, Freischalten wie heute in `src/SilverAndAsh.Core/Gameplay/Teleporter.cs`).
- **Bedienen:** **F** öffnet das Fenster „Ash Altar“. Was es zeigt, hängt vom Stand des Spielers ab:

| Stand | Angebot | kostet |
|---|---|---|
| noch kein Skill | **drei zufällige** der fünf aktiven Skills (eigener Zufall aus dem Seed) | 1 Punkt |
| Skill, kein Effekt | die fünf Statuseffekte, frei wählbar | 1 Punkt |
| Skill und Effekt | die nächste Ausbaustufe (I, dann II, dann III) | 1 Punkt |

- Ohne Punkt ist alles grau, lesbar bleibt es. Bewegen oder ein Treffer schließt das Fenster; die Welt läuft weiter.
- Das Angebot der drei Skills bleibt bis zur Wahl gleich (erneutes Öffnen würfelt nicht neu).
- **Coop:** Jeder Spieler hat eigene Punkte, eigenen Skill, eigenes Angebot; der Altar verbraucht sich nicht.
- **Aussehen:** hüfthoher, verwitterter Steinblock mit Silberschale und zwei Kerzen; die Kerzen brennen (Messing), solange ein Punkt offen ist. Kartensymbol Kerze. Name „Ash Altar“ (vorläufig).
- **Kein HP-Ausbau:** Kein Skill, Effekt oder Ausbau hebt das maximale Leben. Der Deckel bleibt `boons.healthCap` (235); am Deckel kommen wie heute keine Leben-Boni mehr (`Player.cs`, Bonus-Angebot). Leech heilt nur bis zum Maximum.

## 3. Aktive Skills

Gemeinsam: Taste **Q** (neues Feld `UseSkill` in `PlayerInput`), Werte unter `skills.<name>`, Spieltexte englisch. **Der Statuseffekt des Skills löst bei jedem Treffer des Skills sicher aus** (keine Chance); bei Bloodlust bei jedem eigenen Treffer während des Rauschs.

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

- **Beim Fund** (Kiste, Gorm, Händler) würfelt die Waffe, ob sie einen Effekt trägt, und welchen (gleich verteilt). Die Startwaffen (Silver Blade, Pistol) tragen keinen.
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
- **Online-Coop offen:** Q und die Altar-Wahl sind Eingaben im Tick; Punkte, Skill und Angebot je Spieler; Effekte merken ihren Auslöser; Zufall nur mit Seed im Spielzustand; kein veränderlicher `static`-Zustand.

## 7. Schnitt in Issues

Je Zeile ein Issue und ein kleiner PR, Code-PRs nacheinander. Bild und Klang laufen als Probestücke daneben.

**Code**
1. Skillpunkte zählen (Stufe 3, 6, 9; Tod setzt zurück; HUD-Hinweis als Platzhalter).
2. Statuseffekte: Grundgerüst und Marked (Zustand, Ticks, Auffrischen, Ereignisse; Test ohne Skill über einen Testauslöser).
3. Holy Fire (Schaden über Zeit, Kill für den Auslöser).
4. Grave Chill (Tempo von Ghul, Fledermaus, Gorm).
5. Stun mit Taumeln und Immunität bei Gorm.
6. Leech (Bruchteile, nie über das Maximum).
7. Anzeige am Gegner: Platzhalter-Schimmer und Timer.
8. Waffen-Effekte: Wurf beim Fund, Chance je Seltenheit, Anzeige im Inventar.
9. Altarraum im Generator (Test über 100 Ebenen, gleicher Seed = gleicher Grundriss).
10. Teleporter am Altar.
11. Altar bedienen: drei zufällige Skills, Effektwahl, Ausbau (Fehlerzweige: kein Punkt, tot, alles ausgebaut).
12. bis 16. je ein Skill mit Ausbau I: Bloodlust, Holy Water, Ash Nova, Phantom Step, Silver Volley.
17. Synergie.
18. Balance-Report und Bot (Punkte je Lauf, gewählte Skills und Effekte).

**Bild:** Altar-Probestück und Einbau; Probenblatt der fünf Effekte am Ghul (Schimmer, Flammen, Reif …, mit Timer); HUD-Feld für Q mit Leiste bzw. Abklingzeit; je Skill ein kleines Probenblatt.

**Klang** (neue Kategorien, also Hörprobe vor dem Einbau): Altar und Skillpunkt; je Effekt ein Auslöse-Klang; je Skill ein Klang beim Auslösen.

## Noch offen

1. **Ausbau II und III kommen in 1.0 kaum vor** (siehe Abschnitt 1). *Empfehlung:* in 1.0 nur Ausbau I bauen, II und III als Issues mit Label `später` für die nächsten Ebenen.
2. **Wie oft trägt eine gefundene Waffe überhaupt einen Effekt?** *Empfehlung:* 40 %, für alle Seltenheiten gleich; die Seltenheit wirkt schon über die Chance je Treffer.
3. **Effektwahl am zweiten Altar: alle fünf oder drei zufällige?** *Empfehlung:* alle fünf frei („bevorzugter Statuseffekt“), damit man passend zu einer gefundenen Waffe wählen kann.
4. **Ausbaustufen in fester Reihenfolge oder wählbar?** *Empfehlung:* feste Reihenfolge I, II, III – bei höchstens einem Ausbau je Lauf lohnt keine Wahl.
5. **Farben für Leech, Stun und Marked** stehen noch nicht in der Art-Bible. *Empfehlung:* Probenblatt des Grafikdesigners mit den Vorschlägen aus Abschnitt 4, Urteil beim nächsten Spieltest.
