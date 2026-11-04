using System;
using System.Collections.Generic;
using UnityEngine;

// A captain the player owns (item: captain templates): rolled once from a CaptainData template,
// then kept with its level, XP and bond. A plain class so it saves as JSON; the id is what saves
// and lookups key on. Art, the ability and gifts are pools on the template, so the captain keeps
// indices and names into them and finds the template again by name after loading.
[Serializable]
public class Captain
{
    public string id;
    public string templateName;
    public string displayName;
    public CaptainRole role;
    public Element element;
    // The damage type of the captain's ability, and of the tower when enemies hit it.
    public DamageType damageType;
    public int portraitIndex = -1;
    public int abilityIndex = -1;

    public float power;
    public float range;
    public float speed;
    public int health;
    public float defense;
    public float defensePerLevel;
    public float refusalTemper = 1f;
    // -1 on captains saved before traits existed; Trait then works one out from the id.
    public int traitId = -1;

    public CaptainTag[] likes = new CaptainTag[0];
    public CaptainTag[] dislikes = new CaptainTag[0];
    // Gift asset names.
    public string[] likedGifts = new string[0];
    public string[] dislikedGifts = new string[0];
    public string giftGreeting;
    public string giftLikedLine;
    public string giftNeutralLine;
    public string giftDislikedLine;

    // The lowest level the captain can be; XP raises it from here.
    public int level = 1;
    public int xp;
    public float approval;
    // The best bond rank reached; ranks are never lost once reached, even if approval falls.
    public int bondRank;

    [NonSerialized] CaptainData template;

    public CaptainData Template => template;

    // Reconnects a loaded captain to its template.
    public void Bind(CaptainData captainTemplate) => template = captainTemplate;

    public static Captain Roll(CaptainData template, int seed)
    {
        CaptainRoll roll = CaptainRoller.Roll(template.RollSpec(), seed);
        var captain = new Captain
        {
            id = roll.id,
            templateName = template.name,
            displayName = roll.nameIndex >= 0 ? template.names[roll.nameIndex] : template.role.ToString(),
            role = template.role,
            element = (Element)roll.element,
            damageType = roll.damageType,
            portraitIndex = roll.portraitIndex,
            abilityIndex = roll.abilityIndex,
            power = roll.power,
            range = roll.range,
            speed = roll.speed,
            health = roll.health,
            defense = roll.defense,
            defensePerLevel = roll.defensePerLevel,
            refusalTemper = roll.refusalTemper,
            traitId = (int)roll.trait,
            likes = roll.likes.ConvertAll(t => (CaptainTag)t).ToArray(),
            dislikes = roll.dislikes.ConvertAll(t => (CaptainTag)t).ToArray(),
            likedGifts = GiftNames(template, roll.likedGifts),
            dislikedGifts = GiftNames(template, roll.dislikedGifts),
            giftGreeting = Line(template.giftGreetings, roll.greetingLine),
            giftLikedLine = Line(template.giftLikedLines, roll.likedLine),
            giftNeutralLine = Line(template.giftNeutralLines, roll.neutralLine),
            giftDislikedLine = Line(template.giftDislikedLines, roll.dislikedLine),
        };
        captain.Bind(template);
        return captain;
    }

    static string[] GiftNames(CaptainData template, List<int> ids) =>
        ids.ConvertAll(id => template.GiftForId(id)).FindAll(g => g != null).ConvertAll(g => g.name).ToArray();

    static string Line(string[] lines, int index) => index >= 0 && index < lines.Length ? lines[index] : "";

    public CaptainTrait Trait
    {
        get
        {
            if (traitId < 0) traitId = (int)CaptainTraits.ForLegacyId(id);
            return (CaptainTrait)traitId;
        }
    }

    // A Melee Mage, a Magic Knight: a damage type that isn't the role's usual one.
    public bool IsSpecialClass => CaptainRoller.IsSpecialClass(role, damageType);

    public string ClassName => IsSpecialClass ? $"{damageType} {role}" : role.ToString();

    public Sprite Portrait =>
        template != null && portraitIndex >= 0 && portraitIndex < template.portraits.Length ? template.portraits[portraitIndex].portrait : null;

    // The special fired from the meter. Without one, the tower has no special.
    public AbilityData Ability =>
        template != null && abilityIndex >= 0 && abilityIndex < template.abilities.Length ? template.abilities[abilityIndex] : null;

    public string AbilityName => Ability != null ? Ability.displayName : "";
    public string AbilityDescription => Ability != null ? Ability.description : "";

    // Heals or buffs rather than hitting enemies.
    public bool IsSupport => Ability != null && Ability.shape == AbilityData.Shape.Support;

    public Sprite Sprite => template != null ? template.sprite : null;
    public bool TintSprite => template == null || template.tintSprite;
    public Color Color => TowerColor(element);

    public int ResourceCapacity => template != null ? template.resourceCapacity : 24;
    public float ResourceRegen => template != null ? template.resourceRegen : 1f;
    public float SpecialChargeRate => template != null ? template.specialChargeRate : 1f;

    // The tower tint for each element.
    public static Color TowerColor(Element element) => element switch
    {
        Element.Water => new Color(0.2f, 0.45f, 0.9f),
        Element.Fire => new Color(0.95f, 0.45f, 0.15f),
        Element.Ice => new Color(0.6f, 0.9f, 0.9f),
        _ => new Color(0.5f, 0.7f, 0.35f),
    };

    public GiftReaction ReactionTo(GiftData gift)
    {
        if (gift == null) return GiftReaction.Neutral;
        if (Array.IndexOf(likedGifts, gift.name) >= 0) return GiftReaction.Liked;
        if (Array.IndexOf(dislikedGifts, gift.name) >= 0) return GiftReaction.Disliked;
        return GiftReaction.Neutral;
    }

    public string GiftGreeting => Fallback(giftGreeting, "Oh, is that for me?");

    public string GiftLine(GiftReaction reaction) => reaction switch
    {
        GiftReaction.Liked => Fallback(giftLikedLine, "This is exactly what I wanted. Thank you!"),
        GiftReaction.Disliked => Fallback(giftDislikedLine, "Oh. Well. I suppose it's the thought that counts."),
        _ => Fallback(giftNeutralLine, "That's kind of you. Thanks."),
    };

    static string Fallback(string line, string fallback) => string.IsNullOrWhiteSpace(line) ? fallback : line;
}
