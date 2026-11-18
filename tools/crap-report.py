"""CRAP score report for SilverAndAsh.Core (issue #320).

Runs the Core tests once with Coverlet (XPlat Code Coverage), reads the
Cobertura file and computes per method

    CRAP = complexity^2 * (1 - coverage)^3 + complexity

with coverage = line coverage of the method (the same definition
ReportGenerator uses). As a second column the same formula is evaluated with
branch coverage. ReportGenerator runs on the same Cobertura file as a cross
check: every method that both sides can name unambiguously must agree.

Writes a short Markdown report (default: docs/qualitaet/crap-<date>.md) and
compares it with the newest earlier report in that folder. All intermediate
files go to a temporary folder outside the repository, which is deleted
afterwards (unless --keep).

Usage (from anywhere inside the repository):

    python tools/crap-report.py
    python tools/crap-report.py --out docs/qualitaet/crap-2026-11-18.md
    python tools/crap-report.py --coverage <coverage.cobertura.xml>   # skip the test run

The score is a map, not a gate: the script never fails because of a score.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

TEST_PROJECT = "tests/SilverAndAsh.Core.Tests"
MEASURED_PATHS = ["src/SilverAndAsh.Core", "tests/SilverAndAsh.Core.Tests"]
ASSEMBLY = "SilverAndAsh.Core"
NAMESPACE_PREFIX = "SilverAndAsh.Core"
REPORT_DIR = "docs/qualitaet"
TOP = 20
CRAP_LIMIT = 30  # classic threshold from the original CRAP article
DATA_MIN_SCORE = 10  # methods at or above this score are kept for the next comparison

# Coverlet settings (passed as run settings, so the normal `dotnet test` is untouched).
#  - Include: only the Core assembly is measured.
#  - ExcludeByAttribute GeneratedCode: source-generated code (e.g. [GeneratedRegex]).
#  - SkipAutoProps: auto-property accessors (records, plain data) have no logic of their own.
COVERLET_SETTINGS = {
    "Include": f"[{ASSEMBLY}]*",
    "ExcludeByAttribute": "GeneratedCodeAttribute",
    "SkipAutoProps": "true",
    "Format": "cobertura",
}


@dataclass
class Method:
    namespace: str
    cls: str  # class name below the namespace, nested classes joined with '.'
    name: str  # readable method name
    signature: str  # short parameter list
    complexity: int
    lines_valid: int
    lines_covered: int
    branches_valid: int
    branches_covered: int
    rg_class: str  # outer class full name, as ReportGenerator groups it
    rg_name: str  # method name as ReportGenerator shows it

    @property
    def line_rate(self) -> float:
        return self.lines_covered / self.lines_valid if self.lines_valid else 1.0

    @property
    def branch_rate(self) -> float:
        return self.branches_covered / self.branches_valid if self.branches_valid else 1.0

    @property
    def crap(self) -> float:
        return crap(self.complexity, self.line_rate)

    @property
    def crap_branches(self) -> float:
        return crap(self.complexity, self.branch_rate)

    @property
    def key(self) -> str:
        return f"{self.namespace}.{self.cls}.{self.name}{self.signature}"


def crap(complexity: int, coverage: float) -> float:
    return complexity * complexity * (1.0 - coverage) ** 3 + complexity


# ---------------------------------------------------------------- helpers


def run(cmd: list[str], cwd: Path, quiet: bool = False) -> str:
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        sys.stdout.write(result.stdout)
        sys.stderr.write(result.stderr)
        raise SystemExit(f"Command failed ({result.returncode}): {' '.join(cmd[:3])} ...")
    if not quiet:
        sys.stdout.write(result.stdout)
    return result.stdout


def repo_root() -> Path:
    out = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True)
    return Path(out.stdout.strip())


def de(value: float, digits: int = 1) -> str:
    """German number format with a decimal comma."""
    return f"{value:.{digits}f}".replace(".", ",")


def pct(rate: float) -> str:
    return de(rate * 100) + " %"


def short_signature(signature: str) -> str:
    sig = re.sub(r"\[0\.\.\.(,0\.\.\.)*\]", lambda m: "[" + "," * m.group(0).count(",") + "]", signature)
    sig = re.sub(r"`\d+", "", sig)
    sig = re.sub(r"\b(?:\w+\.)+(\w+)", r"\1", sig)
    return sig.replace(",", ", ")


GENERATED_PART = re.compile(r"^<>c(__DisplayClass.*)?$")
ITERATOR_PART = re.compile(r"^<(?P<outer>[^>]+)>d__\d+$")
LOCAL_FUNCTION = re.compile(r"^<(?P<outer>[^>]+)>g__(?P<inner>[^|]+)\|.*$")
LAMBDA = re.compile(r"^<(?P<outer>[^>]*)>b__(?P<id>.+)$")


def readable(class_name: str, method_name: str) -> tuple[str, str, str, str]:
    """Returns (namespace, class, method, rg_name) with compiler names made readable."""
    parts = class_name.split("/")
    outer = parts[0]
    namespace, _, first = outer.rpartition(".")
    classes = [first]
    name = method_name
    for part in parts[1:]:
        iterator = ITERATOR_PART.match(part)
        if iterator:
            name = f"{iterator.group('outer')} (Iterator)" if method_name == "MoveNext" else f"{iterator.group('outer')} › {method_name}"
            continue
        if GENERATED_PART.match(part):
            continue
        classes.append(part)
    rg_name = name
    local = LOCAL_FUNCTION.match(method_name)
    lam = LAMBDA.match(method_name)
    if local:
        name = f"{local.group('outer')} › {local.group('inner')}"
        rg_name = local.group("inner")
    elif lam:
        name = f"{lam.group('outer') or '?'} › Lambda {lam.group('id')}"
        rg_name = ""
    rg_name = rg_name.lstrip(".")  # ReportGenerator shows .ctor as ctor
    return namespace, ".".join(classes), name, rg_name


def branch_counts(line: ET.Element) -> tuple[int, int]:
    text = line.get("condition-coverage")
    if line.get("branch") != "True" or not text:
        return 0, 0
    m = re.search(r"\((\d+)/(\d+)\)", text)
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)


# ---------------------------------------------------------------- reading


def read_cobertura(path: Path):
    root = ET.parse(path).getroot()
    methods: list[Method] = []
    excluded: list[str] = []
    lines: dict[tuple[str, int], tuple[int, int, int]] = {}  # (file, line) -> (hit, b_cov, b_valid)
    for cls in root.iter("class"):
        filename = cls.get("filename", "").replace("\\", "/")
        if "/obj/" in "/" + filename or filename.endswith(".g.cs"):
            excluded.extend(f"{cls.get('name')}.{m.get('name')}" for m in cls.iter("method"))
            continue
        for method in cls.findall("./methods/method"):
            namespace, cname, name, rg_name = readable(cls.get("name"), method.get("name"))
            line_elems = method.findall("./lines/line")
            covered = sum(1 for ln in line_elems if int(ln.get("hits", "0")) > 0)
            b_cov = b_val = 0
            for ln in line_elems:
                c, v = branch_counts(ln)
                b_cov += c
                b_val += v
            methods.append(
                Method(
                    namespace=namespace,
                    cls=cname,
                    name=name,
                    signature=short_signature(method.get("signature", "")),
                    complexity=int(method.get("complexity", "1")),
                    lines_valid=len(line_elems),
                    lines_covered=covered,
                    branches_valid=b_val,
                    branches_covered=b_cov,
                    rg_class=cls.get("name").split("/")[0],
                    rg_name=rg_name,
                )
            )
        for ln in cls.findall("./lines/line"):
            key = (filename, int(ln.get("number")))
            hit = 1 if int(ln.get("hits", "0")) > 0 else 0
            c, v = branch_counts(ln)
            old = lines.get(key, (0, 0, 0))
            lines[key] = (max(old[0], hit), max(old[1], c), max(old[2], v))
    totals = {
        "lines_valid": len(lines),
        "lines_covered": sum(v[0] for v in lines.values()),
        "branches_valid": sum(v[2] for v in lines.values()),
        "branches_covered": sum(v[1] for v in lines.values()),
    }
    # Overloads get their parameter list; unique names stay short.
    seen = defaultdict(int)
    for m in methods:
        seen[(m.namespace, m.cls, m.name)] += 1
    for m in methods:
        if seen[(m.namespace, m.cls, m.name)] == 1:
            m.signature = ""
    return methods, excluded, totals


def read_reportgenerator(folder: Path) -> dict[tuple[str, str], list[float]]:
    scores: dict[tuple[str, str], list[float]] = defaultdict(list)
    for file in folder.glob("*.xml"):
        if file.name.lower().startswith("summary"):
            continue
        root = ET.parse(file).getroot()
        cls = root.findtext("./Summary/Class") or ""
        for element in root.iter("Element"):
            score = element.findtext("CrapScore")
            if score:
                scores[(cls, element.get("name", ""))].append(float(score))
    return scores


def cross_check(methods: list[Method], rg: dict[tuple[str, str], list[float]]) -> tuple[int, list[str]]:
    mine: dict[tuple[str, str], list[Method]] = defaultdict(list)
    for m in methods:
        if m.rg_name:
            mine[(m.rg_class, m.rg_name)].append(m)
    compared = 0
    differences = []
    for key, ms in mine.items():
        theirs = rg.get(key, [])
        if len(ms) != 1 or len(theirs) != 1:
            continue  # overloads or names ReportGenerator merges differently
        compared += 1
        if abs(ms[0].crap - theirs[0]) >= 1.0:
            differences.append(f"{key[0]}.{key[1]}: {de(ms[0].crap)} vs. ReportGenerator {de(theirs[0], 0)}")
    return compared, differences


# ---------------------------------------------------------------- previous report


DATA_START = "<!-- crap-data"
DATA_END = "-->"


def read_previous(report_dir: Path, out: Path):
    candidates = sorted(p for p in report_dir.glob("crap-*.md") if p.resolve() != out.resolve() and p.name < out.name)
    if not candidates:
        return None, None
    text = candidates[-1].read_text(encoding="utf-8")
    start = text.find(DATA_START)
    if start < 0:
        return candidates[-1].name, None
    end = text.find(DATA_END, start)
    return candidates[-1].name, json.loads(text[start + len(DATA_START) : end])


# ---------------------------------------------------------------- writing


def versions(root: Path) -> dict[str, str]:
    tools = json.loads((root / "dotnet-tools.json").read_text(encoding="utf-8"))
    csproj = (root / TEST_PROJECT / "SilverAndAsh.Core.Tests.csproj").read_text(encoding="utf-8-sig")
    coverlet = re.search(r'Include="coverlet\.collector" Version="([^"]+)"', csproj)
    sdk = subprocess.run(["dotnet", "--version"], cwd=root, capture_output=True, text=True).stdout.strip()
    return {
        "sdk": sdk,
        "coverlet": coverlet.group(1) if coverlet else "?",
        "reportgenerator": tools["tools"]["dotnet-reportgenerator-globaltool"]["version"],
    }


def commit_info(root: Path) -> str:
    head = subprocess.run(["git", "log", "-1", "--format=%h (%cs)"], cwd=root, capture_output=True, text=True).stdout.strip()
    dirty = subprocess.run(["git", "status", "--porcelain", "--", *MEASURED_PATHS], cwd=root, capture_output=True, text=True).stdout
    changed = [ln[3:] for ln in dirty.splitlines() if ln[3:].endswith(".cs")]
    return head + (" + ungespeicherte Änderungen an Code oder Tests" if changed else "")


def delta(new: float, old: float | None, digits: int = 1) -> str:
    if old is None:
        return "neu"
    d = new - old
    if abs(d) < 0.05:
        return "="
    return ("+" if d > 0 else "−") + de(abs(d), digits)


def write_report(out: Path, date: str, commit: str, ver: dict, methods: list[Method], excluded: list[str],
                 totals: dict, check: tuple[int, list[str]], previous: tuple[str | None, dict | None]) -> dict:
    ranked = sorted(methods, key=lambda m: (-round(m.crap, 6), -m.complexity, m.key))
    top = ranked[:TOP]
    over = [m for m in methods if m.crap > CRAP_LIMIT]
    line_rate = totals["lines_covered"] / totals["lines_valid"]
    branch_rate = totals["branches_covered"] / totals["branches_valid"]

    by_ns: dict[str, list[Method]] = defaultdict(list)
    for m in methods:
        ns = m.namespace[len(NAMESPACE_PREFIX):].lstrip(".") or "(Wurzel)"
        by_ns[ns].append(m)

    data = {
        "date": date,
        "methods": len(methods),
        "over": len(over),
        "line_rate": round(line_rate, 6),
        "branch_rate": round(branch_rate, 6),
        "namespaces": {
            ns: {"methods": len(ms), "over": sum(1 for m in ms if m.crap > CRAP_LIMIT), "max": round(max(m.crap for m in ms), 2)}
            for ns, ms in sorted(by_ns.items())
        },
        "scores": {m.key: round(m.crap, 2) for m in ranked if m.crap >= DATA_MIN_SCORE},
    }
    prev_name, prev = previous
    old_scores = prev["scores"] if prev else {}

    def short(m: Method) -> str:
        return f"{m.namespace[len(NAMESPACE_PREFIX):].lstrip('.')}.{m.cls}".lstrip(".")

    compared, differences = check
    o = []
    o.append(f"# CRAP-Bericht SilverAndAsh.Core – {date}")
    o.append("")
    o.append("Erzeugt mit `python tools/crap-report.py`. Lesen und Grenzen: [README](README.md).")
    o.append("")
    o.append(f"- **Commit:** {commit}")
    o.append(f"- **Werkzeuge:** .NET SDK {ver['sdk']}, coverlet.collector {ver['coverlet']}, ReportGenerator {ver['reportgenerator']}")
    o.append(f"- **Abdeckung Core:** Zeilen {pct(line_rate)} ({totals['lines_covered']} von {totals['lines_valid']}), "
             f"Zweige {pct(branch_rate)} ({totals['branches_covered']} von {totals['branches_valid']})")
    o.append(f"- **Methoden:** {len(methods)}, davon {len(over)} mit CRAP über {CRAP_LIMIT}")
    dropped = f" – davon {len(excluded)} Methoden erst im Skript aussortiert" if excluded else ""
    o.append("- **Ausgenommen:** generierter Code (`[GeneratedCode]`, Quellgeneratoren in `obj/`)"
             f"{dropped}; Auto-Properties und vom Compiler erzeugte Record-Methoden (ohne eigene Logik)")
    check_text = "alle gleich" if not differences else f"{len(differences)} Abweichungen (siehe unten)"
    o.append(f"- **Gegenprobe ReportGenerator:** {compared} Methoden verglichen, {check_text}")
    o.append("")
    o.append(f"## Die {TOP} Methoden mit dem höchsten CRAP-Score")
    o.append("")
    o.append("CRAP rechnet mit der Zeilenabdeckung (wie ReportGenerator); „CRAP Zweige“ setzt die Zweigabdeckung ein. "
             "Δ = Änderung zum vorigen Bericht.")
    o.append("")
    o.append("| # | Klasse | Methode | Komplexität | Zeilen | Zweige | CRAP | CRAP Zweige | Δ |")
    o.append("|--:|---|---|--:|--:|--:|--:|--:|--:|")
    for i, m in enumerate(top, 1):
        d = delta(m.crap, old_scores.get(m.key)) if prev else "–"
        o.append(f"| {i} | {short(m)} | `{m.name}{m.signature}` | {m.complexity} | {pct(m.line_rate)} | "
                 f"{pct(m.branch_rate)} | {de(m.crap)} | {de(m.crap_branches)} | {d} |")
    o.append("")
    o.append("## Summen je Namensraum")
    o.append("")
    o.append(f"| Namensraum | Methoden | über {CRAP_LIMIT} | höchster Score |")
    o.append("|---|--:|--:|--:|")
    for ns, info in data["namespaces"].items():
        o.append(f"| {ns} | {info['methods']} | {info['over']} | {de(info['max'])} |")
    o.append("")
    o.append("## Vergleich zum vorigen Bericht")
    o.append("")
    if not prev_name:
        o.append("Kein voriger Bericht – dieser Bericht ist die Ausgangslage.")
    elif not prev:
        o.append(f"Voriger Bericht `{prev_name}` hat keinen Datenblock; kein Vergleich möglich.")
    else:
        o.append(f"Vorher: `{prev_name}`.")
        o.append("")
        o.append(f"- Zeilenabdeckung {pct(prev['line_rate'])} → {pct(line_rate)}, "
                 f"Zweigabdeckung {pct(prev['branch_rate'])} → {pct(branch_rate)}")
        o.append(f"- Methoden {prev['methods']} → {len(methods)}, über {CRAP_LIMIT}: {prev['over']} → {len(over)}")
        changes = []
        for ns, info in data["namespaces"].items():
            old = prev["namespaces"].get(ns)
            if old != info:
                was = f"{old['methods']} / {old['over']} / {de(old['max'])}" if old else "neu"
                changes.append(f"  - {ns}: {was} → {info['methods']} / {info['over']} / {de(info['max'])}")
        gone = [ns for ns in prev["namespaces"] if ns not in data["namespaces"]]
        changes += [f"  - {ns}: entfallen" for ns in gone]
        if changes:
            o.append("- Namensräume mit Änderungen (Methoden / über 30 / höchster Score):")
            o.extend(changes)
        else:
            o.append("- Namensräume unverändert.")
        left = [k for k in sorted(prev["scores"], key=lambda k: -prev["scores"][k])[:TOP] if k not in {m.key for m in top}]
        if left:
            o.append(f"- Nicht mehr unter den {TOP} obersten: " + ", ".join(f"`{k.rsplit('.', 1)[-1]}`" for k in left))
    if differences:
        o.append("")
        o.append("## Abweichungen zu ReportGenerator")
        o.append("")
        o.extend(f"- {d}" for d in differences)
    o.append("")
    o.append(f"{DATA_START} Datenblock für den nächsten Vergleich (Methoden ab Score {DATA_MIN_SCORE})")
    o.append(json.dumps(data, ensure_ascii=False, indent=0, sort_keys=True))
    o.append(DATA_END)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(("\n".join(o) + "\n").encode("utf-8"))
    return data


# ---------------------------------------------------------------- main


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--out", help="report file (default: docs/qualitaet/crap-<date>.md)")
    parser.add_argument("--date", help="date in the report header (default: today)")
    parser.add_argument("--coverage", help="existing coverage.cobertura.xml; skips the test run")
    parser.add_argument("--keep", action="store_true", help="keep the temporary folder and print its path")
    args = parser.parse_args()

    root = repo_root()
    date = args.date or dt.date.today().isoformat()
    out = Path(args.out).resolve() if args.out else root / REPORT_DIR / f"crap-{date}.md"
    work = Path(tempfile.mkdtemp(prefix="silverash-crap-"))
    try:
        run(["dotnet", "tool", "restore"], root, quiet=True)
        if args.coverage:
            coverage = Path(args.coverage).resolve()
        else:
            print("Running Core tests with coverage (takes a few minutes) ...", flush=True)
            settings = [f"DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.{k}={v}"
                        for k, v in COVERLET_SETTINGS.items()]
            run(["dotnet", "test", TEST_PROJECT, "-c", "Debug", "--nologo", "-v", "q",
                 "--collect", "XPlat Code Coverage", "--results-directory", str(work / "tests"), "--", *settings],
                root, quiet=True)
            found = list((work / "tests").rglob("coverage.cobertura.xml"))
            if len(found) != 1:
                raise SystemExit(f"Expected one coverage file, found {len(found)}")
            coverage = found[0]
        run(["dotnet", "tool", "run", "reportgenerator", f"-reports:{coverage}", f"-targetdir:{work / 'rg'}",
             "-reporttypes:Xml", "-verbosity:Warning"], root, quiet=True)

        methods, excluded, totals = read_cobertura(coverage)
        check = cross_check(methods, read_reportgenerator(work / "rg"))
        previous = read_previous(root / REPORT_DIR, out)
        data = write_report(out, date, commit_info(root), versions(root), methods, excluded, totals, check, previous)
        print(f"Report: {out.relative_to(root) if out.is_relative_to(root) else out.name}")
        print(f"Lines {pct(data['line_rate'])}, branches {pct(data['branch_rate'])}, "
              f"methods {data['methods']}, over {CRAP_LIMIT}: {data['over']}, "
              f"ReportGenerator check: {check[0]} compared, {len(check[1])} different")
    finally:
        if args.keep:
            print(f"Kept: {work}")
        else:
            shutil.rmtree(work, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
