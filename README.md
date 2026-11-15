# Silver & Ash

Ein Ego-Action-Roguelike im Low-Poly-Stil der PS2-Ära: Als Vampirjäger kämpfst du dich mit Silberklinge und Pistole durch ein Vampirschloss – von den Katakomben bis zur Schlosshalle.

> **Status:** Projektstart – noch nicht spielbar. Ziel für v1.0: Anfang Februar 2027.

## Worum es in diesem Projekt geht

Silver & Ash ist ein **Lernprojekt**: Ziel ist es, den Ablauf professioneller Softwareentwicklung mit KI-Unterstützung kennenzulernen – Issues, Feature-Branches, Pull Requests, Code-Review, automatisierte Tests, CI und Releases. Ein fertiges Spiel ist das Nebenziel.

## Technik

- C# / .NET 10
- [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) für Fenster, Eingabe und Darstellung
- Eigene, einfache 3D-Physik
- xUnit für Tests, GitHub Actions für CI

## Dokumentation

- [Entscheidungen](docs/entscheidungen/) – warum das Projekt so ist, wie es ist
- [Changelog](CHANGELOG.md) – was sich pro Version geändert hat
- [Level-Format](docs/level-format.md) – wie man Level baut
- [Release veröffentlichen](docs/release.md) – Schritt für Schritt
- [CLAUDE.md](CLAUDE.md) – Arbeitsregeln für die KI

## Steuerung (geplant)

| Eingabe | Aktion |
|---|---|
| WASD | Laufen |
| Maus | Umsehen / Zielen |
| Leertaste | Springen |
| Linke Maustaste | Silberklinge |
| Rechte Maustaste | Pistole |
| F | Interagieren (Tür öffnen) |
| Q | Blutrausch |
| Esc | Menü: weiter, Mausempfindlichkeit, zum Titel, beenden (allein steht das Spiel solange still) |

**Einstellungen** (heute nur die Mausempfindlichkeit) speichert das Spiel lokal in `%LOCALAPPDATA%\SilverAndAsh\settings.json` – kein Spielstand, nichts wird verschickt. Fehlt die Datei oder ist sie kaputt, gelten die Standardwerte.

**Run-Statistik:** Jeder beendete Durchlauf (Tod oder Sieg) landet als eine Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log`, z. B. `2026-11-15 18:40:12 | won | seed 123 | 12:34 | ghouls 31 | bats 9 | dealt 4210 | taken 380 | level 6 | rooms 9/9`. Auch das bleibt auf dem eigenen Rechner.
