# CLAUDE.md – Arbeitsregeln für Silver & Ash

Diese Datei liest Claude bei jeder Aufgabe. Sie beschreibt, wie wir arbeiten. Die Hintergründe stehen in [docs/entscheidungen/](docs/entscheidungen/).

## Rollen

- **Der Mensch** ist Product Owner und Tech Lead: schreibt Anforderungen, plant, reviewt, testet, **merged**. Programmiererfahrung: C#-Grundlagen. Ziel ist **Lernen** – Fachbegriffe beim ersten Auftreten kurz erklären, Git-Befehle immer mitzeigen.
- **Claude** schreibt den Großteil des Codes, legt Branches, Commits und Pull Requests an.

## Arbeitsablauf

1. **Ein Issue = ein Branch = ein kleiner Pull Request.** Branch-Namen: `feat/kurze-beschreibung`, `fix/…`, `docs/…`.
2. **Commits nach Conventional Commits:** `feat: …`, `fix: …`, `docs: …`, `test: …`, `refactor: …`, `chore: …`, `ci: …`.
3. **Pull-Request-Text auf Deutsch** mit den Abschnitten: *Was*, *Warum*, *Wie testen*, *Bitte genauer ansehen*. Verweist mit `Closes #<nr>` auf das Issue.
4. **Claude merged nie.** Nur der Mensch merged.
5. **Keine neuen Bibliotheken (NuGet-Pakete) ohne Zustimmung.**
6. **Bei unklaren Anforderungen fragen statt raten.**
7. **Prüfschleife vor jedem Pull Request:** Build, Tests und eigene Code-Review wiederholen, bis keine Fehler mehr auftreten.
   - **Nie Tests verbiegen**, um grün zu werden (nicht löschen, abschwächen, überspringen). Ist ein Test selbst falsch: im Pull Request ausdrücklich begründen.
   - **Notbremse:** nach ~3 gescheiterten Versuchen am selben Fehler aufhören und fragen.
8. **Nach jedem Merge mit neuem Konzept:** eine Konzept-Notiz im Obsidian-Vault `D:\Obsidian\Gamedev` anlegen (`Konzepte/` oder `Werkzeuge/`, Frontmatter `autor: claude`, auf Deutsch, mit Links zum Code).
9. **Retro am Ende jedes Meilensteins:** Claude schreibt sie als Doku in `Retros/` (fertig, Zahlen, gut/schlecht, neue Begriffe, Änderungen für die nächste Woche) und lässt einen kurzen optionalen Abschnitt „Meine Sicht“ frei.
10. **Alles aufs Kanban-Board:** Pull Requests ohne eigenes Issue (z. B. kleine Doku-Änderungen) kommen direkt als Karte aufs Board in die Spalte *Review*.
11. **Merge immer selbst prüfen** (`gh pr view`), bevor lokal aufgeräumt wird. **Jedes Issue sagt, wie es abgeschlossen wird** – per PR-Merge oder durch Schließen des Issues (bei reinen Einstellungen).

## Definition of Done

Ein Pull Request darf gemergt werden, wenn:
- die CI grün ist (Build + Tests),
- neue Spiellogik eigene Tests hat,
- der Mensch das Feature selbst gespielt hat,
- der Mensch jede geänderte Datei grob verstanden hat und Fragen geklärt sind,
- die Doku angepasst ist, falls nötig.

## Sprachen

- **Code auf Englisch** (Namen, Kommentare).
- **Texte im Spiel auf Englisch** (Menüs, Hinweise, HUD).
- **Alles andere auf Deutsch:** Issues, Pull Requests, Doku, Commit-Beschreibungen.

## Architektur (verbindlich)

- **`SilverAndAsh.Core`**: gesamte Spiellogik und Physik. **Darf Raylib nicht referenzieren.** Mathe über `System.Numerics`.
- **`SilverAndAsh.Game`**: Fenster, Eingabe, Darstellung mit Raylib-cs. Enthält keine Spielregeln.
- **`SilverAndAsh.Core.Tests`**: xUnit-Tests für Core.
- **Feste Zeitschritte** (60 Ticks/s) für die Simulation; Darstellung ist davon getrennt. Tests treiben die Simulation tickweise, ohne Fenster.
- **Spiellogik ist 3D** (freies Umsehen, vertikales Zielen, Schwerkraft, Springen). Eigene einfache Physik: Spieler = Kapsel, Wände = Quader, Geschosse = Strahl/Kugel. Keine Physik-Engine.
- **Level = Raster aus Zellen mit eigener Boden- und Deckenhöhe** (in 1.0 alle gleich, später Treppen/Gruben/Emporen).
- **Darstellung von Figuren hinter einer austauschbaren Schnittstelle** – Bewegungen per Code jetzt, Skelett-Animation später.
- **Balancing-Werte** (Leben, Schaden, Abklingzeiten, Geschwindigkeiten …) in einer JSON-Konfigurationsdatei, nicht im Code verstreut.
- **Logs nur lokal:** technisches Log (Fehler/Warnungen); ab v0.8 Run-Statistik pro Durchlauf.

## Setting

Gothic-Vampirjäger. Inspiration (nur Stimmung): Castlevania, Devil May Cry, Vampire Hunter D, Hellsing. **Eigene Welt, eigene Figuren, eigene Namen** – keine Namen, Figuren oder Elemente aus diesen Werken übernehmen.

## Umfang

Der Umfang von v1.0 und der Meilensteinplan stehen in [docs/entscheidungen/0001-projektgrundlagen.md](docs/entscheidungen/0001-projektgrundlagen.md). Was nicht drinsteht, ist nicht Teil von 1.0 – Vorschläge dafür als Issue mit Label `später` anlegen.

## Umgebung

- Windows 10, PowerShell. Git for Windows, .NET SDK 10, GitHub CLI (`gh`), VS Code.
- Repository: `flubby2222/silver-and-ash` (öffentlich). Commit-E-Mail ist die noreply-Adresse – nie eine andere Adresse eintragen.
