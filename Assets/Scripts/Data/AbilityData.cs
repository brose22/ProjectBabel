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
    }

    public string displayName = "Ability";
    [TextArea] public string description;

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
}
