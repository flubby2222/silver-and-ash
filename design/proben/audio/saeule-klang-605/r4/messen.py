#!/usr/bin/env python3
"""Misst die Dateien von Runde 4 mit dem Messskript von Runde 1."""
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import messen  # noqa: E402

messen.HERE = HERE
messen.main()
