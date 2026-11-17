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
