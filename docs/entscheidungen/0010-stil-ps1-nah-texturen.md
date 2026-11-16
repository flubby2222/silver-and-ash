# 0010 – Stil: PS1-nah, mit Texturen und weicher Schattierung

- **Status:** angenommen; Bild und Sichtgrenze am 2026-11-16 nachgetragen
- **Datum:** 2026-11-16
- **Löst ab:**
  - in [0001](0001-projektgrundlagen.md): „3D im Low-Poly-Stil der PS2-Ära“, „Flat Shading mit Vertex-Farben“ und **Texturen** in der Liste „Nicht in 1.0“;
  - in [0009](0009-leitung-designer-auto-merge.md): „Texturen sind weiter nicht Teil von 1.0“.
- **Bleibt:** Alles wird per Skript erzeugt, nichts heruntergeladen (0009). Eigene Welt, eigene Figuren (0001).

## Kontext

Das erste Ghul-Probestück folgte der ersten Art Bible: einfarbige Flächen, harte Kanten, rund 350 Dreiecke. Das Urteil des Menschen: „besser als vorher, aber überhaupt nicht, was ich mir vorgestellt habe“ – falsch in Gestalt, Detailgrad, Farbflächen und Gesicht. Die Vorgaben der Art Bible waren geschätzt, nicht hergeleitet.

Auf seinen Vorschlag hat der Grafikdesigner zuerst recherchiert, wie Figuren und Welten der PlayStation-Zeit tatsächlich gebaut wurden ([`docs/design/grundlagen-grafik.md`](../design/grundlagen-grafik.md)). Der Mensch hat dazu entschieden:

- Texturen sind erlaubt, wenn sie per Skript erzeugt sind.
- Der Detailgrad soll deutlich steigen; ein unscharfer Eindruck ist willkommen, und „wenn PS1-Stil einfacher ist, dann gerne so“.
- Der Ghul ist ein ausgemergelter Mensch, der kriecht – keine Bestie.
- Eigene Idee: die Sichtweite mit einem Dithering-Effekt begrenzen statt mit dunklem Nebel, „wenn, dann aber alles im selben Stil“.

Das zweite Ghul-Probestück (1 024 Dreiecke, eine Textur von 128 × 128 Pixeln) hat er mit „Ja, Richtung stimmt“ bestätigt.

## Entscheidung

### Der Stil ist PS1-nah

Nicht PS2. Gründe aus der Recherche:

- **Kleine Texturen verzeihen.** 64 bis 128 Pixel ohne Filterung brauchen nur grobe, kräftige Formen. Das kann ein Skript. Die gemalten Feinheiten von PS2-Texturen kann es nicht überzeugend.
- **Um die tausend Dreiecke** lassen sich im Skript sauber bauen; mehrere tausend mit glaubhafter Anatomie nicht.
- **Es passt zum Spiel**, das schon harte Pixel und kantige Räume hat.

### Regeln

| Was | Regel |
|---|---|
| Dreiecke | normale Gegner 800 bis 1 200, große Gegner und Bosse 2 000 bis 4 000, Requisiten 100 bis 400, Waffe im Bild bis 1 500 |
| Texturen | eine je Figur, 128 × 128, höchstens 32 Farben; Requisiten und Wandstücke 64 × 64; **keine Filterung, keine Mipmaps** |
| Schattierung von Figuren | weich über die Ecken (**Gouraud**: Die Helligkeit wird an jeder Ecke berechnet und über die Fläche verlaufen gelassen) statt eine Helligkeit je Fläche |
| Licht in der Textur | nur gemalte Tiefe und Licht von oben; die Laterne rechnet weiter das Spiel |
| Umgebung | Wände, Böden und Decken bekommen ebenfalls Texturen im selben Stil. Ein texturierter Gegner in einfarbigen Räumen ist nicht derselbe Stil |
| Bildfehler der PS1 | **keine** wackelnden Ecken, **keine** verzogenen Texturen. Sie stören beim Zielen |
| Herkunft | Geometrie, Textur und Bild entstehen aus einem Skript je Stück |

Der Ghul aus Probestück 3 ist der Maßstab für alle weiteren Figuren.

### Bild und Sichtgrenze (entschieden am 2026-11-16)

Drei Fragen hingen zusammen: Farbtiefe und Körnung, Sichtgrenze, Bildauflösung. Der Mensch hat sie an Vergleichsbildern aus dem echten Spiel entschieden (vier Varianten, PR #235) und **Variante 2** gewählt:

| Frage | Entscheidung |
|---|---|
| Farbtiefe und Körnung | Das ganze 3D-Bild wird auf **15 Bit** (5 je Kanal) mit festem **4 × 4-Punktmuster** gerastert, am Ende des Shaders, für alles in der 3D-Welt |
| Sichtgrenze | **Der Nebel bleibt.** Kein Ausblenden über das Punktmuster |
| Bildauflösung | bleibt **854 × 480** |

Was die Vergleichsbilder gegen die anderen Varianten zeigten: Das Ausblenden über das Punktmuster machte das Bild bis 9 m heller und ferne Gegner besser sichtbar, erzeugte aber an der Sichtgrenze ein unruhiges Punktband mit hartem Dunkel dahinter. Die niedrigere Auflösung machte das Raster deutlich, ferne Gegner aber nur noch wenige Pixel groß.

Offen bleibt, was davon unabhängig ist: Die Fledermaus ist aus 10 m praktisch unsichtbar. Das löst das Modell, nicht die Sichtgrenze. Wie das Bild mit Stein-Texturen und in Bewegung wirkt, zeigt sich erst im Spiel; fällt es dort durch, wird die Entscheidung in einer Retro neu aufgemacht.

## Konsequenzen

- **Mehr Arbeit im Code:** Das Spiel muss Modelle mit Textur laden und weich schattieren können. Bisher zeichnet es nur einfarbige Quader. Dazu kommen Texturen für das Level.
- **v0.14 – Modelle wird größer** als geplant: Zum Austausch der Platzhalter kommen Lader, Shader und Level-Texturen.
- **Die ersten Probestücke sind überholt** (erster Ghul, erste Kiste). Sie bleiben als Vergleich auf dem Branch des Designers.
- **Die Art Bible wird neu gefasst**; ihre Zahlen stammen jetzt aus der Recherche und aus den Urteilen des Menschen.
- **Die Recherche hat Grenzen:** Die meisten Zahlen kommen aus Foren und Fan-Wikis und sind Größenordnungen, keine Messwerte. Das steht im Grundlagen-Dokument bei jeder Aussage dabei.
- **Dateigröße:** Modelle und Texturen liegen als Binärdateien im Repository. Bei kleinen Texturen und wenigen Figuren ist das vertretbar.
- Der Termin Anfang Februar 2027 wird in der nächsten Retro überprüft.
