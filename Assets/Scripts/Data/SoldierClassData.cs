using UnityEngine;

[CreateAssetMenu(menuName = "Babel/Soldier Class", fileName = "SoldierClass")]
public class SoldierClassData : ScriptableObject
{
    public string displayName = "Soldier";
    public Color color = Color.white;
    [Tooltip("Optional art, drawn at 16 px per unit. Without it the soldier is a square in its colour.")]
    public Sprite sprite;
    [Tooltip("Head-and-shoulders art for menus.")]
    public Sprite portrait;

    [Header("Gold")]
    [Tooltip("Gold taken from the tower reserve each time a soldier leaves to act.")]
    public int actCost = 1;
    [Tooltip("Gold taken from the tower reserve to bring a dead soldier back.")]
    public int respawnCost = 4;

    [Header("Combat")]
    public float maxHealth = 20f;
    [Tooltip("Multiplies the captain's power for each hit.")]
    public float damageMultiplier = 1f;
    [Tooltip("How close in tiles the soldier must get to hit.")]
    public float attackRange = 0.4f;
    [Tooltip("How far in tiles this soldier lets its captain reach. A captain's range is the average of its soldier slots' reach, plus the captain's own range bonus.")]
    public float reach = 2f;
    [Tooltip("Tiles per second.")]
    public float moveSpeed = 4f;
    [Tooltip("The kind of tile around the captain this soldier picks to wait on.")]
    public HomeTile homeTile = HomeTile.Any;

    // Assets made before these fields existed read Melee and zeros, which keeps the original
    // hit-and-return behaviour, so every zero below means "off".
    [Header("Damage Type")]
    [Tooltip("Fixed by class (Knight and Rogue Melee, Archer Range, Mage Magic) and decides how the soldier attacks: Melee hits in person, Range fires a homing projectile, Magic damages every enemy in the splash radius.")]
    public DamageType damageType = DamageType.Melee;
    [Tooltip("Tiles a melee hit pushes the enemy back along its path. 0 = no knockback.")]
    public float knockbackDistance = 0f;
    [Tooltip("Chance that a melee hit pushes the enemy back.")]
    [Range(0f, 1f)] public float knockbackChance = 0.25f;
    [Tooltip("Ranged projectile speed in tiles per second. 0 = hits instantly.")]
    public float projectileSpeed = 10f;
    [Tooltip("Radius in tiles around the target that a Magic hit also damages. 0 = single target.")]
    public float splashRadius = 0f;
}
