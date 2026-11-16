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
| Shift | am Boden: Ausweichrolle (Dodge, geduckt) · in der Luft: Dash in Laufrichtung |
| W + Shift (in der Luft) | Dash in Blickrichtung, auch schräg nach oben |
| Linke Maustaste | Silberklinge |
| Rechte Maustaste | Pistole (6 Schuss) |
| R | Nachladen (bei leerem Magazin lädt auch ein Schuss nach) |
| F | Interagieren (Türen, Kisten) |
| Tab (halten) | Karte: betretene Räume, ihre Türen (Schloss, Boss-Tür), Kisten, liegen gelassene Blutphiolen, eigene Position |
| M | Karte an/aus: bleibt offen, auch beim Laufen |
| X, dann 1–4 oder Klick | Bonus nach einem Stufenaufstieg wählen – das Spiel läuft weiter; mehrere Aufstiege warten und kommen nacheinander; man kann dabei laufen, nur ein Treffer schließt die Wahl |
| Q | Blutrausch |
| Esc | Menü: weiter, Mausempfindlichkeit, zum Titel, beenden (allein steht das Spiel solange still) |

**Einstellungen** (heute nur die Mausempfindlichkeit) speichert das Spiel lokal in `%LOCALAPPDATA%\SilverAndAsh\settings.json` – kein Spielstand, nichts wird verschickt. Fehlt die Datei oder ist sie kaputt, gelten die Standardwerte.

**Run-Statistik:** Jeder beendete Durchlauf (Tod oder Sieg) landet als eine Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log`, z. B. `2026-11-15 18:40:12 | won | seed 123 | 12:34 | ghouls 31 | bats 9 | dealt 4210 | taken 380 | level 6 | rooms 9/9`. Auch das bleibt auf dem eigenen Rechner.

## Rechte

© 2026 flubby2222 – alle Rechte vorbehalten. Der Code ist öffentlich einsehbar, damit man die Entwicklung mitverfolgen kann; Verwenden, Kopieren, Verändern oder Weitergeben (Code, Assets, Spiel) ist ohne schriftliche Erlaubnis nicht gestattet. Details in [LICENSE](LICENSE).
