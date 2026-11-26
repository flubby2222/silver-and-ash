using System.Numerics;
using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;
using Xunit;

namespace SilverAndAsh.Core.Tests.Bot;

// Issue #805: keeping distance, stepping back, dodging. Fixed numbers: the tests do not read the bot's constants
// (hint 261), so a changed constant turns one of them red.
public class FightRetreatTests
{
    private static readonly Balance Balance = Balance.Default;
    private static readonly Func<Vector2, bool> Open = _ => true;

    private static FightView View(
        int? health = null, bool ranged = false, float reach = 2f, int rounds = 6, float? target = null,
        bool inSight = true, float? vial = null, float? threat = null, bool canDodge = false) =>
        new(health ?? Balance.Player.MaxHealth, Balance.Player.MaxHealth, ranged, reach, false, rounds, target, inSight, vial, null, threat, canDodge);

    private static string GoalName(FightChoice choice) => choice.Goal.ToString();

    [Theory]
    [InlineData(25f, "Fight")]
    [InlineData(25.5f, "Approach")]
    public void ARangedWeapon_ShootsUpTo25Metres(float target, string expected) =>
        Assert.Equal(expected, GoalName(FightPolicy.Decide(View(ranged: true, reach: 100f, target: target))));

    [Fact]
    public void AttackDistance_IsNinetenthsOfAMeleeReach_AndTheShorterOfReachAndShotRangeForAGun()
    {
        Assert.Equal(1.8f, FightPolicy.AttackDistance(false, 2f), 3);
        Assert.Equal(25f, FightPolicy.AttackDistance(true, 100f), 3);
        Assert.Equal(10f, FightPolicy.AttackDistance(true, 10f), 3);
    }

    [Theory]
    [InlineData(25f, "Vial")]
    [InlineData(25.5f, "Route")]
    public void AHurtPlayer_FetchesAVialUpTo25Metres(float vial, string expected) =>
        Assert.Equal(expected, GoalName(FightPolicy.Decide(View(health: 1, vial: vial))));

    [Theory]
    [InlineData(2.9f, "Retreat")]
    [InlineData(3f, "Fight")]
    public void AMeleeBot_StepsBackFromAThreatCloserThan3Metres(float threat, string expected) =>
        Assert.Equal(expected, GoalName(FightPolicy.Decide(View(target: 1f, threat: threat))));

    [Theory]
    [InlineData(4.4f, "Retreat")]
    [InlineData(4.5f, "Fight")]
    public void ARangedBot_KeepsAtLeast4AndAHalfMetres(float target, string expected) =>
        Assert.Equal(expected, GoalName(FightPolicy.Decide(View(ranged: true, reach: 100f, target: target))));

    [Fact]
    public void ARangedBot_ShootsWhileWalkingBack_OnlyWithSight()
    {
        Assert.Equal(new FightChoice(FightGoal.Retreat, true, false), FightPolicy.Decide(View(ranged: true, reach: 100f, target: 3f)));
        Assert.False(FightPolicy.Decide(View(ranged: true, reach: 100f, target: 3f, inSight: false)).Attack);
    }

    [Fact]
    public void ARangedBot_ReloadsWhileWalkingBack() =>
        Assert.Equal(new FightChoice(FightGoal.Retreat, false, true), FightPolicy.Decide(View(ranged: true, reach: 100f, rounds: 0, target: 3f)));

    [Fact]
    public void AMeleeBot_NeverAttacksOnTheWayBack() =>
        Assert.Equal(new FightChoice(FightGoal.Retreat, false, false), FightPolicy.Decide(View(target: 1f, threat: 2f)));

    [Fact]
    public void TheDodge_NeedsAThreatCloserThan3MetresAndStamina()
    {
        Assert.True(FightPolicy.Decide(View(target: 1f, threat: 2.9f, canDodge: true)).Dodge);
        Assert.False(FightPolicy.Decide(View(target: 1f, threat: 2.9f, canDodge: false)).Dodge);
        Assert.False(FightPolicy.Decide(View(target: 1f, threat: 3f, canDodge: true)).Dodge);
    }

    [Fact]
    public void AHurtPlayerInStrikingDistance_FightsBeforeWalkingToTheVial() =>
        Assert.Equal(FightGoal.Fight, FightPolicy.Decide(View(health: 1, target: 1.8f, vial: 10f)).Goal);

    [Fact]
    public void AHurtPlayerWithTheTargetOutOfSight_StillGoesForTheVial() =>
        Assert.Equal(FightGoal.Vial, FightPolicy.Decide(View(health: 1, target: 1f, inSight: false, vial: 10f)).Goal);

    [Fact]
    public void Retreat_WithoutFoes_StepsOneMetreOfDirection()
    {
        var step = FightRetreat.Choose(Vector2.Zero, [], Open);
        Assert.NotNull(step);
        Assert.Equal(1f, step.Value.Length(), 3);
    }

    [Fact]
    public void Retreat_GoesStraightAwayFromOneFoe()
    {
        var step = FightRetreat.Choose(Vector2.Zero, [new Foe(new Vector2(0f, 2f), 0.5f, null)], Open);
        Assert.Equal(0f, step!.Value.X, 3);
        Assert.Equal(-1f, step.Value.Y, 3);
    }

    [Fact]
    public void Retreat_LeavesTheBlowOfAGhoulThatFacesTheBot()
    {
        var zone = new StrikeZone(new Vector2(0f, 2f), 2.4f, new Vector2(0f, -1f), 0.5f);
        var step = FightRetreat.Choose(Vector2.Zero, [new Foe(new Vector2(0f, 2f), 0.5f, zone)], Open)!.Value;
        Assert.False(zone.Contains(step * 3f));
    }

    [Fact]
    public void Retreat_SkipsWaysThatAreNotFree()
    {
        var step = FightRetreat.Choose(Vector2.Zero, [new Foe(new Vector2(0f, 2f), 0.5f, null)], end => end.Y > -0.5f)!.Value;
        Assert.True(step.Y > -0.17f);
    }

    [Fact]
    public void Retreat_WithNoFreeWay_ReturnsNull() =>
        Assert.Null(FightRetreat.Choose(Vector2.Zero, [], _ => false));

    [Fact]
    public void Retreat_KeepsThePreviousHeadingBetweenEqualWays()
    {
        var step = FightRetreat.Choose(Vector2.Zero, [], Open, new Vector2(1f, 0f))!.Value;
        Assert.Equal(1f, step.X, 3);
    }

    [Fact]
    public void StrikeZone_ContainsOnlyTheWedgeInReach()
    {
        var zone = new StrikeZone(Vector2.Zero, 2f, new Vector2(0f, 1f), 0.5f);
        Assert.True(zone.Contains(new Vector2(0f, 2f)));
        Assert.False(zone.Contains(new Vector2(0f, 2.1f)));
        Assert.False(zone.Contains(new Vector2(0f, -1f)));
        Assert.True(zone.Contains(new Vector2(1.2f, 1f)));
        Assert.False(zone.Contains(new Vector2(1.9f, 0.1f)));
    }

    [Fact]
    public void StrikeZone_AFullCircleContainsEveryDirection()
    {
        var zone = new StrikeZone(Vector2.Zero, 1.5f, Vector2.UnitY, -1f);
        Assert.True(zone.Contains(new Vector2(0f, -1.4f)));
        Assert.False(zone.Contains(new Vector2(0f, -1.6f)));
    }

    [Fact]
    public void Score_ABlowThatHitsCostsMoreThanAllTheRoomIsWorth()
    {
        var zone = new StrikeZone(new Vector2(0f, 2f), 2.4f, new Vector2(0f, -1f), 0.5f);
        var foes = new[] { new Foe(new Vector2(0f, 2f), 0.5f, zone) };
        var inside = FightRetreat.Score(Vector2.Zero, new Vector2(0f, 1f), 0f, foes);
        var outside = FightRetreat.Score(Vector2.Zero, new Vector2(0f, -3f), 0f, foes);
        Assert.True(outside - inside > 90f);
    }

    [Fact]
    public void Score_ClearanceCountsOnlyUpToTwoMetres()
    {
        var foes = new[] { new Foe(new Vector2(0f, 10f), 0f, null) };
        Assert.Equal(20f, FightRetreat.Score(Vector2.Zero, new Vector2(0f, -3f), 0f, foes), 3);
    }

    [Fact]
    public void UnknownWeapon_MessageListsTheValidNames() =>
        Assert.Equal(
            "Unknown weapon 'sword'. Valid: all, axe, blade, crossbow, huntersaxe, pistol, silverblade, stakecrossbow, whip.",
            FightWeapons.UnknownMessage("sword"));

    // A gun has to be given first (hint 262): the run with a pistol repeats exactly and the pistol does damage.
    [Fact]
    public void ARunWithAPistol_RepeatsExactlyAndShoots()
    {
        var zero = new LevelZero(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")));
        var mode = new FightMode { Balance = Balance };

        var first = mode.Fight(zero, 1, WeaponKind.Pistol, new Report());

        Assert.Equal(first, mode.Fight(zero, 1, WeaponKind.Pistol, new Report()));
        Assert.Equal(WeaponKind.Pistol, first.Weapon);
        Assert.True(first.DamageDealt > 0);
    }
}
