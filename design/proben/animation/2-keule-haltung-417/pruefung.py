"""Rechnet die Keulenhaltungen (Probe #417) nach: Reichweite und Nahebene.

Spiegelt WeaponPose.ClubProbePose (siehe probe-haltung.patch): FileToCamera * RotZ(-roll) * RotX(-pitch) *
RotY(yaw) * Translate(x, y, z), Zeilenvektoren wie System.Numerics. Prueft die Eckpunkte der Huellbox der Keule
(club.glb: x,y +-0,0417, z -0,136 bis 0,2745) auf den Abstand zu den Augen nach dem Verkleinern um die Haelfte (Reach 0,30) fuer Punkte im Bild und
auf die Nahebene (0,05). Eine Naeherung, nur Huellbox: den genauen Test macht WeaponPoseTests in Teil 2.
Aufruf: python pruefung.py
"""
import itertools
import math

HALF_H = math.tan(math.radians(35))
HALF_W = HALF_H * 16 / 9
REACH, NEAR = 0.30, 0.05
SHRINK, MARGIN = 0.5, 0.02  # ShrinkTowardsEye; EdgeMargin der Tests
BOX = [(-0.0417, 0.0417), (-0.0417, 0.0417), (-0.136, 0.2745)]


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, s, 0], [-s, c, 0], [0, 0, 1]]


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return [[1, 0, 0], [0, c, s], [0, -s, c]]


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, 0, -s], [0, 1, 0], [s, 0, c]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def vm(v, m):
    return [sum(v[k] * m[k][j] for k in range(3)) for j in range(3)]


# pitch, yaw, roll (Grad), x, y, z (m)
REST_A = (35, -12, 0, -0.12, -0.11, 0.20)
REST_B = (55, -20, 0, -0.13, -0.12, 0.21)
REST_C = (70, -25, 0, -0.13, -0.12, 0.21)
HIT = (0, 45, 0, -0.02, -0.09, 0.24)
WIND = (65, -50, 10, -0.18, -0.17, 0.20)


def pose(rest, hit, wind, t, w):
    q = [rest[i] + (hit[i] - rest[i]) * t + (wind[i] - rest[i]) * w for i in range(6)]
    mirror = [[-1, 0, 0], [0, 1, 0], [0, 0, 1]]
    m = mul(mul(mul(mirror, rz(math.radians(-q[2]))), rx(math.radians(-q[0]))), ry(math.radians(q[1])))
    return m, q[3:6]


def check(rest, hit, wind, t, w):
    m, pos = pose(rest, hit, wind, t, w)
    worst, nearest = 0.0, 9.0
    for corner in itertools.product(*BOX):
        p = vm(corner, m)
        p = [(p[0] + pos[0]) * SHRINK, (p[1] + pos[1]) * SHRINK, (p[2] + pos[2]) * SHRINK]
        inside = p[2] > NEAR and abs(p[0]) <= p[2] * HALF_W + MARGIN and abs(p[1]) <= p[2] * HALF_H + MARGIN
        if inside:
            worst = max(worst, math.sqrt(sum(c * c for c in p)))
        nearest = min(nearest, p[2])
    return worst, nearest


if __name__ == "__main__":
    for name, rest in (("A", REST_A), ("B", REST_B), ("C", REST_C)):
        print(name, "Ruhe: weitester Punkt im Bild %.3f m, nahester z %.3f m" % check(rest, HIT, WIND, 0, 0))
    steps = [i / 20 for i in range(0, 25)]
    print("B Schlag (t 0..1,2):  weitester Punkt %.3f bei t=%.2f" % max((check(REST_B, HIT, WIND, t, 0)[0], t) for t in steps))
    print("B Ausholen (w 0..1):  weitester Punkt %.3f bei w=%.2f" % max((check(REST_B, HIT, WIND, 0, w)[0], w) for w in steps if w <= 1))
    print("B Aufgeladener Schlag (w 1->0, t 0->1): weitester Punkt %.3f bei t=%.2f"
          % max((check(REST_B, HIT, WIND, t, 1 - t)[0], t) for t in steps if t <= 1))
    print("Grenze Reach = %.2f m (nach dem Verkleinern, wie WeaponPoseTests)" % REACH)
