# Glossar

Die eigenen Wörter des Projekts, je einmal festgelegt. Wer in Issues, Pull Requests, Doku oder Aufträgen einen dieser Begriffe verwendet, meint genau das hier. Neue Begriffe kommen dazu, sobald sie zum zweiten Mal gebraucht werden; die Regeln selbst stehen in [CLAUDE.md](../CLAUDE.md) und in den [Entscheidungen](entscheidungen/).

## Rollen

| Begriff | Bedeutung |
| --- | --- |
| **Mensch** | Der Auftraggeber. Gibt die Richtung vor, urteilt über Stil und Ton, merged Leitplanken-PRs und löst Releases aus. |
| **Leitung** | Die Hauptsitzung der KI. Plant, schreibt Issues, vergibt Aufgaben, prüft Ergebnisse, urteilt über Bild. Schreibt möglichst keinen Spielcode. |
| **Arbeits-Sitzung** | Eine Sitzung, die genau ein Issue umsetzt: Code, Grafikdesigner, Audiodesigner oder Animationsdesigner. Rollen: [.claude/agents/](../.claude/agents/). |
| **Reviewer** | Frische Sitzung, die einen Pull Request liest und den Commit-Status `review` setzt. Schreibt keinen Code. |
| **Tester** | Frische Sitzung, die einen Pull Request gegen die Abnahmekriterien prüft und den Commit-Status `tester` setzt. Schreibt keinen Spielcode. |
| **Skill** | Eine Arbeitsanleitung in [.claude/skills/](../.claude/skills/), die eine Sitzung per `/name` lädt. `/to-spec` macht aus einem Vorhaben eine Spezifikation als Issue, `/to-tickets` zerlegt sie in Arbeits-Issues. |
| **Frische Sitzung** | Eine Sitzung ohne Vorwissen aus der Arbeit, die sie prüft. Nur so ist die Prüfung unabhängig. |

## Ablauf

| Begriff | Bedeutung |
| --- | --- |
| **Abnahmekriterien** | Die Liste im Issue, an der sich entscheidet, ob es fertig ist. |
| **Freigabe** | Einer der drei Punkte, die ein Pull Request zum Merge braucht: CI grün, Status `review`, Status `tester`. Wer den PR geschrieben hat, vergibt keine davon. |
| **Auto-Merge** | GitHub merged von selbst, sobald alle drei Freigaben da sind ([ADR 0009](entscheidungen/0009-leitung-designer-auto-merge.md)). |
| **Leitplanke** | Datei, die die Arbeitsweise selbst festlegt: `CLAUDE.md`, `.claude/**`, `.github/**`, `docs/entscheidungen/**`. |
| **Leitplanken-PR** | Pull Request, der eine Leitplanke ändert. Merged nur der Mensch. |
| **Gegenprobe** | Der Beleg im PR, dass eine Änderung wirklich greift: Der neue Test wird rot, wenn man die geprüfte Stelle kaputt macht. |
| **Tests verbiegen** | Tests löschen, abschwächen oder überspringen, damit sie grün werden. Verboten. |
| **Notbremse** | Nach etwa drei gescheiterten Versuchen am selben Fehler aufhören und die Leitung fragen. |
| **Nachziehen** | Offene Pull Requests nach einem Merge auf den neuesten `main` bringen (`tools/pr-nachziehen.ps1`). Das Skript überträgt vorhandene Freigaben, vergibt aber nie neue. |
| **Board** | Die Projekttafel auf GitHub mit den Spalten für den Stand jedes Issues. |
| **Label `später`** | Kennzeichnet alles, was nicht in 1.0 gehört. |
| **Worktree** | Eigener Arbeitsordner einer Sitzung auf eigenem Branch (Git-Funktion), damit sich Sitzungen nicht in die Quere kommen. |
| **Lernnotiz** („Gelerntes“) | Notiz je Rolle außerhalb des Repositorys. Jede Sitzung liest sie vor der Aufgabe und ergänzt höchstens fünf Zeilen. |
| **Retro** | Rückblick am Ende eines Meilensteins. |

## Design

| Begriff | Bedeutung |
| --- | --- |
| **Art-Bible / Audio-Bible** | Die Stil-Dokumente für Bild ([art-bible.md](design/art-bible.md)) und Klang ([audio-bible.md](design/audio-bible.md)). Jede Ablehnung wird dort zur Regel. |
| **Probestück** | Ein Entwurf außerhalb des Spiels (Modell, HUD-Teil, Klang), an dem der Stil geprüft wird, bevor etwas eingebaut wird. |
| **Probenblatt** | Ein Bild, das ein Probestück oder mehrere Fassungen nebeneinander zeigt, meist bei 854 × 480 und 1920 × 1080. Liegt unter `design/proben/`. |
| **Fassung** | Eine von mehreren Varianten eines Probestücks (a, b, c …), aus denen gewählt wird. |
| **Hörprobe** | Klänge, die der Mensch anhört und beurteilt. Pflicht vor der ersten Integration jeder neuen Geräusch-Kategorie – die Leitung kann nicht hören. |
| **Urteil** | Die Entscheidung über ein Probestück. Über Bild urteilt die Leitung, über Ton nur der Mensch. |
| **Vorläufig** | Kennzeichnung für ein Urteil der Leitung, das der Mensch im nächsten Spieltest bestätigt oder ändert. |
| **Einbau** | Der Schritt, in dem ein abgenommenes Probestück ins Spiel kommt – ein eigenes Issue nach der Probe. |
| **Herkunft** | [assets/HERKUNFT.md](../assets/HERKUNFT.md): Jede Asset-Datei steht dort mit Skript und Datum. Alles ist per Skript erzeugt, nichts heruntergeladen. |
| **Bitgleich** | Zwei Läufe desselben Skripts ergeben Byte für Byte dieselbe Datei. Geprüft über die Prüfsummen-Dateien (`*.sha256`). |
| **Rundum geschlossen** | Ein 3D-Modell ohne offene Kanten, fehlende Flächen oder Lücken zwischen seinen Teilen. |
| **Atlas** | Ein Bild, das viele kleine Bildteile (HUD, Menü) enthält. Der **x2-Atlas** hat doppelte Auflösung. |
| **Musterraum** (auch **Schauraum**, im Code *showroom*) | Ein Raum, der alle Bauteile des Spiels nebeneinander zeigt ([musterraum.md](musterraum.md)). |
| **Foto** | Ein Bildschirmfoto, das das Spiel selbst per Skript erzeugt ([foto-skript.md](foto-skript.md)). |

## Spiel und Technik

| Begriff | Bedeutung |
| --- | --- |
| **Core** | `SilverAndAsh.Core`: gesamte Spiellogik und Physik, ohne Raylib. |
| **Game** | `SilverAndAsh.Game`: Fenster, Eingabe, Darstellung, Klang. Keine Spielregeln. |
| **Tick** | Ein fester Zeitschritt der Simulation, 60 je Sekunde. Tests treiben das Spiel tickweise, ohne Fenster. |
| **Ereignis** | Eine Meldung aus `Core` (z. B. „Schlag ausgeführt“), auf die `Game` mit Klang und Effekten reagiert. |
| **Ein Spieler** | In `Core` nie „der Spieler“ – so bleibt Online-Coop möglich ([ADR 0003](entscheidungen/0003-online-coop-offenhalten.md)). |
| **Seed** | Die Startzahl des Zufalls. Sie liegt im Spielzustand, damit ein Durchlauf wiederholbar ist. |
| **Zelle** | Ein Feld im Raster eines Levels, mit Boden- und Deckenhöhe ([level-format.md](level-format.md)). |
| **Balancing-Werte** | Die Zahlen des Spiels (Schaden, Kosten, Zeiten) in `assets/balance.json`. Tests rechnen mit `Balance.Default`. |

## Release und Qualität

| Begriff | Bedeutung |
| --- | --- |
| **Spieltest** | Der Mensch spielt einmal je Release und bekommt dazu die Liste der offenen Urteile samt Startbefehl. |
| **Prüfstrecke** (im Code *test course*) | Ein von Hand gebautes Level mit allen offenen Urteilen eines Releases als Stationen auf einem geraden Weg, nach den echten Spielregeln; Start mit `--course` ([pruefstrecke.md](pruefstrecke.md)). |
| **Balancing-Runde** | Vor jedem Release: Werte prüfen und anpassen, Ergebnis in [balancing.md](balancing.md). |
| **Balance-Report** | Die Auswertung mit Vorher/Nachher, die eine Balancing-Runde belegt. |
| **Bot-Durchlauf** | Ein Durchlauf, in dem ein Programm statt eines Menschen spielt; liefert Zahlen für Tester und Balancing. |
| **CRAP-Bericht** | Bericht je Meilenstein, der zeigt, wo `Core` riskant zu ändern ist ([qualitaet/](qualitaet/)). Eine Landkarte, keine Schranke. |
| **Architektur-Durchsicht** | Blick einer frischen Sitzung auf Doppelungen und Abweichungen von den ADRs, am Ende eines Meilensteins. |
| **ADR** | *Architecture Decision Record*: kurzes Protokoll einer Entscheidung, in [entscheidungen/](entscheidungen/). |
| **Meilenstein** | Ein Arbeitsabschnitt bis zu einem Release; endet mit Retro, CRAP-Bericht und Architektur-Durchsicht. |
