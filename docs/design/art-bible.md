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

**Die Welt** ist entsättigt und kühl. Die Grundfarben der Flächen heute (sie werden mit den Level-Texturen zur Palette des Steins):

| Fläche | RGB |
|---|---|
| Wand | 122, 104, 112 |
| Boden | 84, 72, 78 |
| Decke | 64, 56, 66 |

**Figuren und Gegenstände** müssen sich davon abheben:

- Untote: **kränklich, kalt, etwas heller als die Wand** (Haut um 124, 132, 112).
- **Rot ist reserviert** für Blut, Treffer, Gefahr (Blitz bei kritischem Treffer 230, 40, 50; der normale Treffer-Blitz ist fast weiß) und die höchste Stufe *Vampiric* (blutrot, pulsierend). Kein Rot als bloße Zierde.
- **Warmes Licht** (Gelb, Orange) gehört der Laterne und allem, was der Spieler haben will.
- **Silber** – hell, kühl, fast weiß – gehört dem Jäger und seinen Waffen.
- **Die vier Stufen von Ausrüstung und Kisten haben eigene Farben:** *Worn* = Holzbraun, *Forged* = Stahlblau, *Royal* = Gold, *Vampiric* = Blutrot (bei Kisten heißen sie *Beggar*, *Guard*, *King*, *Vampire*). Festgelegt in [`ChestView.cs`](../../src/SilverAndAsh.Game/Rendering/ChestView.cs) und [`TierColours.cs`](../../src/SilverAndAsh.Game/Rendering/TierColours.cs). Lies sie dort nach, bevor du Kisten oder Beute gestaltest, und verwende sie für nichts anderes.

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
- **Offen für den Einbau:** wie Raylib die Teile eines Modells und ihre Drehpunkte lädt, und wie Farben zwischen Blender und Spiel umgerechnet werden.

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
