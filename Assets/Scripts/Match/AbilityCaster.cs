using System.Collections.Generic;
using UnityEngine;

// Resolves a captain's ability. Attack shapes hit enemies, with damage scaling with the tower's
// soldier power and a chance of the captain's element status; the Support shape heals every
// tower and soldier and boosts them all for the rest of the wave.
public static class AbilityCaster
{
    const float FlashSeconds = 0.35f;
    const float FlashAlpha = 0.55f;
    // Disc sprites are two units across, so a disc scaled by r has radius r.
    const float SingleTargetFlashRadius = 0.45f;
    // Each tower a support ability reaches shows a ring this big.
    const float SupportFlashRadius = 0.6f;
    static readonly Color HealColor = new(0.4f, 1f, 0.5f, FlashAlpha);
    static readonly Color BoostColor = new(1f, 0.85f, 0.3f, FlashAlpha);

    // Heals every working tower and its soldiers by the ability's fraction of their max health, and
    // adds its boost to every tower for the rest of the wave (towers placed later in the wave get
    // it too). Returns the total health restored.
    public static float CastSupport(Tower caster, Sprite discSprite)
    {
        AbilityData ability = caster.Captain.ability;
        if (ability.HasBuff) Tower.AddWaveBoost(ability.buffEffect, ability.buffAmount);

        float healed = 0f;
        Color color = ability.healFraction > 0f ? HealColor : BoostColor;
        foreach (Tower tower in Tower.All)
        {
            if (tower.IsDisabled) continue;
            healed += tower.HealAll(ability.healFraction);
            AbilityZone.Spawn(tower.transform.position, 0f, Vector3.one * SupportFlashRadius, discSprite, color,
                FlashSeconds, ability.hitStopSeconds);
        }
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
                    ability.radius, damage, tower);
                break;
        }
    }

    // Collected up front because damage can remove enemies from Enemy.Active mid-loop.
    static List<Enemy> EnemiesWhere(System.Predicate<Enemy> match) => Enemy.Active.FindAll(match);

    // Flash first, so an enemy this hit kills is still flashing and stays on screen for the hit-stop.
    static void Hit(Tower tower, Enemy enemy, float damage)
    {
        enemy.FlashHit(tower.Captain.ability.hitStopSeconds);
        enemy.TakeDamage(damage, tower, damageType: tower.Captain.damageType);
        if (!enemy.IsTargetable) return;
        enemy.Knockback(tower.Captain.ability.knockbackDistance);
        TryApplyStatus(tower, enemy);
    }

    // Rolls the captain's element status on the enemy.
    static void TryApplyStatus(Tower tower, Enemy enemy)
    {
        // The flat chance rises with bond rank (GDD).
        float chance = tower.Captain.ability.statusChance + BondMath.StatusChanceBonus(tower.BondRank);
        if (Random.value < chance)
            enemy.ApplyStatus(StatusRules.ForElement(tower.Captain.element), tower.SoldierPower, tower);
    }
}
