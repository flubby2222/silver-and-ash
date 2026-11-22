using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A door between two rooms. It fills one cell of the level: while closed or barred, the world treats that cell as
/// wall (so it blocks walking, sight, shots, blood bolts and the enemies' way-finding). Once open it stays open -
/// only an arena's grate can block it for a while. A locked door needs a key (<see cref="Player.Keys"/>) to open.
/// </summary>
public sealed class Door : IInteractable
{
    private readonly int _openingTicks;
    private readonly GratesBalance _grates;
    private int _openedTicks;

    // Barred: the arena wants this door shut. Without a grate it blocks at once; with one only while the grate hangs low.
    private bool _barred;

    // The grate's own clock (issue #558): the motion it is in and the ticks since that began - never the world's clock.
    // At the start it has risen long ago: fully open.
    private GrateMotion _motion = GrateMotion.Lift;
    private int _grateTicks;

    internal Door(int id, LevelDoor plan, Level level, DoorsBalance? balance = null, GratesBalance? grates = null)
    {
        var cell = level[plan.X, plan.Z];
        if (cell.IsSolid)
        {
            throw new ArgumentException($"Door {id} at cell {plan.X}/{plan.Z} must lie on a walkable cell of the level.", nameof(plan));
        }

        Id = id;
        X = plan.X;
        Z = plan.Z;
        Kind = plan.Kind;
        Side = plan.Side;
        RoomA = plan.RoomA;
        RoomB = plan.RoomB;
        BoxMin = new Vector3(plan.X * Level.CellSize, cell.FloorHeight, plan.Z * Level.CellSize);
        BoxMax = new Vector3((plan.X + 1) * Level.CellSize, cell.CeilingHeight, (plan.Z + 1) * Level.CellSize);
        _openingTicks = SimulationClock.SecondsToTicks((balance ?? new DoorsBalance()).OpenTime(plan.Kind));
        HasGrate = plan.HasGrate;
        _grates = grates ?? new GratesBalance();
        _grateTicks = _grates.LiftTicks;
    }

    /// <summary>True if a portcullis hangs in this door (<see cref="LevelDoor.HasGrate"/>).</summary>
    public bool HasGrate { get; }

    /// <summary>
    /// The height of the grate's spike tips above the floor in metres (issue #558), from <see cref="GratePose.Height"/>:
    /// <see cref="GratePose.OpenHeight"/> while it is up. The picture and the passage use this one number. A door
    /// without a grate is "up" unless barred, then "down".
    /// </summary>
    public float GrateHeight => HasGrate
        ? GratePose.Height(_grateTicks, _motion, _grates)
        : _barred ? 0f : GratePose.OpenHeight;

    /// <summary>True while the grate is on its way down or down: barred, even if it does not block yet.</summary>
    public bool IsSealing => _barred;

    /// <summary>Number of the door in its world (position in <see cref="GameWorld.Doors"/>).</summary>
    public int Id { get; }

    /// <summary>Column of the door's cell.</summary>
    public int X { get; }

    /// <summary>Row of the door's cell.</summary>
    public int Z { get; }

    public DoorKind Kind { get; }

    /// <summary>The room on one side (number in <see cref="GameWorld.Rooms"/>).</summary>
    public int RoomA { get; }

    /// <summary>The room on the other side.</summary>
    public int RoomB { get; }

    /// <summary>Which way the passage runs: North/South = along Z, East/West = along X.</summary>
    public RoomSide Side { get; }

    public bool IsOpen { get; private set; }

    /// <summary>
    /// How far the leaves have swung (issue #245): 0 = closed, 1 = fully open. It grows by one tick at a time once the
    /// door is open, over the time of its kind (<see cref="DoorsBalance"/>); a broken cracked wall is at 1 at once.
    /// Only the picture follows it - the way through is free from the tick the door opens (<see cref="Blocks"/>).
    /// </summary>
    public float OpenFraction => !IsOpen ? 0f : _openingTicks == 0 ? 1f : (float)_openedTicks / _openingTicks;

    /// <summary>
    /// True if the player who opened the door stood on the lower side of it along the passage (smaller Z for a door
    /// whose passage runs North/South, smaller X for East/West): the leaves swing away from them, towards the higher
    /// side. False before the door is opened, and if it was opened from the higher side.
    /// </summary>
    public bool OpenedFromLowSide { get; private set; }

    /// <summary>
    /// True while the grate of an arena is down: the door blocks, open or not, and F does nothing. A door with a grate
    /// (issue #558) blocks only while its spike tips hang below <see cref="GratesBalance.BlockHeight"/> - from part way
    /// down the fall until part way up the lift.
    /// </summary>
    public bool IsBarred => HasGrate ? GrateHeight < _grates.BlockHeight : _barred;

    /// <summary>True if nothing can pass: closed, or barred.</summary>
    public bool Blocks => !IsOpen || IsBarred;

    /// <summary>
    /// Every closed door that is not barred is offered - a locked one too, to say that it needs a key. A cracked wall
    /// never is: it is a secret, and F does not open it. Nor is a door whose grate is falling: it will be shut anyway.
    /// </summary>
    public bool CanInteract => !IsOpen && !IsBarred && !_barred && Kind != DoorKind.Cracked;

    /// <summary>Wooden and boss doors open with F. A locked door opens with F only for a player with a key, and uses it up.</summary>
    public InteractionOffer OfferFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Kind != DoorKind.Locked)
        {
            return new("Open", true);
        }

        return player.Keys > 0 ? new("Unlock (1 key)", true) : new("Locked - needs a key", false);
    }

    public Vector3 BoxMin { get; }

    public Vector3 BoxMax { get; }

    /// <summary>Opens the door for a player standing at <paramref name="from"/>: the leaves will swing away from there.</summary>
    internal void Open(Vector3 from)
    {
        IsOpen = true;
        var middle = (BoxMin + BoxMax) / 2f;
        OpenedFromLowSide = Side is RoomSide.North or RoomSide.South ? from.Z < middle.Z : from.X < middle.X;
    }

    /// <summary>
    /// A cracked wall breaks open (see <see cref="DoorKind.Cracked"/>) - nothing swings, it is gone at once: its opening
    /// time is 0, so <see cref="OpenFraction"/> is 1 straight away.
    /// </summary>
    internal void Break() => IsOpen = true;

    /// <summary>
    /// One tick of swinging open (nothing while closed or once fully open) and of the grate's motion (nothing once it
    /// is down or up). True if <see cref="Blocks"/> changed with it - the grate crossed <see cref="GratesBalance.BlockHeight"/>.
    /// </summary>
    internal bool Tick()
    {
        var blocked = Blocks;
        if (IsOpen && _openedTicks < _openingTicks)
        {
            _openedTicks++;
        }

        if (HasGrate && _grateTicks < GratePose.MotionTicks(_motion, _grates))
        {
            _grateTicks++;
        }

        return Blocks != blocked;
    }

    /// <summary>
    /// An arena's grate falls - never at an unbroken cracked wall, which would give the secret away. Without a grate
    /// the door is barred at once. True if a grate started to fall (it was not falling already).
    /// </summary>
    internal bool Bar()
    {
        if (!HasGrate)
        {
            _barred = Kind != DoorKind.Cracked || IsOpen;
            return false;
        }

        if (_barred)
        {
            return false;
        }

        _barred = true;
        Turn(GrateMotion.Fall);
        return true;
    }

    /// <summary>The grate rises (without one the door is free at once). True if a grate started to rise.</summary>
    internal bool Lift()
    {
        var wasBarred = _barred;
        _barred = false;
        if (!HasGrate || !wasBarred)
        {
            return false;
        }

        Turn(GrateMotion.Lift);
        return true;
    }

    // The new motion starts where the grate hangs now, so it never jumps - also when it turns round on its way.
    private void Turn(GrateMotion motion)
    {
        var height = GrateHeight;
        _motion = motion;
        _grateTicks = GratePose.StartTick(height, motion, _grates);
    }
}
