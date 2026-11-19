# Teleporter (Issue #436) – Probestück, drei Varianten

Erzeugt mit `teleporter.py` (Blender-Python, nichts heruntergeladen). Aufruf:
`blender --background --python design/proben/grafik/teleporter-436/teleporter.py`.
Zwei Läufe liefern bitgleiche Dateien (Hash-Vergleich von `blatt.png`, aller `.glb` und eines Spielbilds).
Probenblatt: `blatt.png`.

## Vorläufige Annahmen (im Issue offen, bitte bestätigen)

Das Issue sagt nur „Teleporter an POIs, vor 1.0 gewünscht“. Folgendes ist **vorläufig angenommen**:

1. Ein Teleporter steht je Sehenswürdigkeit (POI) und verbindet sie mit den anderen freigeschalteten.
2. Zustand **ruhend** = noch nicht freigeschaltet oder gesperrt; **aktiv** = benutzbar. Freigeschaltet wird beim ersten Betreten bzw. Benutzen.
3. Benutzen per Taste im Bogen/Kreis, danach Auswahlliste oder Karte.
4. In einem verriegelten Kampfraum bleibt er ruhend.
5. Der Sockel (0,2 m) ist begehbar, nur die Pfeiler/Hörner sperren.

## Fragen an den Menschen

- Stimmen die Annahmen 1 bis 5? Insbesondere: Durchschreiten oder Taste drücken?
- Gesperrt im Kampf: gewollt?
- Welche Variante: A (Empfehlung), B oder C?
- Darf der Teleporter im Kampfraum stehen oder nur in ruhigen POI-Zellen?

## Maße und Dreiecke

Grundfläche rund 1,76 m im Quadrat (passt in die 2-m-Zelle), Sockel 0,2 m hoch. Spieler 1,8 m, Zellentür 3,5 m.

| Variante | Höhe | Dreiecke (Körper + Leuchtteil) | Summe |
|---|---|---|---|
| A Bogentor | 3,30 m | 278 + 16 | 294 |
| B Säulenkreis | 2,45 m | 496 + 52 | 548 |
| C Hornkristall | 2,65 m | 264 + 8 | 272 |

Richtwert der Art Bible für Props: 100 bis 400. **B liegt darüber** (548); wäre mit weniger Pfeilerringen zu kürzen.

## Palette und Farbe

Nur Palettenfarben: Stein, Werkstein (Sonderportal-Regel: der Teleporter ist ein solches), Eisen, Stoff.
Atlas 64×64 (`texturen/`), Farben exakt aus `hauptpalette.json`, keine neue Farbe.
Ruhend: dumpfes Eisen/Dunkel. Aktiv: kaltes Blau-Weiß (Stoff 5 bis 7, Eisen 7).
**Kein Rot** (reserviert), **kein Gold** (Beute/Laterne).

## Geschlossenheit

`tools/models/check_closed.py`: alle sechs Modelle ohne offene Kanten (Regel 21). Bei A meldet es 5 Kanten mit mehr als
zwei Flächen im Körper; das sind sich durchdringende Einzelteile (Pfeiler, Balken, Spitze), keine Löcher.

## Was der Einbau bräuchte (nicht Teil dieses PR)

**Core:** Teleporter-Zustand je POI-Zelle mit Freischalt-Flag; Teleport als Eingabe im Tick (ADR 0003: kein static-Zustand,
Zufall nur mit Seed); Ablehnung im gesperrten Kampfraum; Platzierung im Generator mit Test über 100 Level; Ereignisse
für Zustandswechsel und Benutzung.

**Game:** Ansicht je Zustand (zwei `.glb` oder Atlas-Tausch); Kollisionsfläche nur Pfeiler; die Laterne dimmt das „Leuchten“
im Spiel, ein selbstleuchtender Teil bräuchte eine Option ohne Beleuchtung; optional Puls/Drehung des Leuchtteils.

**Assets:** `assets/models/teleporter.glb`, Zeile in `assets/HERKUNFT.md`; Klang als eigenes Issue für den Audiodesigner.
