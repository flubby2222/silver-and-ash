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
| Rechte Maustaste | Pistole (7 Schuss) |
| R | Nachladen (bei leerem Magazin lädt auch ein Schuss nach) |
| Mausrad, 1 / 2 / 3 | Fernwaffe wechseln (bis zu drei Plätze) |
| F | Interagieren (Türen, Kisten) |
| G | Menüseite „Equipment“ (Inventar) an/aus, aus der Kartenseite blättert G dorthin um; nur hier erscheint der Mauszeiger |
| Tab | Menüseite „Map“ an/aus, aus der Ausrüstungsseite blättert Tab dorthin um: links Werte (Leben, Ausdauer, Schaden, Tempo …) und gewählte Boons, rechts die Karte mit betretenen Räumen, ihren Türen (Schloss, Boss-Tür), Kisten, liegen gelassenen Blutphiolen und der eigenen Position; das Spiel läuft weiter, Laufen und Treffer schließen sie nicht |
| N | Ton aus/an |
| K | Klangpaket wechseln: Klangpaket 1 ↔ alte Geräusche (zum Vergleichen, oben steht kurz der Name) |
| X, dann 1–4 oder Klick | Bonus nach einem Stufenaufstieg wählen – das Spiel läuft weiter; mehrere Aufstiege warten und kommen nacheinander; man kann dabei laufen und getroffen werden; X oder Esc schließt die Wahl |
| Q | Blutrausch |
| Esc | Schließt zuerst ein offenes Fenster (Bonus-Wahl, Menü mit Karte und Ausrüstung), sonst Menü: weiter, Einstellungen, zum Titel, beenden (allein steht das Spiel solange still). In den Einstellungen und bei der Frage vor dem Beenden geht Esc einen Schritt zurück |

**Einstellungen** erreichst du vom Titelbild und aus dem Esc-Menü: Mausempfindlichkeit, Gesamtlautstärke, Lautstärke der Effekte (alle kurzen Geräusche), Lautstärke der Atmosphäre (die Musik) und Fenster oder Vollbild. **Links/Rechts** (oder ein Klick auf die linke oder rechte Hälfte einer Zeile) ändert einen Wert; jede Änderung wirkt sofort. Das Spiel speichert sie lokal in `%LOCALAPPDATA%\SilverAndAsh\settings.json` – kein Spielstand, nichts wird verschickt – und startet beim nächsten Mal genauso. Fehlt die Datei oder ist sie kaputt, gelten die Standardwerte (volle Lautstärke, Fenster). **Beenden** fragt vorher nach; vorausgewählt ist „Cancel“.

**Run-Statistik:** Jeder beendete Durchlauf (Tod oder Sieg) landet als eine Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log`, z. B. `2026-11-15 18:40:12 | won | seed 123 | 12:34 | ghouls 31 | bats 9 | dealt 4210 | taken 380 | level 6 | rooms 9/9`. Auch das bleibt auf dem eigenen Rechner.

## Rechte

© 2026 flubby2222 – alle Rechte vorbehalten. Der Code ist öffentlich einsehbar, damit man die Entwicklung mitverfolgen kann; Verwenden, Kopieren, Verändern oder Weitergeben (Code, Assets, Spiel) ist ohne schriftliche Erlaubnis nicht gestattet. Details in [LICENSE](LICENSE).
