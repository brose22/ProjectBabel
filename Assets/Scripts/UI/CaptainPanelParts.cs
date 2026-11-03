using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Sections of the selected-captain panels, shared by the captain widget and the debug panel.
// Live values register on the given refreshers, which the owning panel runs every frame.
public class CaptainPanelParts
{
    static readonly SupportEffect[] StatEffects = { SupportEffect.ActSpeed, SupportEffect.Damage, SupportEffect.Defense };

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly List<Action> refreshers;

    public CaptainPanelParts(MatchManager match, PlacementController placement, List<Action> refreshers)
    {
        this.match = match;
        this.placement = placement;
        this.refreshers = refreshers;
    }

    // A row that spans the panel, for buttons that share its width.
    public static Transform Row(Transform parent)
    {
        RectTransform row = UIKit.Group(parent, "Row");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 3);
        UIKit.Size(row, flexibleWidth: 1f);
        return row;
    }

    public static void Flexible(Button button, float height = 24f) => UIKit.Size(button, height: height, flexibleWidth: 1f);

    // Slots 3 and 4 open at bond ranks B and S (GDD).
    public static string SlotLabel(int slot) => slot switch
    {
        2 => "Slot 3 (B)",
        3 => "Slot 4 (S)",
        _ => $"Slot {slot + 1}",
    };

    // Name and level, then element, role and bond.
    public void Title(Transform panel, Tower tower)
    {
        Captain captain = tower.Captain;
        Transform title = Row(panel);
        Text name = UIKit.Label(title, captain.displayName, 16);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveLabel(title, () => $"Lv {tower.Level}", refreshers, 14, TextAnchor.MiddleRight, UIKit.AccentColor);
        string element = $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color>";
        UIKit.LiveLabel(panel, () => $"{element} {captain.ClassName}   ·   Bond {BondMath.RankName(tower.BondRank)} ({tower.Approval:0})",
            refreshers, 12, color: UIKit.MutedTextColor);
    }

    // Health, or why the tower is disabled and a Restore button once it can be restored.
    public void Condition(Transform panel, Tower tower)
    {
        if (!tower.IsDisabled)
        {
            UIKit.LiveBar(panel, () => tower.Health / tower.MaxHealth,
                () => $"Health {Mathf.CeilToInt(tower.Health)}/{Mathf.CeilToInt(tower.MaxHealth)}", UIKit.HealthBarColor, refreshers);
            return;
        }
        if (!match.CanRestore(tower))
        {
            UIKit.Label(panel, $"DISABLED  (restorable after wave {tower.RestorableAfterWave})", 12, color: UIKit.BadColor).fontStyle = FontStyle.Bold;
            return;
        }
        Transform row = Row(panel);
        Text disabled = UIKit.Label(row, "DISABLED", 12, color: UIKit.BadColor);
        disabled.fontStyle = FontStyle.Bold;
        UIKit.Size(disabled, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => $"Restore ({match.RestoreCost(tower)} Gold)", () => match.TryRestoreTower(tower), refreshers,
            () => RunState.Current.Gold >= match.RestoreCost(tower), width: 160f, height: 22f);
    }

    public void ReserveAndXp(Transform panel, Tower tower)
    {
        Captain captain = tower.Captain;
        UIKit.LiveBar(panel, () => tower.Reserve / (float)Mathf.Max(1, captain.ResourceCapacity),
            () => $"Reserve {tower.Reserve}/{captain.ResourceCapacity}", UIKit.ReserveBarColor, refreshers);
        UIKit.LiveBar(panel, () => tower.IsMaxLevel ? 1f : tower.Xp / (float)Mathf.Max(1, tower.XpForNextLevel),
            () => tower.IsMaxLevel ? $"XP {tower.Xp} (max level)" : $"XP {tower.Xp}/{tower.XpForNextLevel}", UIKit.XpBarColor, refreshers);
    }

    // Power, range, speed and Defense, then this wave's boosts (green while there are any).
    public void Stats(Transform panel, Tower tower)
    {
        Captain captain = tower.Captain;
        UIKit.LiveLabel(panel, () => $"Power {tower.SoldierPower:0}    Range {tower.Range:0.#}    Speed {captain.speed:0.##}    Defense {tower.Defense:0.#}", refreshers, 12);
        Text boosts = UIKit.LiveLabel(panel, () => BoostLabel(tower), refreshers, 12);
        refreshers.Add(() => boosts.color = HasBoosts(tower) ? UIKit.GoodColor : UIKit.MutedTextColor);
    }

    // Name and Use button, a charge meter, then what the special does.
    public void Ability(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Special");
        AbilityData ability = tower.Captain.Ability;
        if (ability == null)
        {
            UIKit.Label(panel, "No special ability", 12, color: UIKit.MutedTextColor);
            return;
        }
        Transform row = Row(panel);
        Text name = UIKit.Label(row, ability.displayName, 13);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => "Use", () => tower.TryUseAbility(), refreshers, () => tower.CanUseAbility,
            selected: () => tower.CanUseAbility, width: 80f, height: 22f);
        UIKit.LiveBar(panel, () => tower.SpecialCharge,
            () => tower.IsSpecialReady ? "Ready" : $"Charge {Mathf.FloorToInt(tower.SpecialCharge * 100f)}%", UIKit.ChargeBarColor, refreshers, 12f);
        if (!string.IsNullOrEmpty(ability.description))
            UIKit.Label(panel, ability.description, 11, color: UIKit.MutedTextColor);
    }

    // The tower whose Sell was pressed once and now waits for a second press.
    Tower sellArmed;

    // Move (or Cancel move while moving) and Sell, which takes a second press.
    public void Actions(Transform panel, Tower tower)
    {
        sellArmed = null;
        UIKit.Size(UIKit.Group(panel, "Gap"), height: 2f);
        Transform actions = Row(panel);
        if (placement.IsMoving)
            Flexible(UIKit.Button(actions, "Cancel move", placement.CancelMove));
        else
        {
            Button move = UIKit.LiveButton(actions, () => match.MoveCost(tower) > 0 ? $"Move ({match.MoveCost(tower)} Gold)" : "Move (free)",
                placement.BeginMove, refreshers, () => match.CanMove(tower) && match.CanAffordMove(tower));
            Flexible(move);
            UIKit.PadBadge(move, PadGlyph.West, () => GameInput.PadInMatch);
        }
        Flexible(UIKit.LiveButton(actions, () => sellArmed == tower ? "Press again to sell" : $"Sell (+{tower.Reserve} reserve)", () =>
        {
            if (sellArmed != tower) { sellArmed = tower; return; }
            sellArmed = null;
            placement.SellSelected();
        }, refreshers, width: -1f));
    }

    static bool HasBoosts(Tower tower)
    {
        foreach (SupportEffect effect in StatEffects)
            if (tower.Boost(effect) > 0f) return true;
        return false;
    }

    // Boosts every tower has from Buffer specials cast this wave.
    static string BoostLabel(Tower tower)
    {
        var parts = new List<string>();
        foreach (SupportEffect effect in StatEffects)
        {
            float boost = tower.Boost(effect);
            if (boost > 0f) parts.Add($"{effect} +{boost * 100f:0}%");
        }
        return parts.Count == 0 ? "Wave boosts: none" : "Wave boosts: " + string.Join(", ", parts);
    }
}
