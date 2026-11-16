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
| Bildgröße der 3D-Welt | 854 × 480, hart hochskaliert | Details unter etwa 3 cm sind auf einige Meter nicht mehr zu sehen |
| Schattierung | Flat Shading, **Vertex-Farben, keine Texturen** | Jede Fläche hat genau eine Farbe; Form entsteht durch Flächen, nicht durch Bemalung |
| Nebel | beginnt bei 3,25 m, ab 18,2 m nur noch Nebel | Jenseits von etwa 10 m bleibt nur die Silhouette |
| Nebelfarbe | RGB 16, 11, 20 (violett-schwarz) | Sehr dunkle Modellfarben verschwinden früh |
| Laterne | volle Helligkeit nah, 35 % in der Ferne | Farben wirken im Spiel dunkler als im Probebild |
| Blickwinkel | 70° vertikal, Ego-Sicht | Gegner sieht man von vorn und leicht von oben |

## Farben

**Die Welt** ist entsättigt und kühl. Grundfarben heute:

| Fläche | RGB |
|---|---|
| Wand | 122, 104, 112 |
| Boden | 84, 72, 78 |
| Decke | 64, 56, 66 |

**Figuren und Gegenstände** müssen sich davon abheben, sonst gehen sie im Stein unter:

- Der Platzhalter-Ghul nutzt fahle, grünliche Haut (124, 132, 112) und dunkle Lumpen (62, 52, 54). Diese Richtung – **kränklich, kalt, etwas heller als die Wand** – ist der Ausgangspunkt für Untote.
- **Rot ist reserviert** für Blut, Treffer und Gefahr (Treffer-Blitz 230, 40, 50). Kein Rot als bloße Zierde.
- **Warmes Licht** (Gelb, Orange) gehört der Laterne, Beute und allem, was der Spieler haben will.
- **Silber** – hell, kühl, fast weiß – gehört dem Jäger und seinen Waffen.
- Die Farben der Kisten-Stufen sind vergeben ([`TierColours.cs`](../../src/SilverAndAsh.Game/Rendering/TierColours.cs)); Modelle verwenden sie nicht für anderes.

Pro Modell **höchstens vier bis fünf Farben**, davon eine als Akzent.

## Form

- **Silhouette zuerst.** Jede Figur braucht eine Form, die man als schwarzen Umriss von den anderen unterscheidet: der Ghul geduckt mit langen Armen, die Fledermaus breit und flach, Gorm massig mit Kette.
- **Lesbarkeit im Kampf geht vor Schönheit.** Der Kopf ist die Trefferzone für dreifachen Schaden – er muss klar abgesetzt und erkennbar sein. Ein Körperteil, das einen Angriff ankündigt (Arme des Ghuls, Kette von Gorm), muss groß genug sein, um die Bewegung zu zeigen.
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

## Was ein Probestück zeigen muss

Bilder aus vier Blickwinkeln (vorn, Seite, hinten, schräg oben) vor der Nebelfarbe, dazu **ein Bild in Spielauflösung** aus etwa 4 m und eines aus etwa 10 m Entfernung, mit Laterne und Nebel wie im Spiel. Die Leitung urteilt vor allem nach den beiden letzten.

## Gelernte Regeln

Hier trägt die Leitung nach jeder Ablehnung ein: was abgelehnt wurde, warum, und was stattdessen gilt.

| Nr. | Datum | Stück | Regel |
|---|---|---|---|
| – | – | – | *noch keine* |
