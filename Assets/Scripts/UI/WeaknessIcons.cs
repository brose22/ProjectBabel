using UnityEngine;
using UnityEngine.UI;

// The weak-to art (Resources/Weakness): one 16px icon per damage type and element, showing what
// beats an enemy (Advantage). Shown above enemies on the field and in the next-wave enemy tips.
public static class WeaknessIcons
{
    const string Folder = "Weakness/";
    static readonly Sprite[] typeIcons = new Sprite[DamageTypeRules.TypeCount];
    static readonly Sprite[] elementIcons = new Sprite[ElementRules.ElementCount];

    public static Sprite For(DamageType type) => Load(typeIcons, (int)type, type.ToString());
    public static Sprite For(Element element) => Load(elementIcons, (int)element, element.ToString());

    public static DamageType WeakType(EnemyData enemy) => DamageTypeRules.WeakTo(enemy.damageType);
    public static Element WeakElement(Element element) => (Element)ElementRules.WeakTo((int)element);

    // "Weak to" with the type and element icons, as a row in a UI layout.
    public static RectTransform Row(Transform parent, EnemyData enemy, Element element, float iconSize = 32f)
    {
        RectTransform row = UIKit.Group(parent, "Weak To");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 4).childAlignment = TextAnchor.MiddleLeft;
        UIKit.Label(row, "Weak to", 11, color: UIKit.MutedTextColor);
        UIKit.Icon(row, For(WeakType(enemy)), iconSize);
        UIKit.Icon(row, For(WeakElement(element)), iconSize);
        return row;
    }

    static Sprite Load(Sprite[] cache, int index, string name)
    {
        if (index < 0 || index >= cache.Length) return null;
        if (cache[index] == null) cache[index] = Resources.Load<Sprite>(Folder + name.ToLowerInvariant());
        return cache[index];
    }
}
