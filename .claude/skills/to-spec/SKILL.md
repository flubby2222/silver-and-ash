---
name: to-spec
description: Macht aus einem geklärten Vorhaben eine Spezifikation als GitHub-Issue (Ziel, Lösung, Abläufe, Entscheidungen, Tests, Abnahmekriterien). Nur auf ausdrücklichen Aufruf /to-spec.
disable-model-invocation: true
---

# to-spec – Vorhaben zur Spezifikation

Vorgehen nach dem Skill `to-spec` aus `mattpocock/skills` (MIT-Lizenz), eigene Fassung für Silver & Ash (Issue #562). Bei Widerspruch gilt `CLAUDE.md`.

**Eingabe:** ein Vorhaben aus dem Gespräch oder ein Issue mit Urteilen des Menschen. **Ergebnis:** ein Issue, das die Spezifikation trägt. Zerlegen in Arbeits-Issues ist Sache von `/to-tickets`.

## Schritte

1. **Lesen, bevor gefragt wird.** `docs/glossar.md`, die Tabelle in `docs/entscheidungen/README.md` und die ADRs, die das Vorhaben berühren; die betroffenen Issues mit `gh issue view <nr> --comments`. Jedes Urteil des Menschen dort gilt und wird nicht neu gefragt. Begriffe aus dem Glossar genau so verwenden.
2. **Code erkunden.** Welche Klassen in `Core` tragen die Regel, welche in `Game` zeichnen oder klingen? Wo gibt es schon ein Muster (Tabelleneintrag, Ereignis, Raumvorlage)? Was liegt in `assets/balance.json`?
3. **Prüfstellen skizzieren.** Wo kann ein Test die Simulation tickweise ohne Fenster treiben? Welche Zahlen kommen aus `Balance.Default`? Für jede Prüfstelle die Gegenprobe nach Regel 7 nennen: welche Stelle man kaputt macht, damit der Test rot wird. Fehlerzweige bekommen eigene Prüfstellen.
4. **Architektur prüfen** (verbindlicher Abschnitt in `CLAUDE.md`): Regel in `Core` ohne Raylib; „ein Spieler“, Eingaben in den Tick, Zufall nur mit Seed, Klang über Ereignisse (ADR 0003); Zahlen in `balance.json`; neuer Fall als eigene Methode oder Tabelleneintrag. Jeder Widerspruch zu einem ADR wird ausdrücklich genannt – das ist ein Grund, den Menschen zu unterbrechen.
5. **Offenes klären, gebündelt.** Was nach Schritt 1 bis 4 noch offen ist, geht in **ein** Rückfragefenster (`AskUserQuestion`): je Frage die Empfehlung als erste Option, mit kurzer Begründung. Nichts fragen, was ein Issue oder ADR schon entscheidet. Bildfragen darf die Leitung vorläufig selbst entscheiden; Ton urteilt nur der Mensch.
6. **Issue schreiben** (Text unten), mit `gh issue create`, Meilenstein setzen und aufs Board in die Spalte *Backlog*. Hat das Vorhaben schon ein Issue mit Urteilen, die Spezifikation dort als Kommentar anhängen oder ein neues Issue mit Verweis anlegen – nichts überschreiben.
7. **Melden:** Nummer und Link an den Menschen bzw. die Leitung; nächster Schritt ist `/to-tickets`.

## Aufbau der Spezifikation

```markdown
## Ziel
Ein, zwei Sätze: was danach im Spiel anders ist.

## Warum
Anlass, wörtliche Urteile des Menschen mit Datum, Verweise auf Issues und ADRs.

## Lösung
Die Lösung aus Sicht des Spiels, in Absätzen.

## Abläufe aus Sicht eines Spielers
1. Als Spieler … (nummeriert, auch zwei Spieler und Fehlerfälle wie Tod oder Neustart)

## Entscheidungen
Regeln, Schnittstellen, Zuständigkeit Core/Game, Werte in balance.json –
ohne Dateipfade und ohne Code, damit der Text nicht veraltet.

## Tests
Prüfstellen tickweise, Werte aus Balance.Default, Fehlerzweige, je eine Gegenprobe.
Bild: Fotos/Musterraum. Klang: Hörprobe vor dem ersten Einbau einer neuen Kategorie.

## Nicht im Umfang
Was ausdrücklich nicht dazugehört (sonst Issue mit Label `später`).

## Abnahmekriterien
- [ ] prüfbare Sätze

## Abschluss
Wird durch die Unter-Issues erfüllt; schließt, wenn das letzte gemergt ist.
```

## Nicht tun

- Keinen Spielcode schreiben, keine Arbeits-Issues anlegen.
- Keine Einzelfrage nach der anderen; keine Frage zu Dingen, die schon entschieden sind.
- Keine Dateipfade in „Entscheidungen“, keine privaten Pfade in öffentlichen Texten.
