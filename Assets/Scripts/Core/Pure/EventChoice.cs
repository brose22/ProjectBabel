using System;
using System.Collections.Generic;

// One option at a world map event and what it changes. XP and approval go to every party captain
// who hasn't walked out.
[Serializable]
public class EventChoice
{
    public string label = "Move on";
    public int gold;
    public int health;
    public int xp;
    public float approval;

    // A choice can't cost more Gold than the run has, or take the last of its health.
    public bool CanTake(int runGold, int runHealth) => runGold + gold >= 0 && runHealth + health > 0;

    // The effects in a short line, e.g. "+120 Gold, -3 health"; empty when nothing changes.
    public string Describe()
    {
        var parts = new List<string>();
        if (gold != 0) parts.Add($"{gold:+0;-0} Gold");
        if (health != 0) parts.Add($"{health:+0;-0} health");
        if (xp != 0) parts.Add($"{xp:+0;-0} XP each");
        if (approval != 0f) parts.Add($"{approval:+0;-0} approval each");
        return string.Join(", ", parts);
    }
}
