using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// The room of one station while the test course is built (issue #718): what <see cref="CourseStation.Furnish"/> puts
/// into it. Places are given as a step east of the room's first column and a row (<see cref="Top"/> to
/// <see cref="Bottom"/>; the way runs along <see cref="Way"/>). Everything comes in with the real rules - enemies that
/// fight, columns checked by <see cref="ColumnPlacement"/>, doors of the game's kinds.
/// </summary>
public sealed class CourseRoom
{
    /// <summary>The northernmost floor row of every station.</summary>
    public const int Top = 4;

    /// <summary>The southernmost floor row of every station.</summary>
    public const int Bottom = 8;

    /// <summary>The row of the way: every door between two stations lies in it.</summary>
    public const int Way = 6;

    // The great hall south of the antechamber: as wide and deep as assets/rooms/boss/great-hall.txt.
    private const int BossRoomSize = 14;

    private readonly CourseBuilder _course;

    internal CourseRoom(CourseBuilder course, CourseStation station, int number, int index, int firstX)
    {
        _course = course;
        Station = station;
        Number = number;
        Index = index;
        FirstX = firstX;
    }

    /// <summary>The station this room belongs to.</summary>
    public CourseStation Station { get; }

    /// <summary>The station's number on its sign, from 1.</summary>
    public int Number { get; }

    /// <summary>The room's number in the level's rooms.</summary>
    public int Index { get; }

    /// <summary>The westernmost floor column.</summary>
    public int FirstX { get; }

    /// <summary>The easternmost floor column.</summary>
    public int LastX => FirstX + Station.Width - 1;

    private string Owner => $"{Number} - {Station.Name}";

    /// <summary>The level cell <paramref name="step"/> columns east of <see cref="FirstX"/>, in <paramref name="row"/>.</summary>
    public (int X, int Z) Cell(int step, int row) => (FirstX + step, row);

    /// <summary>A free-standing column in the middle of that cell (it must keep its distance to walls and doors).</summary>
    public void Column(int step, int row)
    {
        var (x, z) = Cell(step, row);
        _course.Columns.Add((World.Column.AtCell(x, z), Owner));
    }

    /// <summary>A ghoul starting in that cell.</summary>
    public void Ghoul(int step, int row) => _course.Ghouls.Add(Cell(step, row));

    /// <summary>A bat hovering above that cell.</summary>
    public void Bat(int step, int row) => _course.Bats.Add(Cell(step, row));

    /// <summary><paramref name="amount"/> silver lying in that cell.</summary>
    public void Silver(int step, int row, int amount) => _course.Loot.Add(new ExhibitLoot(LootKind.Silver, amount, OnFloor(step, row)));

    /// <summary>A consumable lying in that cell.</summary>
    public void Consumable(ConsumableKind kind, int step, int row) => _course.Consumables.Add(new ExhibitConsumable(kind, OnFloor(step, row)));

    /// <summary>A weapon lying in that cell, to be taken with F.</summary>
    public void Weapon(WeaponKind kind, int step, int row) => _course.Weapons.Add(new ExhibitWeapon(kind, OnFloor(step, row)));

    /// <summary>An item lying in that cell, to be taken with F.</summary>
    public void Item(ItemKind kind, int step, int row) => _course.Items.Add(new ExhibitItem(kind, OnFloor(step, row)));

    /// <summary>
    /// A teleporter in that cell marking <paramref name="site"/>, its show side along <paramref name="facing"/> (zero: not
    /// turned). The travel choice names it after the station, as its sign does (issue #722) - not after the site.
    /// </summary>
    public void Teleporter(int step, int row, TeleporterSite site, Vector2 facing = default)
    {
        var (x, z) = Cell(step, row);
        _course.Teleporters.Add(new TeleporterPlan(x, z, Index, site) { Facing = facing, Name = Owner });
    }

    /// <summary>Sirius' Relief in the middle of that cell - the only one of the course.</summary>
    public void Well(int step, int row)
    {
        if (_course.Well is not null)
        {
            throw new InvalidOperationException($"The test course: '{Owner}' wants a second well.");
        }

        var (x, z) = Cell(step, row);
        _course.Well = new WellPlan(x, z, Index);
    }

    /// <summary>
    /// A secret room north of this one, as wide, two rows deep, behind a cracked wall in the middle of the north wall
    /// - with a chest place by its north wall, where <see cref="ChestPlacement"/> puts its chest.
    /// </summary>
    public void SecretRoomNorth()
    {
        var middle = FirstX + (Station.Width / 2);
        var secret = _course.AddRoom(
            RoomKind.Secret, $"{Owner} (secret room)", FirstX, Top - 3, LastX, Top - 2, Number, chestSlots: [(middle, Top - 3)]);
        _course.AddDoor(new LevelDoor(middle, Top - 1, Index, secret, RoomSide.North, DoorKind.Cracked), Owner);
    }

    /// <summary>
    /// The boss room south of this one, which must be the antechamber (4 cells wide): the boss portal over its two
    /// middle cells of the south wall, behind it a great hall with Gorm near its far wall, in line with the portal.
    /// </summary>
    public void BossRoomSouth()
    {
        if (Station.Kind != RoomKind.Antechamber || Station.Width != Antechamber.Width)
        {
            throw new InvalidOperationException($"The test course: '{Owner}' must be an antechamber {Antechamber.Width} cells wide to have the boss room.");
        }

        // The portal's first cell sits where the great hall has its door: seven columns in from its west wall.
        var portal = FirstX + 1;
        var west = portal - 6;
        var north = Bottom + 2;
        var gorm = (portal, north + BossRoomSize - 3);
        var boss = _course.AddRoom(
            RoomKind.Boss, $"{Owner} (boss room)", west, north, west + BossRoomSize - 1, north + BossRoomSize - 1, Number, bossSlots: [gorm]);
        _course.Bosses.Add(gorm);
        _course.AddDoor(
            new LevelDoor(portal, Bottom + 1, Index, boss, RoomSide.South, DoorKind.Boss) { MoreCells = [(portal + 1, Bottom + 1)] }, Owner);
    }

    private Vector3 OnFloor(int step, int row)
    {
        var (x, z) = Cell(step, row);
        return new Vector3((x + 0.5f) * Level.CellSize, 0f, (z + 0.5f) * Level.CellSize);
    }
}
