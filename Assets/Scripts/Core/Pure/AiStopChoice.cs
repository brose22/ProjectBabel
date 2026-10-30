using System.Collections.Generic;

// What the AI does at a rest, shop or event.
public static class AiStopChoice
{
    // Rest unless already at full health.
    public static bool ShouldRest(int health, int maxHealth) => health < maxHealth;

    // Buy a gift for the captain with the least approval among those who can take one, keeping
    // reserve Gold back for the matches ahead. Returns an index into approvals, or -1 to stop shopping.
    public static int PickGiftTarget(IReadOnlyList<float> approvals, IReadOnlyList<bool> canTake, int gold, int giftCost, int reserve)
    {
        if (gold - giftCost < reserve) return -1;
        int best = -1;
        for (int i = 0; i < approvals.Count; i++)
        {
            if (!canTake[i]) continue;
            if (best < 0 || approvals[i] < approvals[best]) best = i;
        }
        return best;
    }

    // The value of an event choice to a run at this health; higher is better.
    public static float EventValue(EventChoice choice, int health, int maxHealth, int partySize)
    {
        // Health is scarce when low, so each point is worth more.
        float healthWeight = health < maxHealth * 0.5f ? 6f : 3f;
        return choice.gold * 0.05f + choice.health * healthWeight + choice.xp * 0.1f * partySize + choice.approval * 0.5f * partySize;
    }

    // The index of the best choice that can be taken, or -1 when none can.
    public static int PickEvent(IReadOnlyList<EventChoice> choices, int gold, int health, int maxHealth, int partySize)
    {
        int best = -1;
        float bestValue = float.MinValue;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i] == null || !choices[i].CanTake(gold, health)) continue;
            float value = EventValue(choices[i], health, maxHealth, partySize);
            if (value <= bestValue) continue;
            best = i;
            bestValue = value;
        }
        return best;
    }
}
