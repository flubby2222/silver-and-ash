#:project ../../src/SilverAndAsh.Core/SilverAndAsh.Core.csproj

// Hoerprobe fuer den Richtungsklang (Issue #491): legt einen Geraeusch-Clip des Spiels mehrmals hintereinander als
// Stereo-WAV ab - mit derselben Rechnung wie im Spiel (Hearing.Mix, Werte aus Balance.Default). Der Hoerer steht im
// Ursprung und blickt entlang +Z (Yaw 0, rechts ist dort -X). Die Quelle wandert von links nach rechts und
// entfernt sich danach rechts bis hinter die Hoerweite.
//
// Aufruf (vom Repository aus):
//   dotnet run tools/audio/richtungsklang-probe.cs
//   dotnet run tools/audio/richtungsklang-probe.cs -- assets/audio/p1-ghul-ankuendigung.wav artifacts/hoerprobe/richtungsklang.wav
//
// Panning wie Raylib: die Messung zu SetSoundPan (PR zu #491) ergab pan -1 = nur links, +1 = nur rechts, dazwischen
// einen Verlauf nahe 0.5*x*(3-x^2) mit x = (pan+1)/2 fuer rechts und x = (1-pan)/2 fuer links. Der Pegel ist
// SoundBoard.SoundVolume * Mix.Volume wie in SoundCues.

using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Audio;

var source = args.Length > 0 ? args[0] : "assets/audio/p1-ghul-ankuendigung.wav";
var target = args.Length > 1 ? args[1] : "artifacts/hoerprobe/richtungsklang.wav";

var (rate, mono) = ReadMono(source);
var hearing = Balance.Default.Hearing;
var ears = Vector3.Zero;
var facing = Vector3.UnitZ;

// Links (+X), halb links vorn, vorn, halb rechts vorn, rechts (-X), dann rechts immer weiter weg.
Vector3[] places =
[
    new(6f, 0f, 0f), new(4f, 0f, 4f), new(0f, 0f, 6f), new(-4f, 0f, 4f), new(-6f, 0f, 0f),
    new(-12f, 0f, 0f), new(-20f, 0f, 0f), new(-28f, 0f, 0f), new(-35f, 0f, 0f),
];

var gap = rate / 4;
var left = new List<float>();
var right = new List<float>();
foreach (var place in places)
{
    var mix = Hearing.Mix(ears, facing, place, hearing);
    var gain = SoundBoard.SoundVolume * mix.Volume;
    var toRight = PanLaw((mix.Pan + 1f) / 2f);
    var toLeft = PanLaw((1f - mix.Pan) / 2f);
    Console.WriteLine($"{place,-24} pan {mix.Pan,6:0.00}  volume {mix.Volume,5:0.00}");
    foreach (var sample in mono)
    {
        left.Add(sample * gain * toLeft);
        right.Add(sample * gain * toRight);
    }

    for (var i = 0; i < gap; i++)
    {
        left.Add(0f);
        right.Add(0f);
    }
}

WriteStereo(target, rate, left, right);
Console.WriteLine($"Geschrieben: {target}");

static float PanLaw(float x) => 0.5f * x * (3f - x * x);

static (int Rate, float[] Samples) ReadMono(string path)
{
    using var reader = new BinaryReader(File.OpenRead(path));
    reader.ReadBytes(12); // RIFF, Groesse, WAVE
    int rate = 0, channels = 0, bits = 0;
    while (reader.BaseStream.Position < reader.BaseStream.Length)
    {
        var id = new string(reader.ReadChars(4));
        var size = reader.ReadInt32();
        if (id == "fmt ")
        {
            var format = reader.ReadInt16();
            channels = reader.ReadInt16();
            rate = reader.ReadInt32();
            reader.ReadBytes(6);
            bits = reader.ReadInt16();
            reader.ReadBytes(size - 16);
            if (format != 1 || bits != 16)
            {
                throw new InvalidDataException($"{path}: nur 16-Bit-PCM wird gelesen.");
            }
        }
        else if (id == "data")
        {
            var frames = size / (2 * channels);
            var samples = new float[frames];
            for (var f = 0; f < frames; f++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    sum += reader.ReadInt16() / 32768f;
                }

                samples[f] = sum / channels;
            }

            return (rate, samples);
        }
        else
        {
            reader.ReadBytes(size + (size & 1));
        }
    }

    throw new InvalidDataException($"{path}: kein data-Block.");
}

static void WriteStereo(string path, int rate, List<float> left, List<float> right)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var writer = new BinaryWriter(File.Create(path));
    var dataSize = left.Count * 4;
    writer.Write("RIFF"u8);
    writer.Write(36 + dataSize);
    writer.Write("WAVEfmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)2);
    writer.Write(rate);
    writer.Write(rate * 4);
    writer.Write((short)4);
    writer.Write((short)16);
    writer.Write("data"u8);
    writer.Write(dataSize);
    for (var i = 0; i < left.Count; i++)
    {
        writer.Write(ToShort(left[i]));
        writer.Write(ToShort(right[i]));
    }
}

static short ToShort(float value) => (short)Math.Clamp(MathF.Round(value * 32767f), short.MinValue, short.MaxValue);
