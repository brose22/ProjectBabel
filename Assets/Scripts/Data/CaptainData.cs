using System;
using UnityEngine;

// Ordered round the cycle (ElementRules): each element beats the next and loses to the one before.
public enum Element { Water, Fire, Ice, Grass }

// Things a captain can like or dislike: soldier classes and play styles.
public enum CaptainTag { Knights, Archers, Mages, Rogues, Leaks, Casualties, Selling, Ability, Flawless, Spending, Kills, Boss }

// A portrait in a template's pool and the element it was drawn for (its emblem).
[Serializable]
public struct CaptainPortrait
{
    public Sprite portrait;
    public Element element;
}

// A captain template (item: captain templates): the role and the ranges and pools a captain is
// rolled from when it's recruited. The rolled captain is a Captain; this asset is never a
// character itself.
[CreateAssetMenu(menuName = "Babel/Captain Template", fileName = "CaptainTemplate")]
public class CaptainData : ScriptableObject
{
    [Header("Role")]
    public CaptainRole role;
    [Tooltip("Chance of a special class: a damage type other than the role's usual one (Knight Melee, Mage Magic, Rogue Melee, Archer Range, Healer Magic).")]
    [Range(0f, 1f)] public float specialClassChance = 0.15f;

    [Header("Pools")]
    public string[] names = new string[0];
    [Tooltip("A rolled captain gets a portrait drawn for its element when there is one, otherwise any.")]
    public CaptainPortrait[] portraits = new CaptainPortrait[0];
    [Tooltip("The special a rolled captain fires from the meter. A captain buffs or heals only through its ability.")]
    public AbilityData[] abilities = new AbilityData[0];

    [Header("Art")]
    [Tooltip("The tower sprite. Without one the tower is a square in the element colour.")]
    public Sprite sprite;
    [Tooltip("Tint the sprite with the captain's element colour (for the shared, pale generic sprite).")]
    public bool tintSprite = true;

    [Header("Stat ranges")]
    [Tooltip("Base damage of each soldier act, before class and level scaling.")]
    public StatRange power = new(8f, 12f);
    [Tooltip("Bonus tiles added to the average reach of the soldier classes in the captain's slots.")]
    public StatRange range = new(0f, 0f);
    [Tooltip("Soldier acts per second (the wait after a soldier returns is 1 / speed).")]
    public StatRange speed = new(0.8f, 1.2f);
    public StatRange health = new(100f, 100f);
    [Tooltip("Flat reduction to each enemy hit on the tower and its soldiers, after the advantage multipliers. Hits never drop below 1.")]
    public StatRange defense = new(1f, 1f);
    [Tooltip("Defense gained for each level above 1.")]
    public StatRange defensePerLevel = new(0.25f, 0.25f);

    [Header("Economy")]
    [Tooltip("Most Gold the tower can hold in its own reserve. The reserve only pays for respawning soldiers, so this is a few respawns' worth.")]
    public int resourceCapacity = 24;
    [Tooltip("Gold per second drawn from the shared pool at Normal draw rate, until the reserve is full.")]
    public float resourceRegen = 1f;
    public float specialChargeRate = 1f;

    [Header("Relationship")]
    [Tooltip("Scales the approval lost when the player refuses the captain's requests.")]
    public StatRange refusalTemper = new(0.75f, 1.25f);
    [Tooltip("A rolled captain likes 3 of these and dislikes 3 of the next pool, never the same tag in both.")]
    public CaptainTag[] likePool = new CaptainTag[0];
    public CaptainTag[] dislikePool = new CaptainTag[0];

    [Header("Gifts")]
    [Tooltip("A rolled captain likes 3 of these items and dislikes 2 of the next pool. Items in neither are neutral.")]
    public GiftData[] likedGiftPool = new GiftData[0];
    public GiftData[] dislikedGiftPool = new GiftData[0];
    [Tooltip("What the captain says when the gift window opens; one is rolled. Empty uses a default line.")]
    [TextArea] public string[] giftGreetings = new string[0];
    [TextArea] public string[] giftLikedLines = new string[0];
    [TextArea] public string[] giftNeutralLines = new string[0];
    [TextArea] public string[] giftDislikedLines = new string[0];

    // Rolls a new captain from this template. The same seed gives the same captain.
    public Captain Roll(int seed) => Captain.Roll(this, seed);

    public CaptainRollSpec RollSpec() => new()
    {
        role = role,
        specialClassChance = specialClassChance,
        power = power,
        range = range,
        speed = speed,
        health = health,
        defense = defense,
        defensePerLevel = defensePerLevel,
        refusalTemper = refusalTemper,
        nameCount = names.Length,
        portraitElements = Array.ConvertAll(portraits, p => (int)p.element),
        abilityCount = abilities.Length,
        likePool = Array.ConvertAll(likePool, t => (int)t),
        dislikePool = Array.ConvertAll(dislikePool, t => (int)t),
        likedGiftPool = GiftIds(likedGiftPool),
        dislikedGiftPool = GiftIds(dislikedGiftPool),
        greetingCount = giftGreetings.Length,
        likedLineCount = giftLikedLines.Length,
        neutralLineCount = giftNeutralLines.Length,
        dislikedLineCount = giftDislikedLines.Length,
    };

    // Gift ids for the roller: the index in the liked pool, or past its end for the disliked pool,
    // so the same item in both pools gets one id.
    int[] GiftIds(GiftData[] pool) => Array.ConvertAll(pool, GiftId);

    int GiftId(GiftData gift)
    {
        int liked = Array.IndexOf(likedGiftPool, gift);
        return liked >= 0 ? liked : likedGiftPool.Length + Array.IndexOf(dislikedGiftPool, gift);
    }

    public GiftData GiftForId(int id) =>
        id < 0 ? null : id < likedGiftPool.Length ? likedGiftPool[id] : id - likedGiftPool.Length < dislikedGiftPool.Length ? dislikedGiftPool[id - likedGiftPool.Length] : null;
}
