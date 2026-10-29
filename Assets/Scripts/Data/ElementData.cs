using UnityEngine;

// How one element is presented: its name, colour, icon and the status its captains inflict.
// Gameplay still keys off the Element enum; these assets are for UI and descriptions.
[CreateAssetMenu(menuName = "Babel/Element", fileName = "Element")]
public class ElementData : ScriptableObject
{
    [Tooltip("The element this asset describes.")]
    public Element element;
    public string displayName = "Element";
    [Tooltip("UI colour for the element, matching the status tint the game draws.")]
    public Color color = Color.white;
    [Tooltip("Small art for element tags and tooltips, drawn as painted.")]
    public Sprite icon;

    [Header("Status")]
    [Tooltip("Name of the status this element's captains inflict, for tooltips.")]
    public string statusName;
    [TextArea] public string description;
}
