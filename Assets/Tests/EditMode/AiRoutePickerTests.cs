using NUnit.Framework;

public class AiRoutePickerTests
{
    static AiRouteOption Option(int id, NodeKind kind, bool revealed = true) => new() { id = id, kind = kind, revealed = revealed };

    [Test]
    public void HurtRun_PrefersRest()
    {
        var options = new[] { Option(0, NodeKind.City), Option(1, NodeKind.Rest), Option(2, NodeKind.Elite) };
        Assert.That(AiRoutePicker.Pick(options, 0.4f, 0, 75, out _), Is.EqualTo(1));
    }

    [Test]
    public void HealthyRun_PrefersElite()
    {
        var options = new[] { Option(0, NodeKind.City), Option(1, NodeKind.Rest), Option(2, NodeKind.Elite) };
        Assert.That(AiRoutePicker.Pick(options, 1f, 0, 75, out _), Is.EqualTo(2));
    }

    [Test]
    public void HurtRun_AvoidsElite()
    {
        var options = new[] { Option(0, NodeKind.Elite), Option(1, NodeKind.City) };
        Assert.That(AiRoutePicker.Pick(options, 0.3f, 0, 75, out _), Is.EqualTo(1));
    }

    [Test]
    public void HiddenCities_AreTreatedAsPlain()
    {
        var options = new[] { Option(0, NodeKind.Elite, revealed: false), Option(1, NodeKind.City) };
        Assert.That(AiRoutePicker.Score(options[0], 1f, 0, 75), Is.LessThan(AiRoutePicker.Score(options[1], 1f, 0, 75)));
    }

    [Test]
    public void NoRoads_ReturnsMinusOne()
    {
        Assert.That(AiRoutePicker.Pick(new AiRouteOption[0], 1f, 0, 75, out string reason), Is.EqualTo(-1));
        Assert.That(reason, Is.Not.Empty);
    }
}
