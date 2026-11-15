# 0005 – Ebenen, Räume und Durchlauf

- **Status:** angenommen
- **Datum:** 2026-11-15
- **Löst ab:** in [0001](0001-projektgrundlagen.md) Ablauf, Pause, Bosse („nicht in 1.0“) und den Meilensteinplan ab v0.8; in [0004](0004-fortschritt-stufen-beute-skills.md) „Kisten, Händler, Items, Waffen, Altare kommen danach“ und die anhaltende Bonus-Wahl

## Kontext

Bis v0.7 bestand ein Level aus einer handgebauten Textdatei. Für einen Roguelike-Durchlauf braucht es Ebenen, die jedes Mal anders aussehen, Gründe zum Erkunden, einen Höhepunkt und ein Ende. In einer Grilling-Session (World- und Level-Design) wurde geklärt, wie Ebenen aufgebaut sind, welche Räume es gibt, wie Türen, Schlüssel und Beute zusammenspielen – und was davon schon in 1.0 gehört.

Leitlinie dabei: **Online-Coop bleibt möglich** ([0003](https://github.com/flubby2222/silver-and-ash/pull/54)) – das Spiel steht deshalb außer beim Alleinspiel nie still.

## Entscheidung

### Durchlauf
- Ebenen werden **ab 0 gezählt**. **In 1.0 gibt es nur Ebene 0** (Katakomben); später drei (Katakomben → Kerker → Schlosshalle). Etwa **20 Minuten pro Ebene** für einen mittelmäßig erfahrenen Spieler – ohne harte Grenze.
- Ziel einer Ebene ist der **Bossraum**. Nach dem Sieg fährt in seiner Mitte eine **Wendeltreppe** aus dem Boden – sie führt **nach oben**, zurück geht es nicht. **Sieg in 1.0** = Boss von Ebene 0 besiegt, Treppe genommen.
- **Tod = neuer Durchlauf** auf einer **neu erzeugten** Ebene 0 (neuer Seed, neuer Grundriss).
- **Esc-Menü pausiert nur allein**; im Coop läuft die Welt weiter. Technisch: Die Welt tickt nur nicht, solange **alle** Spieler pausiert haben.
- **Alle anderen Fenster** (Händler, Altar, Inventar, Bonus-Wahl) halten **nie** an. Bewegen oder ein Treffer schließt sie.

### Aufbau einer Ebene
- Eine Ebene besteht aus **Räumen**, durch **Türen** getrennt, **nicht linear** – Räume können mehrere Zugänge haben, **Schleifen** sind erlaubt. Das Grundmuster bleibt bei allen Ebenen gleich; **Design und Gegner** ändern sich mit der Ebene.
- Räume kommen aus **handgebauten Raum-Vorlagen** (im bestehenden Level-Format), die der Generator per Seed auswählt, dreht/spiegelt und verbindet. Mehr Vorlagen = mehr Vielfalt ohne neuen Code.
- Räume sind **groß und verwinkelt** (etwa 10×10 bis 16×16 Zellen, L- und T-Formen, Säulen, Nischen, Zwischenwände) – zur Orientierung und passend zum Bewegungssystem. Gänge sind schmal und knicken ab. Raumzahl ist ein Richtwert, abgestimmt auf die 20 Minuten.
- **Raumarten:**

| Raum | Rolle |
|---|---|
| Start | ruhiger Einstieg, gegnerfrei |
| Kampf | häufigster Raum; wird zur **Arena** (siehe Türen); schwerer, je weiter vom Start |
| Gang | Verbindung |
| Schatz ×3 | einer **hinter verschlossener Tür**, einer **normal**, einer **secret** |
| Händler | gegnerfrei |
| Altar | gegnerfrei, Skillpunkte einlösen |
| Boss | am weitesten vom Start; Wendeltreppe nach dem Sieg |

- **Nur einen Eingang** haben: Schatzräume, Secret Room, Händler, Altar, Boss.
- **Generator-Garantien:** Boss, Händler und Altar sind ohne Schlüssel erreichbar; der Secret Room grenzt an einen Kampfraum; Start, Händler und Altar sind gegnerfrei; gleicher Seed = gleicher Grundriss.
- **Ebene 0:** Decken **6 m** in Räumen (für den Luft-Dash), 3,5 m in Gängen; **keine Höhenunterschiede** (Stufen, Gruben kommen später). Look erst **funktional** (grau-brauner Stein, Kerzen/Fackeln, Nebel), so gebaut, dass Texturen und Animationen später separat dazukommen. Ebenenname beim Betreten.
- **Orientierung:** keine Minimap; **erkennbare Türen** übernehmen die Führung.

### Türen und Interaktion
- **F** ist die Interaktionstaste: Türen, Kisten, Schlösser öffnen, Personen ansprechen. Kleinkram (Verbrauchsgüter, Silber) nimmt man durch **Drüberlaufen** auf.
- **Alle Türen sind standardmäßig zu.**
- Ein **Kampfraum** schließt beim **ersten Betreten** alle Türen (Gitter fallen), die Gegner im Raum **erwachen**. Ist er geräumt, öffnet sich die **Tür, durch die man kam**, von selbst; die übrigen lassen sich öffnen. Endet eine Schleife in einem bereits geräumten Kampfraum, bleiben dessen Türen offen.
- Geöffnete, aufgeschlossene und Türen bereits betretener Nicht-Kampf-Räume **bleiben offen**.
- **Aussehen:** Holz (normal) · Eisen mit Vorhängeschloss (verschlossen) · groß mit Schädeln und rotem Licht (Boss) · herabgelassenes Gitter (Arena).

### Schlüssel, Kisten, Secret Room
- **Schlüssel** passen in jedes Schloss und werden verbraucht. Ein **geräumter Kampfraum** gibt einen mit **15 %**, ein **Gegner** mit **5 %**; fehlt einer, gibt es ihn beim Händler. Ziel: im Schnitt etwa so viele Schlüssel wie Schlösser.
- **Kisten** sind mit **33 %** verschlossen (unabhängig vom Raum).
- Der **Secret Room** liegt hinter einer **rissigen Wand**, die man mit einem **aufgeladenen Klingenhieb** einschlägt.

### Wirtschaft und Ausrüstung
- Währung **Silber**: 1–3 pro Gegner, 15–25 pro Kiste.
- **Händler** verkauft Blutphiole (20), Schlüssel (40), vereinzelt Items/Waffen (100) – **keine Geweihte Munition** (würde sofort wirken). Er **kauft** Items und Inventar-Waffen für **25 %** des Preises; ein verkauftes Item verliert seine Wirkung.
- **Items** (Proof of Concept, passiv für den Durchlauf): **Klingenparade** (Klinge zerschlägt Blutgeschosse, #90), **Rosenkranz aus Silber** (jeder Kill heilt 3).
- **Waffen:** linke Maus = **eine Nahkampfwaffe**; rechte Maus = Fernwaffe, **bis zu 3**, gewechselt mit dem **Mausrad**. **Inventar** (Tab, 6 Plätze) zeigt alle Waffen; ausgerüstete stehen in eigenen, nicht verkäuflichen Slots; die Nahkampfwaffe wird dort getauscht.
- **Nachladen:** Magazin + Ladezeit, mit **R** oder automatisch; Munitionsvorrat unbegrenzt. Pistole: Magazin 7, 0,8 s, Nachladen 1,5 s. **Rifle** (Proof of Concept): 60 Schaden, durchschlägt bis zu 3 Gegner, Magazin 5, 1,4 s, Nachladen 2 s. (Die Armbrust ist für später vorgemerkt.)
- Herkunft von Items und Waffen: **Kisten**, **sicher vom Boss**, vereinzelt **Händler**.

### Stufen und Skills
- Die **Bonus-Wahl** aus v0.7 bleibt, **hält aber nicht mehr an**: Ein Hinweis im HUD zeigt offene Boni, **X** öffnet das bekannte Fenster.
- **Jede 3. Stufe** gibt einen **Skillpunkt**, eingelöst am **Altar** (F).
- Erste Skills: **Blutrausch** und ein **Weihwasser**-Skill. Das Skill-Konzept (Status-Wahl beim ersten Upgrade, Ausbau, Synergien, Namen) wird in einer **eigenen Session** geklärt.

### Boss von Ebene 0: Troll mit Keule und Kette

| Angriff | Ankündigung | Ausweichen |
|---|---|---|
| Keulen-Hammerschlag | Keule hoch, 0,8 s | Schockwelle am Boden → **springen** |
| Ketten-Rundumschwung | Kette angedreht, 0,6 s | Kette auf Brusthöhe → **Dodge** (geduckt) |
| Kettenwurf | Kette glüht, 0,5 s | zieht dich heran → **zur Seite** |
| Phase 2 (ab 50 %) | Brüllen, 1,5 s | schneller; Hammerschlag lässt **Steine von der Decke** fallen |

Verfehlt der Hammerschlag, steckt die Keule kurz fest: Der Troll nimmt dann doppelten Schaden.

### Bewegung
- **Luft-Dash in Blickrichtung**: W + Shift in der Luft dasht dorthin, wohin man schaut – auch nach oben.

### Alle Zahlen sind Startwerte
Chancen, Preise, Magazine, Zeiten und der Troll stehen in `balance.json` und werden nach dem Anspielen angepasst („subject to change“).

### Neuer Meilensteinplan bis 1.0

| Release | Inhalt |
|---|---|
| v0.8 | **Run-Grundgerüst:** Titel, Esc-Menü (Pause nur allein), Game-Over- und Sieg-Bildschirm, Run-Statistik, F-Interaktion, Türen (Arten, Arena), Raum-Vorlagen und Generator (Muster, Seed, Garantien), Gegner erwachen, Schwierigkeit nach Abstand, Ebenenname, 6-m-Decken |
| v0.9 | **Schatz und Wirtschaft:** Schatzräume, Kisten (33 % verschlossen), Schlüssel, Secret Room, Silber, Händler (kaufen, verkaufen) |
| v0.10 | **Boss und Bewegung:** Troll, Wendeltreppe und Sieg, Luft-Dash in Blickrichtung |
| v0.11 | **Ausrüstung:** Waffen-Slots und Mausrad, Nachladen, Rifle, Inventar, Items, Bonus-Wahl mit X ohne Stillstand |
| v0.12 | **Skills:** Altar und erste Skills (nach der Skill-Session) |
| v0.13 | Modelle |
| **v1.0** | Balancing, Bugfixes, Feinschliff |

Nicht in 1.0: Ebenen 1 und 2, weitere Bosse, Höhenunterschiede in Ebenen, Minimap, Lore-Texte, Armbrust, Texturen und Skelett-Animation.

## Konsequenzen

- **Positiv:** Jeder Durchlauf hat einen neuen Grundriss, aber durch Vorlagen und feste Regeln immer spielbar und lesbar. Neue Räume sind Dateien, kein Code.
- **Positiv:** Erkunden lohnt sich (Schatz, Secret, Händler, Altar), ohne erzwungen zu werden; der Boss gibt jeder Ebene einen Höhepunkt.
- **Positiv:** Kein Stillstand außer der Solo-Pause – Online-Coop bleibt offen.
- **Negativ:** Der Umfang bis 1.0 wächst deutlich (Generator, Türen, Wirtschaft, Boss, Ausrüstung, Skills) – dafür hat 1.0 nur eine Ebene.
- **Negativ:** Die Bonus-Wahl aus v0.7 muss umgebaut werden (kein Anhalten mehr).
- **Negativ:** Ohne Minimap hängt die Orientierung an guten Vorlagen und erkennbaren Türen – das muss beim Anspielen geprüft werden.
