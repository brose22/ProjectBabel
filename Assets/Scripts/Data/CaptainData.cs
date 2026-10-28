using UnityEngine;

public enum Element { Water, Fire, Earth, Air }

public enum CaptainRole { Attacker, Buffer, Healer }

// Things a captain can like or dislike: soldier classes and play styles.
public enum CaptainTag { Knights, Archers, Mages, Rogues, Leaks, Casualties, Selling, Ability, Stable, Moving }

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

    [Header("Stats")]
    [Tooltip("Base damage of each soldier act, before class and level scaling.")]
    public float power = 10f;
    [Tooltip("Radius in tiles that soldiers will engage enemies within.")]
    public float range = 2f;
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
    public CaptainTag[] likes = new CaptainTag[3];
    public CaptainTag[] dislikes = new CaptainTag[3];
}
