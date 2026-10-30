using NUnit.Framework;

public class AiStopChoiceTests
{
    [Test]
    public void Rest_OnlyWhenHurt()
    {
        Assert.That(AiStopChoice.ShouldRest(10, 20), Is.True);
        Assert.That(AiStopChoice.ShouldRest(20, 20), Is.False);
    }

    [Test]
    public void Gift_GoesToLowestApproval_AndKeepsAReserve()
    {
        var approvals = new[] { 30f, -10f, 5f };
        var can = new[] { true, true, true };
        Assert.That(AiStopChoice.PickGiftTarget(approvals, can, 300, 75, 100), Is.EqualTo(1));
        Assert.That(AiStopChoice.PickGiftTarget(approvals, can, 150, 75, 100), Is.EqualTo(-1));
    }

    [Test]
    public void Gift_SkipsCaptainsWhoCannotTakeOne()
    {
        var approvals = new[] { 30f, -10f };
        var can = new[] { true, false };
        Assert.That(AiStopChoice.PickGiftTarget(approvals, can, 300, 75, 0), Is.EqualTo(0));
    }

    [Test]
    public void Event_PicksHealthWhenHurt()
    {
        var choices = new[]
        {
            new EventChoice { label = "Loot", gold = 100 },
            new EventChoice { label = "Tend", health = 4 },
        };
        Assert.That(AiStopChoice.PickEvent(choices, 50, 5, 20, 3), Is.EqualTo(1));
    }

    [Test]
    public void Event_SkipsChoicesItCannotTake()
    {
        var choices = new[]
        {
            new EventChoice { label = "Pay", gold = -500, xp = 100 },
            new EventChoice { label = "Move on" },
        };
        Assert.That(AiStopChoice.PickEvent(choices, 50, 20, 20, 3), Is.EqualTo(1));
    }

    [Test]
    public void Event_ReturnsMinusOneWhenNothingCanBeTaken()
    {
        var choices = new[] { new EventChoice { label = "Pay", gold = -500 } };
        Assert.That(AiStopChoice.PickEvent(choices, 50, 20, 20, 3), Is.EqualTo(-1));
    }
}
