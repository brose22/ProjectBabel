using UnityEngine;

[CreateAssetMenu(menuName = "Babel/Enemy", fileName = "Enemy")]
public class EnemyData : ScriptableObject
{
    public string displayName = "Enemy";
    public Color color = Color.red;
    [Tooltip("Body size in tiles.")]
    public float size = 0.6f;

    [Header("Stats")]
    public float maxHealth = 30f;
    [Tooltip("Tiles per second.")]
    public float moveSpeed = 1.2f;
    [Tooltip("Gold added to the shared pool on a kill (GDD: 5-10).")]
    public int goldReward = 7;
    [Tooltip("Global health lost when this enemy reaches the base.")]
    public int leakDamage = 1;

    [Header("Attacking soldiers")]
    public float attackDamage = 6f;
    [Tooltip("Enemies stop and fight any deployed soldier within this many tiles.")]
    public float attackRange = 0.6f;
    public float attackInterval = 1f;
}
