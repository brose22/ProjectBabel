using System.Collections.Generic;

// The locations a run can go to (item: location), which of them the profile has open, and
// starting a run at one. For the Castle's location select; the rules live in LocationRules.
public static class LocationSelect
{
    public readonly struct Entry
    {
        public readonly LocationData location;
        public readonly bool unlocked;

        public Entry(LocationData location, bool unlocked)
        {
            this.location = location;
            this.unlocked = unlocked;
        }
    }

    // Every location in unlock order, with whether the profile has it open.
    public static List<Entry> Entries(MatchData data, Profile profile)
    {
        var entries = new List<Entry>();
        for (int i = 0; i < data.locations.Count; i++)
            if (data.locations[i] != null)
                entries.Add(new Entry(data.locations[i], LocationRules.IsUnlocked(i, profile.LocationsUnlocked, data.locations.Count)));
        return entries;
    }

    public static bool IsUnlocked(MatchData data, Profile profile, LocationData location) =>
        location != null && LocationRules.IsUnlocked(data.locations.IndexOf(location), profile.LocationsUnlocked, data.locations.Count);

    // Starts a new run at an open location, in place of any run in progress, and leaves it waiting
    // for its party. The caller then loads the match scene, which opens on the party setup.
    public static bool TryStartRun(MatchData data, Profile profile, LocationData location)
    {
        if (profile == null || !IsUnlocked(data, profile, location)) return false;
        RunSave.Delete();
        RunState.StartNewRun(profile, location.id, data.startingHealth);
        return true;
    }

    // Where a run goes when no location was picked: the furthest one open. Null without locations.
    public static LocationData Default(MatchData data, Profile profile) =>
        data.locations.Count == 0 ? null : data.locations[LocationRules.Furthest(profile.LocationsUnlocked, data.locations.Count)];

    // The location the run is at; a run whose location is gone plays the first one.
    public static LocationData Of(MatchData data, RunState run)
    {
        if (data.locations.Count == 0) return null;
        return data.FindLocation(run?.Location) ?? data.locations.Find(l => l != null);
    }

    // A run won at this location opens the next one in the profile.
    public static void RecordWin(MatchData data, Profile profile, string locationId)
    {
        int index = LocationRules.IndexOf(data.LocationIds, locationId);
        profile.UnlockLocations(LocationRules.UnlockedAfterWin(index, profile.LocationsUnlocked, data.locations.Count));
    }
}
