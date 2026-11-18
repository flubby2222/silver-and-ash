# Qualitätsbericht: CRAP-Score für Core

Je Meilenstein ein kurzer Bericht, der zeigt, wo `SilverAndAsh.Core` riskant zu ändern ist. Er ist eine **Landkarte** für Aufräum-Issues und ein **Verlauf** über die Meilensteine – **keine Schranke**: kein CI-Schritt, kein Commit-Status, keine Pflicht je PR (#320).

## Die Formel

CRAP (*Change Risk Anti-Patterns*) verbindet je Methode die zyklomatische Komplexität *K* (Zahl der Wege durch die Methode) mit ihrer Testabdeckung *A* (0 bis 1): **CRAP = K² × (1 − A)³ + K**. Eine voll abgedeckte Methode hat also genau ihre Komplexität als Score; jede ungetestete Zeile in verzweigtem Code treibt ihn steil nach oben, und über 30 gilt eine Methode üblicherweise als riskant.

*A* ist hier die **Zeilenabdeckung** der Methode, wie ReportGenerator sie verwendet. Die Spalte „CRAP Zweige“ setzt zusätzlich die Zweigabdeckung ein – strenger, weil auch ein nie genommenes `else` zählt.

## Bericht erzeugen

Aus dem Repository (einmalig vorher `dotnet tool restore`, das Skript macht es auch selbst):

```bash
python tools/crap-report.py
```

Das Skript

1. lässt die Tests von `SilverAndAsh.Core.Tests` einmal mit Coverlet laufen (Debug, dauert etwa drei Minuten statt einer halben, weil jede Zeile mitgezählt wird),
2. rechnet aus der Cobertura-Datei je Methode Komplexität, Abdeckung und Score,
3. lässt ReportGenerator auf dieselbe Datei los und vergleicht dessen Scores mit den eigenen (Zeile „Gegenprobe“ im Kopf),
4. schreibt `docs/qualitaet/crap-<datum>.md` und vergleicht mit dem neuesten älteren Bericht `crap-*.md` im selben Ordner wie die Ausgabedatei.

Zwischendateien liegen in einem temporären Ordner außerhalb des Repositorys und werden danach gelöscht (`--keep` behält sie, z. B. für den HTML-Bericht von ReportGenerator). Weitere Schalter: `--out <datei>`, `--date <JJJJ-MM-TT>` (nur ISO-Datum), `--coverage <cobertura.xml>` (ohne neuen Testlauf) und `--no-check` (ohne ReportGenerator-Gegenprobe). Ungültige Werte für `--out` und `--date` und fehlende Werkzeuge meldet das Skript mit einem Satz, bevor es die Tests startet. Zwei Läufe auf demselben Stand ergeben dieselbe Datei.

Die Tests in `tests/SilverAndAsh.Core.Tests/CrapReportScriptTests.cs` laufen in der CI mit: Sie schreiben mit `--coverage` und `--no-check` zwei Berichte hintereinander und prüfen den Vergleich, ohne den langen Abdeckungslauf.

Der normale `dotnet test` misst **keine** Abdeckung; Coverlet sammelt nur, wenn das Skript `--collect "XPlat Code Coverage"` übergibt.

## Bericht lesen

- **Kopf:** Commit, Werkzeug-Versionen, Gesamtabdeckung (Zeilen und Zweige), Zahl der Methoden und wie viele über 30 liegen.
- **Die 20 höchsten Scores:** Ist *CRAP* ≈ *Komplexität*, ist die Methode voll durchlaufen – das Risiko ist ihre Größe, nicht fehlende Tests. Liegt *CRAP* deutlich über der Komplexität, fehlen Tests für einen Teil der Wege. Liegt *CRAP Zweige* weit über *CRAP*, sind die Zeilen zwar durchlaufen, aber einzelne Bedingungen nie in beide Richtungen.
- **Summen je Namensraum:** Methoden, Anzahl über 30, höchster Score.
- **Vergleich:** Änderung der Kopfzahlen und Namensräume zum vorigen Bericht; Δ in der Tabelle je Methode. Der Datenblock am Ende (HTML-Kommentar) ist für diesen Vergleich da und wird nicht von Hand bearbeitet.

## Was ausgenommen ist

- **Generierter Code:** alles mit `[GeneratedCode]` und alles aus `obj/` (z. B. die Regex-Quellgeneratoren). Das ist nicht unser Code.
- **Auto-Properties** (`SkipAutoProps`): Getter und Setter ohne eigenen Rumpf, wie in Records und Balancing-Klassen, haben keine Logik.
- Vom Compiler erzeugte Record-Methoden (`Equals`, `GetHashCode`, `ToString`) meldet Coverlet von sich aus nicht.

Lambdas und lokale Funktionen stehen als eigene Einträge da (`Build › Carve`), Iteratoren als `Name (Iterator)`. Haben zwei Methoden denselben Namen und dieselbe Signatur (z. B. gleichnamige Lambdas aus verschiedenen Closures), bekommt die zweite den Zusatz ` #2` (nach Zeilennummer sortiert), damit sie im Datenblock nicht die erste überschreibt.

## Grenzen

- **Abdeckung heißt nur „durchlaufen“, nicht „geprüft“.** Ein Test, der eine Methode aufruft und nichts prüft, macht sie grün. Die Gegenproben der Reviewer (Code kurz kaputt machen, der Test muss rot werden) bleiben das schärfere Mittel.
- **Die Komplexität kommt aus dem kompilierten Code**, nicht aus dem Quelltext: `?.`, `??`, `switch`-Ausdrücke und Musterabfragen zählen als Zweige. Ein großer `switch` über eine Aufzählung hat so schnell Komplexität 50 bis 100, ohne schwer verständlich zu sein.
- **`SilverAndAsh.Game` wird nicht gemessen** – es hat keine Tests (Fenster, Grafikkarte) und enthält nach der Architektur keine Spielregeln.
- **Der Score ist keine Schranke.** Er zeigt, wo sich ein genauerer Blick lohnt; was daraus folgt, entscheidet die Leitung als Issue.

## Werkzeuge

Beide hat der Mensch am 2026-11-18 freigegeben:

- **coverlet.collector** (NuGet-Paket im Testprojekt) – misst beim Testlauf, welche Zeilen und Zweige durchlaufen werden, und schreibt eine Cobertura-Datei mit Komplexität je Methode.
- **ReportGenerator** (lokales .NET-Werkzeug, `dotnet-tools.json` im Wurzelordner) – rechnet daraus CRAP-Scores und Risk Hotspots. Maschinenlesbar mit Score für jede Methode ist nur sein Berichtstyp `Xml` (eine Datei je Klasse, Score auf ganze Zahlen gerundet). `JsonSummary` und `XmlSummary` enthalten nur Abdeckung je Klasse; `MarkdownSummary` listet Risk Hotspots, aber nur oberhalb der Schwellen (Komplexität 15, CRAP 30), ganzzahlig und in Version 5.5.12 ohne Zeilenumbrüche zwischen den Tabellenzeilen. Deshalb rechnet das Skript selbst und nutzt ReportGenerator als Gegenprobe.
