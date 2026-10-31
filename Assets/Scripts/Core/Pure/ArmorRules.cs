// Enemy armor types and how much damage each soldier class deals to them (item: armor counters).
// Soldier classes are indexed in the order of the Knights, Archers, Mages, Rogues tags, so this
// stays free of gameplay types. Elements no longer change damage; they only bring their statuses.
public enum ArmorType { Unarmored, Armored, Swift }

public static class ArmorRules
{
    public const int ClassCount = 4;

    // Rows are armor types, columns are Knight, Archer, Mage, Rogue.
    // Armored resists Knights and Mages and is cut open by Rogues; Swift dodges Archers and Rogues
    // and is caught by a Mage's splash.
    static readonly float[,] Multipliers =
    {
        { 1f, 1f, 1f, 1f },
        { 0.65f, 1f, 0.65f, 1.4f },
        { 1f, 0.65f, 1.4f, 0.65f },
    };

    // Damage multiplier for a soldier class (by index) against an armor type; 1 for anything unknown.
    public static float DamageMultiplier(ArmorType armor, int classIndex)
    {
        int row = (int)armor;
        if (row < 0 || row >= Multipliers.GetLength(0) || classIndex < 0 || classIndex >= ClassCount) return 1f;
        return Multipliers[row, classIndex];
    }

    public static string NameOf(ArmorType armor) => armor switch
    {
        ArmorType.Armored => "Armored",
        ArmorType.Swift => "Swift",
        _ => "Unarmored",
    };
}
