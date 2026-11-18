# CLAUDE.md – Arbeitsregeln für Silver & Ash

Hier steht nur, was sich nicht aus dem Code ablesen lässt. Die Hintergründe stehen in [docs/entscheidungen/](docs/entscheidungen/), die Arbeitsweise in [ADR 0009](docs/entscheidungen/0009-leitung-designer-auto-merge.md).

## Rollen

- **Der Mensch** ist Auftraggeber. Er gibt die Richtung vor, nimmt Stil und Hörproben ab, bekommt nach jedem Release eine Vorstellung im Chat und merged Leitplanken-PRs. Er will den **professionellen Umgang mit KI** lernen: Fachbegriffe beim ersten Auftreten kurz erklären, Git-Befehle mitzeigen.
- **Die Leitung** (Claude, Hauptsitzung) plant, schreibt Issues, vergibt Aufgaben, prüft Ergebnisse und trifft Design-Urteile zu Bild. Sie schreibt möglichst keinen Spielcode selbst.
- **Arbeits-Sitzungen** (Code, Grafikdesigner, Audiodesigner) setzen je ein Issue um. **Tester** und **Reviewer** prüfen als frische Sitzungen. Rollen: [.claude/agents/](.claude/agents/).

## Arbeitsablauf

1. **Ein Issue = ein Branch = ein kleiner Pull Request.** Branch-Namen: `feat/…`, `fix/…`, `docs/…`, `chore/…`. Commits nach Conventional Commits.
2. **Jedes Issue nennt Abnahmekriterien** und sagt, wie es abgeschlossen wird (PR-Merge oder Schließen).
3. **Pull-Request-Text auf Deutsch:** *Was*, *Warum*, *Wie testen*, *Bitte genauer ansehen*, dazu `Closes #<nr>`. PRs ohne Issue kommen als Karte aufs Board in die Spalte *Review*.
4. **Sichtbare Änderungen** stehen im `CHANGELOG.md` unter *Unveröffentlicht*.
5. **Merge:** GitHub merged per Auto-Merge nach drei Freigaben – CI grün, Reviewer ohne offene Befunde (Commit-Status `review`), Tester ohne Fehler (Commit-Status `tester`). Wer den PR geschrieben hat, setzt diese Status nie selbst. Einzige Ausnahme ist `tools/pr-nachziehen.ps1`: Es bringt offene PRs nach einem Merge auf den neuesten `main` und überträgt `review` und `tester` auf den neuen Commit, wenn die eigenen Änderungen des PRs dabei gleich geblieben sind. Es vergibt nie eine Freigabe neu.
6. **Leitplanken-PRs merged nur der Mensch:** alles an `CLAUDE.md`, `.claude/**`, `.github/**`, `docs/entscheidungen/**`. Auch Releases (Tags) löst nur der Mensch aus.
7. **Tests nie verbiegen** (nicht löschen, abschwächen, überspringen). Ist ein Test selbst falsch: im PR begründen. **Gegenprobe belegen:** Der PR zeigt den Fall, an dem die Änderung wirklich greift (ein neuer Test wird rot, wenn man die geprüfte Stelle kaputt macht; eine Funktion für den zweiten Lauf wird mit zwei Läufen geprüft).
8. **Notbremse:** nach etwa drei gescheiterten Versuchen am selben Fehler aufhören und die Leitung fragen.
9. **Merge selbst prüfen** (`gh pr view`), bevor lokal aufgeräumt wird.

Mehrere dieser Regeln erzwingen Hooks in [.claude/hooks/](.claude/hooks/). Blockiert ein Hook, die Ursache beheben – nicht den Hook umgehen.

## Die Leitung hält die Arbeit schmal

- **Höchstens zwei Code-Sitzungen gleichzeitig.** Designer mit Probestücken außerhalb des Spiels zählen nicht mit.
- **Pull Requests einzeln durchbringen, in fester Reihenfolge:** Review und Test für den nächsten PR beginnen erst, wenn der davor gemergt und der nächste auf dem neuesten `main` ist. Sind zwei PRs offen, startet keine weitere Code-Sitzung.
- **Warum:** Jeder Merge macht die anderen offenen PRs veraltet; ihre Freigaben müssen dann neu erteilt werden, ohne dass etwas Neues geprüft wird (am 2026-11-17 dreimal bei PR #306).
- **Nach jedem Merge:** `git pull`, `tools/pr-nachziehen.ps1`, Board nachziehen.

## Die Leitung unterbricht den Menschen nur, wenn

- ein neues NuGet-Paket oder Werkzeug nötig wird,
- eine Entscheidung einem ADR widerspricht oder 1.0 verschiebt,
- derselbe Fehler dreimal scheitert,
- eine Hörprobe ansteht,
- ein Leitplanken-PR auf den Merge wartet.

## Design

- Stil-Dokumente: `docs/design/art-bible.md` und `docs/design/audio-bible.md`. Designer lesen sie vor jeder Aufgabe; jede Ablehnung wird dort zur Regel.
- **Alles per Skript erzeugt, nichts heruntergeladen** (Modelle mit Blender-Python, Audio per Synthese). Jede Asset-Datei steht mit Skript und Datum in `assets/HERKUNFT.md`.
- Probestücke entstehen außerhalb des Spiels. Ins Spiel kommt erst, was den Stil dreimal in Folge ohne Nacharbeit trifft.
- **Ton beurteilt der Mensch:** Die Leitung kann nicht hören. Pro neuer Geräusch-Kategorie eine Hörprobe vor der ersten Integration.

## Nach dem Merge

- **Gelerntes:** Jede Sitzung liest vor der Aufgabe die Notiz ihrer Rolle in `D:\Obsidian\Gamedev\Projekte\Gelerntes\` und ergänzt sie am Ende um höchstens fünf Zeilen (was Zeit gekostet hat, was es nächstes Mal einfacher macht). Wiederkehrende Handgriffe werden zu Skript, Test oder Doku im Repository; die Leitung macht daraus Issues.
- **Neues Konzept:** Notiz im Obsidian-Vault `D:\Obsidian\Gamedev` (`Konzepte/`, `Werkzeuge/` oder `Design/`, Frontmatter `autor: claude`, auf Deutsch, mit Links zum Code).
- **Ende eines Meilensteins:** Retro in `Retros/` mit freiem Abschnitt „Meine Sicht“, Meilenstein schließen, Release-Vorstellung im Chat.
- **Ende eines Meilensteins, Blick von oben:** CRAP-Bericht für `Core` (`python tools/crap-report.py`, siehe [docs/qualitaet/](docs/qualitaet/)) und eine Architektur-Durchsicht durch eine frische Sitzung (Doppelungen, Abweichungen von den ADRs). Die Leitung stellt beides im Chat mit eigener Einschätzung vor und bittet um Lesebestätigung; daraus werden Aufräum-Issues. Beides ist keine Schranke für einzelne PRs.
- **Ein Spieltest je Release:** Der Mensch spielt einmal je Release. Die Leitung schneidet Releases als gleich große Mischung aus Code, Bild und Klang und gibt ihm eine kurze Liste der offenen Urteile samt Startbefehl. Dazwischen entscheidet sie Designfragen nach seinen bisherigen Urteilen und kennzeichnet sie als vorläufig.
- **Vor jedem Release eine Balancing-Runde:** Balance-Report mit Vorher/Nachher, Bot-Durchläufe des Testers, `runs.log`, Werte in `balance.json`; Ergebnis in [docs/balancing.md](docs/balancing.md). Tests rechnen mit `Balance.Default`, nie mit festen Balancing-Zahlen.

## Sprachen

- **Code und Texte im Spiel auf Englisch.**
- **Alles andere auf Deutsch:** Issues, Pull Requests, Doku, Commit-Beschreibungen.

## Architektur (verbindlich)

- **`SilverAndAsh.Core`**: gesamte Spiellogik und Physik, **ohne Raylib**. Mathe über `System.Numerics`.
- **`SilverAndAsh.Game`**: Fenster, Eingabe, Darstellung, Klang mit Raylib-cs. Keine Spielregeln.
- **Feste Zeitschritte** (60 Ticks/s); Tests treiben die Simulation tickweise, ohne Fenster.
- **Eigene einfache 3D-Physik**, keine Physik-Engine. Level = Raster aus Zellen mit Boden- und Deckenhöhe.
- **Figuren hinter einer austauschbaren Schnittstelle** dargestellt.
- **Balancing-Werte** in `assets/balance.json`, nicht im Code.
- **Logs nur lokal.**
- **Online-Coop offen halten** (siehe [0003](docs/entscheidungen/0003-online-coop-offenhalten.md)): in `Core` nie „der Spieler“, sondern „ein Spieler“; alles, was das Spiel verändert, kommt als Eingabe in den Tick; Zufall nur mit Seed im Spielzustand; kein veränderlicher `static`-Zustand; Sound und Effekte über Ereignisse aus `Core`.

## Setting

Gothic-Vampirjäger. Inspiration (nur Stimmung): Castlevania, Devil May Cry, Vampire Hunter D, Hellsing. **Eigene Welt, eigene Figuren, eigene Namen** – nichts aus diesen Werken übernehmen.

## Umfang

Was in 1.0 gehört, steht in den ADRs. Alles andere wird ein Issue mit Label `später`.

## Umgebung

- Windows 10, Windows PowerShell 5.1 (kein `pwsh`). Git for Windows, .NET SDK 10, GitHub CLI, Blender.
- Die CI baut in **Release**: vor einem PR auch `dotnet build -c Release` ausführen.
- Repository `flubby2222/silver-and-ash` ist öffentlich. Commit-E-Mail ist die noreply-Adresse und wird nie geändert.
