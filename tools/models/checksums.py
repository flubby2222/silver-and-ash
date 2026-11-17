"""Schreibt die Prüfsummen-Listen: Größe und SHA-256 jeder Datei, die das Spiel aus assets/ lädt.

- assets/models/models.sha256: jede .glb und .png in assets/models/
- assets/textures/textures.sha256: jede .png in assets/textures/ (Level-Texturen, Issue #233)
- assets/audio/audio.sha256: jede .wav und .ogg in assets/audio/ (Geräusche, Issue #267)

Das Spiel vergleicht vor dem Laden jede Datei mit ihrer Liste (ModelChecksums im Core); eine beschädigte oder halb
kopierte Datei erreicht Raylib so nie. Tests prüfen, dass die Listen zu den eingecheckten Dateien passen.

Aufruf nach jedem Neu-Erzeugen eines Modells, einer Textur oder eines Geräuschs (aus dem Repository-Wurzelverzeichnis, normales Python 3):
    python tools/models/checksums.py
"""

import hashlib
import os

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets")
LISTS = [
    ("models", "models.sha256", (".glb", ".png"), "a model"),
    ("textures", "textures.sha256", (".png",), "a level texture"),
    ("audio", "audio.sha256", (".wav", ".ogg"), "a sound"),
]

for folder, list_name, endings, what in LISTS:
    directory = os.path.join(ASSETS, folder)
    lines = []
    for name in sorted(os.listdir(directory)):
        if name.endswith(endings):
            data = open(os.path.join(directory, name), "rb").read()
            lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")

    with open(os.path.join(directory, list_name), "w", encoding="ascii", newline="\n") as f:
        f.write(f"# sha256 size name - written by tools/models/checksums.py, checked by the game before loading {what}\n")
        f.write("\n".join(lines) + "\n")
    print(f"{folder}/{list_name}:")
    print("\n".join(lines))
