# Lebensbalken über dem XP-Balken (Issue #435, Teil 1: Probe, kein Einbau)

Blatt: `blatt-lebensbalken.png` (2200 x 2640). Neu erzeugen:

```powershell
& "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/lebensbalken-435/lebensbalken.py
```

Grundlage: der echte HUD-Atlas (`assets/hud/hud_parts.png`/`.json`), Foto `hud-calm` ohne HUD (`basis-854.png`, mit `--photo hud-calm`), Schrift A „Kapitell“. Rahmen und Balken werden aus Atlas-Teilen gestreckt (Kappen links und rechts, Mittelstück gekachelt), keine neuen Farben, kein Glühen. Zwei Läufe sind bitgleich. `heute-1280.png` ist das echte Bildschirmfoto (1280 x 720) als Beleg für „HEUTE“; das Blatt zeigt „HEUTE“ in 854 x 480 aus denselben Atlas-Teilen.

## Werte (aus `assets/balance.json`)

- Start-Leben 100, je Upgrade +5 (`boons.health`). Gezeigt: Start 100, Mittel 150 (+10 Upgrades), Höchst 235 (+27 Upgrades, Obergrenze, auf ganze Upgrades abgerundet).
- Das Spiel kennt keine Obergrenze für das Leben; sie ist ein Vorschlag aus der Bildbreite.
- **1 Pixel je Lebenspunkt** in allen Varianten (+5 px je Upgrade). Heute sind es 3,44 px je Lebenspunkt (344 px für 100); der Balken wird also zu Beginn kürzer (100 px).
- **Obergrenze bei 854 x 480:** der Rahmen darf nicht in den Boss-Rahmen laufen (links bei x 296, Abstand 6 px, also Rahmenende x 290). A und B: Rahmen = Balken + 10 px, Obergrenze 244 Lebenspunkte. C: Rahmen = Balken + 16 px, Obergrenze 238. Gemeinsam gezeigt: 235. Bei schmaleren Bildern (768 x 480, Boss-Rahmen bei x 253) sinkt sie auf etwa 200. 0,6 px je Lebenspunkt (3 px je Upgrade) gäbe Spielraum bis rund 400 Lebenspunkte, aber 3 px je Upgrade tragen keine Teilstriche.

## Wirkung auf die übrigen Teile (alle Varianten)

- Stufe: bleibt links, sitzt auf der Mitte des Stapels aus Leben und Erfahrung.
- Erfahrung: bleibt 56 px lang (A, B in eigener Fassung, C unter dem Leben in der gemeinsamen).
- Fähigkeiten: können nicht mehr neben der Erfahrung stehen (der Balken wächst dorthin) und rücken in eine zweite Zeile unter Stufe und Erfahrung. Der Hinweis „[X] to level up“ rutscht darunter. Die linke obere Gruppe ist danach etwa 40 px höher (rund 83 statt 43 px).
- Ausdauer: bleibt unten in der Mitte, die Fassung ist ohne Leben nur noch 24 px statt 38 px hoch (spart 14 px Bild); die Fünf-Segmente-Leiste ist unverändert.
- Silber, Schlüssel, Waffen, Gegenstände: unverändert.
- Maße von Hand mit Abstand 6 gelegt, nicht aus `HudLayout` (Core) gerechnet; das ist Sache des Einbaus.

## Varianten

- **A Schlicht:** eigene schmale Fassung (aus der Erfahrungs-Fassung, 4 Zeilen höher) direkt über der Erfahrung, wächst nach rechts. Einfach, aber das Wachsen sieht man nur am Vergleich.
- **B Endkappe und Teilstriche (Empfehlung):** wie A; der Giebel sitzt nur noch am rechten Ende und wandert mit, je Upgrade ein Teilstrich (alle 5 px), jeder fünfte lang. Man liest die Upgrade-Zahl ab, ohne Zahl im HUD (Art-Bible-Regel 11); die Kappe zeigt den Zuwachs ohne Text.
- **C Glieder in einer Fassung:** Leben und Erfahrung teilen eine Fassung, das Leben besteht aus Gliedern (ein Glied je Upgrade) wie die Ausdauer-Segmente. Nachteil: unruhig, rechts neben der Erfahrung bleibt dunkler Grund leer; Teilstände unter 5 Punkten werden nur spaltenweise sichtbar.

## Offen

- Das Urteil über 1 px je Lebenspunkt und den verkürzten Balken (100 statt 344 px) steht beim Auftraggeber.
- Obergrenze als Spielregel (Core) gibt es noch nicht; ohne sie läuft der Balken beim Boss-Rahmen auf.
- Lesbarkeit bei 1280 x 720 ist hier nur im Entwurfsraster gezeigt; Prüfung im Spiel erst beim Einbau.
