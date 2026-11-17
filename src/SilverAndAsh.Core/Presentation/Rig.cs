using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One movable part of a model: its name in the model file, the part it hangs on (null: the figure itself).</summary>
public sealed record RigPart(string Name, string? Parent);

/// <summary>
/// The "skeleton" of a model made of rigid parts (no skinning): every part turns around its own pivot and takes its
/// children with it - the forearm follows the upper arm, the jaw the head. The pivots come from the model file, the
/// order of the parts and who hangs on whom from code.
/// <para>
/// Matrices follow System.Numerics: a point is a row vector, so <c>a * b</c> means "first a, then b".
/// For each part: <c>frame = turn * move(pivot - pivot of the parent) * frame of the parent</c>, and for a part without a
/// parent the frame of the parent is the position of the whole figure in the world. A corner of the part, given
/// relative to its pivot, lands in the world at <c>corner * frame</c>.
/// </para>
/// </summary>
public sealed class Rig
{
    private readonly int[] _parents;
    private readonly Vector3[] _offsets;

    /// <summary>Builds the rig. Every parent must come before its children.</summary>
    /// <exception cref="ArgumentException">A name is twice there, or a parent is missing or comes too late.</exception>
    public Rig(IReadOnlyList<RigPart> parts, IReadOnlyList<Vector3> pivots)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(pivots);
        if (parts.Count != pivots.Count)
        {
            throw new ArgumentException("Every part needs exactly one pivot.", nameof(pivots));
        }

        var names = new List<string>(parts.Count);
        _parents = new int[parts.Count];
        _offsets = new Vector3[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (names.Contains(part.Name))
            {
                throw new ArgumentException($"The part '{part.Name}' is there twice.", nameof(parts));
            }

            var parent = part.Parent is null ? -1 : names.IndexOf(part.Parent);
            if (part.Parent is not null && parent < 0)
            {
                throw new ArgumentException($"The parent '{part.Parent}' of '{part.Name}' must come before it.", nameof(parts));
            }

            names.Add(part.Name);
            _parents[i] = parent;
            _offsets[i] = parent < 0 ? pivots[i] : pivots[i] - pivots[parent];
        }

        Names = names;
        Pivots = [.. pivots];
    }

    /// <summary>The part names, in the order of the rig.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The pivots in the figure's own space (x side, y up, z forward, metres), in the order of the rig.</summary>
    public IReadOnlyList<Vector3> Pivots { get; }

    /// <summary>Number of parts.</summary>
    public int Count => _parents.Length;

    /// <summary>
    /// "Lift" a part by <paramref name="degrees"/>: a turn around the x axis that brings its front end up - a part
    /// hanging down first swings forward, then up. With y up and z forward that is a turn around -x.
    /// </summary>
    public static Matrix4x4 Lift(float degrees) => Matrix4x4.CreateRotationX(-degrees * MathF.PI / 180f);

    /// <summary>
    /// Works out where each part is in the world.
    /// </summary>
    /// <param name="turns">The turn of each part around its pivot (identity: as in the file), in the order of the rig.</param>
    /// <param name="figure">The whole figure: its own space -> world.</param>
    /// <param name="frames">Gets, per part, corner relative to the pivot -> world.</param>
    public void Pose(ReadOnlySpan<Matrix4x4> turns, Matrix4x4 figure, Span<Matrix4x4> frames)
    {
        if (turns.Length != Count || frames.Length != Count)
        {
            throw new ArgumentException($"The rig has {Count} parts.");
        }

        for (var i = 0; i < Count; i++)
        {
            var parent = _parents[i] < 0 ? figure : frames[_parents[i]];
            frames[i] = turns[i] * Matrix4x4.CreateTranslation(_offsets[i]) * parent;
        }
    }
}
