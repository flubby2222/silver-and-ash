---
name: architektur-durchsicht
description: Architektur-Durchsicht am Ende eines Meilensteins – sucht Stellen, an denen der Code schwer zu ändern oder zu testen ist, und legt sie als kurzen Bildbericht mit Top-Empfehlung vor.
disable-model-invocation: true
---

# Architektur-Durchsicht

Vorgehen nach dem Skill `improve-codebase-architecture` aus `mattpocock/skills` (MIT-Lizenz), eigene Fassung für Silver & Ash. Bei Widerspruch gilt `CLAUDE.md`.

Die Durchsicht ist der „Blick von oben“ aus `CLAUDE.md` (Abschnitt *Nach dem Merge*). Sie läuft in einer frischen Sitzung, ändert keinen Code und ist keine Schranke für einzelne PRs.

## Begriffe

- **Modul:** eine Klasse oder eine kleine Gruppe von Klassen mit einer Aufgabe.
- **Schnittstelle:** was ein Aufrufer wissen muss, um das Modul zu benutzen (Methoden, Reihenfolge, Vorbedingungen).
- **Tiefes Modul:** kleine Schnittstelle, viel Verhalten dahinter. **Flaches Modul:** Die Schnittstelle ist fast so umfangreich wie das, was dahinter steckt.
- **Löschprobe:** Würde das Löschen des Moduls die Schwierigkeit an einer Stelle bündeln oder nur verschieben? Nur verschieben = flach.

Für das Spiel gelten die Namen aus `docs/glossar.md`.

## Eingabe

- Optional eine Richtung als Argument (Modul, Bereich, Ärgernis). Dann nur dort suchen.
- Sonst: der Stand von `main` am Ende des Meilensteins.

## Ergebnis

- Ein Bildbericht mit höchstens fünf Kandidaten und einer Top-Empfehlung, außerhalb des Repositorys.
- Der CRAP-Bericht für `Core` (`python tools/crap-report.py`) als zweite Quelle.
- Nach dem Urteil des Menschen: Aufräum-Issues.

## Schritte

### 1. Eingrenzen

Ein Umbau lohnt sich dort, wo sich der Code weiter ändern wird. Deshalb zuerst die Schwerpunkte bestimmen:

- `git log --oneline --name-only` über den Meilenstein: Welche Dateien kommen immer wieder vor?
- CRAP-Bericht: Welche Methoden sind zugleich verzweigt und schwach getestet?
- Befunde der Reviews im Meilenstein, die sich wiederholt haben.

Dann die ADRs in `docs/entscheidungen/` lesen, die den Bereich betreffen. Was dort entschieden ist, wird nicht neu aufgerollt.

### 2. Durchgehen

Den Code der Schwerpunkte lesen und notieren, wo es hakt:

- Wo muss man zwischen vielen kleinen Klassen springen, um einen Begriff zu verstehen?
- Wo ist ein Modul flach (Löschprobe)?
- Wo wächst eine Methode durch angehängte Fälle (Regel „Neue Fälle nicht anhängen“)?
- Wo steht dieselbe Logik doppelt?
- Wo weicht der Code von einem ADR ab (Raylib in `Core`, veränderlicher `static`-Zustand, „der Spieler“ statt „ein Spieler“, Balancing-Zahl im Code)?
- Was lässt sich nur schwer tickweise testen?

Jeder Kandidat braucht einen Beleg: Datei, Stelle, und ein Fall aus dem Meilenstein, in dem es Zeit gekostet hat. Ohne Beleg kein Kandidat.

### 3. Bildbericht

Eine einzelne HTML-Datei ohne Skripte oder Stile von fremden Servern, im Arbeitsordner außerhalb des Repositorys. Je Kandidat eine Karte:

- **Dateien**
- **Problem** in ein bis zwei Sätzen, in den Worten des Glossars
- **Vorschlag** in einfachem Deutsch – noch keine fertige Schnittstelle
- **Gewinn:** Was wird einfacher zu ändern, welcher Test wird möglich oder kürzer?
- **Vorher/Nachher** als einfache Zeichnung (Kästen und Pfeile)
- **Stärke:** *klar*, *prüfenswert* oder *Vermutung*
- **Widerspruch zu einem ADR?** Nur nennen, wenn die Reibung belegt ist; dann deutlich markieren.

Am Ende: **Top-Empfehlung** – welcher Kandidat zuerst und warum. Abweichungen von ADRs und Doppelungen ohne Umbau-Vorschlag stehen als kurze Liste darunter.

### 4. Vorlegen

Die Leitung zeigt dem Menschen den Bericht mit eigener Einschätzung und fragt im Rückfragefenster, welche Kandidaten angegangen werden (Empfehlung als erste Option). Kein langes Interview: Offene Entwurfsfragen zu einem gewählten Kandidaten klärt die Leitung im Issue und legt nur echte Richtungsentscheidungen vor.

### 5. Nacharbeit

- Gewählte Kandidaten werden Aufräum-Issues mit Abnahmekriterien (Skill `to-tickets`); ein Umbau = ein Issue = ein kleiner PR, bestehende Tests bleiben unverändert grün.
- Lehnt der Mensch einen Kandidaten aus einem Grund ab, der auch künftig gilt, schlägt die Leitung ein ADR vor (Leitplanken-PR), damit die nächste Durchsicht ihn nicht wieder bringt.
- Neue Begriffe kommen ins Glossar.
