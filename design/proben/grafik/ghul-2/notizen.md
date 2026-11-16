# Ghul, zweiter Entwurf – Probestück 3 (2026-11-16)

Der Ghul neu: ein ausgemergelter Mensch auf allen vieren, im PS1-nahen Stil aus [`design/grundlagen/grundlagen-grafik.md`](../../../grundlagen/grundlagen-grafik.md) (Nachtrag a bis d), mit Textur. Alles erzeugt [`ghul2.py`](ghul2.py): Geometrie, UVs, Textur, Export und Bilder.

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/ghul-2/ghul2.py
```

Laufzeit etwa eine Minute. Die Zwischenbilder der Render-Durchgänge landen im Temp-Ordner (`%TEMP%\ghul2_passes`), nicht im Repository.

## Bilder

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | 640 × 640, Ruhehaltung, Textur ohne Filterung × Licht je Ecke, kein Nebel |
| `spiel_4m_ruhe_nebel.png`, `spiel_4m_ausholen_nebel.png`, `spiel_10m_ruhe_nebel.png` | Spielbedingungen mit dem **heutigen Nebel** (Formeln aus `level.fs`, je Pixel) |
| `spiel_4m_ruhe_dither.png`, `spiel_4m_ausholen_dither.png`, `spiel_10m_ruhe_dither.png` | dieselben Szenen mit **Sichtgrenze per Dithering** und 15-Bit-Farbe |
| `vergleich_alt_neu_4m.png` | links der erste Ghul (zweite Fassung, Blaugrau), rechts der neue; Ausschnitt aus 4 m mit Nebel, dreifach vergrößert |
| `ghul2_textur.png` | die Textur selbst, 128 × 128 |

**Spielbedingungen:** 854 × 480, 70° vertikal, Augen auf 1,6 m, 4 m bzw. 10 m entfernt, Blick auf 0,5 m Höhe. Der Gang ist wie bisher einfarbig, nicht texturiert.

**So entstehen die Bilder:** Gerendert wird mit Cycles in drei Durchgängen – Texturfarbe, Licht je Ecke, Entfernung je Pixel. Ein Abtastpunkt pro Pixel, ohne Kantenglättung. Danach werden die Durchgänge in numpy zusammengesetzt: `Textur × Licht × Laterne(d)`, dann entweder mit Nebel gemischt oder gerastert. Laterne und Nebel werden hier **je Pixel** gerechnet wie im Shader des Spiels, nicht mehr je Fläche wie bei den ersten Probestücken.

**Die Dither-Fassung im Einzelnen:**

- Farbe auf 5 Bit je Kanal, mit geordnetem 4 × 4-Muster (Bayer).
- Ein Pixel fällt weg, sobald der Ausblend-Anteil die Musterschwelle übersteigt. Es wird keine Nebelfarbe beigemischt; nur die Laterne dunkelt ab.
- Das Ausblenden läuft linear von **9 m bis 18,2 m**. Mit der Nebelkurve (ab 3,25 m) fehlten aus 10 m schon 43 % der Pixel, und die dünnen Glieder zerfielen.

## Was der Ghul jetzt ist

- **Gestalt:** Ein Mensch von etwa 1,5 m, abgemagert, auf Händen und Knien mit aufgestellten Zehen.
  - Der Rücken ist zum Buckel gekrümmt.
  - Der Hals ist lang und dünn; der kleine Kopf (rund 0,22 m) schaut zum Spieler hoch und ist 10° zur Seite geneigt.
  - Der Kiefer hängt schief offen.
- **Körper:**
  - Rippenkorb mit Rippen an den Flanken, eingeschnürte Taille.
  - Die Wirbelsäule ist ein Grat in der Geometrie und eine Knotenreihe in der Textur; die Schulterblätter stehen in beidem vor.
  - Lange Unterarme, große Hände mit fünf gekrümmten Fingern, die sich in den Boden krallen.
  - Blaugraue Lumpen um die Hüften, ausgefranst, ein Fetzen hängt tiefer.
- **Gesicht:**
  - Schmale, schräge, tiefe Augenhöhlen (eingedellt und dunkel gemalt) mit winzigem trübem Augapfel.
  - Hohle Wangen, zwei Nasenschlitze.
  - Die Oberlippe ist weggefault: unregelmäßige Zähne, dunkles Zahnfleisch.
  - Strähnige Haarreste.
- **Asymmetrie:** Der linke Arm greift 5 cm weiter vor; dazu Kopfneigung, schiefer Kiefer und ein ungleicher Saum.
- **Ausholen** (`spiel_4m_ausholen_*.png`): Er richtet sich auf, der Rumpf dreht 34° um die Hüfte. Beide Arme gehen weit über den Kopf, um 120° und 128°, also leicht ungleich. Das Maul reißt auf. Die Silhouette springt von niedrig-breit zu hoch-schmal mit Krallen über dem Kopf.

## Zahlen

**1 024 Dreiecke:**

| Teil | Dreiecke |
|---|---|
| Rumpf mit Hals und Lumpen | 256 |
| Kopf | 120 |
| Kiefer | 36 |
| Oberarm | je 36 |
| Unterarm mit Hand und fünf Fingern | je 162 |
| Oberschenkel | je 36 |
| Unterschenkel mit Fuß | je 72 |

Die Hände sind mit zusammen 324 Dreiecken der teuerste Posten. Sie tragen aber viel von „war einmal ein Mensch“.

**Maße in Spiel-Achsen**, aus der exportierten Datei gelesen:

- **Höhe:** höchster Punkt 0,935 m (Scheitel), Rücken bis 0,80 m – passt zu `Height = 1`.
- **Kopf und Kopfzone:** Kopf mit Kiefer von 0,70 bis 0,935 m hoch, 0,14 m breit, von 0,23 bis 0,43 m vor der Körpermitte. Das liegt in der Kopfzone (ab 0,68 m, `HeadWidth` 0,3, `HeadFrontStart` 0,19). Der Kopf ist schmaler als die Zone. Das ist gewollt, weil der Kopf klein sein soll. Die Zone könnte enger werden, das ist aber eine Spielregel.
- **Über den Radius hinaus:** Seitlich reichen die Ellbogen bis 0,34 m, also innerhalb von `Radius = 0,4`. Nach vorn greifen die Krallen bis 0,65 m, nach hinten reichen die Zehen bis 0,43 m. Wie beim ersten Ghul ragen Hände und Füße über den Radius.
- **Unter dem Boden:** Finger- und Zehenspitzen liegen 2 cm unter dem Boden, damit sie sich sichtbar „hineinkrallen“. Im Spiel verdeckt der Boden sie; bei einer Stufe könnten sie durchstechen.

## Textur

| Was | Wert |
|---|---|
| Größe | 128 × 128, eine für die ganze Figur |
| Farben | 32 (Palette per K-Means), keine Filterung (im glb als NEAREST markiert) |
| Kopf und Kiefer | 64 × 64 von 128 × 128 = ein Viertel der Fläche |
| Rumpf | 64 × 64 |
| Gliedmaßen | je 32 × 48; links und rechts teilen sich dieselben Bereiche |
| Hände, Finger, Füße, Lumpen, Hals | Streifen von 16 Pixeln Höhe |

**Wie die Textur entsteht:**

1. **UVs entstehen beim Bauen:**
   - Jedes Teil ist ein Loft aus Ringen; u läuft rundherum (0 unten, 0,5 oben auf dem Rücken), v entlang des Teils.
   - Jeder Teiltyp bekommt ein festes Rechteck im Atlas, mit einem halben Pixel Rand gegen Überbluten.
   - Die Nähte liegen auf Bauch, Kinn und Hinterkopf.
   - Keine automatische Abwicklung.
2. **Gemalt in 512 × 512 mit numpy, dann auf 128 × 128 verkleinert.** Bausteine:
   - Wertrauschen in zwei Größen für Hautunruhe.
   - Oliv-braune Flecken über einer Schwelle; kein Rot, das ist reserviert.
   - Ein Verlauf rundherum: Licht von oben, Unterseite dunkler.
   - Ein Verlauf entlang: Gelenke dunkler.
   - Weiche Ellipsen für Augenhöhlen, Wangen und Schulterblätter.
   - Streifen für Rippen, Wirbel, Zähne und Stofffalten.
   - Fein gestreiftes Rauschen für Haarsträhnen.
   - Schmutz an Händen, Füßen und Becken; Nägel an den Fingerspitzen.
3. **Palette auf 32 Farben reduziert**, wie eine Textur mit Farbtabelle.

**Farben** (Grundtöne vor Rauschen, Licht und Palette, sRGB):

| Farbe | RGB |
|---|---|
| Haut | 126, 134, 112 |
| Haut über Knochen | 164, 164, 136 |
| Flecken | 92, 96, 66 |
| Schatten | 40, 34, 38 |
| Höhlen | 22, 16, 20 |
| Zähne | 172, 160, 118 |
| Zahnfleisch | 74, 56, 54 |
| Augapfel | 176, 172, 136 |
| Haar | 38, 32, 30 |
| Nägel | 120, 108, 76 |
| Schmutz | 70, 60, 50 |
| Lumpen | 84, 100, 118 (Blaugrau, wie angenommen) |

## Teile und Drehpunkte

Elf starre Teile, Ursprung jeweils der Drehpunkt (Spiel-Achsen). Sie hängen aneinander; im glb sind sie flache Knoten in Ruhehaltung, die Kette steht hier:

| Teil | Drehpunkt | hängt an |
|---|---|---|
| `torso` (mit Hals und Lumpen) | 0, 0,56, −0,24 (Hüfte) | – |
| `head` | 0, 0,68, 0,24 (Halsansatz) | torso |
| `jaw` | 0, 0,76, 0,33 (Kiefergelenk) | head |
| `upper_arm_px`, `upper_arm_nx` | ±0,165, 0,68, 0,17 (Schulter) | torso |
| `forearm_px`, `forearm_nx` (mit Hand) | ±0,30, 0,40, 0,22 (Ellbogen) | Oberarm |
| `thigh_px`, `thigh_nx` | ±0,095, 0,55, −0,24 (Hüftgelenk) | – |
| `shin_px`, `shin_nx` (mit Fuß) | ±0,15, 0,09, 0 (Knie) | Oberschenkel |

Alle Drehungen laufen um die x-Achse. Die Posen stehen als Winkeltabellen im Skript (`REST`, `WIND_UP`); `pose()` zeigt, wie sie zusammengesetzt werden: Eltern-Matrix × Verschiebung zum Drehpunkt × Drehung.

**Kriechen** bräuchte im Code Oberarm, Unterarm, Oberschenkel und Unterschenkel. Das sind vier Winkel je Seite statt heute einem; der Platzhalter dreht nur ganze Arme.

## Vorschläge (nicht umgesetzt)

1. **Gelenke:**
   - **Was die starren Teile leisten:** Die Gelenkkugeln am Ende der oberen Teile (Ellbogen, Knie) verdecken die Fugen in der Ruhehaltung gut.
   - **Wo sie an Grenzen stoßen:** Beim Ausholen drehen die Oberarme um 120° gegen den starren Rumpf. Aus 4 m fällt das kaum auf, aus der Nähe würden Lücken und Durchdringungen an den Schultern sichtbar. Eine Nahansicht habe ich nicht gerendert.
   - **Mein Vorschlag:** Ein einfaches Skelett nur für Schultern und Hüften (Verformung mit festen Gewichten, je Ecke ein oder zwei Knochen). Die Winkel weiter per Code, wie heute. Das ist der Mittelweg aus Abschnitt 6 der Grundlagen und bräuchte die Entscheidung des Menschen: ADR, Art Bible „keine Skelett-Animation“.
2. **Sichtgrenze:**
   - **Was mir die Bilder zeigen:** Die Dither-Fassung hält den Ghul aus 10 m klarer zusammen als der Nebel (`spiel_10m_ruhe_dither.png` gegen `spiel_10m_ruhe_nebel.png`). Der Nebel dunkelt ihn dort schon stark ab, das Dithering lässt ihn hell, bis er zerfällt. Dafür ist das Muster auf nahen Wänden sehr unruhig.
   - **Zu entscheiden:** Die Wahl zwischen Nebel und Dither trifft der Mensch, ebenso die Startweite (hier 9 m).
3. **Level im selben Stil:** Solange Wände und Boden einfarbig sind, wirkt der texturierte Ghul wie aus einem anderen Spiel. Als Nächstes sollte ein Stück Wand mit 64-×-64-Steintextur geprobt werden.
4. **Mipmaps:** Der glTF-Exporter schreibt für die Verkleinerung „nächster Texel aus nächster Mipmap“. Beim Laden in Raylib sollten keine Mipmaps erzeugt und der Filter auf `TEXTURE_FILTER_POINT` gesetzt werden.

## Vom Modell ins Spiel

Wie angenommen: glb, +Y oben, Ursprung zwischen den Füßen.

**Neu:**

- **Textur:** steckt in der Datei (PNG, eingebettet), dazu eine UV-Ebene. Die Datei hat keine Vertex-Farben mehr.
- **Was das Spiel rechnen muss:**
  - Licht je Ecke aus der Welt-Normale (Gouraud; die stufenlose Mischung der sechs Spiel-Helligkeiten),
  - mal Textur,
  - dann Laterne und Nebel oder Dither.

  Dafür braucht es einen Shader mit Texturkoordinaten und Normalen; der heutige Level-Shader hat beides nicht.
- **Farbraum:** Die Textur ist ein normales sRGB-PNG; die Umrechnung linear → sRGB, die bei Vertex-Farben nötig war, entfällt hier.

## Wo ich unsicher bin

- **Aus 4 m von vorn wirkt er spinnenhaft.** Man sieht vor allem vier lange Stangen – zwei Arme, dahinter zwei Oberschenkel – und den Kopf. Der Rumpf liegt hinter dem Kopf und ist aus Augenhöhe kaum zu sehen. Er liest sich als dürre Gestalt, aber „Mensch“ erkennt man vor allem am Gesicht und an den Händen. Seitlich und schräg ist er klar ein kriechender Mensch.
- **Gesicht aus 4 m:** Augenhöhlen, Zähne und Haar sind als dunkle Flecken da, aber klein (Kopf etwa 20 Pixel hoch). Der Kopf ist nicht mehr der helle Zielpunkt wie beim ersten Ghul. Die Haut des Schädels ist zwar heller, aber nicht weit. Ob das für Kopftreffer reicht, sollte der Mensch im Spiel prüfen.
- **Insgesamt dunkler als der alte Ghul:** Gemalte Schatten und Licht je Ecke multiplizieren sich. Das ist stimmungsvoll, aber an der Grenze zu „verschwindet im Boden“. Wenn der Rumpf zu dunkel ist, wäre der erste Schritt, die gemalten Schatten zu schwächen, nicht die Grundfarbe aufzuhellen.
- **Gefahr Spinne:** Die vier gleich langen, gleich dicken Stangen sind das Bestien-Merkmal, das der Mensch nicht will. Gegenmittel für die nächste Runde: Oberschenkel kürzer und dicker als die Arme, Knie weiter unter dem Bauch; die Oberarme bleiben eng am Körper.
- **Rückseite:** Das Becken endet in einer runden Kappe, die von hinten wie eine Glatze wirkt. Eine Lumpenfalte darüber würde das beheben.
