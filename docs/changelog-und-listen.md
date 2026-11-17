# Changelog-Einträge und Listen in offenen Pull Requests

Fast jeder Pull Request (PR) trägt eine Zeile in dieselben Dateien ein: `CHANGELOG.md`, `assets/HERKUNFT.md` und drei Listen in `docs/` ([foto-skript.md](foto-skript.md), [modelle.md](modelle.md), [musterraum.md](musterraum.md)). Sind zwei PRs gleichzeitig offen, stehen ihre Zeilen an derselben Stelle. Nach dem Merge des ersten hat der zweite neue Nachbarzeilen oder sogar einen Merge-Konflikt, obwohl sein eigener Eintrag gleich geblieben ist. Wie das ohne erneute Prüfung durchgeht, steht hier (Issue #313).

## So macht ein PR seinen Eintrag

1. **Eine neue Zeile, ganz oben** in den passenden Unterabschnitt von *Unveröffentlicht* (`### Hinzugefügt`, `### Geändert`, `### Behoben`). Mit Issue-Nummer am Ende, z. B. `(#313)`.
2. **Nur die eigene Zeile.** Fremde Einträge nicht umstellen, nicht umformulieren, nicht „mitbereinigen“. Auch in `HERKUNFT.md` und den Doku-Listen: eine neue Zeile anhängen, bestehende Zeilen stehen lassen.
3. **Muss ein bestehender Eintrag geändert werden** (etwa weil der PR eine Funktion aus einem früheren PR korrigiert), ist das erlaubt, aber im PR-Text unter *Bitte genauer ansehen* zu nennen. Solche Änderungen kann das Nachzieh-Skript bei einem Konflikt nicht selbst zusammenführen (siehe unten).

Am Format des Changelogs ändert sich nichts. Der Release-Workflow liest den Changelog nicht (die Release-Notizen erzeugt GitHub aus den PRs); beim Release wird *Unveröffentlicht* wie bisher umbenannt ([release.md](release.md)).

## Was `tools/pr-nachziehen.ps1` damit macht

Nach jedem Merge bringt das Skript die übrigen offenen PRs auf den neuesten `main` und überträgt die Freigaben `review` und `tester`, wenn die **eigenen Änderungen** des PRs gleich geblieben sind. Verglichen wird ein Fingerabdruck dieser Änderungen.

- **Code und alle anderen Dateien:** wie bisher mit drei Zeilen Kontext. Ändert `main` etwas direkt neben einer Code-Änderung des PRs, gilt der PR als verändert und wird neu geprüft.
- **Listen-Dateien** – genau diese fünf, mit genau dieser Schreibweise und als normale Datei: `CHANGELOG.md`, `assets/HERKUNFT.md`, `docs/foto-skript.md`, `docs/modelle.md`, `docs/musterraum.md` (`$ListFiles` in `tools/pr-nachziehen.lib.ps1`). Jede andere Datei, auch `src/CHANGELOG.md` oder `docs/release.md`, zählt wie Code.
- In den Listen-Dateien: Nur die Zeilen, die der PR hinzufügt oder entfernt, zählen, jede zusammen mit den Überschriften, unter denen sie steht (z. B. *Changelog › [Unveröffentlicht] › Hinzugefügt*). Neue oder geänderte Nachbarzeilen aus `main` ändern den Fingerabdruck nicht. Ein geändertes Wort im eigenen Eintrag, ein Eintrag unter einer anderen Überschrift oder eine doppelt hineingeratene fremde Zeile ändern ihn schon.
- **Merge-Konflikt** (GitHub zeigt *This branch has conflicts*): Liegen die Konflikte nur in Listen-Dateien, führt das Skript `main` selbst ein und behält dabei die neuen Zeilen beider Seiten (Git-Merge-Treiber `union`, nur für diesen einen Merge). Den Merge schiebt es nur hoch, wenn der Fingerabdruck danach gleich ist; dann überträgt es die Freigaben. Sonst bleibt der PR unverändert und das Skript meldet *resolve by hand*.

Nicht verglichen wird in den Listen-Dateien die **Reihenfolge innerhalb eines Abschnitts**: Ob der eigene Eintrag über oder unter dem Nachbarn steht, ändert den Fingerabdruck nicht. Im Code zählt sie.

**Worauf sich das Skript nicht verlässt.** Es arbeitet immer an der Wurzel des Repositorys, in dem es liegt (`git rev-parse --show-toplevel`), egal aus welchem Ordner es gestartet wird. Git läuft ohne die `GIT_*`-Umgebungsvariablen des Aufrufers und mit festen Einstellungen für den Vergleich (Kontext, Präfixe, Umbenennungen, Groß/Klein), sodass weder eine Konfiguration noch eine `.gitattributes`-Datei im Arbeitsbaum, Textumwandler (`textconv`) oder das Merkmal `-diff` den Fingerabdruck verändern. Binärdateien werden wie Text verglichen, Zeilenenden (CRLF) zählen mit. Nach „Update branch“ überträgt es Freigaben nur, wenn der neue Kopf GitHubs Merge-Commit direkt auf dem alten Kopf ist (nicht, wenn jemand dazwischen gepusht hat). Kann es etwas nicht prüfen, setzt es keinen Status.

## Wenn doch von Hand zusammengeführt werden muss

Das passiert bei Konflikten im Code oder wenn beide Seiten denselben Listen-Eintrag geändert haben.

```
git switch <branch>
git merge origin/main
# Konflikte lösen: in Listen beide neuen Zeilen behalten, keine alte Fassung zurückholen
git diff origin/main...HEAD -- CHANGELOG.md assets/HERKUNFT.md docs/
```

Der letzte Befehl zeigt die eigenen Änderungen des PRs. Steht dort mehr als der eigene Eintrag, ist beim Lösen eine fremde Zeile hineingeraten (so geschehen bei #311). Danach `tools/pr-nachziehen.ps1 -Number <nr>` laufen lassen: Ist der eigene Eintrag gleich geblieben, überträgt es die Freigaben.

## Tests

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/pr-nachziehen.tests.ps1
```

Die Tests bauen ein Wegwerf-Repository im Temp-Ordner und prüfen beide Richtungen: verschobene Nachbarzeilen und gelöste Listen-Konflikte → gleicher Fingerabdruck; geänderter eigener Eintrag, Eintrag unter anderer Überschrift, doppelte fremde Zeile, Konflikt im Code → nicht übertragen. Ein eigener Abschnitt prüft, dass eine echte Code-Änderung nie übertragen wird, egal von wo und wie das Skript gestartet wird (Unterordner, Pfade mit `..`, fremde `GIT_*`-Variablen, Repo-Einstellungen, ungesicherte Änderungen, Binärdateien, Groß/Klein, Zeilenenden). Am Ende vergleicht der Test die Zahl der gelaufenen Prüfungen mit der erwarteten, damit ein abgebrochener Block nicht als grün durchgeht.
