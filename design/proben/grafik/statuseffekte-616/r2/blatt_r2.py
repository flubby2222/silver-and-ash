"""Probenblatt Runde 2 (#616). Aufruf aus dem Repository-Wurzelordner:

    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/statuseffekte-616/r2/blatt_r2.py

Erzeugt in diesem Ordner: probenblatt_r2.png, spiel_*.png (854 x 480), holy_fire_b.gif, farbtabelle_r2.csv/.md, messwerte_r2.md.
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import basis as B  # noqa: E402
import effekte as E  # noqa: E402
import effekte_r2 as R  # noqa: E402
import gifschreiber as GIF  # noqa: E402

ORDER = [g[0] for g in R.GEWAEHLT]
NAMEN = {"marked": "Marked B", "holy_fire": "Holy Fire B", "grave_chill": "Grave Chill A", "stun": "Stun A",
         "leech": "Leech B (Violett)"}
BG = np.array((22, 18, 28), float)
WIDTH = 1280
PW, PH = 300, 280


def out(n):
    return os.path.join(HERE, n)


def blank(h):
    return np.tile(BG, (h, WIDTH, 1))


def crop(fr, an, d, w, h, up=0.28):
    S = E.px_per_metre(d)
    cx, cy = int(an["chest"][0]), int(an["chest"][1] - up * S)
    x0 = min(max(cx - w // 2, 0), fr.shape[1] - w)
    y0 = min(max(cy - h // 2, 0), fr.shape[0] - h)
    return fr[y0:y0 + h, x0:x0 + w]


PANEL = {4: (150, 140, 2), 8: (75, 70, 4), 12: (60, 56, 5)}


def panel(effs, d, t=0.0):
    fr = B.frame("one", d, [effs], t=t)
    _, _, an, dd = B.scene("one", d)
    w, h, f = PANEL[d]
    return B.up(crop(fr, an[0], dd, w, h), f)


def put_panel(blk, img, x, y, label):
    blk[y:y + img.shape[0], x:x + img.shape[1]] = img
    B.text(blk, label, x + 2, y + img.shape[0] + 1, 13)


def strip(fr, anchors, f):
    xs = [a["chest"][0] for a in anchors]
    ys = [a["chest"][1] for a in anchors]
    x0, x1 = int(max(min(xs) - 60, 0)), int(min(max(xs) + 60, 854))
    y0, y1 = int(np.mean(ys) - 60), int(np.mean(ys) + 40)
    return B.up(fr[y0:y1, x0:x1], f), (x0, y0)


def tables():
    rows = []
    for e in ORDER:
        for role, (row, step) in R.EFFEKTE2[e]["farben"].items():
            rows.append((R.EFFEKTE2[e]["name"], role, row, step, E.pal(row, step)))
    with open(out("farbtabelle_r2.csv"), "w", encoding="utf-8") as f:
        f.write("Effekt;Rolle;Reihe;Stufe (ab 0);R;G;B\n")
        for n, role, row, step, c in rows:
            f.write(f"{n};{role};{row};{step};{c[0]};{c[1]};{c[2]}\n")
        for i, c in enumerate(R.VIOLETT):
            f.write(f"Reihe Violett (Vorschlag);Stufe {i};Violett;{i};{c[0]};{c[1]};{c[2]}\n")
    with open(out("farbtabelle_r2.md"), "w", encoding="utf-8") as f:
        f.write("| Effekt | Rolle | Reihe | Stufe (ab 0) | RGB |\n|---|---|---|---|---|\n")
        for n, role, row, step, c in rows:
            f.write(f"| {n} | {role} | {row} | {step} | {c[0]}, {c[1]}, {c[2]} |\n")
        f.write("\n**Neue Reihe Violett (Vorschlag, acht Stufen):**\n\n| Stufe | RGB |\n|---|---|\n")
        for i, c in enumerate(R.VIOLETT):
            f.write(f"| {i} | {c[0]}, {c[1]}, {c[2]} |\n")


def header():
    h = blank(500)
    B.text(h, "Statuseffekte am Gegner - Probenblatt Runde 2 (#616)", 16, 8, 28, bold=True)
    B.text(h, "Urteil: Stun A, Grave Chill A, Marked B, Holy Fire B, Leech B. Detailreicher, am meisten Holy Fire. "
           "Neue Reihe Violett fuer Leech.", 16, 46, 15, (190, 185, 200))
    y = 80
    for e in ORDER:
        B.text(h, R.EFFEKTE2[e]["name"], 16, y, 15, bold=True)
        x = 260
        for n, (role, (row, step)) in enumerate(R.EFFEKTE2[e]["farben"].items()):
            if n == 5:
                x, y = 260, y + 32
            c = E.pal(row, step)
            h[y + 2:y + 24, x:x + 28] = c
            B.text(h, f"{role}", x + 32, y - 7, 11, (190, 185, 200))
            B.text(h, f"{row} {step} ({c[0]},{c[1]},{c[2]})", x + 32, y + 6, 11)
            x += 200
        y += 32
    y += 14
    B.text(h, "Reihen im Vergleich: Violett (Vorschlag, neu), Stoff (Blau, bestehend), Blutrot (bestehend, nur fuer die "
           "Angriffs-Ankuendigung)", 16, y, 14, bold=True)
    y += 30
    for name in ("Violett", "Stoff", "Blutrot"):
        B.text(h, name, 16, y + 4, 14)
        for i, c in enumerate(E.ROWS[name]):
            h[y:y + 30, 120 + i * 120:120 + i * 120 + 112] = c
            B.text(h, f"{i}: {c[0]},{c[1]},{c[2]}", 124 + i * 120, y + 30, 11)
        y += 56
    return h


def messwerte(items):
    lines = ["# Messwerte Runde 2 (ohne Koernung): Effektpixel, Mittelfarbe, Abstand zur Umgebung\n"]
    lines.append("| Effekt | Abstand m | Effektpixel | Mittelfarbe | Umgebung | Abstand (RGB) |\n|---|---|---|---|---|---|")
    for d in (8, 12):
        base, masks, anchors, dd = B.scene("line", d)
        for i, g in enumerate(items):
            img = base.copy()
            E.apply(img, masks[i], anchors[i], dd, [g], t=0.0, seed=10 + i)
            diff = np.abs(img - base).sum(axis=2) > 0
            ring = np.zeros_like(diff)
            ys, xs = np.where(diff)
            for dy in range(-6, 7, 3):
                for dx in range(-6, 7, 3):
                    ring[np.clip(ys + dy, 0, 479), np.clip(xs + dx, 0, 853)] = True
            ring &= ~diff & ~masks[i]
            m = img[diff].mean(axis=0)
            b = base[ring].mean(axis=0) if ring.any() else np.zeros(3)
            lines.append(f"| {g[0]} {g[1]} | {d} | {int(diff.sum())} | {m.round().astype(int).tolist()} | "
                         f"{b.round().astype(int).tolist()} | {np.linalg.norm(m - b):.0f} |")
    with open(out("messwerte_r2.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


def main():
    B.ROOM_BACK = -16.0   # Kamera steht im Gang (Runde 1 stand bei 8 m und mehr vor dem Gang)
    R.pruefe_palette2()
    E.pruefe_palette()
    tables()
    canvas = [header()]
    sp = {}

    # Gewaehlte Fassungen in der Reihe, 8 m und 12 m, Kontrolle
    for d, f in ((8, 4), (12, 3)):
        fr = B.frame("line", d, [[g] for g in R.GEWAEHLT])
        _, _, an, _ = B.scene("line", d)
        s, org = strip(fr, an, f)
        blk = blank(s.shape[0] + 70)
        B.text(blk, f"Gewaehlte Fassungen nebeneinander, {d} m, {f}-fach vergroessert", 16, 4, 18, bold=True)
        blk[34:34 + s.shape[0], :min(WIDTH, s.shape[1])] = s[:, :WIDTH]
        for a, e in zip(an, ORDER):
            B.text(blk, NAMEN[e], int((a["chest"][0] - org[0]) * f) - 40, 34 + s.shape[0] + 2, 15)
        canvas.append(blk)
        B.save(fr, out(f"spiel_{d}m_gewaehlt.png"))
        sp[f"l{d}"] = fr
        ctl = B.frame("line", d, [[] for _ in ORDER])
        B.save(ctl, out(f"spiel_{d}m_kontrolle.png"))
        if d == 8:
            s2, _ = strip(ctl, an, f)
            b2 = blank(s2.shape[0] + 40)
            B.text(b2, "Kontrolle: dieselben Ghule ohne Effekt (8 m)", 16, 4, 18, bold=True)
            b2[34:34 + s2.shape[0], :min(WIDTH, s2.shape[1])] = s2[:, :WIDTH]
            canvas.append(b2)

    # Detail-Stufen Holy Fire und Leech: a (zurueckhaltend) und b (kraeftiger)
    for eff, title, bt in (("holy_fire", "Holy Fire: Detail-Stufe a (zurueckhaltend) gegen b (kraeftiger)", (0.3, 0.6)),
                           ("leech", "Leech (Violett): Detail-Stufe a (zurueckhaltend) gegen b (kraeftiger)", (0.3, 0.6))):
        blk = blank(2 * (PH + 24) + 36)
        B.text(blk, title, 16, 4, 18, bold=True)
        a, b = (eff, "R2a"), (eff, "R2b")
        x = [0, 320, 640, 960]
        y1, y2 = 34, 34 + PH + 24
        put_panel(blk, panel([a], 4), x[0], y1, "a, 4 m (2-fach)")
        put_panel(blk, panel([b], 4), x[1], y1, "b, 4 m (2-fach)")
        put_panel(blk, panel([a], 8), x[2], y1, "a, 8 m (4-fach)")
        put_panel(blk, panel([b], 8), x[3], y1, "b, 8 m (4-fach)")
        put_panel(blk, panel([a], 12), x[0], y2, "a, 12 m (5-fach)")
        put_panel(blk, panel([b], 12), x[1], y2, "b, 12 m (5-fach)")
        put_panel(blk, panel([b], 4, bt[0]), x[2], y2, "b, 4 m, spaetere Phase")
        put_panel(blk, panel([b], 4, bt[1]), x[3], y2, "b, 4 m, noch spaeter")
        canvas.append(blk)
        for d in (4, 8, 12):
            for v, g in (("a", a), ("b", b)):
                B.save(B.frame("one", d, [[g]]), out(f"spiel_{eff}_{v}_{d}m.png"))

    # Drei Effekte gleichzeitig
    trios = [[("marked", "R2"), ("holy_fire", "R2b"), ("stun", "R2")],
             [("grave_chill", "R2"), ("leech", "R2b"), ("marked", "R2")]]
    blk = blank(2 * (PH + 24) + 36)
    B.text(blk, "Drei Effekte gleichzeitig: oben Marked + Holy Fire + Stun, unten Grave Chill + Leech + Marked "
           "(4 m, 8 m, 12 m)", 16, 4, 16, bold=True)
    for i, tr in enumerate(trios):
        for j, d in enumerate((4, 8, 12)):
            put_panel(blk, panel(tr, d), j * 320, 34 + i * (PH + 24), f"{' + '.join(e for e, _ in tr)}, {d} m")
            B.save(B.frame("one", d, [tr]), out(f"spiel_dreifach_{i + 1}_{d}m.png"))
    canvas.append(blk)

    sheet = np.concatenate(canvas, axis=0)
    B.save(sheet, out("probenblatt_r2.png"))
    messwerte(R.GEWAEHLT + [])

    # GIF: Holy Fire b und a, vier Metern, acht Phasen
    frames = []
    for k in range(8):
        pa = panel([("holy_fire", "R2a")], 4, k / 8.0)
        pb = panel([("holy_fire", "R2b")], 4, k / 8.0)
        frames.append(np.concatenate([pa, np.tile(BG, (pa.shape[0], 8, 1)), pb], axis=1))
    GIF.write_gif(out("holy_fire_a_b.gif"), frames, delay_cs=10)
    # Leech als zweites GIF (kleiner Aufwand)
    frames = []
    for k in range(8):
        pa = panel([("leech", "R2a")], 4, k / 8.0)
        pb = panel([("leech", "R2b")], 4, k / 8.0)
        frames.append(np.concatenate([pa, np.tile(BG, (pa.shape[0], 8, 1)), pb], axis=1))
    GIF.write_gif(out("leech_a_b.gif"), frames, delay_cs=10)


main()
