using System;
using UnityEngine;

// Which sprite shows each gamepad button for each kind of controller. It sits in Resources and points
// at the sprites it needs, so builds carry those and not the whole icon pack. Babel > Build Pad Icon
// Library fills it in.
public class PadIconLibrary : ScriptableObject
{
    [Serializable]
    public class Set
    {
        public PadFamily family;
        [Tooltip("One sprite per PadGlyph, in the enum's order.")]
        public Sprite[] sprites;
    }

    public Set[] sets;

    public Sprite Sprite(PadFamily family, PadGlyph glyph)
    {
        foreach (Set set in sets)
            if (set.family == family) return set.sprites[(int)glyph];
        return null;
    }
}
