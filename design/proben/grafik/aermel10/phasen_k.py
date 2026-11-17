"""Lokaler Spieltest der Regel fuer arm_forearm (NICHT committen): patcht WeaponPose.HandParts und WeaponViews, fotografiert
die Schlagphasen, die Szenen und stellt src danach aus Git wieder her. Aufruf (aus dem Repo-Wurzelverzeichnis):
    python design/proben/grafik/aermel10/phasen_k.py <modelle-Ordner relativ zu design/proben/grafik> <Ausgabeordner>"""
import glob
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import simulation as sim  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
models, out = sys.argv[1], sys.argv[2]
PIV = {"blade": (0.063, -0.036, -0.126), "pistol": (-0.063, -0.0828, -0.126)}
AX = {w: sim.rest_axis(w, PIV[w]) for w in PIV}


def vec(v):
    return "new(" + ", ".join(f"{c:.5f}f" for c in v) + ")"


def sh(cmd, **kw):
    return subprocess.run(cmd, shell=True, cwd=ROOT, **kw)


pose = os.path.join(ROOT, "src/SilverAndAsh.Core/Presentation/WeaponPose.cs")
views = os.path.join(ROOT, "src/SilverAndAsh.Game/Rendering/WeaponViews.cs")
s = open(pose, encoding="utf-8").read()
s = s.replace("public static IReadOnlyList<RigPart> HandParts { get; } = [new(Arm, null)];",
              'public static IReadOnlyList<RigPart> HandParts { get; } = [new("arm_cuff", null), new("arm_forearm", null)];')
open(pose, "w", encoding="utf-8").write(s)
v = open(views, encoding="utf-8").read()
v = v.replace("    // A model of one part (the sword, a hand) on the grip.", f'''    private readonly Matrix4x4[] _armTurns = new Matrix4x4[2];
    private readonly Matrix4x4[] _armFrames = new Matrix4x4[2];

    // LOKAL fuer die Probefotos: zweiteiliger Aermel. arm_forearm dreht um seinen Drehpunkt so, dass die Laengsachse
    // vom Drehpunkt zum Schulterpunkt zeigt (Schulterpunkt im Kameraraum, hier in die Datei zurueckgerechnet).
    private void DrawArm2(TexturedModel model, Matrix4x4 grip, Matrix4x4 weapon, Vector3 shoulder, Vector3 pivot, Vector3 rest)
    {{
        Matrix4x4.Invert(weapon, out var inverse);
        var d = Vector3.Normalize(Vector3.Transform(shoulder, inverse) - pivot);
        var axis = Vector3.Cross(rest, d);
        var turn = Matrix4x4.Identity;
        if (axis.LengthSquared() > 1e-10f)
        {{
            axis = Vector3.Normalize(axis);
            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(rest, d), -1f, 1f));
            var a = Matrix4x4.CreateFromAxisAngle(axis, angle);
            var b = Matrix4x4.CreateFromAxisAngle(axis, -angle);
            turn = Vector3.Dot(Vector3.TransformNormal(rest, a), d) > Vector3.Dot(Vector3.TransformNormal(rest, b), d) ? a : b;
        }}

        _armTurns[0] = Matrix4x4.Identity;
        _armTurns[1] = turn;
        model.Rig.Pose(_armTurns, grip, _armFrames);
        model.DrawPart(0, _armFrames[0]);
        model.DrawPart(1, _armFrames[1]);
    }}

    // A model of one part (the sword, a hand) on the grip.''', 1)
v = v.replace("DrawOnePart(bladeHand, grip);", f"DrawArm2(bladeHand, grip, WeaponPose.Blade(t, saWind, tremble), {vec(sim.SHOULDER['blade'])}, {vec(PIV['blade'])}, {vec(AX['blade'])});", 1)
v = v.replace("DrawOnePart(hand, grip);", f"DrawArm2(hand, grip, WeaponPose.Pistol(kick), {vec(sim.SHOULDER['pistol'])}, {vec(PIV['pistol'])}, {vec(AX['pistol'])});", 1)
v = v.replace("var t = _swing < 0.4f ? EaseOut(_swing / 0.4f) : 1f - EaseInOut((_swing - 0.4f) / 0.6f);",
              "var t = _swing < 0.4f ? EaseOut(_swing / 0.4f) : 1f - EaseInOut((_swing - 0.4f) / 0.6f);\n        var saW = Environment.GetEnvironmentVariable(\"SA_WIND\"); var saS = Environment.GetEnvironmentVariable(\"SA_SWING\");\n        if (saS is not null) { t = float.Parse(saS, System.Globalization.CultureInfo.InvariantCulture); }\n        var saWind = saW is not null ? float.Parse(saW, System.Globalization.CultureInfo.InvariantCulture) : _windUp;", 1)
v = v.replace("var grip = WeaponPose.Blade(t, _windUp, tremble) * modelMotion;", "var grip = WeaponPose.Blade(t, saWind, tremble) * modelMotion;", 1)
open(views, "w", encoding="utf-8").write(v)
try:
    mdir = os.path.join(ROOT, "design", "proben", "grafik", models)
    for f in os.listdir(mdir):
        shutil.copy(os.path.join(mdir, f), os.path.join(ROOT, "assets", "models", f))
    subprocess.run([sys.executable, "tools/models/checksums.py"], cwd=ROOT, stdout=subprocess.DEVNULL)
    outdir = os.path.join(ROOT, "design", "proben", "grafik", out)
    os.makedirs(outdir, exist_ok=True)
    for name, wind, swing in (("w0", 0, 0), ("w05", 0.5, 0), ("w1", 1, 0), ("s025", 0, 0.25), ("s05", 0, 0.5), ("s075", 0, 0.75), ("s1", 0, 1)):
        env = dict(os.environ, SA_WIND=str(wind), SA_SWING=str(swing))
        subprocess.run(["dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", "room-corridor", "--out", os.path.join(outdir, "tmp")],
                       cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        shutil.move(os.path.join(outdir, "tmp", "room-corridor.png"), os.path.join(outdir, f"blade_{name}.png"))
    for scene in ("room-corridor", "pistol-recoil", "blade-charged"):
        subprocess.run(["dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", scene, "--out", outdir],
                       cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    shutil.rmtree(os.path.join(outdir, "tmp"), ignore_errors=True)
    for f in glob.glob(os.path.join(outdir, "*-screen.png")):
        os.remove(f)
finally:
    sh("git checkout assets/models src")
    for f in ("blade_hand.png", "pistol_hand.png"):
        p = os.path.join(ROOT, "assets", "models", f)
        os.path.exists(p) and os.remove(p)
