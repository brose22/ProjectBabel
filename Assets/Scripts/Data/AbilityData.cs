using UnityEngine;

// A captain's special ability: one per captain, fired by the player when the meter is full.
// The shape picks how it finds enemies; the element status comes from the captain.
[CreateAssetMenu(menuName = "Babel/Ability", fileName = "Ability")]
public class AbilityData : ScriptableObject
{
    public enum Shape
    {
        // Every enemy within radius of the target.
        Area,
        // The target only.
        Single,
        // Every enemy along a line from the tower through the target, out to the ability range.
        Line,
        // A patch of ground at the target that hurts enemies standing in it.
        Zone,
        // Heals and boosts every working tower on the map and their soldiers. Needs no enemy target.
        Support,
    }

    public string displayName = "Ability";
    [TextArea] public string description;
    [Tooltip("Small art for the ability button and tooltips, drawn as painted.")]
    public Sprite icon;

    [Header("Meter")]
    [Tooltip("Seconds to fill the meter at a special charge rate of 1. It only fills while enemies are on the field.")]
    public float chargeSeconds = 20f;

    [Header("Targeting")]
    public Shape shape = Shape.Area;
    [Tooltip("Multiplies the captain's range for picking a target; a Line also uses it as its length.")]
    public float rangeMultiplier = 1f;
    [Tooltip("Area and Zone radius, or half the width of a Line, in tiles.")]
    public float radius = 1f;

    [Header("Effect")]
    [Tooltip("Damage as a multiple of the tower's soldier power. For a Zone this is per second.")]
    public float damageMultiplier = 2f;
    [Tooltip("Pushes each enemy hit back along the path, in tiles.")]
    public float knockbackDistance;
    [Tooltip("How long a Zone stays on the ground, in seconds.")]
    public float zoneDuration = 4f;
    [Tooltip("Chance that each enemy hit gets the captain's element status. Bond ranks will raise it later.")]
    [Range(0f, 1f)] public float statusChance = 0.3f;

    [Header("Support shape")]
    [Tooltip("Fraction of max health restored to every tower and to each of its soldiers.")]
    [Range(0f, 1f)] public float healFraction;
    [Tooltip("Stat boost given to every tower and its soldiers until the wave ends. Heal and None give no boost.")]
    public SupportEffect buffEffect;
    [Tooltip("Size of the boost (0.5 = 50%). Casting again in the same wave adds to it.")]
    public float buffAmount;

    public bool HasBuff => buffEffect is not (SupportEffect.None or SupportEffect.Heal) && buffAmount > 0f;

    [Header("Feel")]
    [Tooltip("Real-time seconds the whole match freezes when the ability lands, while its effect plays. 0 turns it off.")]
    public float hitStopSeconds = 0.15f;
    [Tooltip("How far the camera jolts when the ability is used, in tiles (1 pixel is 0.0625). 0 turns it off.")]
    public float cameraShake = 0.1f;
    [Tooltip("Real-time seconds the camera shake takes to settle.")]
    public float cameraShakeSeconds = 0.25f;
}
