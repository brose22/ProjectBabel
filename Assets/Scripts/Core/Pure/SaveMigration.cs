using System;
using System.Collections.Generic;

// What to do with a save of a given version.
public enum SaveVersionStatus
{
    // Already the current version.
    Current,
    // Older: upgrade it one version at a time.
    Older,
    // Written by a later build than this one: can't be read, and mustn't be lost.
    Newer,
    // No usable version number.
    Invalid,
}

// Upgrades an old save to the current version one step at a time. Each step upgrades from one
// version to the next and is keyed by the version it upgrades from, so a "1 -> 2" step has key 1.
public static class SaveMigration
{
    // Versions start at 1; a save with no version field reads as 0.
    public const int FirstVersion = 1;

    public static SaveVersionStatus Check(int version, int current)
    {
        if (version < FirstVersion) return SaveVersionStatus.Invalid;
        if (version > current) return SaveVersionStatus.Newer;
        return version == current ? SaveVersionStatus.Current : SaveVersionStatus.Older;
    }

    // The keys of the steps that take a save from version to current, in the order to run them.
    // Empty when there is nothing to upgrade or the save can't be upgraded.
    public static List<int> Steps(int version, int current)
    {
        var steps = new List<int>();
        if (Check(version, current) != SaveVersionStatus.Older) return steps;
        for (int from = version; from < current; from++) steps.Add(from);
        return steps;
    }

    // Runs the steps that bring data from version up to current. Returns false, leaving data
    // alone, if the version isn't one that can be upgraded to current or a step is missing; a
    // save that is already current succeeds unchanged.
    public static bool TryUpgrade<T>(ref T data, int version, int current, IReadOnlyDictionary<int, Func<T, T>> steps)
    {
        SaveVersionStatus status = Check(version, current);
        if (status == SaveVersionStatus.Current) return true;
        if (status != SaveVersionStatus.Older) return false;
        List<int> plan = Steps(version, current);
        foreach (int from in plan)
            if (steps == null || !steps.TryGetValue(from, out Func<T, T> step) || step == null) return false;
        T upgraded = data;
        foreach (int from in plan) upgraded = steps[from](upgraded);
        data = upgraded;
        return true;
    }
}
