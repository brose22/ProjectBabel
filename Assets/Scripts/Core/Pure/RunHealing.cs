using System;

// Global health coming back during a run: a little after every won city, and a share of max
// health at a rest.
public static class RunHealing
{
    // Health after healing by amount, never past max.
    public static int Heal(int health, int maxHealth, int amount) => Math.Min(maxHealth, health + Math.Max(0, amount));

    // What a rest restores: fraction of max health, rounded up, at least 1 when fraction is positive.
    public static int RestAmount(int maxHealth, float fraction) =>
        fraction <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(maxHealth * fraction - 1e-4));
}
