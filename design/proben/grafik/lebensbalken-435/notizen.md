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

# Runde 2 (Urteil: Layout B gewählt, aber im neuen HUD-Design; Bezug #412)

Blätter: `blatt-lebensbalken-runde2.png` (2400 x 2120) und `blatt-schmuck-runde2.png` (2800 x 2830), Einzelbilder `runde2-hoechst-1920.png` und `runde2-hoechst-854.png`. Neu erzeugen mit `lebensbalken_runde2.py` (gleicher Blender-Aufruf wie oben); zwei Läufe sind bitgleich.

## Warum das erste Blatt wie das alte Design aussah

Runde 1 las `assets/hud/hud_parts.png` (der einfache Atlas für das 854er Raster). Dessen Rahmen haben Giebel, Fialen und Eckzacken. Das Spiel zeichnet aber seit #409 überall den Atlas in doppelter Auflösung (`hud_parts_x2`, 1708 x 960): glatte Metallrahmen als Relief, sechs Silberstufen im Rahmen, ohne Giebel. `AtlasChoice.Pick` nimmt `_x2`, sobald beide Dateien da sind, bei jeder Fenstergröße. Mein Blatt hätte den x2-Atlas lesen müssen. Runde 2 tut das. Merkpunkt: Proben zum HUD immer aus `_x2` bauen, nicht aus dem einfachen Atlas.

## Layout B im neuen Design

- Raster 1708 x 960: 2 px je Lebenspunkt (1 px bei 854 x 480, etwa 2,25 px bei 1920 x 1080), ein Strich je Upgrade alle 10 px, jeder fünfte lang. Start 100 = 200 px, Mittel 150 = 300 px, Höchst 235 = 470 px.
- Fassung des Lebens: `frame_xp` um 8 Zeilen höher (Balken 28 statt 20), Breite = Balken + 20, Kappen links und rechts, Mitte gekachelt. Das neue Design hat keine Giebel mehr; die Endkappe aus Runde 1 ist hier der rechte Abschluss des Rahmens, der mit dem Balken wandert.
- Obergrenze im Entwurf 244 Lebenspunkte (Rahmen endet 12 px vor dem Boss-Rahmen bei x 592); gezeigt 235.
- Stufe auf der Mitte des Stapels, Fähigkeiten in zweiter Zeile, Erfahrung 112 px; Ausdauer unten in der Mitte in einer Fassung, die 46 statt 76 hoch ist (`frame_vitals` ohne die Zeilen 18 bis 47).
- Die Bilder in 854 x 480 nehmen jeden zweiten Pixel des Entwurfs (das Spiel zeichnet dort ebenfalls den x2-Atlas verkleinert). Das Hintergrundfoto ist `basis-854.png`, 2fach vergrößert (nur Hintergrund, nicht Teil der Beurteilung).

## Schmuck der Rahmen (#412, Teil 1: Probe)

Nur die sechs Silberstufen aus dem Rahmen (72, 100, 132, 166, 200, 230), Licht links oben, keine Glättung, keine neue Farbe. Gezeigt am Lebensbalken (Mittel und Höchst) und an Karte und Ausrüstung (aktiver und ruhender Reiter), je neben dem heutigen schlichten Rahmen.

- **S1 Nieten:** runde Niete an jeder Ecke; am Lebensbalken zusätzlich an den langen Strichen (jedes fünfte Upgrade) auf dem unteren Rand, an Reitern alle etwa 80 px. Wenig Bild, ruhig; die Nieten am Lebensbalken sind zugleich eine Zählhilfe.
- **S2 Eckwinkel:** vier Winkel mit Licht und Schatten und einer Niete im Knick. Deutlichste Rahmung, wirkt aber schwer.
- **S3 Giebel:** dreistufiger Giebel mit Spitzbogenfenster; am Lebensbalken auf dem Ende (wandert mit), an Reitern zwei kleine, damit die Mitte für die goldene Marke frei bleibt. Stärkster Ausdruck, aber der x2-Atlas hat Giebel und Fialen mit Absicht weggelassen (`design/proben/grafik/hud-atlas-402/abweichungen.md`), und er widerspricht Art-Bible-Regel 11 (so wenig Bild wie möglich).
- **Empfehlung S1.** Geringster Bildanteil, keine neue Fläche (die Nieten sitzen im Rand), trägt den Lebensbalken ohne ihn zu überladen und knüpft an die Eck-Nieten der Stufen-Fassung an, die es im x2-Atlas schon gibt. S3 wäre die zweite Wahl nur am Lebensbalken.
- Grenzen: Der Schmuck ist nur auf Lebensbalken und Reiter gezeigt, nicht auf allen Rahmen (Waffen, Gegenstände, Währung, Boss). Beim Einbau braucht es dafür neue Teile im x2-Atlas (der Generator `tools/hud/hud_atlas_doppelt.py` müsste sie als Relief aus Blender rendern; hier sind es von Hand gesetzte Pixel).

## Entscheidung (nur vermerkt)

Das Leben bekommt eine Obergrenze, vorläufig etwa 235; am Deckel fallen keine Leben-Upgrades mehr. Die Zahl ist ein Vorschlag aus der Bildbreite bei 854 x 480 und noch nicht in `balance.json`; sie ist Sache des Einbaus (Core) und der Balancing-Runde.
