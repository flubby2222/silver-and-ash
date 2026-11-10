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
| Q | Blutrausch |
| Esc | Pause |
