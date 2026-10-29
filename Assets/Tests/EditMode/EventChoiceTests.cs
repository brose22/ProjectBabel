using NUnit.Framework;

public class EventChoiceTests
{
    [Test]
    public void CanTake_NeedsTheGoldAndSomeHealthLeft()
    {
        var choice = new EventChoice { gold = -100, health = -3 };
        Assert.That(choice.CanTake(100, 4), Is.True);
        Assert.That(choice.CanTake(99, 4), Is.False);
        Assert.That(choice.CanTake(100, 3), Is.False);
        Assert.That(new EventChoice { gold = 50, health = 2 }.CanTake(0, 1), Is.True);
    }

    [Test]
    public void Describe_ListsOnlyWhatChanges()
    {
        Assert.That(new EventChoice { gold = 120, health = -3 }.Describe(), Is.EqualTo("+120 Gold, -3 health"));
        Assert.That(new EventChoice { xp = 40, approval = 5f }.Describe(), Is.EqualTo("+40 XP each, +5 approval each"));
        Assert.That(new EventChoice().Describe(), Is.Empty);
    }
}
