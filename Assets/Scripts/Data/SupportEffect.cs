// The stat a Buffer captain's special boosts on every tower and its soldiers for the rest of the
// wave. Heal and None are not stat boosts; Healers restore health through the ability's heal fraction.
public enum SupportEffect
{
    None,
    Heal,
    // Soldiers act more often: 0.5 means +50% act speed.
    ActSpeed,
    // Soldier hits do more damage: 0.5 means +50%.
    Damage,
    // Towers and soldiers take less damage: 0.5 means half damage.
    Defense,
}
