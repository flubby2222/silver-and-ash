"""Throwing Knives (Issue #204): Bewegung in der Ich-Sicht als reine Rechnung (ohne Blender).

Zwei Entwuerfe fuer den Wurf und ein Nachladen. Jede Funktion gibt eine Haltung zurueck, die sich wie WeaponPose im
Spiel liest: erst um den Haltepunkt des Aermels drehen (Manschette und Messer gemeinsam), dann verschieben; der
Unterarm zeigt weiter zur Schulter (szene.py rechnet das wie WeaponPose.ForearmTurn).

Achsen wie in blatt.py: x nach links im Bild (zur Bildmitte, die Hand steht rechts), y oben, z vorn; Meter im Mass der
Probe (vor ShrinkTowardsEye). Winkel in Grad:
  pitch  hebt die Spitzen (negativ: Spitzen kippen nach vorn-unten),
  yaw    dreht die Spitzen zur Bildmitte,
  roll   kippt den Faecher im Bild zur Bildmitte,
  fan    Faecherwinkel der aeusseren Messer (Ruhe 17).

Zeiten aus assets/balance.json (throwingKnives): cooldown 1 s = 60 Ticks zwischen zwei Wuerfen, reloadTime 2 s = 120
Ticks. Das Messer entsteht im Tick des Feuerns am Auge (GameWorld.Throw): Tick 0 ist das Loslassen, davor gibt es
keine Zeit zum Ausholen. Das Nachladen laeuft ueber den Fortschritt 0..1 (Magazine.ReloadProgress), weil Gegenstaende
es verkuerzen koennen.
"""

import math

COOLDOWN_TICKS = 60
RELOAD_TICKS = 120
FAN_REST = 17.0
FAN_CLOSED = 15.0          # 14 Grad ist der kleinste Faecher ohne Ueberdeckung in Ruhe; 1 Grad Luft fuer die gekippten Haltungen
ORDER = (2, 0, 1)          # Reihenfolge der Wuerfe: links (zur Bildmitte), rechts, zuletzt das gerade Messer in der Mitte
REST = {"shift": (0.0, 0.0, 0.0), "pitch": 0.0, "yaw": 0.0, "roll": 0.0, "fan": FAN_REST}


def clamp(u):
    return min(1.0, max(0.0, u))


def smooth(u):
    u = clamp(u)
    return u * u * (3 - 2 * u)


def ease_out(u):
    return 1 - (1 - clamp(u)) ** 3


def ease_in(u):
    return clamp(u) ** 3


def held(count_before, tick):
    """Welche Messer (0 rechts, 1 Mitte, 2 links) stecken im Tick `tick` eines Wurfs in der Hand, wenn davor
    `count_before` Messer da waren: das geworfene fehlt ab Tick 0."""
    return sorted(ORDER[3 - count_before + 1:])


def magazine(count):
    """Messer in der Hand bei `count` Messern im Magazin."""
    return sorted(ORDER[3 - count:])


# --- Wurf A: Schnapp ---------------------------------------------------------------------------------------------------

A_TICKS = 18
A_PEAK = {"shift": (0.012, -0.012, 0.040), "pitch": -24.0, "yaw": 6.0, "roll": 0.0}


def a_amount(tick):
    """Tick 0 bis 2 schnappt die Hand nach vorn (sofort, wie der Rueckstoss der Pistole), haelt bis Tick 4, kommt bis
    Tick 14 abgebremst zurueck und schwingt bis Tick 18 einmal leicht nach hinten aus."""
    if tick <= 2:
        return (0.45, 0.85, 1.0)[max(0, int(tick))]
    if tick <= 4:
        return 1.0
    if tick <= 14:
        return 1 - smooth((tick - 4) / 10)
    if tick < A_TICKS:
        return -0.12 * math.sin(math.pi * (tick - 14) / 4)
    return 0.0


def throw_a(tick):
    a = a_amount(tick)
    return {"shift": tuple(v * a for v in A_PEAK["shift"]), "pitch": A_PEAK["pitch"] * a, "yaw": A_PEAK["yaw"] * a,
            "roll": 0.0, "fan": FAN_REST}


# --- Wurf B: Durchzug --------------------------------------------------------------------------------------------------

B_TICKS = 40
B_PEAK = {"shift": (0.085, -0.050, 0.060), "pitch": -38.0, "yaw": 8.0, "roll": 24.0}


def b_amount(tick):
    """Der Arm zieht durch: Tick 0 bis 6 schnell nach vorn-unten-innen, bis Tick 10 laeuft er aus, Tick 10 bis 34 kommt
    er weich zurueck, bis Tick 40 schwingt er aus."""
    if tick <= 6:
        return 0.30 + 0.70 * ease_out(tick / 6)
    if tick <= 10:
        return 1.0 + 0.08 * smooth((tick - 6) / 4)
    if tick <= 34:
        return 1.08 * (1 - smooth((tick - 10) / 24))
    if tick < B_TICKS:
        return -0.06 * math.sin(math.pi * (tick - 34) / 6)
    return 0.0


def throw_b(tick):
    s = b_amount(tick)
    back = tick > 10 and s > 0                  # Rueckweg auf einem Bogen: erst hoch, dann nach aussen
    sx = s ** 0.6 if back else s
    sy = s ** 1.5 if back else s
    x, y, z = B_PEAK["shift"]
    return {"shift": (x * sx, y * sy, z * s), "pitch": B_PEAK["pitch"] * s, "yaw": B_PEAK["yaw"] * s, "roll": B_PEAK["roll"] * sx,
            "fan": FAN_REST}


# --- Nachladen ---------------------------------------------------------------------------------------------------------

R_AWAY = {"shift": (-0.060, -0.240, -0.030), "pitch": -35.0, "roll": -15.0}
R_DOWN, R_SWAP, R_UP, R_FAN, R_OPEN, R_SET = 0.12, 0.30, 0.50, 0.72, 0.88, 0.90


def reload_count(progress):
    """Die drei Handmodelle erscheinen, waehrend die Manschette unter dem Bildrand ist."""
    return 3 if progress >= R_SWAP else 0


def reload(progress):
    """0 bis 0,12 taucht die leere Manschette nach rechts unten weg (beschleunigt); bis 0,50 bleibt sie unter dem
    Bildrand (bei 0,30 erscheinen die Messer); 0,50 bis 0,72 kommt sie abgebremst hoch und schwingt leicht ueber;
    0,72 bis 0,88 oeffnet sich der Faecher; 0,90 bis 1,00 setzt ein kleiner Ruck die Messer fest."""
    p = clamp(progress)
    if p < R_DOWN:
        d = ease_in(p / R_DOWN)
    elif p < R_UP:
        d = 1.0
    else:
        d = 1 - ease_out((p - R_UP) / (R_FAN - R_UP))
    over = 0.012 * math.sin(math.pi * clamp((p - 0.62) / 0.20))
    u = clamp((p - R_FAN) / (R_OPEN - R_FAN))
    spread = 1 - (1 - u) ** 3 + 0.9 * u * (1 - u) ** 2 if p >= R_FAN else 0.0   # oeffnet rasch, leicht ueber 17 Grad
    v = clamp((p - R_SET) / (1 - R_SET))
    tug = 3.0 * math.sin(math.pi * v) * (1 - v)
    x, y, z = R_AWAY["shift"]
    return {"shift": (x * d, y * d + over, z * d), "pitch": R_AWAY["pitch"] * d + tug, "yaw": 0.0,
            "roll": R_AWAY["roll"] * d, "fan": FAN_CLOSED + (FAN_REST - FAN_CLOSED) * spread}
