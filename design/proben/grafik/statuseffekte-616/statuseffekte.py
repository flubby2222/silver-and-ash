"""Probenblatt Statuseffekte (Issue #616). Aufruf (aus dem Repository-Wurzelordner):

    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/statuseffekte-616/statuseffekte.py

Erzeugt im selben Ordner: probenblatt.png, spiel_*.png (854 x 480, Spielauflaesung), farbtabelle.csv, farbtabelle.md,
messwerte.md. Nichts davon fliesst ins Spiel; das Skript liest nur assets/models/ghoul.glb und die Hauptpalette.
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import basis as B  # noqa: E402
import effekte as E  # noqa: E402

ORDER5 = B.ORDER5
NAMEN = {e: E.EFFEKTE[e]["name"] for e in ORDER5}
BG = np.array((22, 18, 28), float)
WIDTH = 1280


def out(name):
    return os.path.join(HERE, name)


def zoom_strip(fr, anchors, f):
    xs = [a["chest"][0] for a in anchors]
    ys = [a["chest"][1] for a in anchors]
    x0, x1 = int(max(min(xs) - 60, 0)), int(min(max(xs) + 60, 854))
    y0, y1 = int(np.mean(ys) - 55), int(np.mean(ys) + 40)
    return B.up(fr[y0:y1, x0:x1], f), (x0, y0)


def labels_under(strip, anchors, org, f, names, y):
    for a, n in zip(anchors, names):
        if n:
            B.text(strip, n, int((a["chest"][0] - org[0]) * f) - 40, y, size=16)


def table_rows():
    rows = []
    for e in ORDER5:
        for role, (row, step) in E.EFFEKTE[e]["farben"].items():
            rows.append((E.EFFEKTE[e]["name"], role, row, step, E.pal(row, step)))
    return rows


def write_tables():
    rows = table_rows()
    with open(out("farbtabelle.csv"), "w", encoding="utf-8") as f:
        f.write("Effekt;Rolle;Reihe;Stufe (ab 0);R;G;B\n")
        for n, role, row, step, c in rows:
            f.write(f"{n};{role};{row};{step};{c[0]};{c[1]};{c[2]}\n")
    with open(out("farbtabelle.md"), "w", encoding="utf-8") as f:
        f.write("| Effekt | Rolle | Reihe | Stufe (ab 0) | RGB |\n|---|---|---|---|---|\n")
        for n, role, row, step, c in rows:
            f.write(f"| {n} | {role} | {row} | {step} | {c[0]}, {c[1]}, {c[2]} |\n")


def sheet():
    E.pruefe_palette()
    write_tables()
    parts = []   # (hoehe, funktion)
    canvas = []

    def block(img):
        canvas.append(img)

    def blank(h):
        return np.tile(BG, (h, WIDTH, 1))

    # Kopf und Farbtabelle
    head = blank(230)
    B.text(head, "Statuseffekte am Gegner - Probenblatt (#616)", 16, 8, 28, bold=True)
    B.text(head, "Ghul aus assets/models/ghoul.glb, Spielbedingungen 854 x 480, Laterne und Nebel, 15-Bit-Koernung. "
           "Kein Rot am Gegner.", 16, 46, 15, (190, 185, 200))
    y = 80
    for e in ORDER5:
        B.text(head, NAMEN[e], 16, y, 18, bold=True)
        x = 170
        for role, (row, step) in E.EFFEKTE[e]["farben"].items():
            c = E.pal(row, step)
            head[y + 2:y + 28, x:x + 40] = c
            B.text(head, f"{role}", x + 46, y - 6, 12, (190, 185, 200))
            B.text(head, f"{row} {step}  ({c[0]},{c[1]},{c[2]})", x + 46, y + 8, 12)
            x += 360
        y += 30
    block(head)

    ohne = [[] for _ in ORDER5]
    for kind, d, title_a, title_b in (
            (8, 8, "Variante A (Zeichen/Partikel), 8 m, 4-fach vergroessert",
             "Variante B (Umriss/Toenung/Kruste), 8 m, 4-fach vergroessert"),
            (12, 12, "Variante A, 12 m im Dunkeln, 3-fach vergroessert",
             "Variante B, 12 m im Dunkeln, 3-fach vergroessert")):
        f = 4 if d == 8 else 3
        for v, title in (("A", title_a), ("B", title_b)):
            fr = B.frame("line", d, [[(e, v)] for e in ORDER5])
            base, masks, anchors, dd = B.scene("line", d)
            strip, org = zoom_strip(fr, anchors, f)
            h = strip.shape[0]
            blk = blank(h + 70)
            B.text(blk, title, 16, 4, 18, bold=True)
            blk[34:34 + h, :min(WIDTH, strip.shape[1])] = strip[:, :WIDTH]
            labels_under(blk, anchors, org, f, [NAMEN[e] for e in ORDER5], 34 + h + 2)
            block(blk)
            B.save(fr, out(f"spiel_{d}m_{v}.png"))
        if d == 8:
            fr = B.frame("line", 8, ohne)
            base, masks, anchors, dd = B.scene("line", 8)
            strip, org = zoom_strip(fr, anchors, 4)
            h = strip.shape[0]
            blk = blank(h + 40)
            B.text(blk, "Kontrolle: dieselben Ghule ohne Effekt (8 m)", 16, 4, 18, bold=True)
            blk[34:34 + h, :min(WIDTH, strip.shape[1])] = strip[:, :WIDTH]
            block(blk)

    # Drei Effekte gleichzeitig
    trios = [[("marked", "A"), ("holy_fire", "A"), ("stun", "A")],
             [("marked", "B"), ("holy_fire", "B"), ("stun", "B")],
             [("grave_chill", "A"), ("leech", "A"), ("marked", "A")],
             [("grave_chill", "B"), ("leech", "B"), ("marked", "B")]]
    blk = blank(2 * 260 + 130)
    B.text(blk, "Drei Effekte gleichzeitig auf einem Ghul: oben Marked + Holy Fire + Stun, unten Grave Chill + Leech + "
           "Marked (links 4 m, 2-fach / rechts 8 m, 4-fach; je A und B)", 16, 4, 16, bold=True)
    for i, tr in enumerate(trios):
        row, col = divmod(i, 2)
        f4 = B.frame("one", 4, [tr])
        _, _, an4, d4 = B.scene("one", 4)
        c4 = B.up(B.crop_around(f4, an4[0], d4, 150, 130), 2)
        f8 = B.frame("one", 8, [tr])
        _, _, an8, d8 = B.scene("one", 8)
        c8 = B.up(B.crop_around(f8, an8[0], d8, 75, 65), 4)
        x0 = col * 640
        y0 = 34 + row * 310
        blk[y0:y0 + 260, x0:x0 + 300] = c4
        blk[y0:y0 + 260, x0 + 310:x0 + 610] = c8[:260, :300] if c8.shape[0] >= 260 else np.pad(
            c8, ((0, 260 - c8.shape[0]), (0, 0), (0, 0)))[:, :300]
        names = " + ".join(NAMEN[e] for e, _ in tr)
        B.text(blk, f"{names} ({'A' if col == 0 else 'B'})", x0 + 4, y0 + 262, 13)
        B.save(f8, out(f"spiel_dreifach_{i + 1}_8m.png"))
        B.save(f4, out(f"spiel_dreifach_{i + 1}_4m.png"))
    block(blk[:34 + 2 * 310])

    sheet_img = np.concatenate(canvas, axis=0)
    B.save(sheet_img, out("probenblatt.png"))
    # Vorschau: Blatt auf 854 Pixel Breite (jede zweite Zeile/Spalte), um die Lesbarkeit zu pruefen
    return sheet_img


def messwerte():
    """Zahlen bei 8 m: Effektpixel, Mittelfarbe, Abstand der Mittelfarben, Kontrast zum Stein."""
    lines = ["# Messwerte bei 8 m (Linie aus fuenf Ghulen, 854 x 480, ohne Koernung)\n"]
    base, masks, anchors, d = B.scene("line", 8)
    res = {}
    for v in "AB":
        res[v] = {}
        for i, e in enumerate(ORDER5):
            img = base.copy()
            E.apply(img, masks[i], anchors[i], d, [(e, v)], t=0.0, seed=10 + i)
            diff = np.abs(img - base).sum(axis=2) > 0
            px = img[diff]
            ring = diff.copy()
            # Umgebung: Rahmen von 6 px um die Effektpixel, ohne den Ghul selbst
            from_ring = np.zeros_like(diff)
            ys, xs = np.where(diff)
            for dy in range(-6, 7, 3):
                for dx in range(-6, 7, 3):
                    from_ring[np.clip(ys + dy, 0, 479), np.clip(xs + dx, 0, 853)] = True
            from_ring &= ~diff & ~masks[i]
            bgm = base[from_ring].mean(axis=0) if from_ring.any() else np.zeros(3)
            res[v][e] = (int(diff.sum()), px.mean(axis=0), bgm)
    for v in "AB":
        lines.append(f"\n## Variante {v}\n\n| Effekt | Effektpixel | Mittelfarbe RGB | Umgebung RGB | Abstand zur Umgebung |\n"
                     "|---|---|---|---|---|")
        for e in ORDER5:
            n, m, bgm = res[v][e]
            lines.append(f"| {NAMEN[e]} | {n} | {m.round().astype(int).tolist()} | {bgm.round().astype(int).tolist()} "
                         f"| {np.linalg.norm(m - bgm):.0f} |")
        lines.append("\nAbstand der Mittelfarben zueinander (RGB-Euklid):\n")
        lines.append("| | " + " | ".join(NAMEN[e] for e in ORDER5) + " |\n|---|" + "---|" * len(ORDER5))
        for a in ORDER5:
            lines.append(f"| {NAMEN[a]} | " + " | ".join(
                f"{np.linalg.norm(res[v][a][1] - res[v][b][1]):.0f}" for b in ORDER5) + " |")
    with open(out("messwerte.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    img = sheet()
    messwerte()
    print("Blatt:", img.shape)
