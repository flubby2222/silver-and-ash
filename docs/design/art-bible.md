# Art Bible – wie Silver & Ash aussieht

Dieses Dokument liest der Grafikdesigner vor jeder Aufgabe. Es hat zwei Teile: den **gewünschten Stil** und die **gelernten Regeln**. Jede Ablehnung eines Probestücks wird unten zu einer Regel, damit derselbe Fehler nicht zweimal vorkommt (siehe [ADR 0009](../entscheidungen/0009-leitung-designer-auto-merge.md)). Begriffe wie Probestück, Probenblatt und Fassung erklärt das [Glossar](../glossar.md).

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
| Ausnahme zerbrechende Säule (2026-11-23) | Die beschädigten Stufen der freistehenden Säule (Z1, Z2) dürfen bis 500 Dreiecke haben statt höchstens 400 wie eine Requisite: Ausbruch, Keil und lose Brocken brauchen die Kanten (abgenommen vom Menschen, Issue #594/#601). Das fallende Modell (Z2 in Stücke zerlegt, mit Schnittflächen, heute 588) darf bis 600 haben, nur für die Dauer des Falls (81 Ticks; Leitung nach dem Review von PR #607). |
| Ausnahme Wendeltreppe (2026-11-25, ersetzt die vom 2026-11-24) | Die Wendeltreppe (`stairs_spiral_a/b/c`) darf bis 1300 Dreiecke haben statt höchstens 400 wie eine Requisite (heute 1228): Seit #723 reicht sie bis unter die 6-m-Decke des Bossraums; Entwurf b mit Eisengeländer (zwanzig Stufenplatten, Spindel, Pfosten und eckiger Handlauf als geschlossene Körper) braucht die Kanten (Urteil des Auftraggebers, Issue #765). Ihr Atlas ist 256 × 512 statt 64 × 64 (Wand- und Bodenkachel, Werkstein, Messing und eine Eisenfläche nebeneinander, Regel „Zusammengesetzte Textur“); das Eisen ist die dunkle Stufe der Reihe Eisen (zu 80 % die zweitdunkelste Stufe, dazu die dunkelste und die dritte), keine neue Farbe. |
| Ausnahme Türrahmen (2026-11-25, vorläufig) | Die Steinrahmen der normalen und der verschlossenen Tür (`door_frame_{normal,locked}_{a,b,c}`) dürfen bis 700 Dreiecke haben statt höchstens 400 wie eine Requisite (heute 628 je Datei, Front und Rückseite zusammen; vorher 388 bei der normalen, 380 bei der verschlossenen Tür; die Formenlehre Gotik schätzt etwa 120, ungemessen): Seit #750 sind sie rundum geschlossene Körper (Regel 21). Die neuen Dreiecke schließen den Rahmen dort, wo er im Spiel an Wand, Boden und Nachbarzelle anliegt: je Teil allein 40 an der Zellkante, 12 am Boden, 6 an der Oberseite und 6 (normale Tür) bzw. 8 (verschlossene Tür) in der Türebene. **Vorläufig von der Leitung angenommen; das Urteil des Auftraggebers im Spieltest steht aus.** |
| Verteilung der Dreiecke | etwa ein Viertel in Kopf und Hände; Rundungen mit 8 bis 12 Seiten |
| Filterung | keine, auch keine Mipmaps: Texel bleiben harte Quadrate |
| Kopf in der Textur | mindestens ein Achtel der Fläche |
| Dithering in der Textur | keines. Gerastert wird am Ende im Bild, sonst doppelt sich das Muster |
| Schattierung | weich über die Ecken (Gouraud): Helligkeit je Ecke aus ihrer Richtung, über die Fläche verlaufend |
| Licht in der Textur | nur gemalte Tiefe (Rippen, Höhlen, Falten) und Licht von oben; nie die Richtung der Laterne |
| UVs | entstehen beim Bauen im Skript; Nähte auf die Unterseite; spiegelbildliche Teile dürfen sich Textur teilen |
| Bewegung | getrennte Teile mit sinnvollem Drehpunkt, per Code gedreht; keine Skelett-Animation |
| Waffen ohne sichtbare Hand (2026-11-19) | **Waffen ohne sichtbare Hand knicken am Griff ab, der Ärmel verdeckt den Ansatz.** Der Drehpunkt liegt im Griff innerhalb der Manschette; der Ärmel bleibt, wie er ist. Ein Knick so stark, dass das Griffende vorn aus der Manschette sticht, ist zu viel (Keule: 35 Grad sauber, 50 Grad sticht durch; Prüfung in `ClubBendTests`) |
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
7. **Die Türflügel behalten ihre Farben** (Urteil des Menschen am 2026-11-21: „Die Türen sind gut so, wie sie sind.“, Issue [#543](https://github.com/flubby2222/silver-and-ash/issues/543)). Holz, Eisen und Messing der drei Türflügel werden nicht auf die Palette umgerechnet; der Test `EnvironmentPaletteTests` führt ihre heutigen Farben als eigene Reihen „Tür“. Eine neue Farbe an einer Tür ist eine bewusste Entscheidung, keine Rundung.

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
- **Rot ist reserviert** für Blut, Treffer, Gefahr (Blitz bei kritischem Treffer 230, 40, 50; der normale Treffer-Blitz ist fast weiß) und die höchste Stufe *Vampiric* (blutrot, pulsierend). Dazu kommen die Augen eines Ghuls, der zum Schlag ausholt (Blutrot-Stufen 150, 20, 30 → 200, 30, 40 → 225, 58, 48, hart, ohne Schein). Kein Rot als bloße Zierde.
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
- **Kein Beschlag auf einer Fuge, die nicht mehr aufgeht** (Urteil des Menschen am 2026-11-21, Issue #543: das eiserne Querband unter dem Kopffeld der Türen stammte aus der Zeit, als nur der untere Teil aufging). Ändert sich, wie ein Teil sich bewegt, fallen Bänder, Leisten und Füllstücke weg, die nur die alte Trennung verdeckt haben; das Material läuft durch.

**Raum-Bausteine:** Bogenarten, Wand in drei Zonen, Profile und die Maße von Wandpfeiler, Säule, Deckenbogen, Nische und Türrahmen stehen in der [Formenlehre Gotik](formenlehre-gotik.md) und sind für alle Bausteine verbindlich.

- **Wandbänder bleiben schlicht; Spitzbögen gehören den Bausteinen** (Urteil des Menschen am 2026-11-21 zur Probe der Wandzonen, Issue [#282](https://github.com/flubby2222/silver-and-ash/issues/282): „Alles a“). Sockel und Abschluss der Wand sind der Quader-Sockel mit Fase und das glatte Gesims; der Spitzbogenfries wird nicht gebaut. Spitzbögen tragen Türrahmen, Deckenbogen und Nische, nie die Wandfläche.

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
- **Rot an Gegnern nur als kurze Angriffs-Ankündigung in den Augen; weiterhin keine roten Umrisse oder Warnzeichen.** Die Augen leuchten nur, solange der Gegner zum Schlag ausholt, in harten Stufen ohne Schein oder Halo (Urteil 22). Feuer und Glut sind Gold und Messing, nicht Rot. Wo Rot erlaubt ist, steht unter „Farben“.

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

### Frost von Grave Chill (Issue #775, nach Probe #616 Variante A)

Regel für die Grabeskälte an einem Gegner, im Spiel seit Issue #775 (gewählt am 2026-11-23 als „Chill A“):

- **Körper:** um 40 % zur Kälte (Stoff 6) getönt. Ein Treffer-Blitz und das rote Aufglühen vor einem Angriff gehen vor; das Glühen von Gorms Keule und Haken bleibt sichtbar.
- **Reifflecken:** 14 Würfel von 3 cm in Reif hell (Stoff 7) auf der Seite, die zum Betrachter zeigt, immer dieselbe Verteilung. Die Verteilung kommt aus einem eigenen festen Seed; das Muster der Probe ist es nicht, nur Zahl und Größe stimmen.
- **Flecken nur auf Körper (Issue #785):** Je Gegnerart liegen ein paar Stäbe im Modell, kleiner als die Figur: beim Ghul der Rumpf von der Hüfte zu den Schultern und der Kopf, beide drehen beim Ausholen mit dem Rumpf; bei der Fledermaus Körper und Kopf; bei Gorm der Rumpf. Ein Fleck sitzt auf der dem Auge zugewandten Seite eines Stabs und rückt entlang der Sichtlinie um den Körperradius zum Auge (höchstens die halbe Strecke). So liegt er im Bild immer über dem Körper, von oben, von unten und von der Seite, und nie daneben. Ein neuer Gegner bekommt einen eigenen Eintrag in der Tabelle (`FrostCore`); ohne Eintrag nimmt er einen Stab in der Mitte seines Körpers.
- **Bodenscheibe:** ein Kreis flach auf dem Boden unter dem Gegner. Seine Größe richtet sich nach dem Körper: Der Radius ist 1,55-mal der Körperradius (0,62 m beim Ghul wie in der Probe, 1,4 m bei Gorm, 0,54 m bei der Fledermaus). Grund ist das Urteil der Leitung vom 2026-11-25, vorläufig: Bei fest 0,62 m verschwand die Scheibe zwischen Gorms Beinen. Den Rand bildet ab 85 % des Radius Reif hell. Innen liegt ein Punktmuster aus Stoff 6 und Stoff 5, je zur Hälfte. Bei einer Fledermaus liegt die Scheibe auf dem Boden unter ihr, nicht an ihr.
- **Scheibe an Stufen (Issue #785):** Jedes Stück der Scheibe liegt auf dem Boden der Zelle, über der es liegt. Steht ein Gegner an der Kante einer Stufe, reicht seine Scheibe auf den tieferen Boden hinunter, statt darüber zu schweben. Wände und Böden, die höher sind als der Boden unter dem Gegner, schneiden die Scheibe ab; sie steckt nie in einer Stufe vor ihm. Der Boden unter dem Gegner ist der höchste Boden, den sein Körper berührt und der nicht über seinen Füßen liegt (`FrostDisc`).
- **Unbeleuchtet, aber im Nebel:** Flecken und Scheibe hellt die Laterne nicht auf und dunkelt sie nicht ab; nur der Nebel deckt sie zu.
- **Kein Verlauf:** Der Frost sieht im ersten und im letzten Tick gleich aus und ist mit dem Ende des Effekts sofort weg.
- **Farben nur aus Stoff:** kein Violett (das gehört Leech), kein Rot.
- **Ausnahme vom Punktmuster in der Textur:** Das Muster der Scheibe ist am Bildpixel festgemacht, wie die Körnung, und wandert deshalb nicht mit. Es ist die einzige Ausnahme von „Dithering in der Textur: keines“ (vorläufig, die Probe zeichnet es so; Urteil der Leitung offen).
- **Neue Formen** (etwa Stun) kommen als eigener Eintrag in die Tabelle der Formen (`StatusForm` in `StatusLooks`, Zeichnung in `StatusView`), nicht als Zweig in der Frost-Zeichnung.

Die Probe liegt im Branch `feat/statuseffekte-probe-616`, Ordner `design/proben/grafik/statuseffekte-616/`.

### Funken von Stun (Issue #778, nach Probe #616 Variante A)

Regel für die Betäubung an einem Gegner, im Spiel seit Issue #778 (abgenommen als „Stun A“):

- **Form statt Farbe:** drei Plus-Funken kreisen **über dem Kopf**, nie auf dem Körper und nie am Boden. Damit unterscheidet sich Stun von Grave Chill (Flecken auf dem Körper, Scheibe am Boden) und von Flamme und Markierung (kreisende Punkte um den Körper) durch Form und Ort, obwohl Eisen 6 und Stoff 7 fast gleich hell sind.
- **Funke:** ein Plus, das zum Betrachter zeigt. Die Mitte ist Eisen 7 (198, 205, 210), die vier Arme sind Eisen 6 (163, 175, 187). Beim Ghul ist ein Arm 7,5 cm lang und 3 cm dick, wie in der Probe. Bei 4 m ist ein Funke etwa 6 Pixel breit, bei 10 m etwa 3 Pixel.
- **Ring:** waagerecht, über der Mitte des Kopfes (beim Ghul 0,33 m vor den Füßen, weil sein Kopf vorne hängt), 0,10 m über dem Scheitel, Radius 0,30 m beim Ghul. Ring und Abstand wachsen mit dem Körperradius wie die Frostscheibe: bei Gorm 0,675 m Radius und 0,225 m über dem Kopf. Der Ring ist nie breiter als der Körperradius. Die Funken wachsen mit Gorm mit, werden bei der kleineren Fledermaus aber nicht kleiner als beim Ghul.
- **Bewegung:** eine Umdrehung je Sekunde, die drei Funken ein Drittel auseinander. Das ist vorläufig, weil die Probe ein Standbild ist.
- **Kein Körperton:** Stun tönt den Körper nicht. Beim gleichzeitigen Grave Chill bleibt der Kälteton.
- **Restzeit über den Funken:** Hat ein Gegner Stun, steht die Restzeit so weit höher, wie die Funken über den Kopf reichen. Das ist vorläufig: In der Probe teilen sich Funken und Zahl den Platz, im Spiel verdeckte die Zahl einen Funken. Ohne Stun steht sie wie bisher.
- **Unbeleuchtet, aber im Nebel**, wie der Frost. **Kein Verlauf:** Die Funken sind vom ersten bis zum letzten Tick der Betäubung da und mit ihrem Ende sofort weg. Bei Gorm sind das 0,2 s.

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
| 18 | 2026-11-18 | Menüs (#351) | Urteile des Menschen: Runde 1 wählte C, aber „noch was eleganter“; Runde 2 wählte C2 Schlank (mehr hat er nicht gesagt) | Menüs folgen C2, siehe Abschnitt „Menüs“. Abgelehnt: A schlicht, B gefasst, C in der ersten Fassung (nicht elegant genug), C1 Fein, C3 Geordnet. Ein Zierelement, das bei 1× kaum zu sehen ist (Giebel), wird größer gezogen |
| 19 | 2026-11-18 | HUD im Spieltest (Issue #387) | Urteile des Menschen nach dem Spieltest: die kleinen grauen Innenrahmen und der senkrechte rote Strich im Lebensbalken stören; die Teile stehen ungleich weit auseinander; Erfahrung soll golden sein; der Hinweis zum Aufsteigen soll unter Stufe und Erfahrung stehen | Kein Doppelrahmen innen in HUD-Fassungen, keine Markierung auf dem Lebensbalken; ein Abstand für alle HUD-Teile; siehe „Feinschliff nach dem Spieltest“ im Abschnitt HUD |
| 20 | 2026-11-18 | HUD und Menü in höherer Auflösung, B geglättet (#393) | Urteil des Menschen: A harte Pixel gewählt, B abgelehnt; Schrift und Zeichen werden weich | Kein Glätten von Schrift und Zeichen im HUD; Pixel bleiben sichtbar, siehe „HUD und Menü in höherer Auflösung“. HUD-Gegenstände und Waffen zeigen am Ende Renderings ihrer Modelle |
| 21 | 2026-11-19 | Keule (Issue #403, Nacharbeit #414) | Urteil des Menschen im Schauraum: „Die Keule ist nicht fertig“ – Lücke zwischen Griff und Kopf, offene Seite am Kopf (die Kappen von Griff, Kopf und Bändern fehlten), Kopf um 25 Grad geknickt. Schauraum, Schlagbewegung und liegende Waffen zeigen jede Seite | **Verbindlich: 3D-Modelle sind rundum geschlossen.** Kein Modell hat offene Seiten, fehlende Rück- oder Unterflächen oder Lücken zwischen seinen Teilen, auch nicht dort, wo man in der Ich-Sicht „nie hinsieht“. Teile sind geschlossene Körper (Kappen an beiden Enden) und überlappen sich dort, wo sie zusammenstoßen. Ein Stiel mit Kopf ist gerade, wenn nicht ausdrücklich anders gewollt. Prüfung: `python tools/models/check_closed.py` (jede Kante gehört zu genau zwei Dreiecken, Ecken gleicher Lage zusammengelegt); jedes neue oder geänderte Modell besteht sie vor dem Probenblatt |
| 22 | 2026-11-19 | Ghul-Augen vor dem Schlag (#447) | Wunsch des Menschen: „Wäre cool, wenn eines ghoul augen kurz rot aufleuchtet, kurz bevor sie zuschlagen.“ Auf Rückfrage Rot gewählt; die Regel „Kein Rot als Signalfarbe an Gegnern“ wird dafür geändert | Rot an Gegnern nur als kurze Angriffs-Ankündigung in den Augen (während des Ausholens, harte Blutrot-Stufen, kein Schein); weiterhin keine roten Umrisse oder Warnzeichen. Dauer und Stärke vorläufig bis zum nächsten Spieltest |
| 23 | 2026-11-19 | Keulen-Haltung (Issue #417) | Runde 1: Die Keule lag wie die Klinge in der Bildtiefe und las sich als Stoßwaffe; ein schräger Griff mit gekipptem Ärmel zeigte einen Knick zwischen Manschette und Unterarm. Urteil des Auftraggebers in Runde 2: Variante B, Ärmel bleibt, nur die Keule knickt am Griff ab (35 Grad hoch, 10 Grad auswärts). Variante C (50 Grad) stach mit dem Griffende vorn durch die Manschette | Waffen ohne sichtbare Hand knicken am Griff ab, der Ärmel verdeckt den Ansatz (siehe „Regeln für Modelle“). Im Treffer-Moment ist der Knick 0 Grad, auch wenn das Ausholen nach einem aufgeladenen Schlag noch abklingt; beim vollen Ausholen 20 Grad / -10 Grad. Das Ausholen verschiebt den Knick linear, der Schlag schrumpft den ganzen Knick auf 0 |
| 24 | 2026-11-20 | HUD-Rahmen ohne Schmuck (Issue #412) | Urteil des Menschen: die glatten Silberrahmen von Feldern, Reitern und Lebensbalken wirken nackt. Probe mit drei Schmuckformen: S1 Nieten gewählt, S2 Eckwinkel und S3 Giebel verworfen (der Giebel nur als Schmuck der HUD-Rahmen; die Menütafel behält ihren Giebel) | **Keine glatten Rahmen ohne Schmuck.** Jeder Rahmen trägt Nieten als Relief: an den Ecken (Radius 4 px bei Seite ab 40 px, sonst 3 px), auf Reitern zusätzlich etwa alle 80 px auf Ober- und Unterkante, am Lebensbalken auf dem unteren Rand mittig zwischen den langen Strichen; alle Pixelmaße im doppelten Atlas (Raster 1708 × 960, im einfachen halb so groß). Am Lebensbalken schneidet das Spiel die Fassung von rechts: Die Nieten sitzen so, dass bei jedem erreichbaren Lebensmaximum jede Niete ganz bleibt oder ganz wegfällt, nie halb (Prüfung: `tools/hud/hud_nieten_pruefen.py`). Nur die sechs Silberstufen des Rahmens, harte Pixel, keine neue Farbe. Sie entstehen im Atlas-Skript (`tools/hud/hud_nieten.py`), nie von Hand, für alle Rahmen gleich. Neue Rahmen bekommen sie im selben Zug |
| 25 | 2026-11-23 | Füllungen der tiefen Wandnische (#553) | Abnahme der Probe (Kopf `ea5109d`): Die Flaschen-Pyramide wurde nicht gewählt. Gewählt: liegender Flaschenstapel 3-2-2, in die Ecke aus linker Laibung und Rückwand geschoben, verstaubt, Hälse mit Korken nach vorn | **Kleine Gegenstände müssen am Umriss erkennbar sein.** **Vorräte liegen geordnet wie in einem Lager, in eine Ecke geschoben, nicht mittig verstreut.** |
| 26 | 2026-11-24 | Rechtecke am Türrahmen (#715) | Spieltest 0.12.0: „Was stört sind nicht die Wandpfeiler sondern diese überstehenden, in die Tür ragenden Rechtecke, die dort keinen Sinn ergeben.“ Es waren die Kämpfer- und Sockelplatten des Türrahmens, eine Achtelmeter vor der Wandebene | **Kein Bauteil ragt ohne erkennbaren Zweck in eine Öffnung.** Platten und Sockel am Rahmen sitzen bündig in der Wandebene; was in der Öffnung steht, muss etwas tragen, halten oder schließen. |
| 27 | 2026-11-25 | Wendeltreppe neu (#765) | Urteil des Auftraggebers über die drei Entwürfe: Entwurf b, Eisengeländer, aber das Eisen dunkler und weniger blau; der Handlauf bleibt eckig. Die alte Treppe war ein Stapel gleicher Stufen um eine Säule, ohne Geländer, und las sich im Nebel nicht als Treppe | **Eine Treppe braucht eine lesbare Führung.** Ein Geländer aus dunklem Eisen gibt der Silhouette eine Schraubenlinie, auch auf 10 m; Das Eisen des Treppengeländers nimmt die dunklen Stufen der Reihe Eisen, nicht die mittleren (zu blau neben dem warmen Stein; Urteil des Auftraggebers „dunkler, weniger blau“ gilt nur diesem Geländer, auch für neues Eisen unmittelbar neben warmem Stein). Fallgitter (neutrales K2), Türflügel (Regel 7), Kisten (Forged: Stufe 4) und die Katakomben-Auswahl „Eisen 1–3“ bleiben, wie sie sind. Teile, die man von nahem sieht, sind geschlossene Körper. Vorläufig: Das Geländer endet zwei Stufen früher als auf dem gewählten Blatt (bei 5,655 m statt 5,905 m, weil der Stein dort enden muss, wo die Lampe beginnt, `StairsLookTests`); das Urteil fällt im Spieltest. |

## HUD (entschieden, Issue #341)

**Gewählt: A3, Silberfassungen auf einer Linie, mit reicher Verzierung (V3); vom Menschen am 18.11.2026 in der überarbeiteten Fassung von Runde 6 abgenommen.** Vorlage: `design/proben/grafik/hud-vorlagen/hud_runde4.py` (`main6`, Endbild `runde6/r6-V3-final-854.png`, 854 × 480; Blatt `runde6/blatt-runde6.png`). Das Layout stammt aus Runde 3 A.

**Maße (854 × 480):**

- Alle unteren Fassungen enden auf **einer gemeinsamen Grundlinie, 6 px über dem unteren Rand (y = 474)**; jede ist 30 px hoch, der Silberrahmen 3 px, innen dunkler Grund. (Bis Issue #387: y = 477.)
- Links die Gegenstände (je 30 px breit), Mitte eine Fassung, 360 px breit, mit Leben (Balken) und fünf Ausdauer-Segmenten, beide ohne Zahl, rechts vier Waffen (je 38 px). Die Abstände folgen der Abstandsregel unten.
- Oben: Stufe, Erfahrung und Fähigkeit links, darunter der gelbe Hinweis „[X] to level up“, solange ein Segen wartet; Boss mit Namen und dünnem Balken in der Mitte; Silber und Schlüssel rechts. Aufnahme-Meldung über der linken Gruppe.
- **Verdeckt: 6,7 bis 8,3 % des Bildes** in den Szenen hud-calm, hud-weapons, hud-items, hud-skillpoint, hud-boss (854 × 480, gemessen am 2026-11-18 als Unterschied zum Foto ohne HUD); bei der Abnahme von A3 mit Verzierung waren es 9,3 %, A3 ohne Verzierung 9,0 %, Runde 3 A 23,2 %.

**Silber (Palettenstufen):** Reihe Eisen `IRON3` bis `IRON7` plus ein helles Silber (226, 228, 230), jeweils zu 80 % zu Grau entsättigt, damit es nicht bläulich wirkt. Fünf feste Stufen je nach Flächenneigung: Lichtkante oben und links hell, Schattenkante unten und rechts dunkel, dazu Punktkörnung. Kein Verlauf, kein Chrom-Glanz, kein weiches Leuchten. Rot nur für Leben. Gold nur für die Aufnahme-Meldung, den Erfahrungsbalken und den Hinweis zum Aufsteigen (Issue #387); alles andere bleibt neutrales Silber.

**Regeln:**

- **Silber statt Stein.** Die Steintextur im HUD ist abgelehnt (Urteil des Menschen).
- **So wenig Spielbild verdecken wie möglich**; Richtwert **unter 10 %**, gemessen am fertigen Bild.
- Layout von Runde 3 A; **Leben und Ausdauer ohne Zahl im HUD, Zahlen auf der Tab-Seite** (#347); Munition, Silber, Stufe und Schlüssel behalten ihre Zahl.
- **Alle Fassungen auf einer Grundlinie**; jede Anzeige hat ihren eigenen dunklen Grund, damit Zeichen auf jedem Spielbild lesbar bleiben.

**Abgelehnt:** A1 (geschlossene Leiste, 12,9 %): verdeckt zu viel. A2 (offener Rahmen, 6,1 %): Zeichen stehen ungeschützt auf dem Spielbild.

**Schrift (Urteil des Menschen, Issue #355): A „Kapitell“.** Eigene Pixelschrift für HUD und Menüs: schlichte Kapitälchen mit kleinen Serifen; Versalhöhe 7 px, Kleinbuchstaben 5 px als kleine Versalien, Zeichenbreite 3–5 px, Abstand 1 px, Leerzeichen 3 px. Pixelmuster und Skripte: `design/proben/grafik/schrift/` (Blatt `blatt-schrift.png`). Cinzel kommt nicht ins Spiel. Menü-Überschriften 3-fach, Einträge 2-fach. **Offen für den Einbau:** eine größere Zahlengröße für Stufe und Munition fehlt noch (Versalhöhe 7 px statt 9–11 px bei Cinzel). Seit Issue #388 steht die Munition 2-fach (14 px), siehe „Munitionszahl“.
- **Verzierung (Runde 5 und 6, Urteil des Menschen: V3, dann überarbeiten):** Ein Motiv an allen Fassungen ab 24 px Breite, immer im selben Silber: Rautenniete in den vier Ecken, Spitzgiebel mit Auge oben in der Mitte (der dünne Doppelrahmen innen ist seit Issue #387 entfernt), an Fassungen über 100 px Breite zusätzlich Fialen mittig an den Enden. Rot und Gold bleiben, wie sie sind; kein Rot als Signalfarbe in der Verzierung.
- **Regeln aus dem Feinschliff (Runde 6):** Licht kommt überall von oben links, auch an den kleinen Teilen (Nieten, Giebel, Fialen nutzen dieselbe Lichtregel wie der Rahmen). Alle Mittelpunkte liegen auf dem Pixelraster, die Giebelbreite passt zur Fassung (gerade oder ungerade), damit die Form spiegelgleich bleibt. Verzierung ragt nie aus dem Bild; Fialen sitzen mittig auf der Fassungskante. Zeichen in kleinen Fassungen so groß, dass sie im 4-fachen Ausschnitt eindeutig lesbar sind (Waffenzeichen 10,5 statt 9,5).

**Nicht gewählt:** V1 (nur Eckrauten): wirkt kaum anders als ohne Verzierung, das HUD bleibt zusammengewürfelt. V2 (Ecken und Giebel): gut, aber ohne Doppelrahmen und Fialen wirken die langen Fassungen noch kahl. Beide bleiben verworfen; wer die Verzierung später ändert, geht von V3 aus.

**Feinschliff nach dem Spieltest (Urteile des Menschen, Issue #387):**

- **Kein Innenrahmen:** Im dunklen Grund einer Fassung steht kein zweiter, grauer Rahmen, auch nicht der helle Ring der aktiven Fassung an der Stufe. Den Ring trägt nur noch die gewählte Waffe, weil er dort die Wahl anzeigt.
- **Keine Markierung auf dem Lebensbalken:** kein senkrechter roter Strich am Füllstand; der Balken selbst zeigt das Leben.
- **Erfahrung golden**, der Hinweis „[X] to level up“ gelb in derselben Goldfarbe, links unter Stufe und Erfahrung.
- **Ein Abstand für alles (6 px):** zwischen den Körpern benachbarter Fassungen (Silberrahmen ohne Nieten und Giebel) und vom Körper zum Bildrand. Die Nieten ragen 3 px über, bei 6 px berühren sich die Verzierungen gerade. Was über oder unter einer Reihe steht (Aufnahme-Meldung, Hinweis, „Consecrated“), hält 6 px zur Verzierung dieser Reihe.
- **Innen gleich verteilt:** Die Inhalte einer Fassung teilen sich den dunklen Grund mit gleich großen Lücken zum Rand und untereinander; ein einzelner Inhalt steht mittig. Zeichen und zugehörige Zahl (Münze und Silber, Schlüssel und Anzahl, Auge und Meldung) stehen 3 px auseinander.
- Die Regel steht als Rechnung in `HudLayout` (Core); die Tests in `HudLayoutTests` prüfen sie für alle Bildbreiten.

**Munitionszahl (Issue #388, vorläufig – Urteil des Menschen steht aus):**

- **Eine große Zahl statt Ziffern neben den Feldern:** In den Waffenfeldern steht nur noch das Waffenbild. Die Patronen der gewählten Fernwaffe stehen groß (Zahlengröße 2-fach, Versalhöhe 14 px) mittig über der Waffenreihe, daneben klein (1-fach) die Magazingröße: `6 / 7`. Groß und klein stehen auf einer Grundlinie, 3 px auseinander, 6 px über der Verzierung der Waffenfelder.
- **Ohne eigene Fassung**, mit dem dunklen Textschatten wie „Consecrated“; der Atlas bleibt unverändert. „Consecrated“ steht darüber, wieder 6 px Abstand.
- **Neutrales Silber, kein Rot.** Die große Zahl in der Hauptfarbe, die Magazingröße in der leiseren Farbe; beim Nachladen wird auch die große Zahl leise. Die Null bleibt durchgestrichen (Schrift „Kapitell“).
- **Nahkampf:** Solange die Nahkampfwaffe in Gebrauch ist (Aufladen, Schlag, bis sie wieder bereit ist), verschwindet die Zahl.
- **Vorrat:** Munition ist unbegrenzt (ADR 0008); hinter dem Schrägstrich steht deshalb die Magazingröße, nicht ein Vorrat.
- Verdeckt bei 854 × 480 in `HudLayoutTests` gerechnet: 7,2 % (Rechtecke der Teile und Texte, also eher zu viel). Fotos: `design/proben/grafik/hud-munition/`.

**Nachlade-Schleier und Skillpunkt-Schein (Issue #389, nach dem Spieltest vom 18.11.2026):**

- **Schleier nur im Waffenfenster (Urteil des Menschen):** Der dunkle Schleier beim Nachladen liegt nur auf dem dunklen Grund innerhalb des Silberrahmens, nie auf dem Rahmen oder darüber hinaus. Zu Beginn deckt er den ganzen Grund.
- **Schleier wandert von links nach rechts (Urteil des Menschen):** Die helle Vorderkante ist die linke Spalte des Schleiers und wandert mit dem Nachladen nach rechts; links wird das Waffenbild frei, rechts bleibt der Rest bedeckt. Rechnung: `HudLayout.ReloadVeil` (Core).
- **Skillpunkt-Schein dezent (Urteil des Menschen: „zu übertrieben“):** Der goldene Schein am Bildrand bleibt Gold (Messing 5), kein Rot. **Vorläufig (Leitung, bis zum nächsten Spieltest):** halbe Stärke (`SkillPointStrength` 0,5) und halbes Band (`SkillPointBand` 6 % der Bildhöhe statt 12 %), in `ScreenEffects` (Core). Regel: Der Schein setzt auch zu Beginn weniger Pixel als der rote Rand bei wenig Leben an seiner schwächsten Stelle.
- Fotos vorher/nachher bei 854 × 480: `design/proben/grafik/effekte-389/`.

### HUD und Menü in höherer Auflösung (entschieden, Issue #393)

**Gewählt: A „harte Pixel“; Urteil des Menschen am 18.11.2026.** Vorlage: `tools/hud/hud_hochaufgeloest.py`, Blatt `design/proben/grafik/hud-hochaufgeloest/blatt-a.png`.

- **Doppelte Auflösung, Pixel bleiben sichtbar:** keine Kantenglättung, kein weicher Schatten, Silber in 8 festen Stufen, Bayer-Raster in Verläufen. Schrift „Kapitell“ bleibt Pixelschrift (Kanten mit halben Pixelstufen, aber hart).
- **Rahmen als Metall-Relief:** in Blender gerendert (Profil aus Fase, Plateau und Stufe, vier Nieten, Licht von oben links), auf harte Pixel heruntergerechnet.
- **Nieten sind Pflicht (Issue #412, Regel 24):** Ecknieten an allen Rahmen, Reiter zusätzlich etwa alle 80 px, Lebensbalken zwischen den langen Strichen, so gesetzt, dass der Schnitt der Fassung keine Niete anschneidet; als Relief aus `tools/hud/hud_nieten.py`, nur die sechs Silberstufen. Eckwinkel und Giebel als Rahmenschmuck sind verworfen (die Menütafel behält ihren Giebel). Vorher/Nachher: `design/proben/grafik/hud-nieten-412/blatt-vorher-nachher.png`.
- **Waffen und Gegenstände im HUD zeigen am Ende kleine Renderings ihrer 3D-Modelle** (feste Kamera, gleiches Licht für alle, auf harte Pixel gerechnet), keine eigens gezeichneten Zeichen. Bis ein Modell existiert, bleibt das heutige Zeichen als Platzhalter.
- **Fehlende Modelle:** Gegenstände Flasche, Auge, Stiefel, Rosenkranz, Halfter, Parade, Egel, Aschenherz; Waffen Gewehr, Donnerbüchse, Keule. Vorhanden sind nur `blade.glb` und `pistol.glb`. Die Modelle entstehen in eigenen Issues.

**Doppelter Atlas (Issue #402, Teil 1 von #397):** `assets/hud/hud_parts_x2.*` und `assets/menu/menu_parts_x2.*`, Raster 1708 × 960, Maßstab 2. Er liegt neben dem heutigen Atlas, der unverändert bleibt.

- **Jedes alte Teil hat ein Gegenstück gleichen Namens** an doppelter Lage und in doppelter Größe (`layoutX/Y`, `w`, `h`, `body`, `shapes`, `slice`). Ausnahme nur Zeichen (`icon_*`): gleiche Mitte, mindestens doppelte Größe (Waffen und Gegenstände etwa 15 % größer und heller), mit 3 px Rand gegen Abschneiden.
- **Neues Teil hinzufügen:** zuerst im heutigen Atlas-Skript, dann das Gegenstück in `tools/hud/hud_atlas_doppelt.py` zeichnen; `python tools/hud/hud_atlas_pruefen.py <Lauf 1> <Lauf 2>` muss ohne Fehler laufen (zwei Läufe bitgleich).
- **Schrift ist nicht im Atlas;** das Spiel zeichnet sie. Zeichen bleiben harte Pixel (Regel 20).

**Abgelehnt: B „geglättet“** (Urteil des Menschen, 18.11.2026): Schrift und Zeichen werden weich. Blatt `blatt-b.png` bleibt als abgelehntes Probestück im Ordner.

## Menüs (entschieden, Issue #351)

**Gewählt: C2 „Schlank“ (Runde 2); vom Menschen am 18.11.2026 abgenommen.** Er hat nur „C2 Schlank“ gesagt; was er an C2 mag, hat er nicht begründet. Vorlage: `design/proben/grafik/menue/runde2/menue_r2.py` (Fassung `P_SCHLANK`, Blatt `blatt-menue-r2.png`, 854 × 480). C2 ist die Form von C aus Runde 1 (HUD-Silberfassung, Schrift A „Kapitell“), schlanker gesetzt:

- **Schmale, hohe Tafeln:** Halbbreite 120 px, also 240 px breit (Runde 1: 300 px). Einträge im Abstand von 62 px, Cursor 36 px hoch.
- **Hoher, steiler Giebel** über jeder Tafel (Halbbreite 15 px, Höhe 20 px), bei 1× gut sichtbar, mit kleinem Auge darunter.
- **Steile Reiter** für die Tab-Seite (Anstieg 0,62), der aktive Reiter ist 50 px hoch, mit Raute auf der Spitze.
- **Cursor:** spitze Silberfassung ohne Zierrauten links und rechts.
- **Keine Doppelungen:** einfacher Rahmen (2,6 px), kein Doppelrahmen, keine Fialen; Regler ohne Teilstriche und ohne Rauten an den Enden, Raute als Knopf.
- Gleich wie im HUD: Silber aus der Reihe IRON, harte Stufen, Gold nur für die Auswahl, kein Rot als Signalfarbe.

**Nicht gewählt:**

- **A „Schlicht“** und **B „Gefasst“** (Runde 1): vom Menschen zugunsten von C abgelehnt, Gründe nannte er nicht.
- **C in der ersten Fassung:** „Menü design C aber noch was eleganter“, also nicht elegant genug. Was „elegant“ heißt, sagte er nicht; die drei Auslegungen in Runde 2 stammen von der Leitung.
- **C1 „Fein“** und **C3 „Geordnet“** (Runde 2): abgelehnt, Gründe nannte er nicht. Einschätzung des Grafikdesigners, nicht sein Urteil: C1 hat den kleinsten Giebel und wirkt am wenigsten wie C; C3 behält viel Zierrat.

Wer die Menüs später ändert, geht von C2 aus. Die Darstellung im Spiel ist Sache der Umsetzungs-Issues (u. a. #348, #349).

### Karte und Ausrüstung als ein Menü (Issue #390, vorläufig)

- **Reiter deckungsgleich:** „[Tab] Map“ und „[G] Equipment“ stehen auf beiden Seiten an genau derselben Stelle, über dem linken Ende der Seitentafel. Beim Blättern wechselt nur, welcher Reiter offen (gold, 8 px höher) ist.
- **Werte-Tafel auf beiden Seiten:** „HUNTER“ und „BOONS“ links, ohne Grund, nur mit 1-px-Rahmen in gedämpftem Silber. Sie beginnt im Abstand der HUD-Regel (6 px) unter der linken oberen HUD-Gruppe einschließlich des gelben Hinweises „[X] to level up“ (`MenuLayout.SheetTopUnder`, aus `HudLayout` gerechnet, seit Issue #387 keine feste Zeile mehr) und deckt kein HUD-Teil und keinen HUD-Text zu. Überschriften 2×, Zeilen 1× wie die HUD-Texte, Namen in der zweiten Farbe, Werte in der Hauptfarbe, alle mit Schatten.
- **Die Seiten stehen rechts davon:** Karte und Ausrüstungstafel beginnen rechts der Werte-Tafel, damit nichts sie verdeckt.
- **Tafeln zu 70 % deckend** (Tafel, Giebel, Reiter, in allen Menüs): Die Szene scheint durch, Schrift und Felder bleiben lesbar. Wird ein Menü damit unlesbar, geht die Deckkraft wieder hoch, nicht die Schrift auf schwarzen Grund.
