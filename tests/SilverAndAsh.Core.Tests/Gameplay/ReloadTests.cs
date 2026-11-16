using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Magazines and reloading (decision record 0008, issue #188).</summary>
public class ReloadTests
{
    private static readonly PistolBalance Pistol = Balance.Default.Pistol;
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Pistol.ReloadTime);
    private static readonly int CooldownTicks = SimulationClock.SecondsToTicks(Pistol.Cooldown);
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly PlayerInput Reload = PlayerInput.None with { Reload = true };

    private static List<GameEvent> Tick(Player player, PlayerInput input, int ticks = 1)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            player.Tick(input);
            events.AddRange(player.Events);
        }

        return events;
    }

    /// <summary>Pulls the trigger once and lets go, then waits until the pistol is ready again.</summary>
    private static List<GameEvent> Fire(Player player)
    {
        var events = Tick(player, Shoot);
        events.AddRange(Tick(player, PlayerInput.None, CooldownTicks + 1));
        return events;
    }

    // ---- Magazine on its own ---------------------------------------------------------------------------------------

    [Fact]
    public void A_new_magazine_is_full()
    {
        var magazine = new Magazine(6, 1.5f);

        Assert.Equal(6, magazine.Rounds);
        Assert.False(magazine.IsReloading);
        Assert.Equal(0f, magazine.ReloadProgress);
    }

    [Fact]
    public void Each_shot_takes_one_round_until_it_is_empty()
    {
        var magazine = new Magazine(3, 1f);

        Assert.True(magazine.TryTake());
        Assert.True(magazine.TryTake());
        Assert.True(magazine.TryTake());
        Assert.False(magazine.TryTake());
        Assert.Equal(0, magazine.Rounds);
    }

    [Fact]
    public void A_full_magazine_needs_no_reload()
    {
        var magazine = new Magazine(3, 1f);

        Assert.False(magazine.StartReload());
        Assert.False(magazine.IsReloading);
    }

    [Fact]
    public void Reloading_takes_its_time_then_the_magazine_is_full()
    {
        var magazine = new Magazine(3, 1f);
        magazine.TryTake();
        Assert.True(magazine.StartReload());
        var ticks = SimulationClock.SecondsToTicks(1f);

        for (var tick = 1; tick < ticks; tick++)
        {
            Assert.False(magazine.Tick());
        }

        Assert.True(magazine.IsReloading);
        Assert.Equal(2, magazine.Rounds);
        Assert.False(magazine.TryTake());
        Assert.InRange(magazine.ReloadProgress, 0.9f, 1f);

        Assert.True(magazine.Tick());
        Assert.False(magazine.IsReloading);
        Assert.Equal(3, magazine.Rounds);
    }

    [Fact]
    public void A_cancelled_reload_leaves_the_rounds_as_they_were()
    {
        var magazine = new Magazine(3, 1f);
        magazine.TryTake();
        magazine.StartReload();
        magazine.Tick();

        magazine.CancelReload();

        Assert.False(magazine.IsReloading);
        Assert.Equal(2, magazine.Rounds);
        Assert.True(magazine.TryTake());
    }

    // ---- The pistol ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_pistol_starts_with_a_full_magazine_from_the_balancing()
    {
        var player = new Player(LevelParser.Parse("P"));

        Assert.Equal(Pistol.MagazineSize, player.Ranged.Magazine.Size);
        Assert.Equal(Pistol.MagazineSize, player.Ranged.Magazine.Rounds);
    }

    [Fact]
    public void Every_shot_takes_a_round_and_an_empty_pistol_does_not_fire()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        for (var shot = 0; shot < Pistol.MagazineSize; shot++)
        {
            events.AddRange(Fire(player));
        }

        Assert.Equal(Pistol.MagazineSize, events.OfType<ShotFired>().Count());
        Assert.Equal(0, player.Ranged.Magazine.Rounds);

        var empty = Tick(player, Shoot);
        Assert.DoesNotContain(empty, e => e is ShotFired);
    }

    [Fact]
    public void A_shot_with_an_empty_magazine_starts_the_reload()
    {
        var player = new Player(LevelParser.Parse("P"));
        for (var shot = 0; shot < Pistol.MagazineSize; shot++)
        {
            Fire(player);
        }

        var events = Tick(player, Shoot);

        Assert.Contains(new ReloadStarted(player.Id), events);
        Assert.True(player.Ranged.Magazine.IsReloading);
    }

    [Fact]
    public void R_reloads_in_the_set_time_and_fills_the_magazine()
    {
        var player = new Player(LevelParser.Parse("P"));
        Fire(player);
        Fire(player);

        var events = Tick(player, Reload);
        Assert.Contains(new ReloadStarted(player.Id), events);

        events.AddRange(Tick(player, PlayerInput.None, ReloadTicks - 1));
        Assert.True(player.Ranged.Magazine.IsReloading);
        Assert.DoesNotContain(events, e => e is ReloadFinished);

        events.AddRange(Tick(player, PlayerInput.None));
        Assert.Contains(new ReloadFinished(player.Id), events);
        Assert.Equal(Pistol.MagazineSize, player.Ranged.Magazine.Rounds);
    }

    [Fact]
    public void While_reloading_the_pistol_does_not_fire()
    {
        var player = new Player(LevelParser.Parse("P"));
        Fire(player);
        Tick(player, Reload);
        Tick(player, PlayerInput.None);

        var events = Tick(player, Shoot);

        Assert.DoesNotContain(events, e => e is ShotFired);
        Assert.Equal(Pistol.MagazineSize - 1, player.Ranged.Magazine.Rounds);
    }

    [Fact]
    public void Holding_R_reloads_once_not_again_and_again()
    {
        var player = new Player(LevelParser.Parse("P"));
        Fire(player);

        var events = Tick(player, Reload, ReloadTicks + 5);

        Assert.Single(events.OfType<ReloadStarted>());
    }

    [Fact]
    public void Pressing_R_again_while_reloading_does_not_start_over()
    {
        var player = new Player(LevelParser.Parse("P"));
        Fire(player);
        var events = Tick(player, Reload);
        events.AddRange(Tick(player, PlayerInput.None, ReloadTicks / 2));

        events.AddRange(Tick(player, Reload));
        events.AddRange(Tick(player, PlayerInput.None, ReloadTicks - (ReloadTicks / 2) - 1));

        Assert.Single(events.OfType<ReloadStarted>());
        Assert.Contains(new ReloadFinished(player.Id), events);
        Assert.Equal(Pistol.MagazineSize, player.Ranged.Magazine.Rounds);
    }

    [Fact]
    public void Still_holding_R_after_the_reload_does_not_reload_after_the_next_shot()
    {
        var player = new Player(LevelParser.Parse("P"));
        Fire(player);
        Tick(player, Reload, ReloadTicks + 1);

        var events = Tick(player, Reload with { Shoot = true });

        Assert.Contains(events, e => e is ShotFired);
        Assert.DoesNotContain(events, e => e is ReloadStarted);
        Assert.False(player.Ranged.Magazine.IsReloading);
    }

    [Fact]
    public void R_with_a_full_magazine_does_nothing()
    {
        var player = new Player(LevelParser.Parse("P"));

        var events = Tick(player, Reload);

        Assert.DoesNotContain(events, e => e is ReloadStarted);
        Assert.False(player.Ranged.Magazine.IsReloading);
    }

    [Fact]
    public void The_cooldown_between_shots_still_counts()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = Tick(player, Shoot);
        events.AddRange(Tick(player, PlayerInput.None));

        events.AddRange(Tick(player, Shoot));

        Assert.Single(events.OfType<ShotFired>());
        Assert.Equal(Pistol.MagazineSize - 1, player.Ranged.Magazine.Rounds);
    }
}
