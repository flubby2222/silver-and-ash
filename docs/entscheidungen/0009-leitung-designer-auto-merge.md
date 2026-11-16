# 0009 – Leitung durch Claude, Designer-Sitzungen und Auto-Merge

- **Status:** angenommen
- **Datum:** 2026-11-16
- **Löst ab:**
  - in [0001](0001-projektgrundlagen.md): die Rollen („Mensch plant, reviewt, testet, merged“), „Grafik-Design spielt eine untergeordnete Rolle“ und **Sound** in der Liste „Nicht in 1.0“;
  - in `CLAUDE.md`: „Claude merged nie“ und die beiden Zeilen der Definition of Done „der Mensch hat das Feature selbst gespielt“ und „jede geänderte Datei grob verstanden“.
- **Bleibt:** Texturen sind weiter nicht Teil von 1.0 (der Look ist Flat Shading mit Vertex-Farben).

## Kontext

Nach knapp einer Woche steht das Projekt etwa dort, wo der Plan Woche 11 vorsah. Der bisherige Ablauf verlangte ungefähr alle 15 Minuten die Aufmerksamkeit des Menschen (Review, Anspielen, Merge). So viel Zeit ist nicht da. Gleichzeitig hat sich das Lernziel verschoben: Vorrangig ist jetzt der **professionelle Umgang mit KI**, also mehrere Sitzungen, die sich gegenseitig prüfen, statt jede Datei selbst zu lesen.

Grundlage war der Vortrag „Rethinking Claude Code Best Practices“ (Lydia Hallie, Anthropic) und eine Grilling-Session am 2026-11-16. Leitgedanke des Vortrags: Überall, wo der Mensch aus dem Ablauf heraustritt, muss etwas anderes die Aufgabe übernehmen – das Modell, die Werkzeuge (Deny-Rules, Hooks) oder die Umgebung (CI).

## Entscheidung

### Rollen

| Rolle | Wer | Aufgabe |
|---|---|---|
| **Auftraggeber** | der Mensch | Richtung, Stil-Abnahme, Hörproben, Rückmeldung nach jedem Release, Merge von Leitplanken-PRs |
| **Leitung** | Claude (Hauptsitzung) | Issues schreiben und priorisieren, Aufgaben vergeben, Ergebnisse prüfen und zurückweisen, Design-Urteile zu Bild, Release-Vorstellung |
| **Code** | Arbeits-Sitzung | Spiellogik und Darstellung laut Issue |
| **Grafikdesigner** | Arbeits-Sitzung | Modelle, später HUD und Räume |
| **Audiodesigner** | Arbeits-Sitzung | Geräusche und Ambiente |
| **Tester** | frische Sitzung pro PR | prüft gegen die Abnahmekriterien des Issues |
| **Reviewer** | frische Sitzung pro PR | unabhängige Code-Review |

Die Rollen-Beschreibungen liegen in [`.claude/agents/`](../../.claude/agents/).

### Merge

GitHub merged einen Pull Request selbst (Auto-Merge), sobald **drei Freigaben** vorliegen:

1. CI grün (Build und Tests),
2. Reviewer-Sitzung ohne offene Befunde,
3. Tester-Bericht ohne Fehler.

Die Sitzung, die den Code geschrieben hat, gibt nie selbst frei. Review und Test werden als Commit-Status `review` und `tester` am PR hinterlegt.

**Leitplanken-PRs merged weiter nur der Mensch.** Das sind PRs, die eine dieser Stellen ändern: `CLAUDE.md`, `.claude/**`, `.github/**`, `docs/entscheidungen/**`. Auch Releases (Tag) bleiben beim Menschen.

### Was die Tester-Sitzung prüft – und was nicht

- Bot-Durchläufe mit festem Seed über `SilverAndAsh.Core`, ohne Fenster, mit Zahlen wie im Balance-Report.
- Bildschirmfotos über ein fest ins Repo aufgenommenes Foto-Skript, das auch Gorm und alle Raumtypen erreicht.
- Bericht „bestanden / nicht bestanden“ gegen die Abnahmekriterien im Issue.

**Spielgefühl kann sie nicht beurteilen.** Das sammelt der Mensch gebündelt nach jedem Release.

### Wie die Designer lernen

1. Die Leitung schreibt die erste Fassung der Stil-Dokumente `docs/design/art-bible.md` und `docs/design/audio-bible.md` aus den bisherigen Entscheidungen.
2. Jeder Designer liefert **Probestücke außerhalb des Spiels**. Jede Ablehnung wird als Regel ins Stil-Dokument geschrieben, damit der nächste Versuch sie kennt.
3. Integriert wird, sobald **drei Stücke in Folge ohne Nacharbeit** durchgehen. Davor gibt es einmal eine **Stil-Abnahme** durch den Menschen (Bilder und erste Hörprobe).

Erste Aufgaben: Ghul, Pistole, Silberklinge und Kiste (Grafik); Pistolenschuss, Klingentreffer, Ghul-Laut, Schritte und Katakomben-Ambiente (Audio).

### Woher Modelle und Töne kommen

- **Alles wird per Skript erzeugt, nichts heruntergeladen.** Keine fremden Pakete, keine KI-Generatoren – so bleibt die Herkunft in einem öffentlichen Repository eindeutig.
- **Modelle:** Blender-Python-Skripte (wie in 0001 vorgesehen).
- **Audio:** Synthese-Skripte. Wiedergabe über Raylib, kein neues Paket.
- **Keine komponierte Musik in 1.0.** Jede der drei Ebenen bekommt ein eigenes Ambiente, der Kampf gegen Gorm eine dichtere Schicht.

### Wer über Ton urteilt

Die Leitung kann Bilder ansehen, aber nicht hören. Bei Audio prüft sie nur Messwerte (Länge, Lautstärke, Frequenzverteilung). Deshalb hört der Mensch **pro neuer Geräusch-Kategorie einmal eine kurze Hörprobe**, bevor die erste Datei dieser Art ins Spiel kommt. Sein Urteil wird zur Regel in der Audio Bible.

### Umfang und Reihenfolge

- Die Code-Sitzung arbeitet v0.11 (Ausrüstung), v0.12 (Waffenkammer) und v0.13 (Skills) wie geplant ab.
- Parallel laufen die Probestücke der Designer. Nach der Stil-Abnahme fließen Modelle in **v0.14 – Modelle** ein.
- Neu: **v0.15 – Klang** vor 1.0.
- Die Issues #213 (HUD), #214 (Props) und #215 (Raumdesign) gehen an den Grafikdesigner, sobald sein Stil abgenommen ist.

### Release-Vorstellung

Nach jedem Release stellt die Leitung die Neuerungen **einmal gesammelt im Chat** vor: was neu ist, welche Design-Urteile sie getroffen hat und warum, was der Tester nicht prüfen konnte, und eine kurze Anspiel-Liste. Die Rückmeldung des Menschen wird zu Issues. Die Leitung wartet nicht, sondern arbeitet am nächsten Meilenstein weiter. Die Retro bleibt als Doku im Vault.

### Wann die Leitung den Menschen unterbricht

Nur in fünf Fällen:

1. Ein neues NuGet-Paket oder Werkzeug wird nötig.
2. Eine Entscheidung widerspricht einem ADR oder verschiebt 1.0.
3. Derselbe Fehler scheitert dreimal (Notbremse).
4. Eine Hörprobe steht an.
5. Ein Leitplanken-PR wartet auf den Merge.

### Sitzungen

Höchstens **drei Arbeits-Sitzungen gleichzeitig**, jede in einem eigenen Arbeitsordner (`git worktree`). Tester und Reviewer laufen kurz und nacheinander dazu. Die Leitung nutzt das stärkste Modell, Arbeits-Sitzungen ein günstigeres.

## Konsequenzen

- **Der Mensch sieht nicht mehr jede Änderung.** Was er früher beim Lesen und Spielen gefunden hat, müssen jetzt Tests, Hooks, CI, Reviewer und Tester finden. Lücken dort bleiben länger unentdeckt.
- **Spielgefühl wird später geprüft:** erst nach dem Release statt vor jedem Merge. Korrekturen kosten dann ein eigenes Issue.
- **Die Leitplanken sind selbst geschützt:** Wer die Regeln ändern will, braucht den Menschen.
- **Die Trennung „Autor gibt nicht selbst frei“ ist eine Arbeitsregel, keine technische Sperre.** Alle Sitzungen arbeiten unter demselben GitHub-Konto; GitHub kann sie nicht unterscheiden.
- **Audio hängt am Ohr des Menschen.** Ohne Hörprobe kommt keine neue Geräusch-Kategorie ins Spiel.
- **Mehr Sitzungen verbrauchen mehr Kontingent.** Die Obergrenze von drei hält das im Rahmen.
- Der Umfang von 1.0 wächst um Klang und gestaltete Modelle; der Termin Anfang Februar 2027 wird in den Retros überprüft.
