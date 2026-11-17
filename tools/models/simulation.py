"""Rechnet die Klingenhaltung aus WeaponPose.cs nach (Zeilenvektoren wie System.Numerics) und prueft, welche Punkte der
Aermel-Mittellinie in welcher Phase im Bild liegen. Nur Hilfsrechnung, keine Spielcode-Aenderung."""
import math

S = 1.8
TH, TV = 1.246, 0.700            # Tangens des halben Blickwinkels waagerecht / senkrecht (70 Grad senkrecht, 854 x 480)
NEAR = 0.10                      # Nahebene 0,05 im geschrumpften Raum = 0,10 ungeschrumpft


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return lambda p: (p[0] * c - p[1] * s, p[0] * s + p[1] * c, p[2])


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return lambda p: (p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c)


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return lambda p: (p[0], p[1] * c - p[2] * s, p[1] * s + p[2] * c)


def blade_to_camera(p, swing, wind):
    """p in Dateikoordinaten (Meter, Skalierung 1,8 schon eingerechnet)."""
    r = math.radians
    q = (-p[0], p[1], p[2])                                   # FileToCamera
    q = rz(r(-(20 + 25 * swing)))(q)
    q = rx(r(-40 * wind))(q)
    q = ry(r(95 * swing - 35 * wind))(q)
    g = (-0.11 + -0.03 * wind + 0.06 * swing, -0.095 - 0.01 * wind + 0.04 * swing, 0.19 - 0.03 * wind)
    return (q[0] + g[0], q[1] + g[1], q[2] + g[2])


def in_view(c, margin=0.0):
    x, y, z = c
    return z > NEAR and abs(x) < TH * z + margin and abs(y) < TV * z + margin


def file_points(path, s=-1):
    """Mittellinie wie in arm.py (C(s*x, y, z) = V(-s*x, y, z) in Spiel-Achsen): Dateikoordinaten x = -s*xr."""
    return [(-s * p[0] * S, p[1] * S, p[2] * S) for p in path]


def report(path, phases, label):
    pts = file_points(path)
    print(label)
    for name, sw, wi in phases:
        vis = []
        for i, p in enumerate(pts):
            c = blade_to_camera(p, sw, wi)
            if in_view(c):
                vis.append(i)
        print(f"  {name:10s} im Bild: Ringe {vis}")


if __name__ == "__main__":
    PHASES = [("Ruhe", 0, 0), ("wind 0.5", 0, 0.5), ("wind 1", 0, 1), ("swing .25", 0.25, 0), ("swing .5", 0.5, 0),
              ("swing .75", 0.75, 0), ("swing 1", 1, 0)]
    i_path = [(0, 0, -0.003), (0, 0, -0.016), (0.0, -0.004, -0.04), (0.035, -0.02, -0.07), (0.09, -0.06, -0.09),
              (0.18, -0.11, -0.12), (0.30, -0.17, -0.16), (0.78, -0.41, -0.32)]
    report(i_path, PHASES, "Fassung i (Mittellinie, ohne Radius)")


# --- Fassung k: Regel fuer den Unterarm (Knoten arm_forearm) ---------------------------------------------------------
def pistol_to_camera(p, kick=0.0):
    r = math.radians
    q = (-p[0], p[1], p[2])                                   # FileToCamera
    q = rz(r(15))(q)                                          # PistolRollDegrees
    q = rx(r(-14 * kick))(q)                                  # RecoilLiftDegrees
    g = (0.095, -0.08 + 0.012 * kick, 0.18 - 0.035 * kick)
    return (q[0] + g[0], q[1] + g[1], q[2] + g[2])


def inverse_linear(weapon):
    """Linearer Teil der Haltung in Ruhe, rueckwaerts (Kameraraum -> Datei), fuer die Ruherichtung."""
    r = math.radians
    if weapon == "blade":
        f = rz(r(20))
    else:
        f = rz(r(-15))
    return lambda v: (-f(v)[0], f(v)[1], f(v)[2])             # erst zurueckdrehen, dann x spiegeln


def norm(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v)


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


SHOULDER = {"blade": (-0.40, -0.65, -0.20), "pistol": (0.40, -0.65, -0.20)}


def rest_axis(weapon, pivot_file):
    """Ruherichtung der Laengsachse des Unterarms in Dateikoordinaten: vom Drehpunkt zum Schulterpunkt, in Ruhe."""
    to_cam = blade_to_camera if weapon == "blade" else pistol_to_camera
    pc = to_cam(pivot_file, 0, 0) if weapon == "blade" else to_cam(pivot_file, 0)
    return norm(inverse_linear(weapon)(norm(sub(SHOULDER[weapon], pc))))


def forearm_points(weapon, pivot_file, length, phase):
    """Punkte der Unterarm-Achse (Kameraraum) in einer Phase nach der Regel: Achse zeigt vom Drehpunkt zum Schulterpunkt."""
    if weapon == "blade":
        pc = blade_to_camera(pivot_file, phase[0], phase[1])
    else:
        pc = pistol_to_camera(pivot_file, phase[0])
    d = norm(sub(SHOULDER[weapon], pc))
    return [(pc[0] + d[0] * length * t / 10, pc[1] + d[1] * length * t / 10, pc[2] + d[2] * length * t / 10) for t in range(11)]


def to_screen(c):
    x, y, z = c
    return (427 + 343 * x / z, 240 - 343 * y / z)


def check(weapon, pivot_file, length, phases):
    print(f"Regel Unterarm, {weapon}: Ruherichtung (Datei) = {tuple(round(v, 3) for v in rest_axis(weapon, pivot_file))}")
    for name, ph in phases:
        pts = forearm_points(weapon, pivot_file, length, ph)
        vis = [(round(to_screen(c)[0]), round(to_screen(c)[1])) for c in pts[1:] if in_view(c)]
        end = pts[-1]
        print(f"  {name:10s} sichtbar (Bildpunkte x,y): {vis[:4]}{' ...' if len(vis) > 4 else ''}  Ende im Bild: {in_view(end)}  "
              f"Ende bei z = {end[2]:.2f}")
