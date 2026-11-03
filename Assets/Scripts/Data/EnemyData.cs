using UnityEngine;

[CreateAssetMenu(menuName = "Babel/Enemy", fileName = "Enemy")]
public class EnemyData : ScriptableObject
{
    public string displayName = "Enemy";
    public Color color = Color.red;
    [Tooltip("Optional art, drawn for the standard enemy size (0.6) and scaled by size. Without it the enemy is a square in its colour.")]
    public Sprite sprite;
    [Tooltip("Optional steps of the walk cycle, shown in turn with the standing sprite between them.")]
    public Sprite[] walkFrames;
    [Tooltip("Head-and-shoulders art for menus and the enemy codex.")]
    public Sprite portrait;
    [Tooltip("Body size in tiles.")]
    public float size = 0.6f;
    [Tooltip("A tougher enemy: it gets a wider health bar with a gold marker and an arrival banner.")]
    public bool elite;

    [Header("Stats")]
    public float maxHealth = 30f;
    [Tooltip("Tiles per second.")]
    public float moveSpeed = 1.2f;
    [Tooltip("Gold added to the shared pool on a kill (GDD: 5-10).")]
    public int goldReward = 7;
    [Tooltip("XP for the captain whose tower lands the killing blow.")]
    public int xpReward = 10;
    [Tooltip("Global health lost when this enemy reaches the base.")]
    public int leakDamage = 1;

    [Header("Attacking soldiers")]
    public float attackDamage = 6f;
    [Tooltip("Enemies stop and fight any deployed soldier within this many tiles.")]
    public float attackRange = 0.6f;
    public float attackInterval = 1f;

    [Header("Attacking towers")]
    [Tooltip("Damage dealt to a tower in passing. 0 means this enemy never hits towers.")]
    public float towerDamage;
    [Tooltip("Enemies hit the closest working tower within this many tiles without stopping.")]
    public float towerAttackRange = 1.5f;
    public float towerAttackInterval = 1f;

    [Header("Ranged")]
    [Tooltip("Shoots deployed soldiers within this many tiles as it walks, using the soldier attack damage and interval. 0 means melee only.")]
    public float rangedAttackRange;

    [Header("Matchups")]
    [Tooltip("How this enemy attacks and what it is weak to: Melee beats Range, Range beats Magic, Magic beats Melee. See DamageTypeRules.")]
    public DamageType damageType;
    [Tooltip("Stacks with the damage type both ways: Water beats Fire, Fire beats Ice, Ice beats Grass, Grass beats Water. See ElementRules.")]
    public Element element;
}
