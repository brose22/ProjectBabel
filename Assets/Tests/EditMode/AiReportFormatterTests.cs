using System.Collections.Generic;
using NUnit.Framework;

public class AiReportFormatterTests
{
    static AiRunRecord Run(int index, string result)
    {
        var wave = new AiWaveRow { wave = 1, seconds = 31.5f, kills = 12, leaks = 1, goldEarned = 80, goldBonus = 7, healthEnd = 19 };
        wave.killsByCaptain["Ana, the Bold"] = 12;
        var match = new AiMatchRow { index = 1, nodeId = 3, kind = "City", layer = 1, result = result == "Victory" ? "Won" : "Lost", wavesCleared = 1, waveCount = 5, kills = 12, leaks = 1 };
        match.waves.Add(wave);
        var run = new AiRunRecord { index = index, seed = 42 + index, result = result, finalHealth = 19, maxHealth = 20, finalGold = 100 };
        run.startParty.Add("Ana");
        run.route.Add("City");
        run.matches.Add(match);
        run.captains.Add(new AiCaptainRow { name = "Ana", element = "Fire", role = "Attacker", kills = 12, damage = 900f, abilities = 2 });
        run.damageByElement["Fire"] = 900f;
        run.decisions.Add(new AiDecisionRow { seconds = 1f, where = "Map", text = "chose city 3" });
        return run;
    }

    [Test]
    public void Csv_HasOneRowPerWavePerRun()
    {
        string csv = AiReportFormatter.Csv(new[] { Run(1, "Victory"), Run(2, "Defeat") });
        string[] lines = csv.TrimEnd('\n').Split('\n');
        Assert.That(lines, Has.Length.EqualTo(3));
        Assert.That(lines[0], Is.EqualTo(AiReportFormatter.CsvHeader));
        Assert.That(lines[1], Does.StartWith("1,43,Victory,1,3,City,1,0,Won,1,31.5,12,1"));
    }

    [Test]
    public void Csv_RowsHaveAsManyColumnsAsTheHeader()
    {
        string csv = AiReportFormatter.Csv(new[] { Run(1, "Victory") });
        string row = csv.Split('\n')[1];
        // The quoted captain name holds a comma, which must not split a column.
        Assert.That(row, Does.Contain("\"Ana, the Bold 12\"").Or.Contain("Ana, the Bold 12"));
        Assert.That(AiReportFormatter.CsvField("a,b"), Is.EqualTo("\"a,b\""));
        Assert.That(AiReportFormatter.CsvField("say \"hi\""), Is.EqualTo("\"say \"\"hi\"\"\""));
    }

    [Test]
    public void Markdown_ListsSeedAndSections()
    {
        string md = AiReportFormatter.Markdown(new[] { Run(1, "Victory") }, "now");
        Assert.That(md, Does.Contain("# AI Run report"));
        Assert.That(md, Does.Contain("Seed: 43"));
        Assert.That(md, Does.Contain("### Waves"));
        Assert.That(md, Does.Contain("### Decisions"));
        Assert.That(md, Does.Not.Contain("Summary across runs"));
    }

    [Test]
    public void Markdown_ShowsTheRunNote()
    {
        AiRunRecord run = Run(1, "Victory");
        run.note = "All 20 waves of the enemy base were completed";
        Assert.That(AiReportFormatter.Markdown(new[] { run }, "now"), Does.Contain("- Note: All 20 waves"));
        Assert.That(AiReportFormatter.Markdown(new[] { Run(1, "Victory") }, "now"), Does.Not.Contain("Note:"));
    }

    [Test]
    public void Markdown_BatchAddsAnAggregate()
    {
        string md = AiReportFormatter.Markdown(new[] { Run(1, "Victory"), Run(2, "Defeat") }, "now");
        Assert.That(md, Does.Contain("Summary across runs"));
        Assert.That(md, Does.Contain("Victories: 1 of 2 (50%)"));
        Assert.That(md, Does.Contain("Defeats by layer: layer 1 x1"));
    }

    [Test]
    public void Totals_IgnoreVisitedStops()
    {
        AiRunRecord run = Run(1, "Victory");
        run.matches.Add(new AiMatchRow { index = 2, kind = "Rest", result = "Visited", kills = 99 });
        Assert.That(run.Kills, Is.EqualTo(12));
    }
}
