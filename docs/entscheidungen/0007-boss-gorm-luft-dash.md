# 0007 – Boss Gorm und Luft-Dash

- **Status:** angenommen
- **Datum:** 2026-11-15
- **Ergänzt:** [0005](0005-ebenen-raeume-durchlauf.md) „Boss von Ebene 0: Troll mit Keule und Kette“ und „Bewegung“. Die Angriffe und ihre Ankündigungen von dort gelten weiter; hier stehen die Einzelheiten.

## Kontext

ADR 0005 legte den Boss von Ebene 0 grob fest: einen Troll mit Keule und Kette, drei Angriffe, eine zweite Phase, danach eine Wendeltreppe. Dazu kam der Luft-Dash in Blickrichtung. Für v0.10 („Boss & Bewegung“) war offen, wie der Kampf im Einzelnen abläuft, wie man ausweicht, was der Boss hinterlässt und wie der Sieg aussieht. Das wurde in einer Grilling-Session geklärt. Bis v0.9 bekam der Bossraum gewöhnliche Gegner (6 Ghule, 3 Fledermäuse), und der Sieg war „Bossraum geräumt“.

## Entscheidung

### Gorm, the Chained

- Der Troll heißt **„Gorm, the Chained“** (Name im Spiel englisch).
- Er kämpft **allein**; der Bossraum bekommt keine anderen Gegner mehr.
- Sobald man den Bossraum betritt, erscheint oben in der Mitte ein großer **Lebensbalken mit Namen**.
- **Größe 3,2 m** (Decke im Bossraum 6 m).
- **Kopf als Schwachpunkt mit ×1,5**. Gut zu treffen ist er, wenn die Keule feststeckt und Gorm sich vorbeugt.

### Startwerte (in `balance.json`, nach dem Anspielen anzupassen)

| Wert | Start |
|---|---|
| Leben | 1600 |
| Laufen | 2,5 m/s |
| Hammerschlag | 35 Schaden, Schockwelle bis 6 m |
| Rundumschwung | 25 Schaden, Reichweite 4 m |
| Kettenwurf | 15 Schaden, zieht auf 3 m heran |
| Keule steckt fest | 2 s, doppelter Schaden |
| Pause nach jedem Angriff | 1,5 s |
| Phase 2 (ab 50 % Leben) | alles 30 % schneller; pro Hammerschlag 4 Steine à 20 Schaden |

### Verhalten

- Gorm geht langsam auf den Spieler zu und wählt den Angriff nach Abstand:
  - **nah (bis 4 m):** Hammerschlag oder Rundumschwung, je 50 %;
  - **4–15 m:** Kettenwurf.
- Nach jedem Angriff **1,5 s Pause** (in Phase 2 kürzer). Nie zweimal hintereinander derselbe Angriff.

### Angriffe und Ausweichen

Jeder Angriff hat **eine** Antwort:

| Angriff | Ankündigung | Ablauf | Ausweichen |
|---|---|---|---|
| **Hammerschlag** | Keule hoch, **roter Kreis am Boden**, 0,8 s | Schockwelle als Ring am Boden: 8 m/s, 0,4 m hoch, bis 6 m | **springen**: wer in der Luft ist, wenn der Ring ankommt, bleibt heil |
| **Rundumschwung** | Kette wird angedreht, 0,6 s | Kette rundherum auf Brusthöhe, 4 m | **ducken** oder **Dodge**; Springen hilft nicht |
| **Kettenwurf** | Haken glüht rot, 0,5 s | Haken fliegt mit 18 m/s geradeaus dorthin, wo der Spieler beim Wurf stand; Säulen und Wände halten ihn auf. Trifft er, zieht er den Spieler in 0,4 s auf 3 m heran, ohne Kontrolle | **zur Seite** oder hinter eine Säule |

- **Keule steckt fest:** Nur wenn der Hammerschlag **niemanden trifft**, steckt die Keule 2 s fest. Gorm beugt sich vor (Kopf tiefer), die Keule glüht, er nimmt **doppelten Schaden**.
- **Phase 2 ab 50 %:**
  - Gorm brüllt 1,5 s (Kopf in den Nacken, kurzes Beben im Bild) und wird danach 30 % schneller.
  - Nach jedem Hammerschlag fallen **4 Steine** von der Decke, bis 5 m um den Spieler.
  - Jeder Stein wirft **1 s vorher einen dunklen Schatten** und trifft dann mit Radius 1 m und 20 Schaden.

### Aussehen (Platzhalter bis v0.13)

- Kästen wie bei den Ghulen, mit Code bewegt.
- Graugrüne Haut und Lumpen; in der rechten Hand eine Holzkeule mit Eisenbändern, in der linken eine rostige Kette.
- Alle Ankündigungen sind deutlich sichtbar, siehe Tabelle oben.

### Sieg

- Gorm hinterlässt einen **Silberhaufen (100–150)** und **eine Blutphiole**. Boss-Beute mit Stufen (ADR 0006) kommt ab v0.11 dazu.
- In der **Mitte des Bossraums** fährt in 3 s eine **Wendeltreppe** aus dem Boden.
- **„[F] Climb the stairs“** nimmt sie, danach erscheint der **Siegbildschirm**. Mit F, damit man nicht aus Versehen gewinnt, bevor man die Beute aufgehoben hat.
- **Sieg in 1.0** = Gorm besiegt und Treppe genommen. Bisher war der Sieg „Bossraum geräumt“; das entfällt.

### Bossraum

- Es bleibt bei **einer Vorlage**, `boss/great-hall.txt`.
- Die vier Säulenblöcke bleiben als Deckung gegen den Kettenwurf; die Mitte bleibt frei für die Treppe.
- Statt der Plätze `G`/`B` gibt es **einen Platz für Gorm**.

### Statistik

Endbildschirm und `runs.log` zeigen zusätzlich **„Gorm slain“** und die **Dauer des Kampfs gegen Gorm** (Grundlage für die Balancing-Runde 2).

### Luft-Dash in Blickrichtung

- **W + Shift in der Luft** dasht dorthin, wohin man schaut, auch schräg nach oben.
- **A/S/D + Shift** dashen wie bisher flach in Laufrichtung.
- Strecke (4 m) und Ausdauer wie bisher, **einmal pro Sprung**. Stößt man an die Decke, endet der Dash dort.

### Reihenfolge in v0.10

1. Startraum mit einer Tür (#158)
2. Blutphiolen auf der Karte (#159)
3. Luft-Dash in Blickrichtung
4. Spawn-Budget (#157)
5. Gorm-Grundgerüst: Körper, Leben, Balken, Laufen, Tod, Treppe, Sieg, Beute, Statistik
6. Gorms drei Angriffe
7. Phase 2 und Steine
8. Balancing-Runde 2 und Release 0.10

## Konsequenzen

- **Positiv:** Jede Ebene bekommt einen Höhepunkt. Jeder Angriff fragt eine andere Bewegung ab (springen, ducken, zur Seite), so wird das Bewegungssystem zum Kern des Bosskampfs.
- **Positiv:** Die Säulen im Bossraum geben dem Kampf Raum für Taktik (Deckung gegen den Kettenwurf).
- **Negativ:** Der Sieg hängt an einem neuen, großen Gegner mit eigener KI. Bis Gorm fertig ist, gibt es keinen Sieg mehr, weil der Bossraum leer ist. Die Issues 5–7 kommen deshalb direkt nacheinander.
- **Negativ:** Viele neue Zahlen. Ohne Anspielen und Balancing-Runde sind sie nur geschätzt.

## Nachtrag (16.11.2026, #181): Kettenschwung als Bogen

Nach dem Anspielen von Phase 2 war der Rundumschwung „praktisch unberechenbar“: Er traf überall gleichzeitig im Moment, in dem die Ankündigung endete. Jetzt streicht die Kette nach der Ankündigung in **0,6 s** (`swingTime`) in einem **Halbkreis** (`swingArc` = 180°) von Gorms Linker über vorne nach rechts. Getroffen wird, wer im Moment des Vorbeistreichens in Reichweite und auf Brusthöhe steht.
- **Antworten:** Dodge, wenn die Kette ankommt, oder hinter Gorm laufen.
- **Reichweite:** Die Werte aus der Tabelle oben wurden in #174 verdoppelt (8 m).
