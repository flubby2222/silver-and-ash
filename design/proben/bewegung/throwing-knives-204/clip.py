"""Throwing Knives (Issue #204), Bewegungsprobe: Clips der Ich-Sicht als GIF.

Ablauf je Clip (Ticks, 60 je Sekunde): 30 Ticks Ruhe mit drei Messern, drei Wuerfe im Abstand von 60 Ticks
(3 -> 2 -> 1 -> 0 Messer), dann 120 Ticks Nachladen, 30 Ticks Ruhe. Zusammen 360 Ticks = 6 Sekunden.
Gezeigt wird jeder zweite Tick bei 30 Bildern je Sekunde (GIF kennt nur ganze Hundertstelsekunden je Bild; ffmpeg
verteilt 3, 3 und 4 Hundertstel so, dass die Dauer stimmt).

Das fliegende Messer liegt dort, wo es der Spielkern fuehrt: am Auge gestartet, 0,5 m je Tick geradeaus, Spitze nach
vorn, ohne Drehung. Gezeigt bis Tick 10 nach dem Loslassen (5 m, das Ende des Gangs der Probe).

Schritte (der Ordner nach "--" nimmt bilder/ und die GIFs auf):
  blender --background --python clip.py -- <Ordner> a      Bilder fuer Wurf A rendern
  blender --background --python clip.py -- <Ordner> b      Bilder fuer Wurf B rendern
  blender --background --python clip.py -- <Ordner> r      Bilder fuer Ruhe und Nachladen rendern
  blender --background --python clip.py -- <Ordner> gif    drei GIFs schreiben (braucht ffmpeg im Suchpfad)
"""

import os
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bewegung as M  # noqa: E402

LEAD, TAIL, STEP, FPS, FLIGHT = 30, 30, 2, 30, 10
TOTAL = LEAD + 3 * M.COOLDOWN_TICKS + M.RELOAD_TICKS + TAIL
THROWS = {"a": (M.throw_a, M.A_TICKS, "A Schnapp"), "b": (M.throw_b, M.B_TICKS, "B Durchzug")}


def at(kind, tick):
    """(Name des Bildes, Haltung, Messer in der Hand, Flug-Ticks oder None) fuer den Tick `tick` des Clips."""
    pose_at, length, _label = THROWS[kind]
    t = tick - LEAD
    if t < 0:
        return "ruhe-3", M.REST, M.magazine(3), None
    if t < 3 * M.COOLDOWN_TICKS:
        n, u = 3 - t // M.COOLDOWN_TICKS, t % M.COOLDOWN_TICKS
        flight = u if 0 < u <= FLIGHT else None
        if u > length and flight is None:
            return f"ruhe-{n - 1}", M.REST, M.magazine(n - 1), None
        return f"{kind}-{n}-{u:02d}", pose_at(u), M.held(n, u), flight
    t -= 3 * M.COOLDOWN_TICKS
    if t <= M.RELOAD_TICKS:
        p = t / M.RELOAD_TICKS
        if M.R_DOWN + 0.01 <= p <= M.R_UP - 0.01:
            return "leer", M.reload(0.3), (), None
        return f"r-{t:03d}", M.reload(p), M.magazine(M.reload_count(p)), None
    return "ruhe-3", M.REST, M.magazine(3), None


def render(out, part):
    import szene as Z
    s = Z.setup()
    todo = {}
    for kind in THROWS:
        for tick in range(0, TOTAL, STEP):
            name, pose, shown, flight = at(kind, tick)
            mine = name[0] == part if name[0] in "ab" else part == "r"
            if mine:
                todo[name] = (pose, shown, flight)
    os.makedirs(os.path.join(out, "bilder"), exist_ok=True)
    for name, (pose, shown, flight) in sorted(todo.items()):
        Z.pose(s, pose)
        objs = list(s.room) + s.sleeve + [o for i in shown for o in s.knives[i]] + s.blade
        if flight:
            p = Z.B.EYE + Z.V(0, 0, 0.5 * flight)
            Z.B.place(s.shot, Z.B.thrown((p.x, p.y, p.z)))
            objs += s.shot
        img = Z.np.array(Z.B.player(objs, s.cam))
        Z.S.save_png(img[::-1], os.path.join(out, "bilder", name + ".png"))
    print("CLIP gerendert", part, len(todo))


def gif(out):
    ffmpeg = shutil.which("ffmpeg")
    assert ffmpeg, "ffmpeg fehlt im Suchpfad"
    pal = "split[x][y];[x]palettegen=stats_mode=diff[p];[y][p]paletteuse=dither=none:diff_mode=rectangle"
    for kind in THROWS:
        seq = os.path.join(out, "folge-" + kind)
        shutil.rmtree(seq, ignore_errors=True)
        os.makedirs(seq)
        for i, tick in enumerate(range(0, TOTAL, STEP)):
            shutil.copyfile(os.path.join(out, "bilder", at(kind, tick)[0] + ".png"), os.path.join(seq, f"{i:04d}.png"))
    src = [os.path.join(out, "folge-" + k, "%04d.png") for k in "ab"]
    label = [THROWS[k][2] for k in "ab"]
    text = "drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':fontsize={}:fontcolor=white:x=10:y=8:text='{}'"
    jobs = [("wurf-a-schnapp.gif", [src[0]], f"[0:v]{text.format(22, label[0])},{pal}"),
            ("wurf-b-durchzug.gif", [src[1]], f"[0:v]{text.format(22, label[1])},{pal}"),
            ("wurf-a-und-b.gif", src, f"[0:v]scale=427:240:flags=neighbor,{text.format(14, label[0])}[l];"
                                      f"[1:v]scale=427:240:flags=neighbor,{text.format(14, label[1])}[r];[l][r]hstack,{pal}")]
    for name, inputs, graph in jobs:
        cmd = [ffmpeg, "-y", "-loglevel", "error"]
        for i in inputs:
            cmd += ["-framerate", str(FPS), "-i", i]
        cmd += ["-filter_complex", graph, "-loop", "0", os.path.join(out, name)]
        subprocess.run(cmd, check=True)
        print("CLIP", name, os.path.getsize(os.path.join(out, name)))
    for kind in THROWS:
        shutil.rmtree(os.path.join(out, "folge-" + kind))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    if args[1] == "gif":
        gif(args[0])
    else:
        render(args[0], args[1])
