# Art Bible – wie Silver & Ash aussieht

Dieses Dokument liest der Grafikdesigner vor jeder Aufgabe. Es hat zwei Teile: den **gewünschten Stil** und die **gelernten Regeln**. Jede Ablehnung eines Probestücks wird unten zu einer Regel, damit derselbe Fehler nicht zweimal vorkommt (siehe [ADR 0009](../entscheidungen/0009-leitung-designer-auto-merge.md)).

**Stand:** erste Fassung der Leitung vom 2026-11-16, aus den bisherigen Entscheidungen abgeleitet. Vom Menschen noch nicht abgenommen; die Stil-Abnahme steht aus.

## Leitbild

Ein Vampirjäger steigt mit einer Laterne durch Katakomben, Kerker und Schlosshalle hinab. Man sieht nur, was die Laterne erreicht; alles dahinter verschwindet in violett-schwarzem Nebel. Die Welt ist alt, kalt und aus Stein. Farbe ist selten und bedeutet immer etwas: Gefahr, Beute, Leben.

Der Look ist **Low-Poly wie zur PS2-Zeit**: wenige, kantige Flächen, keine Texturen, sichtbare Pixel. Das ist eine bewusste Entscheidung, kein Mangel. Ein Modell ist gut, wenn man es an seiner **Silhouette** erkennt.

Stimmung als Anhaltspunkt: Castlevania, Devil May Cry, Vampire Hunter D, Hellsing. **Nichts daraus wird übernommen** – keine Figur, kein Name, keine erkennbare Form.

## Technischer Rahmen (steht im Code)

Diese Werte gelten und stehen in [`LookSettings.cs`](../../src/SilverAndAsh.Game/Rendering/LookSettings.cs) und [`level.fs`](../../assets/shaders/level.fs):

| Was | Wert | Folge für Modelle |
|---|---|---|
| Bildgröße der 3D-Welt | 854 × 480, hart hochskaliert | Ein Pixel ist bei 4 m etwa 1 cm, bei 10 m etwa 3 cm groß; Details unter zwei Pixeln gehen verloren |
| Schattierung | Flat Shading, **Vertex-Farben, keine Texturen** | Jede Fläche hat genau eine Farbe; Form entsteht durch Flächen, nicht durch Bemalung |
| Nebel | beginnt bei 3,25 m, ab 18,2 m nur noch Nebel | Bei 10 m ist knapp die Hälfte der Farbe Nebel (Anteil etwa 0,43), ab etwa 14 m bleibt fast nur die Silhouette (Richtwert) |
| Nebelfarbe | RGB 16, 11, 20 (violett-schwarz) | Sehr dunkle Modellfarben verschwinden früh |
| Laterne | volle Helligkeit nah, 35 % in der Ferne | Farben wirken im Spiel dunkler als im Probebild |
| Blickwinkel | 70° vertikal, Ego-Sicht, Augen auf 1,6 m | Den Ghul (etwa 1 m hoch) sieht man von vorn und leicht von oben; die Fledermaus (schwebt über Kopfhöhe) und Gorm (über 3 m) sieht man **von unten** – dort muss das Modell gut aussehen |

Die beiden Formeln aus dem Shader, mit Entfernung `d` in Metern zur Kamera:

- **Laterne:** `helligkeit = 1 − 0,65 · min(d / 18,2; 1)`
- **Nebel:** `anteil = smoothstep(3,25; 18,2; d)`, also 0 bis 3,25 m, 1 ab 18,2 m, dazwischen eine weiche S-Kurve
- **Ergebnis:** `farbe = mischen(vertexfarbe · helligkeit, nebelfarbe, anteil)`

## Farben

**Die Welt** ist entsättigt und kühl. Grundfarben heute:

| Fläche | RGB |
|---|---|
| Wand | 122, 104, 112 |
| Boden | 84, 72, 78 |
| Decke | 64, 56, 66 |

**Figuren und Gegenstände** müssen sich davon abheben, sonst gehen sie im Stein unter:

- Der Platzhalter-Ghul nutzt fahle, grünliche Haut (124, 132, 112) und dunkle Lumpen (62, 52, 54). Diese Richtung – **kränklich, kalt, etwas heller als die Wand** – ist der Ausgangspunkt für Untote.
- **Rot ist reserviert** für Blut, Treffer, Gefahr (Treffer-Blitz 230, 40, 50) und die höchste Stufe *Vampiric* (blutrot, pulsierend). Kein Rot als bloße Zierde.
- **Warmes Licht** (Gelb, Orange) gehört der Laterne und allem, was der Spieler haben will.
- **Silber** – hell, kühl, fast weiß – gehört dem Jäger und seinen Waffen.
- **Die vier Stufen von Ausrüstung und Kisten haben eigene Farben:** *Worn* = Holzbraun, *Forged* = Stahlblau, *Royal* = Gold, *Vampiric* = Blutrot (bei Kisten heißen sie *Beggar*, *Guard*, *King*, *Vampire*). Festgelegt in [`ChestView.cs`](../../src/SilverAndAsh.Game/Rendering/ChestView.cs) und [`TierColours.cs`](../../src/SilverAndAsh.Game/Rendering/TierColours.cs). Sie bedeuten im Spiel „so wertvoll ist das“. Lies sie dort nach, bevor du Kisten oder Beute gestaltest, und verwende sie für nichts anderes.

Pro Modell **höchstens vier bis fünf Farben**, davon eine als Akzent.

## Form

- **Silhouette zuerst.** Jede Figur braucht eine Form, die man als schwarzen Umriss von den anderen unterscheidet: der Ghul geduckt mit langen Armen, die Fledermaus breit und flach, Gorm massig mit Kette.
- **Lesbarkeit im Kampf geht vor Schönheit.** Der Kopf ist bei Ghul und Gorm eine eigene Trefferzone: Ein Fernwaffen-Treffer dort ist kritisch (Pistole beim Ghul dreifacher, bei Gorm 1,5-facher Schaden; die Werte stehen in `Balance` und können sich ändern). Der Kopf muss deshalb klar abgesetzt und erkennbar sein. Die Fledermaus hat keine Kopfzone. Ein Körperteil, das einen Angriff ankündigt (Arme des Ghuls, Kette von Gorm), muss groß genug sein, um die Bewegung zu zeigen.
- **Modell und Trefferkörper passen zusammen.** Die Maße stehen in `BodySizes` im Core. Ein Modell darf nicht deutlich größer oder kleiner aussehen als das, was man treffen kann.
- **Kantig, nicht rund.** Lieber sechs klare Flächen als sechzehn, die eine Rundung vortäuschen.
- **Bewegung kommt per Code.** Körperteile, die sich bewegen sollen (Arme, Kiefer, Flügel, Deckel), sind eigene Teile mit einem sinnvollen Drehpunkt. Keine Skelett-Animation.

## Richtwerte

Vorläufig, bis die ersten Probestücke zeigen, was im Spiel trägt:

| Art | Dreiecke |
|---|---|
| Kleiner Gegenstand (Phiole, Schlüssel) | bis 60 |
| Waffe im Bild | bis 250 |
| Kiste, Requisite | bis 200 |
| Normaler Gegner | bis 400 |
| Boss | bis 900 |

## Vom Modell ins Spiel – noch offen

Diese Punkte sind nicht entschieden. Sie werden mit dem ersten Probestück festgelegt und dann hier eingetragen; bis dahin schlägt der Grafikdesigner eine Lösung vor und begründet sie:

- **Dateiformat** des Modells und wie die Vertex-Farben ohne Material aus Blender ins Spiel kommen.
- **Achsen und Einheit:** Blender rechnet mit z nach oben, das Spiel mit x rechts, y oben, z vorn; eine Einheit ist ein Meter.
- **Ursprung:** voraussichtlich zwischen den Füßen auf dem Boden, wie bei den Platzhaltern.
- **Ablage und Namen** der Dateien unter `assets/` und der Erzeuger-Skripte unter `tools/`.

Fest steht: Figuren hängen im Spiel hinter einer austauschbaren Schnittstelle (z. B. `IGhoulView` in [`GhoulViews.cs`](../../src/SilverAndAsh.Game/Rendering/GhoulViews.cs)). Ein Modell ersetzt den Platzhalter dort, ohne die Spiellogik zu berühren. Jede Datei unter `assets/` bekommt eine Zeile in `assets/HERKUNFT.md` (Datei, Skript, Datum); die Datei wird mit dem ersten integrierten Stück angelegt.

## Was ein Probestück zeigen muss

Alle Bilder entstehen **in Blender**, mit dem Skript des Probestücks, ohne Fenster:

1. **Vier Ansichten** (vorn, Seite, hinten, schräg oben) vor der Nebelfarbe, flach schattiert, nur Vertex-Farben.
2. **Zwei Bilder unter Spielbedingungen**, gerendert in 854 × 480 mit 70° vertikalem Blickwinkel aus Augenhöhe 1,6 m, einmal aus 4 m und einmal aus 10 m Entfernung. Laterne und Nebel werden mit den beiden Formeln oben nachgebaut (pro Fläche aus ihrer Entfernung zur Kamera berechnet). Das ist eine Annäherung an das Spiel, kein Ersatz dafür.

Die Leitung urteilt vor allem nach den beiden Bildern unter Spielbedingungen. Sobald das Foto-Skript des Spiels existiert ([#224](https://github.com/flubby2222/silver-and-ash/issues/224)), ersetzt dessen Szene „Modell-Probe“ den Nachbau in Blender.

## Abnahme

- Die Leitung nimmt jedes Probestück an oder lehnt es mit Begründung ab. Jede Ablehnung wird unten zur Regel.
- **Ins Spiel** kommt ein Stück erst, wenn **drei Stücke in Folge ohne Nacharbeit** angenommen wurden.
- Davor sieht der Mensch einmal eine **Stil-Abnahme**: eine Seite mit den Bildern der ersten Stücke. Sein Urteil geht vor und wird ebenfalls zur Regel.

## Gelernte Regeln

Hier trägt die Leitung nach jeder Ablehnung ein: was abgelehnt wurde, warum, und was stattdessen gilt.

| Nr. | Datum | Stück | Regel |
|---|---|---|---|
| – | – | – | *noch keine* |
