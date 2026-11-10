using System;
using System.Collections.Generic;
using NUnit.Framework;

public class SaveMigrationTests
{
    [TestCase(1, 1, SaveVersionStatus.Current)]
    [TestCase(3, 3, SaveVersionStatus.Current)]
    [TestCase(1, 3, SaveVersionStatus.Older)]
    [TestCase(2, 3, SaveVersionStatus.Older)]
    [TestCase(4, 3, SaveVersionStatus.Newer)]
    [TestCase(0, 3, SaveVersionStatus.Invalid)]
    [TestCase(-1, 3, SaveVersionStatus.Invalid)]
    public void Check_SortsAVersionAgainstTheCurrentOne(int version, int current, SaveVersionStatus status) =>
        Assert.That(SaveMigration.Check(version, current), Is.EqualTo(status));

    [Test]
    public void Steps_RunFromTheSaveVersionUpToTheOneBeforeCurrent()
    {
        Assert.That(SaveMigration.Steps(1, 4), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(SaveMigration.Steps(3, 4), Is.EqualTo(new[] { 3 }));
    }

    [TestCase(4, 4)]
    [TestCase(5, 4)]
    [TestCase(0, 4)]
    public void Steps_AreEmptyWhenThereIsNothingToUpgrade(int version, int current) =>
        Assert.That(SaveMigration.Steps(version, current), Is.Empty);

    // Steps that each append the version they upgrade to, so the result shows the order they ran in.
    static Dictionary<int, Func<string, string>> StepsFrom(params int[] versions)
    {
        var steps = new Dictionary<int, Func<string, string>>();
        foreach (int version in versions) steps[version] = data => $"{data}>{version + 1}";
        return steps;
    }

    [Test]
    public void TryUpgrade_RunsEachStepInOrder()
    {
        string data = "1";
        Assert.That(SaveMigration.TryUpgrade(ref data, 1, 4, StepsFrom(3, 1, 2)), Is.True);
        Assert.That(data, Is.EqualTo("1>2>3>4"));
    }

    [Test]
    public void TryUpgrade_SkipsStepsBelowTheSaveVersion()
    {
        string data = "3";
        Assert.That(SaveMigration.TryUpgrade(ref data, 3, 4, StepsFrom(1, 2, 3)), Is.True);
        Assert.That(data, Is.EqualTo("3>4"));
    }

    [Test]
    public void TryUpgrade_LeavesACurrentSaveAlone()
    {
        string data = "4";
        Assert.That(SaveMigration.TryUpgrade(ref data, 4, 4, null), Is.True);
        Assert.That(data, Is.EqualTo("4"));
    }

    [TestCase(5)]
    [TestCase(0)]
    public void TryUpgrade_RefusesANewerOrInvalidVersion(int version)
    {
        string data = "x";
        Assert.That(SaveMigration.TryUpgrade(ref data, version, 4, StepsFrom(1, 2, 3)), Is.False);
        Assert.That(data, Is.EqualTo("x"));
    }

    [Test]
    public void TryUpgrade_RunsNothingWhenAStepIsMissing()
    {
        string data = "1";
        Assert.That(SaveMigration.TryUpgrade(ref data, 1, 4, StepsFrom(1, 3)), Is.False);
        Assert.That(data, Is.EqualTo("1"));
    }
}
