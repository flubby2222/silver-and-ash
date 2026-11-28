"""Throwing Knives (Issue #204), Bewegungsprobe: Pruefung jeder Phase von Wurf A, Wurf B und Nachladen.

1. Jeder Tick (Wurf A 0..18, Wurf B 0..40, je fuer 3, 2 und 1 Messer vor dem Wurf; Nachladen 0..120) an der Geometrie:
   Regel 30 (Dreiecke der Messer gegen Manschette und Unterarm ausser dem Deckel), sichtbare Punkte hinter dem Deckel
   (der Schnitt des Handmodells), Abstand zur Silver Blade, Naehe zum Auge, Reichweite, offenes Ende des Unterarms.
2. Schluesselhaltungen im Bild 854 x 480: Ueberdeckung benachbarter Messer und der Wicklungen (Masken je Messer wie
   staffel.overlap, immer mit allen drei Messern: strengster Fall), Bildpunkte im Zielfenster.
3. Faecher 0 bis 20 Grad: ab welchem Winkel die Messer im Bild frei stehen (Nachladen oeffnet von FAN_CLOSED auf 17).
4. Nachladen: solange die Manschette unter dem Bildrand ist, steht kein Bildpunkt eines Messers im Bild.

Schreibt zahlen.txt und bricht ab, wenn eine Phase einen Durchstich, einen sichtbaren Schnitt oder eine Ueberdeckung hat.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python pruefe.py
"""

import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bewegung as M  # noqa: E402
import szene as Z  # noqa: E402

KEYS_A = (0, 1, 2, 4, 8, 12, 16)
KEYS_B = (0, 2, 4, 6, 10, 16, 22, 28, 34, 37)
KEYS_R = (0.04, 0.08, 0.54, 0.58, 0.62, 0.68, 0.72, 0.76, 0.80, 0.88, 0.93, 1.0)
HIDDEN = (0.13, 0.30, 0.49)


def worst(rows):
    return {"pierce": sum(r["pierce"] for r in rows), "seen": sum(r["seen"] for r in rows),
            "open_end": sum(r["open_end"] for r in rows), "blade_mm": min(r["blade_mm"] for r in rows),
            "near": min(r["near"] for r in rows), "far": max(r["far"] for r in rows),
            "knick": max(r["knick"] for r in rows), "cross": max(r["cross"] for r in rows)}


def every_tick(s, name, pose_at, ticks, shown_at):
    rows = []
    for t in range(ticks + 1):
        knick = Z.pose(s, pose_at(t))
        r = Z.measure(s, tuple(shown_at(t)))
        r["knick"] = knick
        rows.append(r)
    w = worst(rows)
    line = (f"{name}: {ticks + 1} Ticks | Dreieckspaare gegen den Aermel {w['pierce']} | sichtbare Punkte hinter dem Deckel "
            f"{w['seen']} | offenes Unterarm-Ende im Bild {w['open_end']} | kleinster Abstand zur Silver Blade "
            f"{w['blade_mm']:.0f} mm | naechster Punkt am Auge {w['near'] * 100:.1f} cm (Grenze 5) | weitester Punkt "
            f"{w['far'] * 100:.1f} cm (Grenze 30) | groesster Knick Manschette/Unterarm {w['knick']:.1f} Grad")
    ok = w["pierce"] == 0 and w["seen"] == 0 and w["open_end"] == 0 and w["near"] >= Z.NEAR and w["far"] <= Z.REACH
    return line, ok, w


def keys(s, name, poses):
    lines, ok, aim, pixels = [], True, 0, 0
    for label, p in poses:
        Z.pose(s, p)
        r = Z.picture(s)
        wrap = sum(b for _a, b, _n in r["wrap"])
        lines.append(f"  {name} {label}: Bildzeilen mit Ueberdeckung {len(r['rows'])}, Wicklung ueberdeckt {wrap} px, "
                     f"sichtbar je Messer {r['visible']} px, im Zielfenster {r['aim']} px")
        ok = ok and not r["rows"] and wrap == 0
        aim, pixels = max(aim, r["aim"]), max(pixels, r["pixels"])
    return lines, ok, aim, pixels


def travel(s, pose_at, ticks):
    """Weg des Haltepunkts im Bild: groesster Abstand von der Ruhe und groesster Sprung zwischen zwei Ticks (Bildpunkte)."""
    def point(t):
        p = pose_at(t)
        q = Z.project(Z.G(s.hand + Z.V(*p["shift"])))
        return q[0], q[1]
    home = Z.project(Z.G(s.hand))
    pts = [point(t) for t in range(ticks + 1)]
    far = max(((x - home[0]) ** 2 + (y - home[1]) ** 2) ** 0.5 for x, y in pts)
    jump = max(((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5 for a, b in zip([home[:2]] + pts, pts))
    return far, jump


def main():
    s = Z.setup()
    lines, good = ["Throwing Knives (Issue 204), Bewegungsprobe: Zahlen aus pruefe.py",
                   f"Zeiten: Wurfabstand {M.COOLDOWN_TICKS} Ticks, Nachladen {M.RELOAD_TICKS} Ticks (assets/balance.json); "
                   f"Wurf A {M.A_TICKS} Ticks, Wurf B {M.B_TICKS} Ticks, danach Ruhe bis Tick {M.COOLDOWN_TICKS}", ""], True

    lines.append("Faecher (Ruhehaltung, alle drei Messer):")
    free = []
    for f in (0, 4, 8, 10, 12, 13, 14, 15, 16, 17, 18, 20):
        Z.pose(s, dict(M.REST, fan=float(f)))
        g, r = Z.measure(s), Z.picture(s)
        lines.append(f"  {f:2d} Grad: Dreieckspaare gegen den Aermel {g['pierce']}, sichtbar hinter dem Deckel {g['seen']}, "
                     f"Bildzeilen mit Ueberdeckung {len(r['rows'])}")
        if g["pierce"] == 0 and g["seen"] == 0 and not r["rows"]:
            free.append(f)
    lines += [f"  frei: {free} Grad; Nachladen oeffnet von {M.FAN_CLOSED:.0f} auf {M.FAN_REST:.0f} Grad", ""]
    good = good and int(M.FAN_CLOSED) in free and 18 in free

    summary = {}
    for tag, name, pose_at, ticks, key_ticks in (("A", "Wurf A Schnapp", M.throw_a, M.A_TICKS, KEYS_A),
                                                  ("B", "Wurf B Durchzug", M.throw_b, M.B_TICKS, KEYS_B)):
        lines.append(f"{name}, jeder Tick:")
        knick, gap = 0.0, 9e9
        for before in (3, 2, 1):
            line, ok, w = every_tick(s, f"  {before} -> {before - 1} Messer", pose_at, ticks, lambda t, b=before: M.held(b, t))
            lines.append(line)
            good, knick, gap = good and ok, max(knick, w["knick"]), min(gap, w["blade_mm"])
        klines, ok, aim, pixels = keys(s, "Tick", [(f"{t:2d}", pose_at(t)) for t in key_ticks])
        far, jump = travel(s, pose_at, ticks)
        lines += [f"{name}, Schluesselhaltungen im Bild (alle drei Messer):"] + klines + [""]
        good = good and ok
        summary[tag] = (name, ticks, knick, gap, aim, pixels, far, jump)

    lines.append("Nachladen, jeder Tick (120 Ticks, Fortschritt = Tick / 120):")
    line, ok, _w = every_tick(s, "  0 -> 3 Messer", lambda t: M.reload(t / M.RELOAD_TICKS), M.RELOAD_TICKS,
                              lambda t: M.magazine(M.reload_count(t / M.RELOAD_TICKS)))
    lines.append(line)
    good = good and ok
    line, ok, _w = every_tick(s, "  strengster Fall, immer drei Messer", lambda t: M.reload(t / M.RELOAD_TICKS),
                              M.RELOAD_TICKS, lambda t: (0, 1, 2))
    lines.append(line)
    good = good and ok
    klines, ok, _aim, _px = keys(s, "Fortschritt", [(f"{p:.2f}", M.reload(p)) for p in KEYS_R])
    lines += ["Nachladen, Schluesselhaltungen im Bild (alle drei Messer):"] + klines
    good = good and ok
    lines.append("Nachladen, Manschette unter dem Bildrand (hier erscheinen die drei Handmodelle):")
    for p in HIDDEN:
        Z.pose(s, M.reload(p))
        r, g = Z.picture(s), Z.measure(s)
        lines.append(f"  Fortschritt {p:.2f}: Bildpunkte der Messer {sum(r['visible'])}, Ecken von Manschette und Messern im Bild {g['in_frame']}")
        good = good and sum(r["visible"]) == 0 and g["in_frame"] == 0
    hidden = [t for t in range(M.RELOAD_TICKS + 1) if M.R_DOWN <= t / M.RELOAD_TICKS <= M.R_UP]
    inside = 0
    for t in hidden:
        Z.pose(s, M.reload(t / M.RELOAD_TICKS))
        inside += Z.measure(s)["in_frame"]
    lines += [f"  Ticks {hidden[0]} bis {hidden[-1]}: Ecken von Manschette und Messern im Bild, Summe {inside}", ""]
    good = good and inside == 0

    lines.append("Vergleich der beiden Wuerfe:")
    for tag in ("A", "B"):
        name, ticks, knick, gap, aim, pixels, far, jump = summary[tag]
        lines.append(f"  {name}: Hand ruht wieder nach {ticks} von {M.COOLDOWN_TICKS} Ticks ({M.COOLDOWN_TICKS - ticks} Ticks Ruhe vor dem "
                     f"naechsten Wurf) | groesster Knick {knick:.1f} Grad | kleinster Abstand zur Silver Blade {gap:.0f} mm | "
                     f"Haltepunkt wandert bis {far:.0f} px, groesster Sprung je Tick {jump:.0f} px | im Zielfenster "
                     f"{Z.AIM[0]} x {Z.AIM[1]} px bis {aim} px | Aermel und Messer bedecken bis {pixels} px")
    lines.append("ERGEBNIS: " + ("alle Phasen frei" if good else "NICHT FREI, siehe oben"))
    with open(os.path.join(HERE, "zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join("ZAHL " + x for x in lines))
    assert good, "eine Phase ist nicht frei"


if __name__ == "__main__":
    main()
