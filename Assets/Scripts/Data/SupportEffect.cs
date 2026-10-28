// What a Buffer or Healer captain does for soldiers, through their aura or their ability.
// Heal restores soldier health; the rest are stat boosts.
public enum SupportEffect
{
    None,
    Heal,
    // Soldiers act more often: 0.5 means +50% act speed.
    ActSpeed,
    // Soldier hits do more damage: 0.5 means +50%.
    Damage,
    // Soldiers take less damage: 0.5 means half damage.
    Defense,
}
