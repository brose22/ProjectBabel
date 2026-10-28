using UnityEngine;

[CreateAssetMenu(menuName = "Babel/Soldier Class", fileName = "SoldierClass")]
public class SoldierClassData : ScriptableObject
{
    public string displayName = "Soldier";
    public Color color = Color.white;

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
    [Tooltip("Tiles per second.")]
    public float moveSpeed = 4f;
}
