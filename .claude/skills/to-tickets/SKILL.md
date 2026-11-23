---
name: to-tickets
description: Zerlegt eine Spezifikation (Issue) in senkrechte, einzeln prüfbare Arbeits-Issues mit „Blockiert von“, Meilenstein und Board-Spalte. Nur auf ausdrücklichen Aufruf /to-tickets.
disable-model-invocation: true
---

# to-tickets – Spezifikation in Arbeits-Issues zerlegen

Vorgehen nach dem Skill `to-tickets` aus `mattpocock/skills` (MIT-Lizenz), eigene Fassung für Silver & Ash (Issue #562). Bei Widerspruch gilt `CLAUDE.md`.

**Eingabe:** eine Spezifikation, meist aus `/to-spec`. **Ergebnis:** Arbeits-Issues, je eins pro Branch und kleinem PR. Die Spezifikation selbst bleibt unverändert.

## Schritte

1. **Lesen.** Die Spezifikation mit `gh issue view <nr> --comments`, dazu `docs/glossar.md` und die genannten ADRs und Issues. Offene Issues zum selben Thema suchen (`gh issue list --search …`), damit nichts doppelt entsteht.
2. **Senkrecht schneiden.** Jeder Schnitt liefert für sich etwas Prüfbares durch alle nötigen Schichten (Regel in `Core`, Test, Darstellung in `Game`, Wert in `balance.json`) – nicht „erst alle Datenstrukturen, dann alle Bilder“. Ein Schnitt passt in einen kleinen PR und lässt `main` spielbar.
3. **Breite Umbauten in drei Schritten,** je ein eigenes Issue: *erweitern* (Neues neben dem Alten), *umstellen* (Aufrufer nutzen das Neue), *altes entfernen*. Jeder Schritt ist für sich grün.
4. **Code, Bild und Klang trennen.** Bild und Bewegung: erst Probestück des Designers außerhalb des Spiels, Urteil, dann Einbau als eigenes Issue. Klang: Hörprobe je neuer Geräusch-Kategorie vor dem ersten Einbau. Ist ein Urteil schon gefallen, gleich den Einbau schneiden.
5. **Kanten setzen.** Für jeden Schnitt: Welche anderen müssen vorher gemergt sein? Daraus „Blockiert von #…“. Ketten kurz halten; was unabhängig ist, bleibt unabhängig. Höchstens zwei Code-Sitzungen laufen gleichzeitig – mehr parallele Code-Schnitte bringen nichts.
6. **Granularität abstimmen.** Den Schnitt als nummerierte Liste zeigen (Titel, Art Code/Bild/Klang, Blockiert von, ein Satz Inhalt). Was dazu offen ist (zu grob, zu fein, Reihenfolge), geht gebündelt in **ein** Rückfragefenster (`AskUserQuestion`), Empfehlung als erste Option. Nichts fragen, was Spezifikation, Issues oder ADRs schon entscheiden.
7. **Anlegen, Blocker zuerst.** In Reihenfolge der Abhängigkeiten mit `gh issue create`, damit jede „Blockiert von“-Nummer schon existiert. Je Issue: Meilenstein der Spezifikation (`--milestone`), aufs Board (`gh project item-add`) in die Spalte *Backlog*, als Unter-Issue an die Spezifikation hängen (`gh api repos/{owner}/{repo}/issues/<spec>/sub_issues -F sub_issue_id=<id>`, die `id` aus `gh api repos/{owner}/{repo}/issues/<nr> --jq .id`). Keine neuen Labels erfinden; `später` nur für Dinge außerhalb von 1.0.
8. **Melden:** Liste der neuen Nummern mit Kanten an die Leitung.

## Vorlage je Issue

```markdown
## Ziel
Was nach diesem Schnitt im Spiel prüfbar anders ist.

## Warum
Teil von #<spec>. Welcher Ablauf aus der Spezifikation hier entsteht.

## Aufgaben
1. … (Core-Regel, Ereignis, Wert in balance.json, Darstellung, Klang – nur was dieser Schnitt braucht)

## Abnahmekriterien
- [ ] Test tickweise mit Werten aus Balance.Default; Fehlerzweige mit eigenen Tests
- [ ] Gegenprobe: welche Stelle verbogen wird, damit der Test rot wird
- [ ] Fotos / Hörprobe / Bot-Durchlauf, wo nötig; CHANGELOG bei sichtbarer Änderung

## Abschluss
Per PR-Merge (`Closes #…`) oder Schließen nach Urteil. Blockiert von #…
```

## Nicht tun

- Die Spezifikation nicht ändern (kein Umschreiben, keine Häkchen) – nur Unter-Issues anhängen.
- Keine waagerechten Schichten-Issues („alle Modelle“, „alle Tests“).
- Kein Issue, das Code und Hörprobe oder Probestück mischt.
- Keine privaten Pfade in öffentlichen Texten.
