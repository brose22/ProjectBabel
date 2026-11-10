using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// The results screen after a match.
public partial class RunScreens
{
    void BuildResults()
    {
        bool won = match.State == MatchState.Won;
        RunState run = RunState.Current;

        RectTransform root = NewScreen("Results");
        RectTransform panel = UIKit.Box(root, "Panel");
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, 16, 6, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        bool runWon = won && run.Map != null && run.Map.IsComplete;
        Title(panel, runWon ? "The enemy base has fallen" : won ? "Victory" : "Defeat", won ? UIKit.AccentColor : UIKit.BadColor);
        string summary = runWon ? $"Run won in {run.MatchesPlayed} matches"
            : won ? $"Match {run.MatchesPlayed} won{(match.IsEliteMatch ? " (elite city)" : "")}"
            : $"The run ends after {run.MatchesPlayed} match{(run.MatchesPlayed == 1 ? "" : "es")} won";
        UIKit.Label(panel, summary, 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform stats = UIKit.Group(panel, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 8, fitContent: true).padding.top = UIKit.Gap;
        Stat(stats, "WAVES", $"{match.WavesCleared}/{match.WaveCount}");
        Stat(stats, "KILLS", $"{match.Kills}");
        Stat(stats, "LEAKS", $"{match.Leaks}");
        Stat(stats, "CASUALTIES", $"{match.Casualties}");
        Stat(stats, "HEALTH", $"{run.Health}/{run.MaxHealth}");
        Stat(stats, "GOLD", $"{run.Gold}").color = UIKit.AccentColor;
        if (match.WinGoldEarned + match.ReserveRefunded > 0)
            UIKit.Label(panel, $"+{match.WinGoldEarned} Gold for the win, +{match.ReserveRefunded} returned from reserves", 12, TextAnchor.MiddleCenter, UIKit.GoodColor);
        if (match.HealthRestored > 0)
            UIKit.Label(panel, $"+{match.HealthRestored} health for the win", 12, TextAnchor.MiddleCenter, UIKit.GoodColor);
        if (match.LocationUnlocked != null)
            UIKit.Label(panel, $"{match.LocationUnlocked.displayName} is open", 13, TextAnchor.MiddleCenter, UIKit.GoodColor);
        if (!won)
        {
            UIKit.Label(panel, $"-{match.GoldLost} Gold lost with the run", 12, TextAnchor.MiddleCenter, UIKit.BadColor);
            if (match.CaptainsLost.Count > 0)
                UIKit.Label(panel, $"Lost on the field: {string.Join(", ", match.CaptainsLost.Select(c => c.displayName))}" +
                                   $" and {match.SoldiersLost} soldier{(match.SoldiersLost == 1 ? "" : "s")}", 12, TextAnchor.MiddleCenter, UIKit.BadColor);
        }

        UIKit.Size(UIKit.Header(panel, "Captains"), flexibleWidth: 1f);
        ResultColumns(ResultRow(panel, false), "", "LEVEL", "BOND", "APPROVAL", "CHANGE", 9, UIKit.MutedTextColor).preferredHeight = 14f;
        // The party, then anyone who walked out at the end of this match, so the player sees who left.
        var rows = new List<Captain>(run.Party);
        if (relationships != null) rows.AddRange(relationships.Walkouts.Where(c => !rows.Contains(c)));
        for (int i = 0; i < rows.Count; i++)
            CaptainResultRow(panel, run, rows[i], i % 2 == 0);

        UIKit.Size(UIKit.Group(panel, "Gap"), height: UIKit.Gap);
        if (won && !runWon) UIKit.Primary(UIKit.Button(panel, "Back to the map", match.ReturnToMap, 220f, 32f), refreshers);
        RectTransform buttons = UIKit.Group(panel, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 8, fitContent: true);
        // A lost or finished run goes home to the Castle.
        if (!won || runWon) UIKit.Primary(UIKit.Button(buttons, "Back to the Castle", match.ReturnToCastle, 170f, 28f), refreshers);
        UIKit.Button(buttons, "Main menu", match.ReturnToMenu, 150f, 28f);
    }

    // The run's match number, health (with a meter) and Gold, in a framed box in the top right.
    void BuildRunStats(RectTransform root, RunState run)
    {
        RectTransform box = UIKit.Box(root, "Run Stats", blocksClicks: false);
        UIKit.Place(box, Vector2.one, new Vector2(-UIKit.Pad, -UIKit.Pad), Vector2.zero);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(box, 6, 6, fitContent: true);
        layout.padding.left = layout.padding.right = 10;
        // Top-aligned, so the captions line up although the health column is taller.
        layout.childAlignment = TextAnchor.UpperCenter;

        Stat(box, "MATCH", $"{run.MatchesPlayed + 1}");
        StatRule(box);
        Text health = Stat(box, "HEALTH", $"{run.Health}/{run.MaxHealth}");
        if (run.Health * 3 <= run.MaxHealth) health.color = UIKit.BadColor;
        float healthFraction = run.Health / (float)Mathf.Max(1, run.MaxHealth);
        UIKit.LiveBar(health.transform.parent, () => healthFraction, () => "", UIKit.HealthBarColor, refreshers, 8f);
        StatRule(box);
        Stat(box, "GOLD", $"{run.Gold}").color = UIKit.AccentColor;
    }

    static void StatRule(Transform parent) => UIKit.Size(UIKit.Panel(parent, "Rule", UIKit.RuleColor, blocksClicks: false), 2f, 34f);

    // A stat column; sub is a small line under the value, such as how the captain ranks in the party.
    static Text Stat(Transform parent, string caption, string value, string sub = null)
    {
        RectTransform stat = UIKit.Group(parent, caption);
        UIKit.Layout<VerticalLayoutGroup>(stat, 0, -1f).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stat, 66f);
        UIKit.Label(stat, caption, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        Text text = UIKit.Label(stat, value, 15, TextAnchor.MiddleCenter);
        if (sub != null) UIKit.Label(stat, sub, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        return text;
    }

    // "#2/4": where the captain stands among the party still with the run on a stat, 1 being highest.
    static string PartyRank(RunState run, Captain captain, Func<Captain, float> stat)
    {
        List<Captain> present = run.Party.FindAll(c => c != null);
        if (!present.Contains(captain) || present.Count < 2) return null;
        float mine = stat(captain);
        int rank = 1 + present.FindAll(c => stat(c) > mine + 0.001f).Count;
        return $"#{rank}/{present.Count}";
    }

    static RectTransform ResultRow(Transform panel, bool shaded)
    {
        RectTransform row = UIKit.Panel(panel, "Row", shaded ? UIKit.InsetColor : Color.clear, blocksClicks: false);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(row, 0, 8);
        layout.padding.left = layout.padding.right = UIKit.Pad;
        return row;
    }

    // The captain table's columns: name, level, bond, approval and its change.
    static LayoutElement ResultColumns(RectTransform row, string name, string level, string bond, string approval, string change,
        int size, Color color, Color? changeColor = null)
    {
        UIKit.Size(UIKit.Label(row, name, size + 1, color: color), flexibleWidth: 1f);
        UIKit.Size(UIKit.Label(row, level, size, color: color), 44f);
        UIKit.Size(UIKit.Label(row, bond, size, color: color), 56f);
        UIKit.Size(UIKit.Label(row, approval, size + 1, TextAnchor.MiddleRight, color), 54f);
        UIKit.Size(UIKit.Label(row, change, size, TextAnchor.MiddleRight, changeColor ?? color), 48f);
        return UIKit.Size(row, height: 22f, flexibleWidth: 1f);
    }

    // One table row per captain, with a walkout this match noted after the name.
    void CaptainResultRow(Transform panel, RunState run, Captain captain, bool shaded)
    {
        Tower tower = Tower.All.Find(t => t.Captain == captain);
        float change = relationships != null ? relationships.ApprovalChangeThisMatch(captain) : 0f;
        int level = tower != null ? tower.Level : Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        bool walkedOut = relationships != null && relationships.Walkouts.Contains(captain);
        string note = walkedOut ? $"  <color={UIKit.Hex(UIKit.BadColor)}>walked out!</color>" : "";

        Color changeColor = change > 0f ? UIKit.GoodColor : change < 0f ? UIKit.BadColor : UIKit.MutedTextColor;
        ResultColumns(ResultRow(panel, shaded), captain.displayName + note, $"Lv {level}", BondMath.RankName(ApprovalStore.RankOf(captain)),
            $"{ApprovalStore.ApprovalOf(captain):0}", $"{change:+0.#;-0.#;0}", 12, UIKit.TextColor, changeColor);
    }
}
