using System.Collections.Generic;

// One road out of the current city, as the AI sees it. Hidden cities show as plain: their kind
// isn't known until intel reveals them.
public struct AiRouteOption
{
    public int id;
    public NodeKind kind;
    public bool revealed;
}

// Picks the next city on the world map: rest when hurt, shop when rich, elites when the run is
// healthy, otherwise the plain city.
public static class AiRoutePicker
{
    const float HurtFraction = 0.6f;
    const float HealthyFraction = 0.8f;

    public static float Score(AiRouteOption option, float healthFraction, int gold, int giftCost)
    {
        if (!option.revealed) return 0f;
        switch (option.kind)
        {
            case NodeKind.Rest:
                return healthFraction < HurtFraction ? 6f : healthFraction < 0.9f ? 1.5f : -1f;
            case NodeKind.Shop:
                return gold >= giftCost * 2 ? 3f : gold >= giftCost ? 1f : -1f;
            case NodeKind.Event:
                return healthFraction < HurtFraction ? -1f : 1f;
            case NodeKind.Elite:
                return healthFraction >= HealthyFraction ? 2.5f : healthFraction >= HurtFraction ? 0.5f : -3f;
            default:
                return 0.5f;
        }
    }

    // The index of the best option (the first on a tie), or -1 with none. reason says why.
    public static int Pick(IReadOnlyList<AiRouteOption> options, float healthFraction, int gold, int giftCost, out string reason)
    {
        reason = "no roads";
        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < options.Count; i++)
        {
            float score = Score(options[i], healthFraction, gold, giftCost);
            if (score <= bestScore) continue;
            best = i;
            bestScore = score;
        }
        if (best < 0) return -1;
        AiRouteOption chosen = options[best];
        reason = !chosen.revealed ? "unknown city, nothing better known"
            : chosen.kind == NodeKind.Rest ? $"{healthFraction:P0} health, resting"
            : chosen.kind == NodeKind.Shop ? $"{gold} Gold to spend at a shop"
            : chosen.kind == NodeKind.Elite ? $"{healthFraction:P0} health, taking the elite for the reward"
            : chosen.kind == NodeKind.Event ? "event looks safe"
            : "plain city";
        return best;
    }
}
