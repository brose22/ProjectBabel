using System.Collections.Generic;
using UnityEngine;

// Resolves a captain's ability against the enemies on the field. Damage scales with the
// tower's soldier power; each enemy hit may also get the captain's element status.
public static class AbilityCaster
{
    const float FlashSeconds = 0.35f;
    const float FlashAlpha = 0.55f;
    // Disc sprites are two units across, so a disc scaled by r has radius r.
    const float SingleTargetFlashRadius = 0.45f;

    public static void Cast(Tower tower, Enemy target, Sprite squareSprite, Sprite discSprite)
    {
        AbilityData ability = tower.Captain.ability;
        float damage = tower.SoldierPower * ability.damageMultiplier;
        Vector3 origin = tower.transform.position;
        Vector3 at = target.transform.position;
        Color color = StatusRules.TintOf(StatusRules.ForElement(tower.Captain.element));
        color.a = FlashAlpha;

        switch (ability.shape)
        {
            case AbilityData.Shape.Single:
                Hit(tower, target, damage);
                AbilityZone.Spawn(at, 0f, Vector3.one * SingleTargetFlashRadius, discSprite, color, FlashSeconds);
                break;

            case AbilityData.Shape.Area:
                foreach (Enemy enemy in EnemiesWhere(e => Vector2.Distance(at, e.transform.position) <= ability.radius))
                    Hit(tower, enemy, damage);
                AbilityZone.Spawn(at, 0f, Vector3.one * ability.radius, discSprite, color, FlashSeconds);
                break;

            case AbilityData.Shape.Line:
                Vector2 end = AbilityMath.LineEnd(origin, at, tower.AbilityRange);
                foreach (Enemy enemy in EnemiesWhere(e => AbilityMath.DistanceToSegment(e.transform.position, origin, end) <= ability.radius))
                    Hit(tower, enemy, damage);
                Vector2 line = end - (Vector2)origin;
                float angle = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;
                AbilityZone.Spawn((Vector2)origin + line * 0.5f, angle, new Vector3(line.magnitude, ability.radius * 2f, 1f),
                    squareSprite, color, FlashSeconds);
                break;

            case AbilityData.Shape.Zone:
                // The status lands on whoever is in the zone when it is cast; the zone itself only burns.
                foreach (Enemy enemy in EnemiesWhere(e => Vector2.Distance(at, e.transform.position) <= ability.radius))
                    TryApplyStatus(tower, enemy);
                AbilityZone.Spawn(at, 0f, Vector3.one * ability.radius, discSprite, color, ability.zoneDuration,
                    ability.radius, damage);
                break;
        }
    }

    // Collected up front because damage can remove enemies from Enemy.Active mid-loop.
    static List<Enemy> EnemiesWhere(System.Predicate<Enemy> match) => Enemy.Active.FindAll(match);

    static void Hit(Tower tower, Enemy enemy, float damage)
    {
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
