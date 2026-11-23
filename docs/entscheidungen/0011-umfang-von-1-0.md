# 0011 – Umfang von 1.0: größer, ohne festen Termin

- **Status:** angenommen (der Mensch hat am 2026-11-20 „Größere 1.0“ entschieden; sein Merge dieses ADR ist die Annahme)
- **Datum:** 2026-11-20
- **Löst ab:**
  - in [0001](0001-projektgrundlagen.md): den Termin „v1.0 Anfang Februar 2027“;
  - in [0005](0005-ebenen-raeume-durchlauf.md): aus der Liste „Nicht in 1.0“ die Punkte Armbrust und Texturen (Texturen erlaubt schon [0010](0010-stil-ps1-nah-texturen.md)) sowie „weitere Bosse“, soweit es die Zwischenboss-Räume (#337) betrifft (vorläufig, siehe unten);
  - in [0008](0008-ausruestung-haendler-waffenkammer.md): den Meilensteinplan ab v0.11 (v0.11 und v0.12 bekommen Teleporter und Brunnen dazu, ab v0.13 ist der Plan neu);
  - in [0009](0009-leitung-designer-auto-merge.md): „Keine komponierte Musik in 1.0“, „v0.14 – Modelle“ als Sammelmeilenstein für die Modelle, „v0.15 – Klang“ als letzten Meilenstein vor 1.0 und den Termin Anfang Februar 2027;
  - in [0010](0010-stil-ps1-nah-texturen.md): „v0.14 – Modelle wird größer“ und den Termin Anfang Februar 2027.
- **Bleibt:** Eine Ebene in 1.0 (Katakomben), Sieg = Gorm besiegt und Treppe genommen (0005, 0007). Alles per Skript erzeugt (0009). Online-Coop nach 1.0, aber offen gehalten (0003).

## Kontext

Der Umfang von 1.0 steht verteilt in den ADRs 0001, 0005, 0008 und 0009. Seit dem Spieltest zu v0.11 sind Dinge dazugekommen, die dort nicht stehen: Teleporter, der Brunnen „Well of Sirius“, das neue HUD, Musik für die Katakomben und Texturen. Gleichzeitig liegen Entwürfe für neue Gegner und für die Raumgestaltung im Backlog.

Der Mensch hat am 2026-11-20 im Rückfragefenster entschieden:

- **Richtung:** „Größere 1.0“.
- **Pflicht in 1.0:** vier neue Waffen, Skills und Altare, neue Gegner, Raumgestaltung.
- **Termin:** „Umfang zählt“ – 1.0 erscheint, wenn der Umfang fertig ist.

## Entscheidung

### Was in 1.0 gehört

Zusätzlich zu dem, was bis v0.11 gebaut ist:

| Bereich | Inhalt |
|---|---|
| Ausrüstung | Rest von v0.11: acht Items (#195, #196), Händler-Raum (#197), Old Plunderer mit Kaufen und Verkaufen (#198) |
| Orte | Teleporter (#436), Brunnen „Well of Sirius“ (#438) |
| Waffen | Hunter's Axe, Stake Crossbow, Throwing Knives, Whip (#201–#204) |
| Skills | Skillpunkte, Altare, Blutrausch als Skill (#104) – nach einer Entwurfsrunde mit dem Menschen |
| Gegner | Skelett mit Armbrust (#334), Gruftwächter (#335), starker Ghul (#333), Regeln für stärkere Varianten (#336), Zwischenboss-Räume (#337) |
| Räume | Props (#214), Bauteile (#247, #284), mehr Texturen (#244), Wände in Zonen (#282), überarbeitetes Raumdesign (#215) |
| Bild | HUD und Menü in Richtung A (#342, #397), Modelle rundum geschlossen (#416) |
| Klang | Geräusche und Ambiente (0009), Musik für die Katakomben (#271) |

Schon erledigt und deshalb nicht mehr in der Liste: Klang im Raum mit Richtung und Entfernung (#491, in v0.12) und der HUD-Rahmen mit Schmuck (#412).

### Was nicht in 1.0 gehört

Ebenen 1 und 2 und ihre Bosse, Online-Coop (#53), Waffen-Upgrades (#143), Spezial-Händler (#105), aktive Items per Taste (#285), Höhenunterschiede in Ebenen, Lore-Texte und alles mit dem Label `später` – außer den Zwischenboss-Räumen (#337, siehe *Vorläufig*).

Weiter gilt aus 0001 und 0005: keine dauerhafte Minimap (die Karte auf Tab bleibt), keine Skelett-Animation, kein Speichern, kein Hub mit Unlockables.

### Neuer Meilensteinplan

| Version | Inhalt |
|---|---|
| **v0.11 – Ausrüstung** | Rest wie oben, dazu Teleporter und Brunnen; Balancing-Runde, Release |
| **v0.12 – Waffenkammer** | vier neue Waffen; Balancing-Runde, Release |
| **v0.13 – Skills** | Altare und erste Skills |
| **v0.14 – Gegner** | drei neue Gegner, stärkere Varianten, Zwischenboss-Räume |
| **v0.15 – Räume** | Props, Bauteile, Texturen, Raumdesign |
| **v0.16 – Klang** | restliche Geräusche, Ambiente, Musik für die Katakomben (#271) |
| **v1.0** | Balancing, Bugfixes, Feinschliff |

v0.11 ist als 0.11.0 und 0.11.1 schon erschienen. Wo der Rest von v0.11 sowie Teleporter und Brunnen landen (auf GitHub hängen #436 und #438 inzwischen an v0.12), legt die Leitung beim Anpassen der Meilensteine fest (siehe *Folgearbeit*).

Modelle bekommen keinen eigenen Meilenstein mehr: Jedes Modell erscheint in dem Meilenstein, zu dem sein Inhalt gehört. Der bisherige Meilenstein „v0.14 – Modelle“ wird aufgeteilt.

Jeder Release bleibt eine Mischung aus Code, Bild und Klang, damit der Mensch je Release einmal alles beurteilen kann.

### Termin

Es gibt keinen festen Termin mehr. Die Leitung nennt in jeder Retro eine Schätzung für 1.0 und begründet sie mit dem Tempo der letzten Meilensteine. Erste Schätzung, noch ohne Messung: nicht vor dem Frühjahr 2027.

### Vorläufig

- **Zwischenboss-Räume** (#337) waren Teil der gewählten Richtung, aber kein eigener Punkt der Pflichtliste. Sie gelten als in 1.0, bis der Mensch es anders sagt.
- Die **Reihenfolge** von v0.14 bis v0.16 darf die Leitung umstellen, wenn Proben früher fertig sind.

## Konsequenzen

- **Positiv:** Der Umfang steht wieder an einer Stelle. Was dazugekommen ist, hat einen Platz im Plan. Kein Termin drängt halbfertige Teile in ein Release.
- **Negativ:** 1.0 rückt deutlich nach hinten. Ohne Termin fehlt der Druck, zu streichen; die Schätzung in der Retro ersetzt ihn nur, wenn sie ehrlich bleibt.
- **Folgearbeit:** Meilensteine auf GitHub anpassen (v0.14 aufteilen, v0.15 und v0.16 anlegen, Issues zuordnen, auch die Bild-Issues #342, #397 und #416), Labels `später` an den Issues prüfen – ausdrücklich an #337, Entwurfsrunde zu den Skills ansetzen.
