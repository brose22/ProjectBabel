using System;
using System.Collections.Generic;

// A captain's class (item: captain templates). It sets the usual damage type but doesn't fix it,
// and a captain buffs or heals only because its ability does.
public enum CaptainRole { Knight, Mage, Rogue, Archer, Healer }

// A stat's range on a captain template. Rolls land anywhere between min and max.
[Serializable]
public struct StatRange
{
    public float min;
    public float max;

    public StatRange(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    public bool Contains(float value) => value >= Math.Min(min, max) && value <= Math.Max(min, max);
}

// What a captain template offers to roll from, as counts, ranges and int ids so this stays free
// of game types. Elements are Element enum values; tags and gifts are whatever ids the caller uses.
public class CaptainRollSpec
{
    public CaptainRole role;
    // Chance the damage type is not the role's usual one, making a special class.
    public float specialClassChance;
    public StatRange power, range, speed, health, defense, defensePerLevel, refusalTemper;
    public int nameCount;
    // The element each portrait was drawn for; a captain gets a portrait of its own element when there is one.
    public int[] portraitElements = Array.Empty<int>();
    public int abilityCount;
    public int[] likePool = Array.Empty<int>();
    public int[] dislikePool = Array.Empty<int>();
    public int[] likedGiftPool = Array.Empty<int>();
    public int[] dislikedGiftPool = Array.Empty<int>();
    public int greetingCount, likedLineCount, neutralLineCount, dislikedLineCount;
    public int likes = 3, dislikes = 3, likedGifts = 3, dislikedGifts = 2;
}

// One rolled captain. Indices point into the template's pools and are -1 when a pool is empty.
public class CaptainRoll
{
    public string id;
    public int nameIndex, portraitIndex, abilityIndex;
    public int element;
    public DamageType damageType;
    public float power, range, speed, defense, defensePerLevel, refusalTemper;
    public int health;
    public List<int> likes = new(), dislikes = new(), likedGifts = new(), dislikedGifts = new();
    public int greetingLine, likedLine, neutralLine, dislikedLine;
}

// Rolls captains from templates. The same spec and seed always give the same captain.
public static class CaptainRoller
{
    // The damage type a role usually has; any other is a special class.
    public static DamageType UsualDamageType(CaptainRole role) => role switch
    {
        CaptainRole.Mage => DamageType.Magic,
        CaptainRole.Archer => DamageType.Range,
        CaptainRole.Healer => DamageType.Magic,
        _ => DamageType.Melee,
    };

    public static bool IsSpecialClass(CaptainRole role, DamageType damageType) => damageType != UsualDamageType(role);

    // The usual type, or with specialChance one of the other two, evenly.
    public static DamageType RollDamageType(CaptainRole role, float specialChance, Random random)
    {
        DamageType usual = UsualDamageType(role);
        if (random.NextDouble() >= specialChance) return usual;
        int step = 1 + random.Next(2);
        return (DamageType)(((int)usual + step) % 3);
    }

    public static CaptainRoll Roll(CaptainRollSpec spec, int seed)
    {
        var random = new Random(seed);
        var bytes = new byte[16];
        random.NextBytes(bytes);
        var roll = new CaptainRoll
        {
            id = new Guid(bytes).ToString("N"),
            nameIndex = Index(spec.nameCount, random),
            element = random.Next(ElementRules.ElementCount),
            damageType = RollDamageType(spec.role, spec.specialClassChance, random),
            abilityIndex = Index(spec.abilityCount, random),
            power = Between(spec.power, random),
            range = Between(spec.range, random),
            speed = Between(spec.speed, random),
            health = (int)Math.Round(Between(spec.health, random)),
            defense = Between(spec.defense, random),
            defensePerLevel = Between(spec.defensePerLevel, random),
            refusalTemper = Between(spec.refusalTemper, random),
        };
        roll.portraitIndex = PortraitFor(spec.portraitElements, roll.element, random);
        roll.likes = Pick(spec.likePool, spec.likes, null, random);
        roll.dislikes = Pick(spec.dislikePool, spec.dislikes, roll.likes, random);
        roll.likedGifts = Pick(spec.likedGiftPool, spec.likedGifts, null, random);
        roll.dislikedGifts = Pick(spec.dislikedGiftPool, spec.dislikedGifts, roll.likedGifts, random);
        roll.greetingLine = Index(spec.greetingCount, random);
        roll.likedLine = Index(spec.likedLineCount, random);
        roll.neutralLine = Index(spec.neutralLineCount, random);
        roll.dislikedLine = Index(spec.dislikedLineCount, random);
        return roll;
    }

    // Rounded to two decimals so stats read cleanly in menus.
    static float Between(StatRange range, Random random)
    {
        float min = Math.Min(range.min, range.max), max = Math.Max(range.min, range.max);
        float value = min + (float)random.NextDouble() * (max - min);
        return Math.Min(max, Math.Max(min, (float)Math.Round(value, 2)));
    }

    static int Index(int count, Random random) => count > 0 ? random.Next(count) : -1;

    // A portrait drawn for the element if there is one, otherwise any.
    static int PortraitFor(int[] portraitElements, int element, Random random)
    {
        var matching = new List<int>();
        for (int i = 0; i < portraitElements.Length; i++)
            if (portraitElements[i] == element) matching.Add(i);
        if (matching.Count > 0) return matching[random.Next(matching.Count)];
        return Index(portraitElements.Length, random);
    }

    // Up to count distinct ids from the pool, leaving out any in exclude, in pool order.
    static List<int> Pick(int[] pool, int count, List<int> exclude, Random random)
    {
        var candidates = new List<int>();
        foreach (int id in pool)
            if (!candidates.Contains(id) && (exclude == null || !exclude.Contains(id))) candidates.Add(id);
        var picked = new List<int>();
        while (picked.Count < count && candidates.Count > 0)
        {
            int i = random.Next(candidates.Count);
            picked.Add(candidates[i]);
            candidates.RemoveAt(i);
        }
        picked.Sort((a, b) => Array.IndexOf(pool, a).CompareTo(Array.IndexOf(pool, b)));
        return picked;
    }
}
