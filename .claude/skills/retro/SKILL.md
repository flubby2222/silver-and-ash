---
name: retro
description: Rückblick auf eine Sitzung oder einen Pull Request – nicht auf das Spiel, sondern auf die Arbeitsumgebung der Sitzungen. Findet, was Zeit gekostet hat, und macht daraus Prüfungen, Skripte, Regeln oder Doku.
disable-model-invocation: true
---

# Retro: Was hat die Sitzungen aufgehalten?

Vorgehen nach dem Skill `retro` aus `mattpocock/skills` (MIT-Lizenz), eigene Fassung für Silver & Ash. Bei Widerspruch gilt `CLAUDE.md`.

Die Retro am Ende eines Meilensteins (Notiz in `Retros/`, siehe `CLAUDE.md`) bleibt davon unberührt. Dieser Skill ist der kleine Rückblick dazwischen: nach einem PR mit mehreren Runden, nach einer gescheiterten Sitzung oder wenn der Mensch `/retro` aufruft.

## Eingabe

- Der Verlauf der eigenen Sitzung, dazu die Berichte der Arbeits-, Reviewer- und Tester-Sitzungen, die dazugehören.
- Optional eine PR- oder Issue-Nummer als Argument: dann nur dieser Vorgang.

## Ergebnis

- Eine kurze Liste von Kandidaten, nach Schwere geordnet, mit Beleg und Vorschlag.
- Nach dem Urteil des Menschen: je angenommenem Kandidaten ein Issue oder eine Zeile in der Gelerntes-Notiz. Der Skill ändert selbst keine Leitplanken.

## Schritte

### 1. Reibung sammeln

Den Verlauf durchgehen und jede Stelle notieren, an der Zeit verloren ging. Nur Belegtes zählt (Befehl, Fehlermeldung, Runde der Review), keine Vermutungen. Kategorien:

| Kategorie | Frage | Wohin gehört die Abhilfe? |
|---|---|---|
| **Wegfindung** | Wo wurde lange nach Datei, Begriff oder Entscheidung gesucht? | `docs/`, `docs/glossar.md`, Verweis im Issue |
| **Automatische Prüfungen** | Welcher Fehler fiel erst in Review, Test oder CI auf, obwohl ihn eine Maschine früher hätte finden können? | Test, CI-Schritt, Hook in `.claude/hooks/` |
| **Regeln mit Urteil** | Welchen Befund hat der Reviewer zum wiederholten Mal geschrieben? | `.claude/agents/reviewer.md` oder die Rolle, die den Fehler macht |
| **Auftrag** | Was fehlte im Issue oder im Auftrag an die Sitzung, sodass sie raten oder nachfragen musste? | Issue-Vorlage, Skill `to-tickets` |
| **Werkzeuge** | Welcher Handgriff wurde von Hand wiederholt oder neu gebaut? Welcher Befehl scheiterte mehrfach? | Skript in `tools/`, Doku |
| **Leerlauf** | Was wurde gebaut oder geprüft, das schon entschieden, schon vorhanden oder nicht gefragt war? | Regel für die Leitung, Prüfschritt vor dem Auftrag |
| **Zugang** | Welche Information war für die Sitzung nicht erreichbar (Urteil des Menschen nur im Chat, Probe nur in einem Arbeitsordner)? | Kommentar am Issue, Stil-Dokument, Vault |

### 2. Abhilfe wählen

Für jeden Fund die schwächste Abhilfe, die ihn zuverlässig verhindert, in dieser Reihenfolge:

1. **Maschine statt Regel:** Lässt sich der Fehler mechanisch erkennen, wird er ein Test, ein CI-Schritt oder ein Hook, keine Zeile Text.
2. **Skript statt Wiederholung:** Ein wiederkehrender Handgriff kommt nach `tools/` mit einer kurzen Beschreibung in `docs/`.
3. **Rolle statt `CLAUDE.md`:** Eine Regel, die nur eine Rolle betrifft, steht in deren Datei unter `.claude/agents/`.
4. **`CLAUDE.md` nur als letztes Mittel:** Dort steht, was jede Sitzung braucht und sich nicht aus dem Code ablesen lässt. Für jede neue Zeile prüfen, ob eine alte dafür entfallen kann.
5. **Gelerntes-Notiz:** Was noch kein Muster ist (einmal aufgetreten), kommt als Zeile in die Notiz der Rolle, nicht in eine Regel.

Kein Vorschlag darf einen Hook lockern, einen Test abschwächen oder eine Freigabe ersparen.

### 3. Dem Menschen vorlegen

- Höchstens fünf Kandidaten, die schwersten zuerst. Je Kandidat: was geschah (ein Satz, mit Beleg), was es gekostet hat, Vorschlag, betroffene Datei.
- Als Rückfrage im Rückfragefenster, die Empfehlung als erste Option. Der Rest kommt ohne Rückfrage in die Gelerntes-Notizen.

### 4. Umsetzen lassen

- Angenommene Kandidaten werden Issues mit Abnahmekriterien (Skill `to-tickets`). Die Leitung plant sie ein.
- Änderungen an `CLAUDE.md`, `.claude/**`, `.github/**` und `docs/entscheidungen/**` kommen als Leitplanken-PR; den merged nur der Mensch.
- Abgelehnte Kandidaten mit Begründung in der Gelerntes-Notiz der Leitung vermerken, damit sie nicht erneut vorgeschlagen werden.
