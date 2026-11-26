using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Bot;

// The fight bot of tools/SilverAndAsh.BotRun (issue #803): its decision is a function of numbers, so it is tested with
// the values the game really has (Balance.Default), not with made-up ones.
public class FightBotTests
{
    private static readonly Balance Balance = Balance.Default;

    private static FightView View(
        int? health = null, bool ranged = false, float reach = 2f, bool reloading = false, int rounds = 6,
        float? target = null, bool inSight = true, float? vial = null, float? key = null) =>
        new(health ?? Balance.Player.MaxHealth, Balance.Player.MaxHealth, ranged, reach, reloading, rounds, target, inSight, vial, key);

    [Fact]
    public void WithNothingAround_TheBotWalksTheRoute() =>
        Assert.Equal(new FightChoice(FightGoal.Route, false, false), FightPolicy.Decide(View()));

    [Fact]
    public void AnEmptyRangedWeapon_IsReloadedStandingStill() =>
        Assert.Equal(new FightChoice(FightGoal.Hold, false, true), FightPolicy.Decide(View(ranged: true, rounds: 0, target: 5f)));

    [Fact]
    public void WhileReloading_TheBotHoldsAndDoesNotPressReloadAgain() =>
        Assert.Equal(new FightChoice(FightGoal.Hold, false, false), FightPolicy.Decide(View(ranged: true, rounds: 0, reloading: true, target: 5f)));

    [Fact]
    public void AMeleeWeaponWithoutRounds_NeverReloads() =>
        Assert.False(FightPolicy.Decide(View(rounds: 0, target: 1f)).Reload);

    [Fact]
    public void AHurtPlayerWithAVialInReach_GoesForTheVialBeforeFighting()
    {
        var hurt = (int)(Balance.Player.MaxHealth * FightPolicy.HurtShare) - 1;
        Assert.Equal(FightGoal.Vial, FightPolicy.Decide(View(health: hurt, target: 1f, vial: 10f)).Goal);
    }

    [Fact]
    public void AtHalfHealth_ThePlayerIsNotYetHurt()
    {
        var half = (int)(Balance.Player.MaxHealth * FightPolicy.HurtShare);
        Assert.Equal(FightGoal.Fight, FightPolicy.Decide(View(health: half, target: 1f, vial: 10f)).Goal);
    }

    [Fact]
    public void AVialBeyondPickupRange_IsIgnored() =>
        Assert.Equal(FightGoal.Route, FightPolicy.Decide(View(health: 1, vial: FightPolicy.PickupRange + 1f)).Goal);

    [Fact]
    public void ATargetWithinMeleeReach_IsFought() =>
        Assert.Equal(new FightChoice(FightGoal.Fight, true, false), FightPolicy.Decide(View(reach: 2f, target: 1.5f)));

    [Fact]
    public void ATargetJustOutsideTheMeleeStartDistance_IsApproached()
    {
        var edge = FightPolicy.AttackDistance(false, 2f);
        Assert.Equal(FightGoal.Approach, FightPolicy.Decide(View(reach: 2f, target: edge + 0.01f)).Goal);
        Assert.Equal(FightGoal.Fight, FightPolicy.Decide(View(reach: 2f, target: edge)).Goal);
    }

    [Fact]
    public void ATargetOutOfSight_IsApproachedEvenWhenClose() =>
        Assert.Equal(FightGoal.Approach, FightPolicy.Decide(View(target: 0.5f, inSight: false)).Goal);

    [Fact]
    public void ARangedWeapon_ShootsFromAfarButNotBeyondTheShotRange()
    {
        Assert.Equal(FightGoal.Fight, FightPolicy.Decide(View(ranged: true, reach: 100f, target: FightPolicy.ShotRange)).Goal);
        Assert.Equal(FightGoal.Approach, FightPolicy.Decide(View(ranged: true, reach: 100f, target: FightPolicy.ShotRange + 1f)).Goal);
    }

    [Fact]
    public void ATargetComesBeforeAKey() =>
        Assert.Equal(FightGoal.Fight, FightPolicy.Decide(View(target: 1f, key: 3f)).Goal);

    [Fact]
    public void WithoutATarget_AKeyInReachIsFetched() =>
        Assert.Equal(FightGoal.Key, FightPolicy.Decide(View(key: 3f)).Goal);

    [Fact]
    public void DamageSource_IsNullWithoutAHit() =>
        Assert.Null(DamageSource.Of([new GhoulStruck(1, Hit: true)]));

    [Fact]
    public void DamageSource_NamesTheStrikerOfTheTick()
    {
        Assert.Equal("ghoul", DamageSource.Of([new PlayerHit(0, 10), new GhoulStruck(1, Hit: true)]));
        Assert.Equal("bat", DamageSource.Of([new PlayerHit(0, 10), new BatBit(1, 0)]));
        Assert.Equal("Gorm (swing)", DamageSource.Of([new PlayerHit(0, 10), new GormSwung(1, Hit: true)]));
        Assert.Equal("Gorm (shockwave)", DamageSource.Of([new PlayerHit(0, 10), new ShockwaveHit(0)]));
        Assert.Equal("Gorm (hook)", DamageSource.Of([new PlayerHit(0, 10), new HookHit(0)]));
        Assert.Equal("Gorm (stone)", DamageSource.Of([new PlayerHit(0, 10), new StoneHit(0)]));
        Assert.Equal("projectile", DamageSource.Of([new PlayerHit(0, 10), new ProjectileHitPlayer(1, 0, default)]));
    }

    [Fact]
    public void DamageSource_AMissedSwingIsNoSource() =>
        Assert.Equal("unknown", DamageSource.Of([new PlayerHit(0, 10), new GhoulStruck(1, Hit: false)]));

    [Theory]
    [InlineData("blade", WeaponKind.SilverBlade)]
    [InlineData("Pistol", WeaponKind.Pistol)]
    [InlineData("axe", WeaponKind.HuntersAxe)]
    [InlineData("hunters-axe", WeaponKind.HuntersAxe)]
    [InlineData("whip", WeaponKind.Whip)]
    [InlineData("stake_crossbow", WeaponKind.StakeCrossbow)]
    public void WeaponNames_AreRead(string text, WeaponKind expected)
    {
        Assert.True(FightWeapons.TryParse(text, out var weapons));
        Assert.Equal([expected], weapons);
    }

    [Fact]
    public void WeaponNames_AllMeansTheFiveWeapons()
    {
        Assert.True(FightWeapons.TryParse("all", out var weapons));
        Assert.Equal(5, weapons.Count);
    }

    [Fact]
    public void WeaponNames_ListsDropDoubles_AndUnknownNamesFail()
    {
        Assert.True(FightWeapons.TryParse("whip,blade,whip", out var weapons));
        Assert.Equal([WeaponKind.Whip, WeaponKind.SilverBlade], weapons);
        Assert.False(FightWeapons.TryParse("whip,sword", out _));
    }

    [Fact]
    public void Clock_ShowsGameTime() =>
        Assert.Equal("1:04.2", FightResult.Clock(3856));

    [Fact]
    public void WinRateLine_CountsWonOverPlayed()
    {
        Assert.Equal("WIN RATE Whip: 1/4 (25%)", FightMode.WinRateLine(WeaponKind.Whip, 1, 4));
        Assert.Equal("WIN RATE Whip: 0/0 (0%)", FightMode.WinRateLine(WeaponKind.Whip, 0, 0));
    }

    [Fact]
    public void ResultLine_NamesOutcomePlaceAndLastDamage()
    {
        var lost = new FightResult(3, WeaponKind.Pistol, FightEnd.Died, 600, 1, 2, 14, "room 7 (Combat)", "ghoul", 130, 900);
        Assert.Equal("seed 3 Pistol: LOST after 0:10.0 (600 ticks) | level 1 | combat rooms 2/14 cleared | died in room 7 (Combat) | last damage ghoul | damage taken 130 | damage dealt 900", lost.Line());
        Assert.Contains("TICK LIMIT", (lost with { End = FightEnd.TickLimit }).Line());
        Assert.Contains("STUCK", (lost with { End = FightEnd.Stuck }).Line());
        Assert.Contains("WON", (lost with { End = FightEnd.Won }).Line());
    }

    // A short real run: the player is mortal, the run ends by itself, and the same seed gives the same result.
    [Fact]
    public void ARun_EndsAndRepeatsExactly()
    {
        var zero = new LevelZero(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")));
        var mode = new FightMode { Balance = Balance };

        var first = mode.Fight(zero, 1, WeaponKind.SilverBlade, new Report());

        Assert.Equal(first, mode.Fight(zero, 1, WeaponKind.SilverBlade, new Report()));
        Assert.Equal(FightEnd.Died, first.End);
    }
}
