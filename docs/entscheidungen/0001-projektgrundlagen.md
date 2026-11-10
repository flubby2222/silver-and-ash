# 0001 – Projektgrundlagen

- **Status:** angenommen
- **Datum:** 2026-11-10

## Kontext

Das Projekt soll den Ablauf professioneller Softwareentwicklung mit KI erlebbar machen. Vorkenntnisse: C#-Grundlagen, wenig Erfahrung mit Git und Werkzeugen. Ein fertiges Spiel ist Nebenziel; Grafik-Design spielt eine untergeordnete Rolle. Die Entscheidungen wurden in einem gemeinsamen Planungsgespräch getroffen.

## Entscheidung

### Ziel und Rahmen
- **v1.0 Anfang Februar 2027**, ~12 Wochen bei 5–10 Std./Woche. Verschiebungen werden bewusst in der Retro entschieden.
- **Rollen:** Mensch = Product Owner / Tech Lead (plant, reviewt, testet, merged); Claude schreibt den Großteil des Codes.

### Das Spiel
- **Genre:** Ego-Action-Roguelike in Echtzeit, 3D im Low-Poly-Stil der PS2-Ära.
- **Setting:** Gothic-Vampirjäger, eigene Welt.
- **Ablauf:** 3 Ebenen (Katakomben → Kerker → Schlosshalle), Treppe zur nächsten Ebene, nach Ebene 3 gewonnen. Tod = Neustart auf Ebene 1.
- **Steuerung:** WASD, Maus frei umsehen (auch vertikal), Leertaste springen, Items durch Drüberlaufen, Esc = Pause (Fortsetzen, Mausempfindlichkeit, Beenden).
- **Kampf:**
  - Linke Maustaste: **Silberklinge** – kurze Reichweite, viel Schaden, kurze Abklingzeit
  - Rechte Maustaste: **Pistole mit Silberkugeln** – weniger Schaden, längere Abklingzeit
  - **Blutrausch** (Q): Blutleiste füllt sich durch Nahkampftreffer; ~8 s höhere Feuerrate und Bewegung, roter Bildrand
- **Items:** Blutphiole (heilt), Geweihte Munition (mehr Schaden, ~20 s)
- **Gegner:** Ghul (kriecht, Nahkampf), Vampirfledermaus (fliegt, spuckt Blutgeschosse)
- **HUD:** Waffen im Bild, Fadenkreuz, Leben, Abklingzeiten, roter Bildschirmblitz bei Treffer, Treffer-Feedback am Gegner.
- **Look:** niedrige Auflösung hochskaliert, Flat Shading mit Vertex-Farben, Nebel.
- **Modelle:** zuerst Platzhalter aus Grundformen im Code, später per Blender-Python-Skript erzeugt. Bewegungen per Code; Skelett-Animation später.
- **Level:** Raster aus Zellen mit Boden-/Deckenhöhe. Erst handgebaut (Textdatei), dann Zufallsgenerator mit Seed.

### Technik
- C# / .NET 10, Raylib-cs, xUnit.
- Projekte `SilverAndAsh.Core` (ohne Raylib), `SilverAndAsh.Game`, `SilverAndAsh.Core.Tests`.
- Eigene einfache 3D-Physik, feste Zeitschritte (siehe [0002](0002-spiellogik-von-anfang-an-3d.md)).
- Balancing-Werte in JSON. Logs nur lokal.

### Prozess
- Öffentliches GitHub-Repository, `main` geschützt (nur per Pull Request mit grüner CI).
- Kanban auf GitHub Projects, Wochen-Meilensteine, wöchentliche Retro.
- Release pro Meilenstein mit automatisch gebauter Windows-`.zip` und Changelog.
- Arbeitsregeln und Definition of Done: siehe [CLAUDE.md](../../CLAUDE.md).

### Meilensteine

| Woche | Release | Inhalt |
|---|---|---|
| 1 | v0.1 | Walking Skeleton: Repository, Projekte, CI, Release-Pipeline, Fenster mit drehendem Würfel, technisches Log |
| 2 | v0.2 | Ego-Kamera, Bewegung, Physik (Kapsel, Wände, Schwerkraft, Springen), handgebautes Testlevel |
| 3 | v0.3 | Level-Darstellung im PS2-Look, Zellenhöhen |
| 4 | v0.4 | Silberklinge, Pistole, Fadenkreuz, HUD, Platzhalter-Waffen |
| 5 | v0.5 | Ghul: Verhalten, Leben, Schaden, Treffer-Feedback, Tod |
| 6 | v0.6 | Vampirfledermaus: Fliegen, Blutgeschosse |
| 7 | v0.7 | Items, Blutrausch, Balancing-Datei |
| 8 | v0.8 | Titel, Pause, Game Over, Sieg, Treppe, 3 Ebenen, Run-Statistik |
| 9–10 | v0.9 | Zufallsgenerator für Level |
| 11 | v0.10 | Modelle per Blender-Skript |
| 12 | **v1.0** | Balancing, Bugfixes, Feinschliff |

### Nicht in 1.0 (später)
Skelett-Animation, Hub mit Unlockables, Ausweich-Dash, komplexere Level (Treppen, Gruben, Emporen), Bossgegner, weitere Gegner und Items, Texturen, Sound, Speichern, Minimap.

## Konsequenzen

- Der Umfang ist bewusst klein gehalten, damit das Spiel fertig wird und der komplette Ablauf bis zum Release durchlaufen wird.
- Alles ist Code oder Text (auch die Modelle) und damit prüfbar, versionierbar und von der KI bearbeitbar.
- Ego-Perspektive, 3D-Physik und zwei Kampfarten machen das Projekt anspruchsvoller als ein 2D-Spiel; der Zeitplan kann sich verschieben.
