"""Lokale Hilfe fuer das Probestueck Gorms Schlag (kein Spielcode, wird nach dem Filmen aus Git wiederhergestellt).

Schreibt:
  * MotionScenes: zwei Bewegungs-Szenen "gorm-slam" (Spieler 8 m von Gorm) und "gorm-slam-far" (14 m, der ganze Ring im Bild): Gorm im
    6-m-Saal, die Szene laeuft bis kurz vor dem Hammerschlag (Warnkreis, 85 % des Anlaufs vor) und filmt dann 120 Ticks (2 s): Schlag und Welle.
  * GormFloorEffects.Draw: ist SA_WAVE gesetzt (welle_a oder welle_b, Dateiname ohne .glb in assets/models), wird die Welle aus den Bausteinen
    dieses Modells zu einem Ring gesetzt (gleiche Stuecke wie `Ring`: pieces = max(12, radius * 10)); sonst bleibt der Ring aus Kaesten.
  * SurfaceVariants.At: erzwingt die Wandvariante SA_FORCE_WALL (a, b, c).
Aufruf: python patch_lokal8.py apply   (zuruecksetzen: git checkout src)
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
MOTION = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Photos", "MotionScenes.cs")
VARIANTS = os.path.join(ROOT, "src", "SilverAndAsh.Core", "Presentation", "SurfaceVariants.cs")
VIEW = os.path.join(ROOT, "src", "SilverAndAsh.Game", "Rendering", "GormView.cs")

SCENE_ENTRIES = '''        SlamScene("gorm-slam", 8),
        SlamScene("gorm-slam-far", 14),
'''

SCENE_METHOD = r'''
    // Local probe: Gorm's hammer blow, filmed from the last moments of the wind-up (warning circle) through the whole wave.
    private static MotionScene SlamScene(string name, int metres) =>
        new(name, "Gorm's hammer blow and the shock wave on the floor (local probe).",
            studio =>
            {
                var world = studio.Arena('T', metres, RoomTemplate.RoomCeilingHeight);
                for (var tick = 0; tick < 3000 && !(world.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer, StateProgress: >= 0.85f }); tick++)
                {
                    world.Tick(PlayerInput.None);
                }

                // Gorm walks up to the player while waiting: put the player back `metres` away from him, looking at him.
                var gorm = world.Gorm!;
                var behind = gorm.Position.Z - metres >= 2f;
                var spot = new System.Numerics.Vector3(gorm.Position.X, 0f, behind ? gorm.Position.Z - metres : gorm.Position.Z + metres);
                world.Player.PlaceAt(spot);
                PhotoStudio.Face(world.Player, gorm.Position + new System.Numerics.Vector3(0f, 1.5f, 0f));
                return new PhotoSetup(world) { Ticks = 120 };
            });
'''

VIEW_CODE = r'''
    private static TexturedModel? _waveModel;
    private static bool _waveTried;

    private static bool DrawWaveProbe(Vector3 origin, float radius)
    {
        var name = Environment.GetEnvironmentVariable("SA_WAVE");
        if (name is null)
        {
            return false;
        }

        var isA = name == "welle_a";
        if (!_waveTried)
        {
            _waveTried = true;
            var parts = isA
                ? new[] { new SilverAndAsh.Core.Presentation.RigPart("shard", null), new SilverAndAsh.Core.Presentation.RigPart("puff", null) }
                : new[] { new SilverAndAsh.Core.Presentation.RigPart("band", null) };
            _waveModel = TexturedModel.TryLoad(Path.Combine(AppContext.BaseDirectory, "assets", "models", name + ".glb"), parts, null);
        }

        if (_waveModel is null)
        {
            return false;
        }

        var count = isA ? 2 : 1;
        var turns = new Matrix4x4[count];
        Array.Fill(turns, Matrix4x4.Identity);
        var frames = new Matrix4x4[count];
        var pieces = Math.Max(12, (int)(radius * 10f));
        for (var piece = 0; piece < pieces; piece++)
        {
            var angle = piece * 2f * MathF.PI / pieces;
            var arc = 2f * MathF.PI * radius / pieces;
            var hash = ((piece * 7919) % 13) / 13f;
            var spin = Matrix4x4.CreateRotationY(-angle);
            if (!isA)
            {
                var place = Matrix4x4.CreateScale(1f, 1f, arc) * spin
                    * Matrix4x4.CreateTranslation(origin + new Vector3(MathF.Cos(angle) * radius, 0.02f, MathF.Sin(angle) * radius));
                _waveModel.Rig.Pose(turns, place, frames);
                _waveModel.DrawPart(0, frames[0]);
                continue;
            }

            foreach (var (back, height, part) in new[] { (0f, 0.75f + (0.5f * hash), 0), (-0.55f, 0.9f, 1), (-1.1f, 0.6f, 1) })
            {
                var r = radius + back;
                if (r < 0.3f)
                {
                    continue;
                }

                var place = Matrix4x4.CreateScale(1f, height, part == 0 ? arc / 0.6f : 1f) * spin
                    * Matrix4x4.CreateTranslation(origin + new Vector3(MathF.Cos(angle) * r, 0.02f, MathF.Sin(angle) * r));
                _waveModel.Rig.Pose(turns, place, frames);
                _waveModel.DrawPart(part, frames[part]);
            }
        }

        return true;
    }

    // A flat ring of small boxes on the floor.'''


def main():
    if sys.argv[1:] != ["apply"]:
        print(__doc__)
        return
    m = open(MOTION, encoding="utf-8").read()
    if "SlamScene(" not in m:
        m = m.replace('        DoorOpening("door-normal-opening"', SCENE_ENTRIES + '        DoorOpening("door-normal-opening"', 1)
        m = m.replace("    // The door of `kind` opened by the player", SCENE_METHOD + "\n    // The door of `kind` opened by the player", 1)
        open(MOTION, "w", encoding="utf-8").write(m)
    v = open(VARIANTS, encoding="utf-8").read()
    if "SA_FORCE_WALL" not in v:
        v = v.replace("    public TextureVariant At(int x, int z, SurfaceKind kind) =>\n",
                      "    public TextureVariant At(int x, int z, SurfaceKind kind) =>\n"
                      "        kind == SurfaceKind.Wall && Environment.GetEnvironmentVariable(\"SA_FORCE_WALL\") is { } forced\n"
                      "            ? Enum.Parse<TextureVariant>(forced, true) :\n", 1)
        open(VARIANTS, "w", encoding="utf-8").write(v)
    w = open(VIEW, encoding="utf-8").read()
    if "DrawWaveProbe" not in w:
        w = w.replace("    // A flat ring of small boxes on the floor.", VIEW_CODE, 1)
        w = w.replace("            Ring(wave.Origin, MathF.Max(0.3f, wave.Radius), Wave, 0.35f);",
                      "            if (!DrawWaveProbe(wave.Origin, MathF.Max(0.3f, wave.Radius)))\n            {\n"
                      "                Ring(wave.Origin, MathF.Max(0.3f, wave.Radius), Wave, 0.35f);\n            }", 1)
        open(VIEW, "w", encoding="utf-8").write(w)
    print("ok")


main()
