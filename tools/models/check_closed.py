"""Prueft, dass jedes Netz der .glb-Modelle rundum geschlossen ist (Art-Bible, Regel 21, Issue #414).

Je Netz: Ecken mit gleicher Lage werden zusammengelegt (Flat-Shading und UV-Naehte trennen Ecken, nicht Kanten); danach
muss jede Kante zu genau zwei Dreiecken gehoeren. Kanten mit nur einer Flaeche sind offene Seiten oder Luecken.
Kanten mit mehr als zwei Flaechen (Teile, die sich Kanten teilen) werden getrennt gemeldet.

Aufruf (normales Python 3, aus dem Repository-Wurzelverzeichnis):
    python tools/models/check_closed.py                      alle Modelle in assets/models/
    python tools/models/check_closed.py assets/models/club.glb [...]
Rueckgabewert 1, wenn ein geprueftes Modell offene Kanten hat.
"""

import collections
import glob
import json
import os
import struct
import sys


MODELS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "models")
CT = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def read_glb(path):
    data = open(path, "rb").read()
    jlen, _ = struct.unpack_from("<II", data, 12)
    js = json.loads(data[20:20 + jlen])
    blob = data[20 + jlen + 8:]
    return js, blob


def accessor(js, blob, idx):
    a = js["accessors"][idx]
    bv = js["bufferViews"][a["bufferView"]]
    n = NC[a["type"]]
    fmt = CT[a["componentType"]]
    size = struct.calcsize(fmt)
    stride = bv.get("byteStride") or size * n
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * n, blob, off + i * stride) for i in range(a["count"])]


def check(path):
    """Liste (Netzname, Dreiecke, offene Kanten, Kanten mit mehr als zwei Flaechen)."""
    js, blob = read_glb(path)
    result = []
    for node in js["nodes"]:
        if "mesh" not in node:
            continue
        mesh = js["meshes"][node["mesh"]]
        for prim in mesh["primitives"]:
            pos = accessor(js, blob, prim["attributes"]["POSITION"])
            idx = [int(r[0]) for r in accessor(js, blob, prim["indices"])] if "indices" in prim else list(range(len(pos)))
            keys = {}
            ids = [keys.setdefault(tuple(round(c * 1e5) for c in p), len(keys)) for p in pos]
            flat = [ids[i] for i in idx]
            tri = [flat[i:i + 3] for i in range(0, len(flat), 3)]
            edges = collections.Counter()
            for a, b, c in tri:
                if a == b or b == c or a == c:
                    continue
                for u, v in ((a, b), (b, c), (c, a)):
                    edges[(min(u, v), max(u, v))] += 1
            open_edges = sum(1 for n in edges.values() if n == 1)
            multi = sum(1 for n in edges.values() if n > 2)
            result.append((node.get("name", mesh.get("name", "?")), len(tri), open_edges, multi))
    return result


def main():
    paths = sys.argv[1:] or sorted(glob.glob(os.path.join(MODELS, "*.glb")))
    bad = 0
    for path in paths:
        parts = check(path)
        open_total = sum(p[2] for p in parts)
        bad += open_total > 0
        print(f"{os.path.basename(path)}: {'OFFEN' if open_total else 'geschlossen'} "
              f"({sum(p[1] for p in parts)} Dreiecke, {open_total} offene Kanten)")
        for name, tris, o, m in parts:
            if o or m:
                print(f"    {name}: {tris} Dreiecke, {o} offene Kanten, {m} Kanten mit mehr als zwei Flaechen")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
