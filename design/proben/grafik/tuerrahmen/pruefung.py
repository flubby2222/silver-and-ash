"""Prueft die Oeffnungsbewegung rechnerisch (ohne Spiel): Fuer jede Tuer und vier Phasen (zu, 1/3, 2/3, offen) und beide
Schwingrichtungen werden die beweglichen Teile bewegt und geprueft:
  1. liegt jede Ecke in der Zelle (|x| <= 1, 0 <= y <= 3,5)?
  2. schneidet eine kantenweise Dreiecksflaeche des beweglichen Teils eine Flaeche des festen Teils (portal) oder eines
     anderen beweglichen Teils? (Strecke-Dreieck-Test, nur echte Durchstiche, Beruehrung zaehlt nicht)
Kopie fuer die Tuerrahmen (door_<art>_<variante>.glb); ausgenommen sind nur die Scharnierpfosten in der Tuerebene (|z| <= 0.16),
alle Rahmenteile davor (Laeufe, Kaempfer, Sockel) werden geprueft.
Aufruf: blender --background --python pruefung.py [-- Ordner mit den glb] (Blender-Python wegen numpy)
"""
import json
import math
import os
import struct
import sys

import numpy as np

DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelle")
PHASES = (0.0, 1 / 3, 2 / 3, 1.0)
OPEN_DEG = 90.0
GRATE_RISE = 1.8


def load(path):
    data = open(path, "rb").read()
    jlen, = struct.unpack_from("<I", data, 12)
    js = json.loads(data[20:20 + jlen])
    boff = 20 + jlen + 8
    out = {}
    for node in js["nodes"]:
        mesh = js["meshes"][node["mesh"]]["primitives"][0]
        def acc(i):
            a = js["accessors"][i]
            bv = js["bufferViews"][a["bufferView"]]
            comp = {"VEC3": 3, "SCALAR": 1}[a["type"]]
            dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}[a["componentType"]]
            return np.frombuffer(data, dt, a["count"] * comp, boff + bv.get("byteOffset", 0) + a.get("byteOffset", 0)).reshape(-1, comp)
        pos = acc(mesh["attributes"]["POSITION"]).astype(float)
        idx = acc(mesh["indices"]).reshape(-1, 3)
        t = np.array(node.get("translation", [0, 0, 0]), float)
        out[node["name"]] = {"pivot": t, "tris": (pos + t)[idx]}                  # Dreiecke in Dateikoordinaten (Welt der Tuer)
    return out


def rot_y(tris, pivot, angle):
    c, s = math.cos(angle), math.sin(angle)
    rel = tris - pivot
    x = rel[..., 0] * c + rel[..., 2] * s                                          # System.Numerics, Zeilenvektoren
    z = -rel[..., 0] * s + rel[..., 2] * c
    return np.stack([x, rel[..., 1], z], axis=-1) + pivot


def seg_tri_hits(segs, tris, eps=1e-3):
    """Anzahl der Strecken (n,2,3), die ein Dreieck (m,3,3) im Inneren durchstechen."""
    p0, p1 = segs[:, 0][:, None], segs[:, 1][:, None]
    a, b, c = tris[:, 0][None], tris[:, 1][None], tris[:, 2][None]
    e1, e2, d = b - a, c - a, p1 - p0
    h = np.cross(d, e2)
    det = (e1 * h).sum(-1)
    ok = np.abs(det) > 1e-9
    inv = np.where(ok, 1.0 / np.where(ok, det, 1), 0)
    s = p0 - a
    u = inv * (s * h).sum(-1)
    q = np.cross(s, e1)
    v = inv * (d * q).sum(-1)
    t = inv * (e2 * q).sum(-1)
    hit = ok & (u > eps) & (v > eps) & (u + v < 1 - eps) & (t > eps) & (t < 1 - eps)
    if os.environ.get("SA_DEBUG"):
        for ti in np.where(hit.any(axis=0))[0][:6]:
            c = tris[ti].mean(axis=0)
            print("      Dreieck fest bei x %.3f y %.3f z %.3f" % tuple(c))
    return int(hit.any(axis=1).sum())


def edges(tris):
    return np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]])


def static_for(k, static, parts):
    """Feste Flaechen ohne den Scharnierpfosten (dort sitzt das Scharnier im Stein, das ist gewollt)."""
    tris = static["tris"]
    if k not in parts or k == "grate":
        return tris
    px = abs(parts[k]["pivot"][0]) if k != "lock" else abs(parts["leaf"]["pivot"][0])
    post = (np.abs(tris[..., 0]).min(axis=1) >= px - 0.01) & (tris[..., 1].max(axis=1) <= 2.01) & (np.abs(tris[..., 2]).max(axis=1) <= 0.16)
    return tris[~post]


def check(name):
    parts = load(os.path.join(DIR, name))
    moving = {k: v for k, v in parts.items() if k in ("leaf_a", "leaf_b", "leaf", "grate")}
    child = {"lock": "leaf"}
    statics = [parts[k] for k in ("portal", "portal_front", "portal_back") if k in parts]
    static = {"pivot": statics[0]["pivot"], "tris": np.concatenate([p["tris"] for p in statics])} if statics else None
    rows = []
    for sign in (1, -1):
        for p in PHASES:
            posed = {}
            for k, v in moving.items():
                if k == "grate":
                    posed[k] = v["tris"] + np.array([0, GRATE_RISE * p, 0])
                else:
                    ang = math.radians(80.0 if name.startswith("door_locked") else OPEN_DEG) * p * sign * (-1 if k == "leaf_b" else 1)
                    posed[k] = rot_y(v["tris"], v["pivot"], ang)
            for k, parent in child.items():
                if k in parts and parent in moving:
                    ang = math.radians(80.0 if name.startswith("door_locked") else OPEN_DEG) * p * sign
                    posed[k] = rot_y(parts[k]["tris"], parts[parent]["pivot"], ang)
            allv = np.concatenate([t.reshape(-1, 3) for t in posed.values()])
            out_x = max(0.0, float(np.abs(allv[:, 0]).max()) - 1.0)
            out_y = max(0.0, float(allv[:, 1].max()) - 3.5)
            hits_static = sum(seg_tri_hits(edges(t), static_for(k, static, parts)) for k, t in posed.items()) if static else 0
            keys = list(posed)
            hits_self = 0
            for i in range(len(keys)):
                for j in range(len(keys)):
                    if i != j and not ({keys[i], keys[j]} == {"leaf", "lock"}):
                        hits_self += seg_tri_hits(edges(posed[keys[i]]), posed[keys[j]])
            rows.append((sign, p, out_x, out_y, hits_static, hits_self))
    return rows


for name in sorted(f for f in os.listdir(DIR) if f.endswith(".glb") and "cracked" not in f and f.endswith("_a.glb")):
    print(name)
    for sign, p, ox, oy, hs, hm in check(name):
        print(f"  Richtung {sign:+d} Phase {p:.2f}: ueber Zelle x {ox*100:.1f} cm, y {oy*100:.1f} cm; Durchstiche fest {hs}, bewegt {hm}")
