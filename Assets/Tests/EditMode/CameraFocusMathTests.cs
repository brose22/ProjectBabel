using NUnit.Framework;
using UnityEngine;

public class CameraFocusMathTests
{
    static readonly Vector2 Home = new(12f, 6.75f);
    static readonly Vector2 HomeHalf = new(12f, 6.75f);
    static readonly Vector2 ZoomedHalf = new(6f, 3.375f);

    [Test]
    public void ClampCentre_CentresOnAPointAwayFromTheEdges()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(10f, 7f), Home, HomeHalf, ZoomedHalf);
        Assert.That(centre, Is.EqualTo(new Vector2(10f, 7f)));
    }

    [Test]
    public void ClampCentre_KeepsTheZoomedViewInsideTheHomeView()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(0.5f, 13f), Home, HomeHalf, ZoomedHalf);
        Assert.That(centre.x, Is.EqualTo(6f).Within(1e-5f));
        Assert.That(centre.y, Is.EqualTo(10.125f).Within(1e-5f));
    }

    [Test]
    public void ClampCentre_StaysHomeWhenNotZoomedIn()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(20f, 1f), Home, HomeHalf, HomeHalf);
        Assert.That(centre, Is.EqualTo(Home));
    }
}
