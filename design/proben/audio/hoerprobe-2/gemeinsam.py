"""Hoerprobe 2 – gemeinsame Hilfen.

Nutzt die Synthese-Bausteine aus Hoerprobe 1 (../hoerprobe-1/synth.py), damit
beide Proben auf demselben Werkzeug beruhen, und kopiert Vergleichsdateien
aus Hoerprobe 1 hierher.
"""
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE.parent / "hoerprobe-1"
sys.path.insert(0, str(HP1))

import synth  # noqa: E402,F401  (wird von den Skripten weiterverwendet)


def copy_reference(name):
    """Kopiert eine Datei aus Hoerprobe 1 als vergleich-<name> hierher."""
    shutil.copyfile(HP1 / name, HERE / f"vergleich-{name}")
