# Gorms Schlag, Welle am Boden (Issue #299), Probestück mit zwei Entwürfen

## Was das Spiel heute tut (nachgesehen, nichts davon ändert sich)
- **Auslösen:** Beim Hammerschlag (`GormAttack.Hammer`) endet der Anlauf (`hammerWindUp` 0,8 s, dabei ein roter Warnkreis bis zur Reichweite der Welle, `GormFloorEffects`), dann setzt `Gorm.Release` die `Shockwave` (Mittelpunkt = Gorms Position, Radius 0).
- **Ausbreitung:** Der Radius wächst um `shockwaveSpeed` = 8 m/s je Sekunde bis `shockwaveRadius` = 12 m, also **1,5 s** Dauer; dann verschwindet sie. In Phase 2 fallen zusätzlich Steine.
- **Trefferzone:** `Gorm.RunShockwave`: Wer vom Rand der wachsenden Scheibe erreicht wird (flache Entfernung ≤ Radius), wird **einmal** getroffen, **wenn er niedriger als `shockwaveHeight` = 0,4 m über dem Boden ist** (Springen rettet), Schaden `hammerDamage` = 40.
- **Zeichnung:** `GormFloorEffects.Draw` → `Ring(...)`: `max(12, Radius · 10)` Kästen entlang des Kreises (0,12 m breit, **0,35 m hoch**, Länge = Bogenlänge), einfarbig (220, 210, 190), Radius mindestens 0,3 m. Nur der **Rand** wird gezeichnet, nicht die Fläche dahinter.

## Bewegungsbilder (`tools/clip.ps1 -Sheet`, 16 Bilder gleichmäßig über 2 s, Anlauf-Ende bis Ende der Welle; Wandkacheln der Palette A)
Einzelbild-Reihenfolge von links nach rechts, zeilenweise; die Spieler-Anzeige zeigt auch den Treffer (roter Bildschirmrand).

| Datei | Inhalt |
|---|---|
| `heute_gorm-slam.png`, `welle_a_gorm-slam.png`, `welle_b_gorm-slam.png` | Spieler 8 m von Gorm, Blick auf Gorm: Welle kommt auf den Spieler zu und trifft |
| `heute_gorm-slam-far.png`, `welle_a_gorm-slam-far.png`, `welle_b_gorm-slam-far.png` | Spieler 14 m entfernt: die Welle läuft als ganzer Bogen über den Boden und erreicht ihn nicht (12 m) |

**Es gab keine Bewegungs-Szene für den Schlag.** Ich habe zwei lokale gebaut (`patch_lokal8.py` schreibt sie nach `MotionScenes.cs`): `gorm-slam` (8 m) und `gorm-slam-far` (14 m): Gorm im 6-m-Saal (`Arena('T', …)`), die Szene tickt ohne Eingabe bis zum Anlauf des Hammers (85 % des Anlaufs), setzt den Spieler dann `metres` von Gorm entfernt zurück (Gorm läuft während des Wartens auf ihn zu), und filmt 120 Ticks. **Die Code-Sitzung kann beide Szenen übernehmen** (kleine Methode `SlamScene`, siehe Patch); sie sind nicht eingecheckt.

## Entwurf A: aufbrechender Stein mit Staub (`welle_a.glb`, Knoten `shard` 48 und `puff` 48 Dreiecke)
Entlang des Randes werden Gruppen aus drei schräg nach außen aufgeworfenen **Steinplatten** gesetzt (Wandkachel der Variante a, Höhe 0,36 bis 0,56 m, mal 0,75 bis 1,25), mit einem **roten Riss** (Blutrot, zwei Stufen) am Fuß; dahinter zwei Reihen **Staubhaufen** (Stein-Rampe, hellste Stufen), niedriger werdend. Akzent Blutrot nur als Riss (Gefahr, nie Zierde).
**Was nicht stimmt:** Aus 14 m (`welle_a_gorm-slam-far.png`) ist der Rand **schwächer** als heute: Der Stein hat nur Wandhelligkeit, der Staub (höchstens 151/127/137) ist dunkler als das heutige Hellbeige (220/210/190); der rote Riss trägt als feiner Faden, aber nicht den Umriss. Mit Stein allein bekommt man die Helligkeit der Rampe nicht. Aus 8 m wirkt der Ring wie ein aufgebrochener Boden, gut, aber unruhig; die Platten stehen gleichmäßig (Wiederholung) und sind ohne Bewegung der Textur. Dreiecke je Ring bei Radius 12: etwa 120 Gruppen mal 3 Teile mal 48, also über 17 000: die CPU-Transformation je Teil (`DrawPart`) ist **nicht gemessen**; ein Ring aus weniger, größeren Gruppen wäre ein Fall für den Code.

## Entwurf B: Druckwelle mit hellem Rand (`welle_b.glb`, Knoten `band`, 4 Dreiecke)
Ein **Keil** je Ringstück (1 m Kreislänge, auf die Bogenlänge skaliert): vorn ein senkrechter **heller Rand** (Knochen-Reihe, wie das Hellbeige von heute, 0,35 m hoch), dahinter eine nach innen abfallende Fläche (0,86 m tief) in **Blutrot-Stufen**, hell am Rand, innen dunkel, 2 × 2 gerastert. Rand und Körper sind genau die Stufen der Palette; Blutrot trägt die Gefahr, der helle Rand die Lesbarkeit.
**Gut:** Rand und Ausbreitung sind **mindestens so deutlich wie heute** (der Rand hat dieselbe Helligkeit und Höhe, die rote Fläche dahinter ist aus 8 m ein klar lesbarer Ring); der Ring ist mit 4 Dreiecken je Stück billig (bei Radius 12 etwa 480 Dreiecke).
**Was nicht stimmt:** Die rote Fläche hinter dem Rand zeigt eine **Zone von 0,86 m** als gefährlich, die es nicht ist (die Trefferzone ist die Scheibe bis zum Rand, die Welle ist an der Stelle schon vorbei, wo sie rot ist); sie **liest sich wie Blut** (stärkster Akzent von allen). Wenn die Fläche nur als dünner, schnell verblassender Schweif gedacht ist, sollte sie schmaler werden (0,4 m). Textur ohne Bewegung: kein Laufen, kein Ausfransen (nach Issue mit dem Animationsdesigner abzustimmen); denkbar ist ein UV-Versatz je Tick oder zwei wechselnde Muster (zwei Texturfelder, nach Radius gewählt).

## Empfehlung zur Entscheidung
Für die **Lesbarkeit** ist B die sicherere Fassung (Rand wie heute, Akzent mit Zweck); A sieht „körperlicher“ aus, verliert aber an Helligkeit aus der Ferne. Eine Mischung liegt nahe: Rand aus B, dahinter nur wenige Staubhaufen aus A.

## Was die Code-Sitzung wissen muss
- Zeichnen: `GormFloorEffects.Draw` setzt statt `Ring(...)` die Bausteine auf den Kreis (Platz und Drehung wie `Ring`: lokale x-Achse radial nach außen, z am Kreis entlang, Ursprung am Boden; für B `Matrix4x4.CreateScale(1, 1, Bogenlänge)`, für A Gruppen mit Höhe 0,75 bis 1,25 nach einem Zählerwert und Staub um 0,55 und 1,1 m zurückversetzt). Die Probe dazu ist `DrawWaveProbe` in `patch_lokal8.py`. Rückfall auf den Kasten-Ring bei fehlender Datei (Prüfsummen, `HERKUNFT.md`).
- Einzelstücke eines Modells brauchen die Skalierung in der Zeichenmatrix (`TexturedModel.DrawPart(frame)` nimmt eine beliebige Matrix).
- Skript `welle.py` (Blender); liest die Wandkachel der Variante a und die Reihen `Stein`, `Blutrot`, `Haut/Knochen` aus `assets/palette/hauptpalette.json`; Textur 128 × 128, 19 Farben.
- Der **Warnkreis** vor dem Schlag ist unverändert (nicht Teil dieses Auftrags).

## Fotowerkzeug (nichts in `src/` oder `assets/`)
`patch_lokal8.py` (Szenen, Wellen-Zeichnung, Wandvariante), `filme9.py` (Exe direkt, nur im `bin`-Ordner getauscht; `tools/clip.ps1 -Sheet`), `welle.py`.
