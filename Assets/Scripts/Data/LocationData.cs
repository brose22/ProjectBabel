using System.Collections.Generic;
using UnityEngine;

// One place the player can take a party from the Castle (item: location): its world map, the
// enemies it fields and the elements they use. Locations unlock in the order MatchData lists them.
[CreateAssetMenu(menuName = "Babel/Location", fileName = "Location")]
public class LocationData : ScriptableObject
{
    [Tooltip("Saved in the profile and run; don't change it once players have saves.")]
    public string id = "location";
    public string displayName = "Location";
    [TextArea] public string description;

    [Header("World map")]
    [Tooltip("Size, branching, special-node odds and gate tiers of this location's world map.")]
    public LocationMapSettings map = new();

    [Header("Enemies")]
    [Tooltip("The enemies this location fields. An authored wave enemy not in the pool is played by the pool enemy nearest it in max health. Empty plays the waves as authored.")]
    public List<EnemyData> enemies = new();
    [Tooltip("The elements this location's enemies use. An enemy with another element takes its city's element, one of these. Empty keeps every enemy's own.")]
    public List<Element> elements = new();
    [Tooltip("The enemy base's boss here. Empty uses the match's boss.")]
    public EnemyData boss;

    public LocationSpec Spec => new() { map = map, elements = elements.ConvertAll(e => (int)e).ToArray() };

    // The enemy this location fields in place of an authored one.
    public EnemyData Field(EnemyData authored)
    {
        List<EnemyData> pool = enemies.FindAll(e => e != null);
        if (authored == null || pool.Count == 0 || pool.Contains(authored)) return authored;
        return pool[LocationRules.StandIn(authored.maxHealth, pool.ConvertAll(e => e.maxHealth))];
    }

    // The element an enemy fights with in a city leaning to cityElement (-1 for none).
    public Element EnemyElement(EnemyData enemy, int cityElement) =>
        (Element)LocationRules.EnemyElement((int)enemy.element, elements.ConvertAll(e => (int)e), cityElement);
}
