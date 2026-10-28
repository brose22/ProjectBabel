using System.Collections.Generic;
using UnityEngine;

// Resolves a captain's ability. Attack shapes hit enemies, with damage scaling with the tower's
// soldier power and a chance of the captain's element status; the Support shape heals and
// boosts friendly towers.
public static class AbilityCaster
{
    const float FlashSeconds = 0.35f;
    const float FlashAlpha = 0.55f;
    // Disc sprites are two units across, so a disc scaled by r has radius r.
    const float SingleTargetFlashRadius = 0.45f;
    // A support ability that only affects its own tower still shows a ring this big.
    const float MinSupportFlashRadius = 0.6f;
    static readonly Color HealColor = new(0.4f, 1f, 0.5f, FlashAlpha);
    static readonly Color BoostColor = new(1f, 0.85f, 0.3f, FlashAlpha);

    // Every working tower within the ability radius of the caster (the caster included) is
    // healed and gets the timed boost. Returns the total health restored.
    public static float CastSupport(Tower caster, Sprite discSprite)
    {
        AbilityData ability = caster.Captain.ability;
        Vector3 origin = caster.transform.position;
        float healed = 0f;
        foreach (Tower tower in Tower.All)
        {
            bool inRange = tower == caster || Vector2.Distance(origin, tower.transform.position) <= ability.radius;
            if (tower.IsDisabled || !inRange) continue;
            healed += tower.HealAll(ability.healFraction);
            tower.AddTimedBoost(ability.buffEffect, ability.buffAmount, ability.buffSeconds);
        }

        Color color = ability.healFraction > 0f ? HealColor : BoostColor;
        AbilityZone.Spawn(origin, 0f, Vector3.one * Mathf.Max(ability.radius, MinSupportFlashRadius), discSprite, color,
            FlashSeconds, ability.hitStopSeconds);
        return healed;
    }

    public static void Cast(Tower tower, Enemy target, Sprite squareSprite, Sprite discSprite)
    {
        AbilityData ability = tower.Captain.ability;
        float damage = tower.SoldierPower * ability.damageMultiplier;
        Vector3 origin = tower.transform.position;
        Vector3 at = target.transform.position;
        Color color = StatusRules.TintOf(StatusRules.ForElement(tower.Captain.element));
        color.a = FlashAlpha;
        // The effect pops open over the hit-stop, so it plays while the match is frozen.
        float pop = ability.hitStopSeconds;

        switch (ability.shape)
        {
            case AbilityData.Shape.Single:
                Hit(tower, target, damage);
                AbilityZone.Spawn(at, 0f, Vector3.one * SingleTargetFlashRadius, discSprite, color, FlashSeconds, pop);
                break;

            case AbilityData.Shape.Area:
                foreach (Enemy enemy in EnemiesWhere(e => Vector2.Distance(at, e.transform.position) <= ability.radius))
                    Hit(tower, enemy, damage);
                AbilityZone.Spawn(at, 0f, Vector3.one * ability.radius, discSprite, color, FlashSeconds, pop);
                break;

            case AbilityData.Shape.Line:
                Vector2 end = AbilityMath.LineEnd(origin, at, tower.AbilityRange);
                foreach (Enemy enemy in EnemiesWhere(e => AbilityMath.DistanceToSegment(e.transform.position, origin, end) <= ability.radius))
                    Hit(tower, enemy, damage);
                Vector2 line = end - (Vector2)origin;
                float angle = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;
                AbilityZone.Spawn((Vector2)origin + line * 0.5f, angle, new Vector3(line.magnitude, ability.radius * 2f, 1f),
                    squareSprite, color, FlashSeconds, pop);
                break;

            case AbilityData.Shape.Zone:
                // The status lands on whoever is in the zone when it is cast; the zone itself only burns.
                foreach (Enemy enemy in EnemiesWhere(e => Vector2.Distance(at, e.transform.position) <= ability.radius))
                {
                    enemy.FlashHit(pop);
                    TryApplyStatus(tower, enemy);
                }
                AbilityZone.Spawn(at, 0f, Vector3.one * ability.radius, discSprite, color, ability.zoneDuration, pop,
                    ability.radius, damage);
                break;
        }
    }

    // Collected up front because damage can remove enemies from Enemy.Active mid-loop.
    static List<Enemy> EnemiesWhere(System.Predicate<Enemy> match) => Enemy.Active.FindAll(match);

    // Flash first, so an enemy this hit kills is still flashing and stays on screen for the hit-stop.
    static void Hit(Tower tower, Enemy enemy, float damage)
    {
        enemy.FlashHit(tower.Captain.ability.hitStopSeconds);
        enemy.TakeDamage(damage);
        if (!enemy.IsTargetable) return;
        enemy.Knockback(tower.Captain.ability.knockbackDistance);
        TryApplyStatus(tower, enemy);
    }

    static void TryApplyStatus(Tower tower, Enemy enemy)
    {
        if (Random.value < tower.Captain.ability.statusChance)
            enemy.ApplyStatus(StatusRules.ForElement(tower.Captain.element), tower.SoldierPower);
    }
}
