using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class DamageRulesTests
{
    private const int Base = 35;

    [Fact]
    public void Without_rules_a_hit_does_its_base_damage()
    {
        var result = new DamageRules().Calculate(new Hit(Weapon.Pistol, HitZone.Head, Base));

        Assert.Equal(new DamageResult(Base, false), result);
    }

    [Fact]
    public void A_pistol_headshot_does_triple_damage_and_is_critical()
    {
        var result = DamageRules.Standard().Calculate(new Hit(Weapon.Pistol, HitZone.Head, Base));

        Assert.Equal(new DamageResult(105, true), result);
    }

    [Fact]
    public void A_pistol_body_shot_and_the_blade_get_no_zone_bonus()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(new DamageResult(Base, false), rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, Base)));
        Assert.Equal(new DamageResult(40, false), rules.Calculate(new Hit(Weapon.Blade, HitZone.Head, 40)));
    }

    [Fact]
    public void A_new_rule_plugs_in_without_touching_anything_else()
    {
        // Like a future item "blessed ammunition": +20 % for every pistol hit.
        var rules = DamageRules.Standard();
        var blessedAmmo = new EveryHitWith(Weapon.Pistol, 1.2f);

        rules.Add(blessedAmmo);

        Assert.Equal(new DamageResult(42, false), rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, Base)));
        Assert.Equal(new DamageResult(126, true), rules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, Base)));
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40)).Amount);
    }

    [Fact]
    public void A_removed_rule_no_longer_counts()
    {
        var rules = DamageRules.Standard();
        var blessedAmmo = new EveryHitWith(Weapon.Pistol, 1.2f);
        rules.Add(blessedAmmo);

        Assert.True(rules.Remove(blessedAmmo));

        Assert.Equal(Base, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, Base)).Amount);
    }

    [Fact]
    public void Half_points_are_rounded_up()
    {
        var rules = new DamageRules();
        rules.Add(new EveryHitWith(Weapon.Blade, 1.5f));

        // 7 x 1.5 = 10.5 -> 11. (The default "banker's rounding" in .NET would give 10: it rounds .5 to the even number.)
        Assert.Equal(11, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 7)).Amount);
    }

    private sealed record EveryHitWith(Weapon Weapon, float Factor) : IDamageRule
    {
        public bool IsCritical => false;

        public float Multiplier(Hit hit) => hit.Weapon == Weapon ? Factor : 1f;
    }
}
