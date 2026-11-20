# Entwurf: Skills, Skillpunkte und Altare (#104)

- **Stand:** Entwurf zur Vorstellung beim Menschen, 2026-11-20. Noch nichts davon ist gebaut.
- **Grundlagen:** [ADR 0004](../entscheidungen/0004-fortschritt-stufen-beute-skills.md) (Blutrausch wird Skill, Altare planbar statt Zufall), [ADR 0005](../entscheidungen/0005-ebenen-raeume-durchlauf.md) (jede 3. Stufe ein Skillpunkt, Altarraum, Fenster halten nie an), [ADR 0003](../entscheidungen/0003-online-coop-offenhalten.md) (Coop offen), ADR 0011 (noch offen, PR #481: Skills und Altare gehören in 1.0, Meilenstein v0.13).
- **Alle Zahlen sind vorläufige Startwerte.** Sie kommen in `assets/balance.json` (neuer Abschnitt `skills`) und werden in der Balancing-Runde angepasst.

## 1. Vorschlag in Kürze

- **Skillpunkte:** Jede **3. Stufe** gibt einen Skillpunkt (wie ADR 0005). Ein Lauf erreicht heute etwa Stufe 9 bis 10 (Balance-Report), also **etwa 3 Skillpunkte je Lauf**. Die Bonus-Wahl (X) bleibt daneben unverändert.
- **Altar:** ein Steinaltar in einem eigenen, ruhigen **Altarraum** (ein Eingang, ohne Schlüssel erreichbar, gegnerfrei), **einer je Ebene**, etwa auf einem Drittel des Wegs zum Boss. Der Altarraum bekommt einen **eigenen Teleporter**, damit man spätere Punkte ohne langen Rückweg einlöst.
- **Wahl:** Am Altar mit **F** öffnet sich ein kleines Fenster mit **allen Skills** des Spiels. Jeder kostet **1 Skillpunkt** und lässt sich einmal lernen. Keine Ränge, kein Baum, kein Zufall.
- **Ein Lauf trägt also etwa 3 Skills**, davon **höchstens einen aktiven** (Taste **Q**, z. B. Blutrausch). Lernt man einen zweiten aktiven, ersetzt er den ersten.
- **Tod:** Skills und offene Skillpunkte sind weg, wie Stufe, Boni und Ausrüstung. Der nächste Lauf beginnt ohne Skills.

## 2. Grundmodelle zur Wahl

| | **A – Feste Liste, freie Wahl** (Empfehlung) | **B – Drei zufällige zur Wahl** | **C – Aktive Fähigkeiten mit Baum** |
|---|---|---|---|
| **So geht es** | Der Altar zeigt alle 8 Skills. Man wählt frei, 1 Punkt je Skill. | Der Altar bietet je Punkt 3 Skills, gezogen mit dem Seed (wie die Bonus-Wahl). | Zwei Plätze für aktive Fähigkeiten (Q, E) mit Abklingzeit; jede hat 2 bis 3 Ausbaustufen; daneben wenige Passive. |
| **Aufwand** | **klein bis mittel**: eine Liste, ein Fenster, ein Zähler | **mittel**: wie A, dazu Ziehen, Angebote stapeln, Tests über Seeds | **groß**: zwei Tasten, Abklingzeiten im HUD, Ausbaustufen, viel mehr Balancing |
| **Balancing** | Planbar. Gefahr: alle wählen dasselbe. Gegenmittel: Skills für verschiedene Spielweisen, keiner klar am stärksten. Bot-Durchläufe je Skill sind einfach. | Mehr Abwechslung je Lauf, aber wieder Zufall – genau das sollten Altare laut ADR 0004 abschwächen. | Stärkster Eingriff in Kampf und Tempo; Gorm müsste neu abgestimmt werden. |
| **Koop** | Einfach: Jeder Spieler hat eigene Punkte und eigene Skills. Die Wahl ist eine Eingabe im Tick. | Einfach, Angebot je Spieler aus dem Seed der Welt. | Mittel: mehr Zustand je Spieler, mehr Ereignisse. |
| **Passt zu** | „Keep it simple“, ADR 0004 („planbar statt Zufall“) | Bonus-Wahl (gleiches Muster, aber doppelt gemoppelt) | ADR 0005 „Ausbau, Synergien“ – aber weit über 3 Punkte je Lauf hinaus |

**Empfehlung: Modell A.** Bei nur etwa 3 Punkten je Lauf ist jede Wahl wichtig; sie soll planbar sein, und der Zufall steckt schon in Boni, Kisten und Brunnen. Modell A ist das kleinste, lässt sich später zu B erweitern (Altar zeigt dann nur eine Auswahl) und hält Koop einfach. C wäre ein eigenes Spielsystem und gehört nicht in 1.0.

## 3. Skills

Gemeinsam für alle: Kosten **1 Skillpunkt**, Werte in `balance.json` unter `skills.<name>`, Name und Wirkungssatz im Spiel englisch. Gelernte Skills liegen am Spieler (neuer Zustand in `Player.cs`, Muster wie die Boni in `Boon.cs` und `Player.Choose`). Kein Skill ändert das maximale Leben – so bleibt der Deckel 235 und die Regel „am Deckel keine Leben-Boni mehr“ unberührt.

### Kern für 1.0 (8)

| Skill | Wirkung (Spieltext, englisch) | Art | Spielt zusammen mit | Betroffene Stellen im Code |
|---|---|---|---|---|
| **Bloodlust** | "Melee hits fill your blood bar. When it is full, press Q: shoot and run faster for a few seconds." | aktiv (Q) | Silver Blade, Club, Axe, Whip; Fernwaffen im Rausch; Executioner | `Player.cs`: Leiste und Restzeit in Ticks (Muster `Consecrate` / `_consecratedTicksLeft`), Feuerrate in `ranged.Cooldown.TryUse`, Tempo über `SpeedFactor`; `GameWorld.StrikeWithMelee` füllt die Leiste; `PlayerInput` neues Feld `UseSkill`; Ereignisse in `GameEvent.cs`; `SoundCues.cs` (Tabelleneintrag); `HudView.cs`, `ScreenEffectsView.cs` (Bildrand) |
| **Holy Water** | "Press Q: throw a vial of holy water. The undead burn while they stand in it." | aktiv (Q), Abklingzeit | Club (Rückstoß hinein), Whip (heranziehen hinein), Ghul-Gruppen, Gorm steht still (Keule steckt fest) | neue Klasse für die Pfütze (Position, Radius, Restzeit in Ticks) in `GameWorld`, Schaden über `GameWorld.Damage` an `ITarget`; Wurf als gerader Strahl auf den Boden (ohne neue Flugbahn-Physik); `PlayerInput.UseSkill`; Ereignisse; `ProjectileView.cs`/neue Ansicht |
| **Second Wind** | "Your stamina bar has one more segment." | passiv | Dodge, Luft-Dash, aufgeladene Schläge, Gorm-Ausweichen | `Stamina.cs` (Kapazität und Segmente, heute fest aus `StaminaJumps`), `Player.cs` (Stamina-Aufbau); `Presentation/HudLayout.cs` und `HudReadout.cs` (Leiste mit einem Segment mehr) |
| **Phantom Step** | "Dodging costs one stamina segment less." | passiv | Second Wind, Gorms Kettenschwung, Fledermaus-Sturzflug | `Player.cs` Dodge (`Stamina.TryPay(_player.DodgeStaminaSegments)`) |
| **Executioner** | "Fully charged melee strikes deal 30% more damage." | passiv | Hunter's Axe, Club, Gorm mit steckender Keule, rissige Wände | neue Schadensregel nach `IDamageRule` in `Damage.cs` (liest `Hit.Charge`, Muster `DamageBoonBonus`) |
| **Fresh Load** | "The first shot after a reload deals 50% more damage." | passiv | Rifle, Blunderbuss, Stake Crossbow, Quickdraw Holster | `Magazine.cs` (Merker „frisch geladen“), `Player.cs` beim Schuss, `Hit` um ein Feld erweitern, Regel in `Damage.cs` |
| **Last Rites** | "Below a quarter of your health you deal 25% more damage." | passiv | Ashen Heart, Leech, Silver Rosary | `Hit` bekommt den Anteil Leben des Angreifers, Regel in `Damage.cs`; `GameWorld.Damage` setzt den Wert |
| **Grave Tithe** | "Enemies drop silver more often." | passiv | Händler (Old Plunderer), Well of Sirius | `GameWorld.DropSilver` (Chance je Spieler, der den Kill macht); Werte `silver.*` bleiben, Aufschlag unter `skills` |

Die Prozentzahlen in den Spieltexten sind **vorläufig**; im Spiel kommen sie aus `balance.json` (wie bei den Items).

**Blutrausch im Einzelnen (vorläufig):** Leiste füllt sich nur durch Nahkampftreffer (z. B. 8 Treffer bis voll), Q bei voller Leiste: 6 s lang Fernwaffen 1,5-mal so schnell und 20 % schneller laufen. Wirkt nur auf den eigenen Spieler. Mit Geweihter Munition: Faktoren werden multipliziert, nicht ersetzt – das muss die Balancing-Runde ansehen.

### Später (Label `später`)

| Skill | Wirkung | Art | Spielt zusammen mit | Betroffene Stellen |
|---|---|---|---|---|
| **Frenzied Blood** | Ausbau von Bloodlust: auch Kopftreffer füllen die Leiste. | passiv, braucht Bloodlust | Pistol, Rifle, Hunter's Eye | `GameWorld.FireShot`, Bloodlust-Zustand |
| **Crimson Feast** | Kills im Blutrausch heilen ein wenig. | passiv, braucht Bloodlust | Silver Rosary, Leech | `GameWorld` beim Kill, `Player.Heal` |
| **Ash Ward** | Der erste Treffer nach Betreten eines Kampfraums wird abgefangen. | passiv | Arena-Türen, Gorm | `Player.TakeDamage`, `GameWorld.EnterRooms` |
| **Grave Sense** | Die Karte zeigt Kisten und den Altar auch in unbetretenen Räumen. | passiv | Karte (M), Schlüssel | `MapView.cs`, Kartendaten aus `GameWorld` |
| **Chain Breaker** | Rückstoß von Club und Whip wirft Gegner um (kurz betäubt). | passiv | Club, Whip, Holy Water | `Knockback.cs`, `Ghoul.cs`, `Bat.cs` |
| **Undertaker's Luck** | Kisten enthalten etwas öfter Ausrüstung. | passiv | Kisten, Schlüssel, Beute-Topf | `GameWorld.OpenChest`, `LootPool.cs` |

Ausbau-Skills (Frenzied Blood, Crimson Feast) und alles, was Wirtschaft oder Karte ändert, kommt erst nach 1.0 – bei 3 Punkten je Lauf würden sie die Wahl nur verwässern.

## 4. Altar

- **Aussehen (für den Grafikdesigner):** ein hüfthoher, verwitterter Steinblock mit eingelassener **Silberschale** und zwei niedergebrannten Kerzen; die Kerzen brennen (Messing-Reihe), solange der Spieler einen offenen Skillpunkt hat, sonst sind sie aus. Kein Rot, keine Stufenfarben, kein weiches Glühen (Art-Bible).
- **Name im Spiel:** "Ash Altar" (vorläufig).
- **Platzierung:**
  - eigene Raumart `Altar` (ADR 0005): ein Eingang, ohne Schlüssel erreichbar, gegnerfrei, Schutzraum wie Schatzraum und Händler;
  - genau einer je Ebene, etwa **auf einem Drittel des Wegs** vom Start zum Boss (der Händler steht auf halbem Weg) – so findet man ihn meist kurz nach dem ersten Punkt (Stufe 3);
  - **eigener Teleporter** im Altarraum (dritter Ort neben Start und Bosstor): freigeschaltet beim Betreten; so löst man spätere Punkte ein, ohne zurückzulaufen;
  - der **Brunnen** steht nie im Altarraum (jeder Ort hat seinen eigenen Raum, leichter lesbar); nie hinter einer verschlossenen Tür, nie neben dem Geheimraum;
  - auf der Karte ein eigenes Symbol (z. B. Kerze), wie das Münz-Symbol des Händlers.
- **Bedienung:**
  - **F** am Altar öffnet das Fenster „Ash Altar“: Liste der Skills mit Name, Wirkungssatz, „active“/„passive“, Häkchen für gelernte; oben die offenen Skillpunkte;
  - wählen mit Klick oder Zifferntaste, solange das Fenster offen ist; ohne Punkt ist alles grau, das Fenster lässt sich trotzdem lesen;
  - Bewegen oder ein Treffer schließt es; die Welt läuft weiter (ADR 0005, 0008);
  - HUD: bei neuem Punkt kurz „Skill point – find the Ash Altar“; ein aktiver Skill zeigt ein kleines Feld mit „Q“ und Leiste bzw. Abklingzeit;
  - im Koop: Jeder Spieler hat eigene Punkte und eigene Skills; der Altar verbraucht sich nicht.
- **Klänge für die Hörprobe** (neue Kategorien, also Hörprobe vor dem Einbau):
  1. Skillpunkt erhalten (Familie *Beute und Bedienung*: klein, hell, kurz);
  2. Skill am Altar gelernt (tiefer Stein- oder Glockenschlag, eine Geste);
  3. Blutrausch bereit (Leiste voll, kurz);
  4. Blutrausch beginnt und endet (zwei Gesten: Anschwellen, Abklingen);
  5. Holy Water: Phiole zerspringt und zischt (eine Geste), dazu ein leises Zischen der Pfütze, das Untote treffen.

## 5. Offene Entscheidungen für den Menschen

**Frage 1: Wie wählt man Skills am Altar?**
1. Feste Liste, freie Wahl: alle Skills sichtbar, 1 Punkt je Skill (Empfehlung)
2. Der Altar bietet je Punkt drei zufällige Skills zur Wahl
3. Aktive Fähigkeiten auf Q und E mit Ausbaustufen, dazu wenige passive

**Frage 2: Wie oft gibt es einen Skillpunkt?**
1. Jede 3. Stufe, etwa 3 je Lauf (Empfehlung)
2. Jede 2. Stufe, etwa 4 bis 5 je Lauf
3. Jede 3. Stufe, dazu einer geschenkt beim ersten Altarbesuch

**Frage 3: Wo steht der Altar?**
1. Eigener Altarraum auf einem Drittel des Wegs, mit eigenem Teleporter (Empfehlung)
2. Eigener Altarraum ohne Teleporter
3. Im Startraum, kein eigener Raum (weicht von ADR 0005 ab)

**Frage 4: Wie viele aktive Skills (Taste Q) trägt ein Lauf?**
1. Höchstens einen; ein neuer aktiver ersetzt den alten (Empfehlung)
2. Zwei, auf Q und E
3. Nur Blutrausch ist aktiv, alle anderen Skills sind passiv

**Frage 5: Woran sieht man den Blutrausch?**
1. Ruhiger dunkelroter Bildrand und die Leiste im HUD (Empfehlung)
2. Kein Rot: Bild leicht entsättigt, Rand dunkel
3. Nur Leiste im HUD und Klang, kein Bildrand

## 6. Schnitt in Issues

Reihenfolge innerhalb jeder Spalte; Code-PRs einzeln nacheinander. Bild und Klang laufen als Probestücke daneben und kommen erst nach Abnahme ins Spiel.

### Code

1. **Skillpunkte zählen** – `Player` bekommt Skillpunkte; Wert `skills.pointEveryLevels` in `balance.json`.
   - Test: Stufe 3, 6, 9 geben je einen Punkt, Stufe 1, 2, 4 keinen; mehrere Stufen in einem Tick geben mehrere Punkte.
   - Ereignis `SkillPointGained`; Tod setzt alles zurück (zwei Läufe im Test).
   - HUD-Hinweis „Skill point“ (Platzhalter-Text).
2. **Altarraum im Generator** – Raumart `Altar`, ein bis zwei Raum-Vorlagen, Kartensymbol.
   - Test über 100 Ebenen: genau ein Altarraum, ein Eingang, ohne Schlüssel erreichbar, gegnerfrei, kein Brunnen darin.
   - Gleicher Seed = gleicher Grundriss; andere Räume, Kisten und Gegner verschieben sich nicht (eigener Zufall aus dem Seed).
3. **Altar bedienen** – Altar als `IInteractable` (Platzhalter-Kasten), Fenster, Eingabe `LearnSkill` in `PlayerInput`.
   - Lernen kostet einen Punkt, ohne Punkt nichts; ein Skill nur einmal.
   - Bewegen oder Treffer schließt das Fenster; die Welt läuft weiter.
   - Fehlerzweige mit eigenen Tests (kein Punkt, schon gelernt, tot).
4. **Teleporter am Altar** – dritter Teleporter-Ort.
   - Test über 100 Ebenen; Freischalten beim Betreten; Reise vom Bosstor zum Altar.
5. **Bloodlust** – Leiste, Q (`UseSkill`), Wirkung, Ereignisse, HUD-Feld, Bildrand.
   - Tests mit echten Treffern Tick für Tick: Leiste füllt sich nur durch Nahkampf; Q bei nicht voller Leiste tut nichts; Dauer endet genau nach den Ticks aus `balance.json`.
   - Gegenprobe: Feuerrate im Rausch messbar höher.
6. **Passive I: Second Wind, Phantom Step** – Ausdauer.
   - Test: Segmente, Dodge-Kosten, mit und ohne Skill.
7. **Passive II: Executioner, Fresh Load, Last Rites** – Schadensregeln.
   - Test je Regel; Gegenprobe: Regel aus, Schaden wie vorher.
8. **Passive III: Grave Tithe**.
   - Test mit festem Seed: mehr Silber-Drops über viele Kills; nur für den Spieler mit dem Kill.
9. **Holy Water** – Wurf, Pfütze, Schaden je Tick, Abklingzeit.
   - Tests: Ghul in der Pfütze nimmt Schaden, außerhalb nicht; Pfütze endet nach ihrer Zeit; Abklingzeit.
   - Spieler nehmen keinen Schaden (Koop).
10. **Balance-Report und Bot** – Skillpunkte je Lauf, gewählte Skills, Laufzeit mit und ohne Skills; Ergebnis in `docs/balancing.md`.

### Bild

1. **Altar-Probestück** (Modell per Blender-Skript, Kerzen an/aus) – Probenblatt mit vier Ansichten; Urteil der Leitung.
2. **Altar-Einbau** – Modell ersetzt den Platzhalter; Eintrag in `assets/HERKUNFT.md`; Foto im Musterraum.
3. **HUD-Teile** – Feld für den aktiven Skill (Q, Leiste, Abklingzeit), Skillpunkt-Hinweis, Rahmen des Altarfensters, Kartensymbol; als Atlas-Teile in Richtung A.
4. **Blutrausch-Bildrand** – Probenblatt mit den Fassungen aus Frage 5.
5. **Holy Water** – Phiole und Pfütze am Boden, Probenblatt.

### Klang

1. **Hörprobe Skills** – die fünf Klänge aus Abschnitt 4, je mit Skript; Urteil des Menschen.
2. **Einbau** – abgenommene Klänge als Tabelleneinträge in `SoundCues.cs`; Eintrag in `assets/HERKUNFT.md`.

## Unsicherheiten

- **3 Punkte je Lauf** ist aus den letzten Balance-Reports geschätzt (Stufe 9 bis 10). Mit neuen Gegnern (v0.14) kann sich das verschieben.
- **Zusammenspiel Blutrausch und Geweihte Munition** (beide machen die Pistole schneller) muss die Balancing-Runde ansehen.
- **Holy Water** braucht eine neue Art Objekt in der Welt (Fläche mit Schaden über Zeit); das ist der größte Code-Schritt der Liste.
- **Last Rites** erweitert `Hit` um den Lebensanteil des Angreifers – kleine Änderung an einer viel genutzten Stelle.
- Ein **dunkelroter Bildrand** berührt die Regel „Rot ist reserviert für Blut, Treffer, Gefahr“: Blutrausch ist Blut, der Rand muss sich aber klar vom Treffer-Blitz unterscheiden (ruhig statt aufblitzend).
