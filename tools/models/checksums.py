"""Schreibt assets/models/models.sha256: Größe und SHA-256 jeder .glb und .png in assets/models/.

Das Spiel vergleicht vor dem Laden jede Modelldatei mit dieser Liste (ModelChecksums im Core); eine beschädigte
oder halb kopierte Datei erreicht Raylib so nie. Ein Test prüft, dass die Liste zu den eingecheckten Dateien passt.

Aufruf nach jedem Neu-Erzeugen eines Modells (aus dem Repository-Wurzelverzeichnis, normales Python 3):
    python tools/models/checksums.py
"""

import hashlib
import os

MODELS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "models")

lines = []
for name in sorted(os.listdir(MODELS)):
    if name.endswith((".glb", ".png")):
        data = open(os.path.join(MODELS, name), "rb").read()
        lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")

with open(os.path.join(MODELS, "models.sha256"), "w", encoding="ascii", newline="\n") as f:
    f.write("# sha256 size name - written by tools/models/checksums.py, checked by the game before loading a model\n")
    f.write("\n".join(lines) + "\n")
print("\n".join(lines))
