using UnityEngine;

// Ordered round the cycle (ElementRules): each element beats the next and loses to the one before.
public enum Element { Water, Fire, Ice, Grass }

public enum CaptainRole { Attacker, Buffer, Healer }

// Things a captain can like or dislike: soldier classes and play styles.
public enum CaptainTag { Knights, Archers, Mages, Rogues, Leaks, Casualties, Selling, Ability, Flawless, Spending, Kills, Boss }

[CreateAssetMenu(menuName = "Babel/Captain", fileName = "Captain")]
public class CaptainData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Captain";
    public Element element;
    public CaptainRole role;
    public Color color = Color.white;
    public string abilityName;
    [TextArea] public string abilityDescription;
    [Tooltip("The special ability fired from the meter. Without one, the tower has no special.")]
    public AbilityData ability;

    [Header("Art")]
    [Tooltip("The captain's tower sprite. Without one the tower is a square in the captain colour.")]
    public Sprite sprite;
    [Tooltip("Tint the sprite with the captain colour (for the shared, pale generic sprite).")]
    public bool tintSprite;
    [Tooltip("Head-and-shoulders art for menus and dialogue, drawn as painted (never tinted).")]
    public Sprite portrait;

    [Header("Stats")]
    [Tooltip("Base damage of each soldier act, before class and level scaling.")]
    public float power = 10f;
    [Tooltip("Bonus tiles added to the average reach of the soldier classes in this captain's slots (0 for none).")]
    public float range = 0f;
    [Tooltip("Soldier acts per second (the wait after a soldier returns is 1 / speed).")]
    public float speed = 1f;
    public int health = 100;
    [Tooltip("Most Gold the tower can hold in its own reserve.")]
    public int resourceCapacity = 20;
    [Tooltip("Gold per second drawn from the shared pool at Normal draw rate.")]
    public float resourceRegen = 2f;
    public float specialChargeRate = 1f;
    public int level = 1;

    [Header("Relationship")]
    [Tooltip("Scales the approval lost when the player refuses this captain's requests (GDD: varies by captain).")]
    public float refusalTemper = 1f;
    public CaptainTag[] likes = new CaptainTag[3];
    public CaptainTag[] dislikes = new CaptainTag[3];

    [Header("Gifts")]
    [Tooltip("Items this captain likes. Items in neither list are neutral.")]
    public GiftData[] likedGifts = new GiftData[0];
    public GiftData[] dislikedGifts = new GiftData[0];
    [Tooltip("What the captain says when the gift window opens. Blank uses a default line.")]
    [TextArea] public string giftGreeting;
    [TextArea] public string giftLikedLine;
    [TextArea] public string giftNeutralLine;
    [TextArea] public string giftDislikedLine;

    public GiftReaction ReactionTo(GiftData gift)
    {
        if (gift == null) return GiftReaction.Neutral;
        if (System.Array.IndexOf(likedGifts, gift) >= 0) return GiftReaction.Liked;
        if (System.Array.IndexOf(dislikedGifts, gift) >= 0) return GiftReaction.Disliked;
        return GiftReaction.Neutral;
    }

    public string GiftGreeting => Line(giftGreeting, "Oh, is that for me?");

    public string GiftLine(GiftReaction reaction) => reaction switch
    {
        GiftReaction.Liked => Line(giftLikedLine, "This is exactly what I wanted. Thank you!"),
        GiftReaction.Disliked => Line(giftDislikedLine, "Oh. Well. I suppose it's the thought that counts."),
        _ => Line(giftNeutralLine, "That's kind of you. Thanks."),
    };

    static string Line(string line, string fallback) => string.IsNullOrWhiteSpace(line) ? fallback : line;
}
