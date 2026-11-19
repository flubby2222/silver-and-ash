using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Pins down what <see cref="Player.Tick"/> does, tick by tick (issue #325: splitting it into phases must not change
/// anything). Two bot runs with a fixed seed write the whole visible state of a player after every tick, together with
/// the events of that tick, into a trace; the test compares a checksum of the trace with the one written down below.
/// <para>
/// The other player tests check single rules; this one catches what they cannot see - two steps in a new order, a
/// step left out, a value read one step too early. If a change to the player's behaviour (or to the standard values
/// in <see cref="Balance.Default"/>) is meant, the checksum changes with it: the pull request says so and puts in the
/// new one (the test prints it). A pure refactoring never touches it.
/// </para>
/// <para>
/// Numbers are written with four decimals: the camera's landing dip and Gorm's aim use sine and cosine, whose last bit
/// may differ between Windows and Linux (the CI) - a real change shows up far above that.
/// </para>
/// </summary>
public class PlayerTickTraceTests
{
    // Checksums of the traces as Player.Tick wrote them before the split into phases (issue #325, main at 5fc2b73).
    // Changed on purpose in #413: a charge the stamina can no longer pay holds and lets the bar refill (first difference
    // in each run: the tick a stalled charge would have kept the empty bar from refilling).
    // Gorm duel changed on purpose in #430: the chain swing reaches 12 m instead of 8 m, so it hits the bot where it
    // missed before (parcours traces unchanged).
    // Two parcours seeds: together their bots reach almost every line of the tick (one alone misses a few).
    private const string GormDuelChecksum = "96A7BD0EF1B6A17085C2BC10AA9E77B3E2B7BDB48DAFDAA993F29E8BDF93577F";

    [Theory]
    [InlineData(1, "D9933F5097C3E8FAED875CB10664AA4B31447231594EBCB9647AFC7FE5AADE8A")]
    [InlineData(4, "B59A6BBEB46D54E81BC6C0E2E2A3F17F3E78CEE118EC607EB8BA3E492D9ECA23")]
    public void A_bot_run_through_a_parcours_gives_the_same_trace_as_before(int seed, string checksum)
    {
        var trace = ParcoursTrace(seed, ticks: 6000);

        Assert.True(checksum == Checksum(trace), $"The parcours trace for seed {seed} changed - new checksum {Checksum(trace)}.");
    }

    [Fact]
    public void A_bot_run_against_Gorm_gives_the_same_trace_as_before()
    {
        var trace = GormDuelTrace(seed: 325, ticks: 4000);

        Assert.True(GormDuelChecksum == Checksum(trace), $"The Gorm duel trace changed - new checksum {Checksum(trace)}.");
    }

    [Fact]
    public void The_same_seed_gives_the_same_trace_and_another_seed_another_one()
    {
        // The checksums above only mean something if the run itself is repeatable - and if the bot really does
        // different things for different seeds.
        Assert.Equal(ParcoursTrace(7, 600), ParcoursTrace(7, 600));
        Assert.NotEqual(ParcoursTrace(7, 600), ParcoursTrace(8, 600));
    }

    [Fact]
    public void The_parcours_bots_visit_every_part_of_the_tick()
    {
        // A trace is only worth pinning down if the bots reach the rules in question (together, not each alone).
        var trace = ParcoursTrace(1, ticks: 6000) + ParcoursTrace(4, ticks: 6000);

        foreach (var expected in new[]
                 {
                     nameof(PlayerJumped), nameof(PlayerLanded), nameof(PlayerDashed), nameof(PlayerDodged),
                     nameof(PlayerStepped), nameof(MeleeSwung), nameof(ShotFired), nameof(ReloadStarted),
                     nameof(ReloadFinished), nameof(WeaponSwitched), nameof(ConsecrationEnded), "bonus=True",
                     "exhausted=True", "inventory=True", "health=0",
                 })
        {
            Assert.Contains(expected, trace, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Gorm_bot_is_pulled_and_dies()
    {
        var trace = GormDuelTrace(seed: 325, ticks: 4000);

        Assert.Contains("pulled=True", trace, StringComparison.Ordinal);
        Assert.Contains("health=0", trace, StringComparison.Ordinal);
    }

    // ---- The two runs -----------------------------------------------------------------------------------------------

    /// <summary>
    /// A level with steps (1 - 3), pits, pillars, a body to bump into and a box to jump onto. The bot holds a random
    /// input for a random number of ticks; now and then it is consecrated or hurt.
    /// </summary>
    private static string ParcoursTrace(int seed, int ticks)
    {
        string[] rows =
        [
            "##################",
            "#................#",
            "#..1122233..__...#",
            "#..1122233..__...#",
            "#.......#........#",
            "#...P.......#....#",
            "#..__.....1......#",
            "#..__.....12.....#",
            "#.........123....#",
            "#..#.............#",
            "#................#",
            "##################",
        ];
        var level = LevelParser.Parse(string.Join('\n', rows));
        var start = level.PlayerStartPosition;
        CylinderBody[] bodies = [new(start + new Vector3(4f, 0f, -4f), 0.4f, 1.8f)];
        BoxBody[] boxes = [new(start + new Vector3(-4.5f, 0f, 5f), start + new Vector3(-3.5f, 0.6f, 6f))];
        var player = new Player(level, () => bodies, Balance.Default, new Random(seed), () => boxes);
        player.Equip(1, WeaponKind.Rifle);
        player.Equip(2, WeaponKind.Blunderbuss);

        var random = new Random(seed);
        var trace = new StringBuilder();
        var input = PlayerInput.None;
        var holdTicks = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            if (holdTicks-- <= 0)
            {
                input = RandomInput(random);

                // Often short presses (fresh presses at the end of a dash or dodge, just before a landing), sometimes long ones.
                holdTicks = random.Next(2) == 0 ? random.Next(1, 6) : random.Next(1, 40);

                // Now and then look up or down - only up and down, so the walking directions stay exact.
                if (random.Next(5) == 0)
                {
                    player.Look(PlayerInput.None with { Look = new Vector2(0f, random.Next(-300, 300)) });
                }
            }

            if (random.Next(400) == 0)
            {
                player.Consecrate(random.Next(1, 4));
            }

            if (random.Next(300) == 0 && !player.IsDead)
            {
                player.TakeDamage(1, []);
            }

            // Buttons that only count for one tick: menu key, wheel and number keys.
            var thisTick = holdTicks == 0 ? input : input with { PressMenu = null, CycleSlot = 0, SelectSlot = null };

            // At the end: charge the blade and die while charging - the charge must be dropped without a swing.
            var dyingFrom = ticks - 60;
            if (tick >= dyingFrom - 30)
            {
                thisTick = PlayerInput.None with { Melee = tick > dyingFrom - 30 };
            }

            if (tick == dyingFrom)
            {
                player.TakeDamage(player.Health, []);
            }

            player.Tick(thisTick);
            Write(trace, tick, player);
        }

        return trace.ToString();
    }

    /// <summary>Gorm and a player in a hall; the bot walks, jumps, dodges and fights until Gorm has killed it.</summary>
    private static string GormDuelTrace(int seed, int ticks)
    {
        var rows = Enumerable.Range(0, 25).Select(z => new string(
            Enumerable.Range(0, 15).Select(x => (x, z) == (7, 3) ? 'P' : '.').ToArray()));
        var level = LevelParser.Parse(string.Join('\n', rows));
        var player = new Player(level, balance: Balance.Default, random: new Random(seed));
        var gorm = new Gorm(9, player.Position + new Vector3(0f, 0f, 9f), -Vector3.UnitZ, Balance.Default, new Random(seed));

        var random = new Random(seed);
        var trace = new StringBuilder();
        var input = PlayerInput.None;
        var holdTicks = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            if (holdTicks-- <= 0)
            {
                input = RandomInput(random) with { PressMenu = null };
                holdTicks = random.Next(1, 60);
            }

            player.Tick(input);
            Write(trace, tick, player);
            gorm.Act(level, [player], [player.Body], []);
        }

        return trace.ToString();
    }

    private static PlayerInput RandomInput(Random random)
    {
        Vector2[] moves =
        [
            Vector2.Zero, new(0f, 1f), new(0f, -1f), new(1f, 0f), new(-1f, 0f), new(1f, 1f), new(-1f, 1f), new(0.5f, 1f),
        ];
        return PlayerInput.None with
        {
            Move = moves[random.Next(moves.Length)],
            Jump = random.Next(3) == 0,
            Dash = random.Next(6) == 0,
            Melee = random.Next(5) == 0,
            Shoot = random.Next(4) == 0,
            Reload = random.Next(12) == 0,
            CycleSlot = random.Next(10) == 0 ? (random.Next(2) == 0 ? 1 : -1) : 0,
            SelectSlot = random.Next(15) == 0 ? random.Next(3) : null,
            PressMenu = random.Next(40) == 0 ? MenuKey.Equipment : null, // G: open or close the equipment page
        };
    }

    // ---- Writing and checking a trace -------------------------------------------------------------------------------

    private static void Write(StringBuilder trace, int tick, Player player)
    {
        var ranged = string.Join(' ', player.RangedSlots.Select(weapon => weapon is null
            ? "-"
            : $"{weapon.Kind}:{weapon.Magazine.Rounds}/{weapon.Magazine.IsReloading}/{weapon.Cooldown.IsReady}"));
        trace.Append(CultureInfo.InvariantCulture, $"{tick} pos={V(player.Position)} prev={V(player.PreviousPosition)}")
            .Append(CultureInfo.InvariantCulture, $" vel={V(player.Velocity)} grounded={player.IsGrounded}")
            .Append(CultureInfo.InvariantCulture, $" dash={player.IsDashing} dodge={player.IsDodging} pulled={player.IsPulled}")
            .Append(CultureInfo.InvariantCulture, $" bonus={player.HasPerfectJumpBonus} exhausted={player.IsExhausted}")
            .Append(CultureInfo.InvariantCulture, $" stamina={player.Stamina.Amount}/{player.Stamina.IsRecharging}")
            .Append(CultureInfo.InvariantCulture, $" health={player.Health} crouch={F(player.Crouch.Amount)}")
            .Append(CultureInfo.InvariantCulture, $" dip={F(player.LandingDip.Depth)}/{player.LandingDip.IsActive}")
            .Append(CultureInfo.InvariantCulture, $" charge={player.MeleeCharge.IsCharging}/{F(player.MeleeCharge.Fraction)}")
            .Append(CultureInfo.InvariantCulture, $" melee={player.MeleeCooldown.IsReady} slot={player.SelectedSlot} [{ranged}]")
            .Append(CultureInfo.InvariantCulture, $" consecrated={F(player.ConsecratedSecondsLeft)}")
            .Append(CultureInfo.InvariantCulture, $" inventory={player.IsInventoryOpen}");
        foreach (var gameEvent in player.Events)
        {
            trace.Append(' ').Append(Describe(gameEvent));
        }

        trace.Append('\n');
    }

    /// <summary>An event with its type and its numbers - written by hand so the culture and the last bits do not matter.</summary>
    private static string Describe(GameEvent gameEvent) => gameEvent switch
    {
        PlayerLanded landed => $"{nameof(PlayerLanded)}({F(landed.FallHeight)})",
        MeleeSwung swung => $"{nameof(MeleeSwung)}({V(swung.Origin)},{V(swung.Direction)},{F(swung.Charge)},{swung.Weapon})",
        ShotFired shot => $"{nameof(ShotFired)}({V(shot.Origin)},{V(shot.Direction)},{shot.Consecrated},{shot.Weapon})",
        _ => gameEvent.ToString(),
    };

    private static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);

    private static string V(Vector3 value) => $"({F(value.X)};{F(value.Y)};{F(value.Z)})";

    private static string Checksum(string trace) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace)));
}
