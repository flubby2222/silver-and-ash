# Art Bible – wie Silver & Ash aussieht

Dieses Dokument liest der Grafikdesigner vor jeder Aufgabe. Es hat zwei Teile: den **gewünschten Stil** und die **gelernten Regeln**. Jede Ablehnung eines Probestücks wird unten zu einer Regel, damit derselbe Fehler nicht zweimal vorkommt (siehe [ADR 0009](../entscheidungen/0009-leitung-designer-auto-merge.md)).

**Stand:** zweite Fassung vom 2026-11-16. Die erste war geschätzt und hat zu einem Ghul geführt, den der Mensch ablehnte. Diese Fassung beruht auf der Recherche des Grafikdesigners ([Grundlagen Grafik](grundlagen-grafik.md)) und auf den Urteilen des Menschen. Den Stil hat er am zweiten Ghul-Entwurf bestätigt („Ja, Richtung stimmt“). Die Entscheidung dazu steht in [ADR 0010](../entscheidungen/0010-stil-ps1-nah-texturen.md).

**Maßstab** für alle weiteren Figuren ist der Ghul aus Probestück 3 (Ordner `design/proben/grafik/ghul-2/` auf dem Branch des Grafikdesigners, `claude/design-probe-ghul-45a8c4`), in der nachgearbeiteten Fassung.

**Die Art Bible hat Vorrang vor der Recherche.** Wo [Grundlagen Grafik](grundlagen-grafik.md) etwas anderes sagt, gilt dieses Dokument. Bekannte Abweichungen dort: Abschnitt 5 rechnet noch ohne Texturen; die Sichtgrenze per Punktmuster aus Nachtrag b ist verworfen (ADR 0010: Körnung ja, Nebel bleibt); „400 Dreiecke am oberen Rand der PS1“ ist falsch gerechnet (400 liegt im unteren Drittel von 250 bis 750); Verweise auf `ghul-2` und einzelne Bilder zeigen auf den Branch des Designers, nicht auf `main`.

## Leitbild

Ein Vampirjäger steigt mit einer Laterne durch Katakomben, Kerker und Schlosshalle hinab. Man sieht nur, was die Laterne erreicht. Die Welt ist alt, kalt und aus Stein. Farbe ist selten und bedeutet immer etwas: Gefahr, Beute, Leben.

Der Look ist **PS1-nah**: um die tausend Dreiecke je Figur, kleine ungefilterte Texturen mit sichtbaren Pixeln, weiche Schattierung über die Ecken. Das Bild darf körnig und etwas unscharf wirken. Die Bildfehler der PS1 – wackelnde Ecken, verzogene Texturen – bauen wir **nicht** nach; sie stören beim Zielen.

**Alles im selben Stil:** Figur, Waffe, Kiste, Wand und Boden folgen denselben Regeln. Ein texturierter Gegner in einem einfarbigen Raum ist ein Stilbruch.

Stimmung als Anhaltspunkt: Castlevania, Devil May Cry, Vampire Hunter D, Hellsing. **Nichts daraus wird übernommen** – keine Figur, kein Name, keine erkennbare Form.

## Was der Mensch gesagt hat

Wörtlich aus dem Gespräch mit der Leitung (Tippfehler berichtigt); ADR 0010 und die Recherche geben dieselben Sätze sinngemäß wieder:

- Zum ersten Ghul (einfarbige Flächen, etwa 350 Dreiecke): „besser als vorher, ja. Allerdings überhaupt nicht, was ich mir vorgestellt habe.“ Falsch waren Gestalt und Proportionen, zu klotzig und zu wenig Detail, flächige Farben ohne Textur, Gesicht und Ausdruck.
- Zum Stil: „Kann gerne einen unscharfen Eindruck machen. Wenn PS1-Style einfacher ist, dann gerne so.“
- Zur Sichtgrenze: Er überlegt, sie „mit einem Dithering-Effekt zu beschränken, statt des schwarzen Mantels … Wenn, dann aber alles im selben Stil.“
- Zum Ghul: ein ausgemergelter Mensch, kriechend.
- Zum zweiten Entwurf (Textur, 1 024 Dreiecke): „Ja, Richtung stimmt.“
- Zum Vergleich der vier Bild-Varianten aus dem Spiel: „2: Körnung mit Nebel“.

## Technischer Rahmen

Diese Werte stehen heute im Code ([`LookSettings.cs`](../../src/SilverAndAsh.Game/Rendering/LookSettings.cs), [`level.fs`](../../assets/shaders/level.fs)):

| Was | Wert | Folge für Modelle |
|---|---|---|
| Bildgröße der 3D-Welt | 854 × 480, hart hochskaliert | Ein Pixel ist bei 4 m etwa 1 cm, bei 10 m etwa 3 cm groß; Details unter zwei Pixeln gehen verloren |
| Nebel | beginnt bei 3,25 m, ab 18,2 m nur noch Nebel | Bei 10 m ist knapp die Hälfte der Farbe Nebel (Anteil etwa 0,43), ab etwa 14 m bleibt fast nur die Silhouette (Richtwert) |
| Nebelfarbe | RGB 16, 11, 20 (violett-schwarz) | Sehr dunkle Modellfarben verschwinden früh |
| Laterne | volle Helligkeit nah, 35 % in der Ferne | Farben wirken im Spiel dunkler als im Probebild |
| Blickwinkel | 70° vertikal, Ego-Sicht, Augen auf 1,6 m | Den Ghul sieht man von vorn und leicht von oben; die Fledermaus (schwebt über Kopfhöhe) und Gorm (über 3 m) sieht man **von unten** – dort muss das Modell gut aussehen |

Die Formeln aus dem Shader, mit Entfernung `d` in Metern zur Kamera:

- **Laterne:** `helligkeit = 1 − 0,65 · min(d / 18,2; 1)`
- **Nebel:** `anteil = smoothstep(3,25; 18,2; d)`
- **Ergebnis:** `farbe = mischen(grundfarbe · helligkeit, nebelfarbe, anteil)`

**Was das Spiel noch nicht kann:** Es zeichnet heute nur einfarbige Flächen und einfache Körper (Quader, flache Scheiben), meist mit einer Helligkeit je Fläche; es lädt keine Texturen und keine Modelle. Texturierte Modelle laden und weich schattieren muss es erst lernen. Bis dahin entstehen Probebilder in Blender.

**Bild und Sichtgrenze** (ADR 0010, entschieden am 2026-11-16): Das 3D-Bild wird auf 15 Bit (5 je Kanal) mit festem 4 × 4-Punktmuster gerastert; **der Nebel bleibt** die Sichtgrenze; die Auflösung bleibt 854 × 480. Probebilder zeigen deshalb Nebel **mit** dieser Körnung: Muster (Bayer) an der Bildschirmposition, nach Licht, Laterne und Nebel angewendet. Die frühere Fassung „Ausblenden über das Punktmuster“ entfällt.

## Regeln für Modelle

| Was | Regel |
|---|---|
| Dreiecke | normale Gegner 800 bis 1 200; große Gegner und Bosse 2 000 bis 4 000; Requisiten 100 bis 400; Waffe im Bild bis 1 500 |
| Textur | eine je Figur, 128 × 128, höchstens 32 Farben; die Waffe im Bild zählt wie eine Figur (128 × 128), weil sie immer nah ist; Requisiten und Wandstücke 64 × 64 mit 16 bis 32 Farben |
| Zusammengesetzte Textur (2026-11-17) | Trägt eine Textur unveränderte vorhandene Kacheln nebeneinander (etwa Wand- und Bodenkachel) und nutzt sonst nur Farben der Hauptpalette, darf sie die Summe der Farben ihrer Teile haben; jede Teilfläche hält die 32 Farben ein. |
| Verteilung der Dreiecke | etwa ein Viertel in Kopf und Hände; Rundungen mit 8 bis 12 Seiten |
| Filterung | keine, auch keine Mipmaps: Texel bleiben harte Quadrate |
| Kopf in der Textur | mindestens ein Achtel der Fläche |
| Dithering in der Textur | keines. Gerastert wird am Ende im Bild, sonst doppelt sich das Muster |
| Schattierung | weich über die Ecken (Gouraud): Helligkeit je Ecke aus ihrer Richtung, über die Fläche verlaufend |
| Licht in der Textur | nur gemalte Tiefe (Rippen, Höhlen, Falten) und Licht von oben; nie die Richtung der Laterne |
| UVs | entstehen beim Bauen im Skript; Nähte auf die Unterseite; spiegelbildliche Teile dürfen sich Textur teilen |
| Bewegung | getrennte Teile mit sinnvollem Drehpunkt, per Code gedreht; keine Skelett-Animation |
| Herkunft | Geometrie, UVs, Textur und Bilder entstehen aus **einem** Skript je Stück; nichts heruntergeladen |
| Werkzeuge | Blender-Python mit den mitgelieferten Modulen (auch NumPy); kein `pip install`, keine weiteren Programme |

## Farben

Die Palette steht in [`assets/palette/hauptpalette.json`](../../assets/palette/hauptpalette.json) (dazu `hauptpalette.gpl` für Bildprogramme); sie entsteht aus [`tools/palette/hauptpalette.py`](../../tools/palette/hauptpalette.py). Sie ist das Ergebnis von drei Vergleichsrunden (Issue [#279](https://github.com/flubby2222/silver-and-ash/issues/279)); der Mensch hat am 2026-11-17 Fassung A aus Runde 3 gewählt. Fertige Texturen und Modelle sollen nur Farben daraus verwenden.

**Urteile des Menschen, 2026-11-17** (Tippfehler berichtigt):

- Zu Runde 1: „Die Paletten ändern auch Player Model und Waffen (dachte, das machen wir nicht?)“
- Zu Runde 2: „Die ersten drei Paletten sehen sehr bunt aus und bilden ein regenbogenartiges Muster aufgrund der Wiederholung der Texturen. Geht das subtiler, homogener? Verglichen mit den ersten dreien ist der ursprüngliche Stand recht gut. Nur weil man so viele Farben hat, muss man nicht gleich alle nutzen. Man kann sich Kontraste, die nicht so gut ins Leitbild passen, für Akzente aufsparen.“
- Zu Runde 3 (Fassung A, B oder heutiger Stand): „A“.

**Regeln daraus:**

1. **Der heutige Stein ist der Maßstab.** Die Reihe *Stein* ist aus ihm gebildet: 15 Stufen, ein Farbton (violettgrau bis rosagrau), gleichmäßig in der Helligkeit; Stufe 0 ist die Nebelfarbe (16, 11, 20), Stufe 14 die hellste (151, 127, 137). Wände, Böden und Decken bestehen nur aus dieser Reihe. Fassung A bedeutet: der Stein sieht im Spiel praktisch aus wie heute, nur liegen seine Farben jetzt in der Rampe.
2. **Eine Fläche bleibt in einer Rampe.** Abwechslung entsteht über Helligkeit (Stufen) und über die Varianten a/b/c, nicht über eingestreute Farbtöne. Warm und kalt in einer Fläche haben mit der Kachelwiederholung (eine Kachel ist 1 m) ein Regenbogenmuster erzeugt. Neue Bausteine malen ihren Stein mit der Rampe, nie mit einem eigenen Farbton.
3. **Akzente aufsparen.** Die Reihen *Holz*, *Eisen*, *Messing/Gold* und *Blutrot* sind **Akzent**: nur kleine Flächen mit Zweck (Beute, Türen, Gefahr, später Props und Licht), nie Wand, Boden oder Decke. Dass die Palette mehr Farben kann, heißt nicht, dass jede Szene sie nutzt. *Grund*-Reihen (Stein, Haut/Knochen, Stoff) dürfen Flächen füllen.
4. **Die Palette gilt zunächst für die Umgebung.** Spielfigur, Waffen, Ärmel, Gegner und Kisten bleiben unverändert, bis der Mensch anderes entscheidet. Vergleiche ändern daran nichts.
5. **Je Ebene eine eigene Stein-Reihe mit demselben Aufbau** (15 Stufen, ein Farbton); Kerker und Schlosshalle folgen später. Die Reihen für Blutrot und die Stufenfarben der Beute sind in allen Ebenen gleich.
6. **Werkstein nur für besondere Portale** (Urteil des Menschen am 2026-11-17, aus dem Bild der Türrahmen-Probe gewählt: das Bosstor in der „blauen“, hellen Fassung; Issue [#303](https://github.com/flubby2222/silver-and-ash/issues/303)). Die Akzent-Reihe *Werkstein* hat 4 Stufen (171, 159, 164 bis 209, 193, 200): der Stein der Mauer, zum Grau gemischt und heller als jede Stein-Stufe – behauener, gesetzter Stein. Sie gehört den Bogenläufen, Kämpfern und Sockeln des Bosstors und künftiger besonderer Portale, **nie einer Wandfläche** und keinem gewöhnlichen Durchgang. Holz- und Eisentür tragen ihre Rahmen in der Stein-Reihe; ihr Werkstein ist nur der hellere Teil der Stein-Rampe (Stufen 4 und mehr über der Mauer).

**Aufbau der Datei:** `grund` und `akzent` mit je Reihe Name, Farben und Verwendung bzw. Zweck; `katakomben` ist die Auswahl der Katakomben-Texturen (Stein, Holz 1–4, Eisen 1–3, Haut/Knochen 6–7, 24 Farben); `stufenfarben` sind die festen Ankerpunkte der Beute. Die Reihen außer Stein haben 8 Stufen (dunkel nach hell, Farbton-Verschiebung), Methode wie bei Slynyrd „Pixelblog 1: Color Palettes“.

**Stufenfarben der Beute** (festgelegt in [`ChestView.cs`](../../src/SilverAndAsh.Game/Rendering/ChestView.cs) und [`TierColours.cs`](../../src/SilverAndAsh.Game/Rendering/TierColours.cs); die Palette übernimmt sie genau, Stand des Codes 2026-11-17):

| Stufe (Kiste) | Reihe | Farbe (RGB) |
|---|---|---|
| Worn (Beggar) | Holz, Stufe 3 | 110, 76, 46 |
| Forged (Guard) | Eisen, Stufe 4 | 92, 112, 140 |
| Royal (King) | Messing/Gold, Stufe 5 | 214, 170, 64 |
| Vampiric (Vampire) | Blutrot, Stufen 3 und 5 | 150, 20, 30 bis 200, 30, 40 (pulsiert) |

Abweichung: Die dunklen Bänder der Kistenplatzhalter im Code (56, 44, 36; 52, 58, 70; 140, 100, 36; 40, 10, 14) liegen nicht in der Palette. Sie gehören dem Platzhalter ohne Modell; die Kistenmodelle bleiben ohnehin unverändert (Regel 4).

**Figuren und Gegenstände** müssen sich vom Stein abheben:

- Untote: **kränklich, kalt, etwas heller als die Wand** (Haut um 124, 132, 112).
- **Rot ist reserviert** für Blut, Treffer, Gefahr (Blitz bei kritischem Treffer 230, 40, 50; der normale Treffer-Blitz ist fast weiß) und die höchste Stufe *Vampiric* (blutrot, pulsierend). Kein Rot als bloße Zierde.
- **Warmes Licht** (Gelb, Orange) gehört der Laterne und allem, was der Spieler haben will.
- **Silber** – hell, kühl, fast weiß – gehört dem Jäger und seinen Waffen.
- **Die vier Stufen von Ausrüstung und Kisten haben eigene Farben** (Tabelle oben). Lies sie im Code nach, bevor du Kisten oder Beute gestaltest, und verwende sie für nichts anderes.

## Form

- **Silhouette zuerst.** Jede Figur braucht einen Umriss, den man von den anderen unterscheidet.
- **Menschen bleiben Menschen.** Ein Untoter, der einmal ein Mensch war, hat die Proportionen eines Menschen: Schultern, Brustkorb, Becken, Gelenke an der richtigen Stelle. Unheimlich wird er durch Auszehrung, Haltung und Asymmetrie, nicht durch Tierformen.
- **Ausgemergelt heißt Knochen unter Haut, nicht Strichfigur.** Rumpf und Schultern behalten Masse.
- **Kopf klein halten:** bei menschlichen Figuren höchstens ein Sechstel bis ein Siebtel der Körperhöhe. Große Köpfe wirken niedlich.
- **Mindestens zwei Asymmetrien** je Untotem (Kopfneigung, ungleiche Arme, schiefer Kiefer, ungleicher Saum).
- **Lesbarkeit im Kampf geht vor Schönheit.** Der Kopf ist bei Ghul und Gorm eine eigene Trefferzone: Je nach Fernwaffe macht ein Treffer dort mehr Schaden. Die Faktoren stehen in `Balance` und ändern sich mit dem Balancing; heute sind es beim Ghul dreifach für Pistole und Rifle und kein Bonus für die Blunderbuss, bei Gorm 1,5-fach für alle drei. Der Kopf muss deshalb klar abgesetzt und erkennbar sein. Die Fledermaus hat keine Kopfzone.
- **Die Ankündigung ist die wichtigste Haltung.** Zwischen Ruhe und Ausholen muss die Silhouette deutlich wechseln (beim Ghul: von niedrig und breit zu hoch und schmal).
- **Modell und Trefferkörper passen zusammen, und das Spiel gibt vor.** Die Maße stehen in `BodySizes` im Core; die Kopfzone des Ghuls ist dort nach dem heutigen Platzhalter bemessen. Das Modell legt Körper und Kopf in diese Maße. Passt eine Gestalt nicht hinein (zum Beispiel ein Kopf an langem Hals), ändert der Designer nichts stillschweigend, sondern schlägt in den Notizen neue Maße vor. Darüber entscheidet die Leitung, weil es das Spiel verändert; umgesetzt wird es von einer Code-Sitzung.

Hintergrund mit Quellen: [Grundlagen Grafik](grundlagen-grafik.md), Abschnitte 3 und 4 und der Nachtrag d. Verbindlich sind nur die Regeln in diesem Dokument.

## Vom Modell ins Spiel

Vorschlag des Grafikdesigners, von der Leitung angenommen; der Einbau im Code steht noch aus und kann Einzelheiten ändern:

- **Format:** glTF binär (`.glb`). Raylib lädt es ohne Zusatzpaket.
- **Achsen und Einheit:** in der Datei wie im Spiel – x seitlich, y oben, z vorn, eine Einheit ist ein Meter.
- **Ursprung:** zwischen den Füßen auf dem Boden. Jedes bewegliche Teil trägt seinen Drehpunkt.
- **Ablage:** `assets/models/<name>.glb`, Erzeuger-Skript `tools/models/<name>.py`, Namen auf Englisch.
- **Einbau:** wie Raylib die Teile und Drehpunkte lädt und wie die Farben ankommen (sRGB-Werte der Textur unverändert, Licht als Faktor darauf), steht in [`docs/modelle.md`](../modelle.md). Jedes bewegliche Teil ist ein eigener, nicht verschachtelter Knoten mit einem Mesh aus einem Primitiv.

Figuren hängen im Spiel hinter einer austauschbaren Schnittstelle (z. B. `IGhoulView` in [`GhoulViews.cs`](../../src/SilverAndAsh.Game/Rendering/GhoulViews.cs)). Jede Datei unter `assets/` bekommt eine Zeile in `assets/HERKUNFT.md` (Datei, Skript, Datum); die Datei wird mit dem ersten integrierten Stück angelegt.

## Was ein Probestück zeigen muss

Alle Bilder entstehen in Blender, mit dem Skript des Probestücks, ohne Fenster:

1. **Vier Ansichten** (vorn, Seite, hinten, schräg oben) vor der Nebelfarbe, mit Textur und Schattierung.
2. **Bilder unter Spielbedingungen** in 854 × 480 mit 70° vertikalem Blickwinkel aus Augenhöhe 1,6 m: Ruhehaltung aus 2 m, aus 4 m und aus 10 m, bei Gegnern zusätzlich die Ankündigung aus 4 m. Alle mit Nebel und 15-Bit-Körnung (siehe „Technischer Rahmen“).
3. **Der Gang im Bild ist texturiert**, im selben Stil wie das Stück.
4. **Die Textur** selbst als Bild.
5. **Notizen:** Dreiecke je Teil, Texturgröße und wie sie erzeugt ist, Farben, Drehpunkte, angewendete Regeln, und wo der Designer unsicher ist.

Das ist eine Annäherung an das Spiel, kein Ersatz. Sobald das Spiel Modelle laden kann, zählt das Bild aus dem Foto-Skript des Spiels ([#224](https://github.com/flubby2222/silver-and-ash/issues/224)).

## Abnahme

- Die Leitung nimmt jedes Probestück an oder lehnt es mit Begründung ab. Jede Ablehnung wird unten zur Regel.
- **Ins Spiel** kommt ein Stück erst, wenn **drei Stücke in Folge ohne Nacharbeit** angenommen wurden. Stand: noch keines.
- Das Urteil des Menschen geht vor und wird ebenfalls zur Regel.

## Effekte

Effekte sind Flammen, Funken, Staub, Blut, Treffer und Licht, das sich bewegt. Den Effektdesigner beschreibt Issue [#352](https://github.com/flubby2222/silver-and-ash/issues/352); das erste Stück ist die Fackel ([#353](https://github.com/flubby2222/silver-and-ash/issues/353)).

### Stilregeln

- **Wenige, grobe Teilchen.** Lieber vier Funken, die man sieht, als vierzig, die im Punktmuster verschwinden. Aus 2 bis 4 m sind Teilchen mindestens zwei bis drei Pixel groß, im Bild nachgemessen.
- **Harte Pixel.** Teilchen und gemalte Effekt-Texturen haben keine Kantenglättung und keine weichen Ränder.
- **Nur Farben aus der Hauptpalette** (siehe „Farben“). Feuer und Glut nehmen die Reihe *Messing/Gold*, das Licht einer Fackel ist Messing 5.
- **Mitgekörnt.** Effekte werden vor der 15-Bit-Körnung gezeichnet und bekommen dasselbe Punktmuster wie die Wand dahinter (siehe „Technischer Rahmen“).
- **Kein weiches Glühen.** Kein Bloom, kein Leuchtschein um Flammen und Funken. Was hell sein soll, ist hell, weil das Licht die Umgebung aufhellt.
- **Kein Rot als Signalfarbe an Gegnern.** Gegner werden nicht durch rote Effekte (Augen, Umrisse, Warnzeichen) hervorgehoben. Feuer und Glut sind Gold und Messing, nicht Rot. Wo Rot erlaubt ist, steht unter „Farben“.

### Format der Probestücke

Effekt-Probestücke entstehen außerhalb des Spiels, im Ordner `design/proben/effekte/<stück>/`, mit einem Skript, das alles neu erzeugt:

1. **Drei Varianten** in derselben Szene unter Spielbedingungen (854 × 480, 70°, Augenhöhe 1,6 m, Texturen, Laterne, Nebel, Körnung). Zwischen den Varianten ändert sich nur das, was beurteilt wird.
2. **Ein Streifen je Variante:** 8 bis 12 Einzelbilder aus einer Sekunde, als Ausschnitt um den Effekt. Ein Balken unter jedem Bild zeigt, wie hell das Licht gerade ist; so lässt sich Flackern auch im Standbild beurteilen.
3. **Ein Blatt** mit allen Streifen untereinander, beschriftet.
4. **Ein GIF je Variante** im vollen Bild, als nahtlose Schleife.
5. **Eine README** mit Dateien, Varianten, Szene, Aufruf und Grenzen.

### Fackel: Wahl und Ablehnungen (Runde 1 und 2, 2026-11-18)

Regel für Feuer an der Wand (Fackel, Kohlebecken und Ähnliches):

- **Flamme:** eine kleine gemalte Textur mit wenigen Bildern, als Daumenkino (Runde 1, Variante A). Dazu nur wenige einzelne Funken.
- **Licht:** Es flackert unruhig, und seine Mitte wandert, sodass sich der Lichtfleck an der Wand bewegt (Runde 1, Variante B). Die Stärke ist mittel: Die Helligkeit schwankt um etwa ±25 %, die Mitte wandert bis etwa 3,5 cm zur Seite und 2,7 cm in der Höhe (Runde 2, Variante B).
- **Nicht gewählt:**
  - Eine Flamme aus groben Quadraten als Teilchen (Runde 1, B): Sie war auf den Bildern klein und unförmig.
  - Licht in harten Stufenringen (Runde 1, C): Die Kreise an der Wand wirkten künstlich.
  - Ruhiges Flackern mit ±15 % und einer Mitte, die bis 2,1 / 1,6 cm wandert (Runde 2, A).
  - Starkes Flackern mit ±32 % und einer Mitte, die bis 4,5 / 3,5 cm wandert (Runde 2, C).

Die Proben liegen in `design/proben/effekte/fackel/` (Runde 1) und `design/proben/effekte/fackel/runde2/`.

### Mündungsfeuer: Wahl und Ablehnungen (2026-11-18)

Stand der Effekt-Probestücke: **2 von 3 Treffern in Folge** (Fackel, Mündungsfeuer). Ins Spiel kommt ein Effekt erst nach drei angenommenen Stücken ohne Nacharbeit.

Regel für den Schuss einer Fernwaffe in der Ich-Sicht (Pistole; für Rifle und Blunderbuss noch nicht geprüft):

- **Blitz:** ein breiter, flacher Kegel aus zwei Polygonen in Messing/Gold, 3 Bilder (Variante C).
- **Rauch:** eine dicke Wolke aus 10 groben Quadraten in Stein, die in den ersten 5 Ticks entsteht, wächst und etwa 0,6 s steht. Frischer Rauch ist in den ersten Bildern vom Blitz gold gefärbt.
- **Funken:** keine.
- **Aufleuchten:** zurückhaltend, etwa +50 % an der Mündung über 4 Bilder (67 ms), in groben Stufen, mitgekörnt.
- **Nicht gewählt** (der Mensch nannte keinen Grund):
  - Sternblitz aus 5 und dann 3 Spitzen mit 4 Funken und Rauchfaden, Licht +80 % (Variante A).
  - Funkenstoß: winziger Blitz, 10 Funken als Kegel, kein Rauch, Licht +75 % in einem Bild (Variante B).
- **Hinweis der Leitung** (kein Urteil des Menschen) für den späteren Einbau: Die Wolke steht 0,6 s. Bei schneller Schussfolge prüfen, dass sie das Ziel nicht verdeckt.

Die Probe liegt in `design/proben/effekte/muendungsfeuer/`.

## Gelernte Regeln

| Nr. | Datum | Stück | Was war falsch | Regel |
|---|---|---|---|---|
| 1 | 2026-11-16 | Ghul, erste Fassung | Die Lumpen lagen fast auf der Bodenfarbe; aus 10 m verschwand der Rumpf, und die Figur zerfiel in Kopf und Arme | Keine große Fläche einer Figur liegt im Farbton auf Boden, Wand oder Decke. Abstand entsteht über den Farbton, nicht nur über die Helligkeit |
| 2 | 2026-11-16 | Ghul, erste Fassung | Von vorn – der Richtung, aus der man Gegner fast immer sieht – war der Kopf ein heller Klumpen ohne Gesicht; helle Augen auf hellem Schädel sah man nicht | Ein Kopf braucht von vorn dunkle Formen, die ihn als Gesicht lesbar machen, aus 4 m mindestens zwei bis drei Pixel groß |
| 3 | 2026-11-16 | Ghul, erste Fassung | Die Ausholen-Haltung fehlte als Bild | Bei Gegnern gehört die Ankündigung zu jedem Probestück |
| 4 | 2026-11-16 | Ghul, zweite Fassung | Zwei quadratische Augen über einem breiten, geraden Mund lasen sich als Grinsen | Münder unregelmäßig, nach unten gezogen oder gezackt; Augenhöhlen nicht quadratisch |
| 5 | 2026-11-16 | Ghul, erste und zweite Fassung | Urteil des Menschen: „überhaupt nicht, was ich mir vorgestellt habe“ – Gestalt, Detailgrad, Farbflächen ohne Textur, Gesicht | Der ganze Stil wurde ersetzt (ADR 0010). Lehre für die Leitung: Stilvorgaben herleiten und früh am Bild beim Menschen prüfen, statt sie zu schätzen |
| 6 | 2026-11-16 | Ghul, zweiter Entwurf | Von vorn standen die Oberschenkel als zweites Paar langer Säulen hinter den Armen; die Figur las sich als Stelzenwesen | Eine Figur muss auch von vorn als das erkennbar sein, was sie ist; jede Haltung wird in der Ansicht geprüft, aus der der Spieler sie sieht |
| 7 | 2026-11-16 | Ghul, zweiter Entwurf | Aus 10 m waren die Glieder ein bis zwei Pixel breit und die Figur fast weg | Aus 10 m ist eine Figur als Masse mit erkennbarer Haltung lesbar; Glieder mindestens zwei bis drei Pixel breit, im Bild nachgemessen |
| 8 | 2026-11-17 | Hauptpalette, Runde 1 und 2 | Runde 1 färbte auch Spielfigur und Waffen um (nicht gewollt); Runde 2 mischte warme und kalte Töne in die Flächen, und mit der Kachelwiederholung entstand ein Regenbogenmuster | Ein Vergleichsblatt ändert nur die eine Sache, die beurteilt wird; alles andere bleibt wie in `main`. Eine Fläche bleibt in einer Rampe, kräftige Reihen sind Akzente (siehe Farben) |
| 9 | 2026-11-18 | Fackel, Runde 1, Variante B | Die Flamme aus groben Quadraten war auf den Bildern klein und unförmig | Eine Flamme ist eine kleine gemalte Textur mit wenigen Bildern, kein Haufen Teilchen. Teilchen nur für einzelne Funken, Staub und Ähnliches |
| 10 | 2026-11-18 | Fackel, Runde 1, Variante C | Das Licht fiel in harten Stufenringen ab; die Kreise an der Wand wirkten künstlich | Licht fällt weich ab und ist nicht in Stufen geteilt. Die Pixel-Härte entsteht durch die Körnung, nicht durch Ringe |
| 11 | 2026-11-18 | HUD, Runde 3 | Urteil des Menschen: Layout A gefällt, aber Steintextur nein, Farbe Silber, „so viel vom Spiel sichtbar wie möglich“; Runde 3 A verdeckte 23,2 % des Bildes | HUD-Metall ist Silber aus der Hauptpalette (Reihe IRON): harte Helligkeitsstufen, Körnung, 1 px helle Kante oben und dunkle unten, kein Chrom-Glanz, kein weiches Leuchten. Keine Steintextur im HUD. Das HUD verdeckt so wenig Bild wie möglich; der verdeckte Anteil wird aus dem Bild gemessen und auf dem Blatt genannt. Leben und Ausdauer ohne Zahl im HUD; die Zahlen stehen auf der Tab-Seite (Issue #347, Urteil des Menschen: „Die Zahlen nehmen wir doch raus und ins Tab Menü auf“; ersetzt die Zwischenregel „Zahlen drin“). Zahlen, die man zum Spielen braucht (Munition, Silber, Stufe, Schlüssel), bleiben |
| 12 | 2026-11-18 | HUD, Schrift | Die Vorlagen nutzen Cinzel (Windows-Schrift) | Cinzel kommt nicht ins Spiel; der Mensch will eine eigene Pixelschrift (Issue #355). Für die Vorlagen reicht Cinzel |
| 13 | 2026-11-18 | Mündungsfeuer, Variante A | Urteil des Menschen: Sternblitz mit Funken und Rauchfaden nicht gewählt; einen Grund nannte er nicht | Beim Schuss kein Sternblitz aus Spitzen mit wenigen Funken; Blitz als Kegel, siehe „Mündungsfeuer: Wahl und Ablehnungen“ |
| 14 | 2026-11-18 | Mündungsfeuer, Variante B | Urteil des Menschen: Funkenstoß mit winzigem Blitz und ohne Rauch nicht gewählt; einen Grund nannte er nicht | Beim Schuss keinen Funkenkegel statt Blitz und Rauch; siehe „Mündungsfeuer: Wahl und Ablehnungen“ |
| 15 | 2026-11-18 | Pixelschrift, B Textura (Issue #355) | Urteil des Menschen: „Schrift a ists“, B Textura nicht gewählt (ohne genannten Grund). Einschätzung der Leitung: B war in HUD-Größe schwer lesbar (n/u, „Gorm“) | Eine gebrochene Schrift mit dicken Stämmen verklebt auf 7 px Versalhöhe; Lesbarkeit der Zahlen und Namen geht vor Zierform |
| 16 | 2026-11-18 | Pixelschrift, C Schmal (Issue #355) | Urteil des Menschen: C Schmal nicht gewählt (ohne genannten Grund). Einschätzung der Leitung: C wirkte neutral statt gotisch | Eine schmale Schrift ohne Serifen genügt dem gotischen Stil nicht; die Schrift trägt zur Stimmung bei |
| 17 | 2026-11-18 | Pixelschrift, Ziffer 0 und Buchstabe O (Issue #355) | Der Tester fand in Schrift A „O“ und „0“ mit demselben Bitmuster; in HUD-Zahlen (Munition, Silber) wäre das ein Lesefehler | **Verbindlich:** Ziffer 0 und Buchstabe O müssen sich unterscheiden (Befund des Testers). In einer gewählten Pixelschrift darf kein Zeichen einem anderen gleichen; `check()` in `schrift.py` prüft das für A und bricht sonst ab. **Vorläufig:** Die Form mit Schrägstrich ist eine Entscheidung der Leitung vom 2026-11-18 und gilt bis zum Urteil des Menschen im Spieltest. |

## HUD (entschieden, Issue #341)

**Gewählt: A3, Silberfassungen auf einer Linie, mit reicher Verzierung (V3); vom Menschen am 18.11.2026 in der überarbeiteten Fassung von Runde 6 abgenommen.** Vorlage: `design/proben/grafik/hud-vorlagen/hud_runde4.py` (`main6`, Endbild `runde6/r6-V3-final-854.png`, 854 × 480; Blatt `runde6/blatt-runde6.png`). Das Layout stammt aus Runde 3 A.

**Maße (854 × 480):**

- Alle unteren Fassungen enden auf **einer gemeinsamen Grundlinie, y = 477**; jede ist 30 px hoch, der Silberrahmen 3 px, innen dunkler Grund.
- Links zwei Gegenstände (je 30 px breit, Abstand 4, ab x = 12), Mitte eine Fassung x 247 bis 607 mit Leben (Balken) und fünf Ausdauer-Segmenten, beide ohne Zahl, rechts vier Waffen (je 38 px, Abstand 3, ab x = 690).
- Oben: Stufe, Erfahrung und zwei Fähigkeiten links; Boss mit Namen und dünnem Balken in der Mitte; Silber und Schlüssel rechts. Aufnahme-Meldung (einzige Goldfläche) über der linken Gruppe.
- **Verdeckt: 9,3 % des Bildes** mit Verzierung (gemessen: Unterschied zum Foto ohne HUD); A3 ohne Verzierung waren 9,0 %, Runde 3 A 23,2 %.

**Silber (Palettenstufen):** Reihe Eisen `IRON3` bis `IRON7` plus ein helles Silber (226, 228, 230), jeweils zu 80 % zu Grau entsättigt, damit es nicht bläulich wirkt. Fünf feste Stufen je nach Flächenneigung: Lichtkante oben und links hell, Schattenkante unten und rechts dunkel, dazu Punktkörnung. Kein Verlauf, kein Chrom-Glanz, kein weiches Leuchten. Rot nur für Leben, Gold nur für die Aufnahme-Meldung.

**Regeln:**

- **Silber statt Stein.** Die Steintextur im HUD ist abgelehnt (Urteil des Menschen).
- **So wenig Spielbild verdecken wie möglich**; Richtwert **unter 10 %**, gemessen am fertigen Bild.
- Layout von Runde 3 A; **Leben und Ausdauer ohne Zahl im HUD, Zahlen auf der Tab-Seite** (#347); Munition, Silber, Stufe und Schlüssel behalten ihre Zahl.
- **Alle Fassungen auf einer Grundlinie**; jede Anzeige hat ihren eigenen dunklen Grund, damit Zeichen auf jedem Spielbild lesbar bleiben.

**Abgelehnt:** A1 (geschlossene Leiste, 12,9 %): verdeckt zu viel. A2 (offener Rahmen, 6,1 %): Zeichen stehen ungeschützt auf dem Spielbild.

**Schrift (Urteil des Menschen, Issue #355): A „Kapitell“.** Eigene Pixelschrift für HUD und Menüs: schlichte Kapitälchen mit kleinen Serifen; Versalhöhe 7 px, Kleinbuchstaben 5 px als kleine Versalien, Zeichenbreite 3–5 px, Abstand 1 px, Leerzeichen 3 px. Pixelmuster und Skripte: `design/proben/grafik/schrift/` (Blatt `blatt-schrift.png`). Cinzel kommt nicht ins Spiel. Menü-Überschriften 3-fach, Einträge 2-fach. **Offen für den Einbau:** eine größere Zahlengröße für Stufe und Munition fehlt noch (Versalhöhe 7 px statt 9–11 px bei Cinzel).
- **Verzierung (Runde 5 und 6, Urteil des Menschen: V3, dann überarbeiten):** Ein Motiv an allen Fassungen ab 24 px Breite, immer im selben Silber: Rautenniete in den vier Ecken, Spitzgiebel mit Auge oben in der Mitte, dünner Doppelrahmen innen, an Fassungen über 100 px Breite zusätzlich Fialen mittig an den Enden. Rot und Gold bleiben, wie sie sind; kein Rot als Signalfarbe in der Verzierung.
- **Regeln aus dem Feinschliff (Runde 6):** Licht kommt überall von oben links, auch an den kleinen Teilen (Nieten, Giebel, Fialen nutzen dieselbe Lichtregel wie der Rahmen). Alle Mittelpunkte liegen auf dem Pixelraster, die Giebelbreite passt zur Fassung (gerade oder ungerade), damit die Form spiegelgleich bleibt. Verzierung ragt nie aus dem Bild; Fialen sitzen mittig auf der Fassungskante. Zeichen in kleinen Fassungen so groß, dass sie im 4-fachen Ausschnitt eindeutig lesbar sind (Waffenzeichen 10,5 statt 9,5).

**Nicht gewählt:** V1 (nur Eckrauten): wirkt kaum anders als ohne Verzierung, das HUD bleibt zusammengewürfelt. V2 (Ecken und Giebel): gut, aber ohne Doppelrahmen und Fialen wirken die langen Fassungen noch kahl. Beide bleiben verworfen; wer die Verzierung später ändert, geht von V3 aus.
