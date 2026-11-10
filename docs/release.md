# Ein Release veröffentlichen

Ein Release ist ein fester, spielbarer Stand des Spiels mit Versionsnummer – z. B. `v0.1.0`. GitHub baut dafür automatisch eine Windows-`.zip`.

## Versionsnummern (Semantic Versioning)

`MAJOR.MINOR.PATCH`, z. B. `0.1.0`:

| Teil | Wann erhöhen | Beispiel |
|---|---|---|
| **MAJOR** | Großer Meilenstein bzw. nicht mehr kompatibel | `1.0.0` = fertiges Spiel |
| **MINOR** | Neue Funktionen | `0.2.0` = Bewegung & Physik |
| **PATCH** | Nur Fehlerkorrekturen | `0.2.1` = Bugfix für 0.2.0 |

Solange MAJOR `0` ist, gilt das Spiel als „in Entwicklung“. Bei uns: **ein MINOR pro Wochen-Meilenstein** (`v0.1.0`, `v0.2.0`, …).

Eine Endung wie `-beta` (z. B. `v0.3.0-beta`) markiert das Release auf GitHub als *Pre-release*.

## Ablauf

1. **Alles für den Meilenstein ist gemergt**, die CI auf `main` ist grün.
2. **CHANGELOG abschließen** – per Pull Request (`main` ist geschützt). Weil jeder PR seine Änderung schon unter *Unveröffentlicht* eingetragen hat, ist das nur noch Umbenennen: *Unveröffentlicht* wird zu `## [0.1.0] – 2026-11-22 – Name des Meilensteins`, darüber kommt ein neuer, leerer *Unveröffentlicht*-Abschnitt, unten die Vergleichs-Links.
3. **Lokal auf den neuesten Stand:**
   ```
   git switch main
   git pull
   ```
4. **Tag setzen und hochladen:**
   ```
   git tag -a v0.1.0 -m "Silver & Ash 0.1.0 – Walking Skeleton"
   git push origin v0.1.0
   ```
5. **Zuschauen:** Unter *Actions* läuft der Workflow **Release** (ca. 3–5 Minuten). Danach liegt das Release unter *Releases* – mit der `.zip` und automatisch erzeugten Notizen aus den gemergten Pull Requests.
6. **Ausprobieren:** `.zip` herunterladen, entpacken, `SilverAndAsh.Game.exe` starten. Unten rechts muss die Versionsnummer stehen.
7. **Abschließen:** Release-Issue schließen und den **Meilenstein schließen** (*Issues → Milestones → Close*). Claude schreibt die Retro.

## Wenn etwas schiefgeht

- **Workflow rot:** Unter *Actions* den Lauf öffnen, den roten Schritt aufklappen. Der Tag bleibt bestehen; nach der Korrektur (per Pull Request) einen **neuen** Tag mit höherer PATCH-Nummer setzen (z. B. `v0.1.1`). Bestehende Tags werden nicht verschoben.
- **Windows warnt beim Start** („Der Computer wurde durch Windows geschützt“): Das passiert bei Programmen ohne digitale Signatur. *Weitere Informationen → Trotzdem ausführen.* Eine Signatur kostet Geld und ist für ein Lernprojekt nicht nötig.

## Wie funktioniert das?

Siehe [`.github/workflows/release.yml`](../.github/workflows/release.yml). Bei jedem Pull Request, der das Spiel ändert, läuft derselbe Workflow als **Probelauf**: Er baut die `.zip`, veröffentlicht aber nichts. Die `.zip` lässt sich trotzdem herunterladen (im Workflow-Lauf unter *Artifacts*).
