"""Schreibt oder prüft die Prüfsummen-Listen: Größe und SHA-256 jeder Datei, die das Spiel aus assets/ lädt.

- assets/models/models.sha256: jede .glb und .png in assets/models/
- assets/textures/textures.sha256: jede .png in assets/textures/ (Level-Texturen, Issue #233)
- assets/audio/audio.sha256: jede .wav und .ogg in assets/audio/ (Geräusche, Issue #267)

Das Spiel vergleicht vor dem Laden jede Datei mit ihrer Liste (ModelChecksums im Core); eine beschädigte oder halb
kopierte Datei erreicht Raylib so nie. Tests prüfen, dass die Listen zu den eingecheckten Dateien passen.

Aufruf nach jedem Neu-Erzeugen eines Modells, einer Textur oder eines Geräuschs (aus dem Repository-Wurzelverzeichnis, normales Python 3):
    python tools/models/checksums.py

Eine Liste wird nur geschrieben, wenn sich ihr Inhalt ändert, und immer mit LF (wie .gitattributes sie führt);
ein Lauf ohne echte Änderung lässt git status leer (Issue #636).

    python tools/models/checksums.py --check

schreibt nichts, vergleicht nur und endet mit Exit-Code 1, wenn eine Liste fehlt, veraltet ist oder neben ihr eine
verwaiste .sha256-Datei liegt; die betroffenen Dateien stehen in der Ausgabe. Exit-Code 0 heißt: alles passt.

--help zeigt nur diese Hilfe; eine unbekannte Option bricht ab. In beiden Fällen schreibt das Skript keine Datei (Issue #604).
"""

import argparse
import hashlib
import os
import sys

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets")
LISTS = [
    ("models", "models.sha256", (".glb", ".png"), "a model"),
    ("textures", "textures.sha256", (".png",), "a level texture"),
    ("audio", "audio.sha256", (".wav", ".ogg"), "a sound"),
]


def entries(directory, endings):
    """The list lines of one folder, sorted by name: 'sha256 size name'."""
    lines = []
    for name in sorted(os.listdir(directory)):
        if name.endswith(endings):
            with open(os.path.join(directory, name), "rb") as f:
                data = f.read()
            lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    return lines


def list_text(lines, what):
    """The whole list file as it is written, with LF line endings."""
    header = f"# sha256 size name - written by tools/models/checksums.py, checked by the game before loading {what}\n"
    return header + "\n".join(lines) + "\n"


def read_list(path):
    """The list on disk with its line endings made LF (a CRLF checkout counts as equal), or None if it is missing."""
    if not os.path.isfile(path):
        return None
    with open(path, "rb") as f:
        return f.read().decode("ascii").replace("\r\n", "\n")


def write_lists(assets=ASSETS):
    for folder, list_name, endings, what in LISTS:
        directory = os.path.join(assets, folder)
        path = os.path.join(directory, list_name)
        lines = entries(directory, endings)
        text = list_text(lines, what)
        if read_list(path) == text:
            # Leave the file untouched: rewriting it would change its size in a CRLF checkout, and git status would
            # then report it as modified although nothing changed (Issue #636).
            print(f"{folder}/{list_name}: unchanged ({len(lines)} files)")
            continue
        with open(path, "w", encoding="ascii", newline="\n") as f:
            f.write(text)
        print(f"{folder}/{list_name}: written")
        print("\n".join(lines))
    for orphan in orphaned_lists(assets):
        print(f"warning: {orphan} is not written by this script (orphaned list?)")


def orphaned_lists(assets=ASSETS):
    """Every .sha256 file in the list folders that no entry of LISTS writes, as 'folder/name'."""
    orphans = []
    for folder, list_name, _, _ in LISTS:
        for name in sorted(os.listdir(os.path.join(assets, folder))):
            if name.endswith(".sha256") and name != list_name:
                orphans.append(f"{folder}/{name}")
    return orphans


def list_problems(folder, list_name, lines, on_disk):
    """What is wrong with one list, one line per affected file; empty if the list is current."""
    where = f"{folder}/{list_name}"
    if on_disk is None:
        return [f"{where}: missing"]
    listed = {}
    for line in on_disk.split("\n"):
        if line and not line.startswith("#"):
            digest_size, _, name = line.rpartition(" ")
            listed[name] = digest_size
    current = {line.rpartition(" ")[2]: line.rpartition(" ")[0] for line in lines}
    problems = []
    for name in sorted(set(listed) | set(current)):
        if name not in listed:
            problems.append(f"{where}: {folder}/{name} has no entry")
        elif name not in current:
            problems.append(f"{where}: entry for {folder}/{name}, but the file is gone")
        elif listed[name] != current[name]:
            problems.append(f"{where}: {folder}/{name} differs (size or sha256)")
    if not problems:
        # Same entries, but not the text this script would write (header, order, blank lines).
        problems.append(f"{where}: not in the form this script writes")
    return problems


def check_lists(assets=ASSETS):
    """Compares every list with the files on disk without writing anything; returns the problems found."""
    problems = []
    for folder, list_name, endings, what in LISTS:
        directory = os.path.join(assets, folder)
        lines = entries(directory, endings)
        on_disk = read_list(os.path.join(directory, list_name))
        if on_disk != list_text(lines, what):
            problems.extend(list_problems(folder, list_name, lines, on_disk))
    problems.extend(f"{orphan}: orphaned list, not written by this script" for orphan in orphaned_lists(assets))
    return problems


def run_check(assets=ASSETS):
    """--check: prints the result and returns the exit code (0 = every list current, 1 = something differs)."""
    problems = check_lists(assets)
    if not problems:
        print("checksums: all lists match the files")
        return 0
    print(f"checksums: {len(problems)} problem(s), run python tools/models/checksums.py to rewrite the lists:")
    for problem in problems:
        print(f"  {problem}")
    return 1


if __name__ == "__main__":
    # Read the command line first: --help and wrong options end here, before any list is written.
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="write nothing, compare only; exit code 1 on any difference")
    arguments = parser.parse_args()
    if arguments.check:
        sys.exit(run_check())
    write_lists()
