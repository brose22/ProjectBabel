using NUnit.Framework;
using UnityEngine;

public class CameraFocusMathTests
{
    // The 24 x 12 map with a 6 tile border all round.
    static readonly Rect Padded = new(-6f, -6f, 36f, 24f);
    static readonly Vector2 ZoomedHalf = new(6f, 3.375f);

    [Test]
    public void ClampCentre_CentresOnAPointAwayFromTheEdges()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(10f, 7f), Padded, ZoomedHalf);
        Assert.That(centre, Is.EqualTo(new Vector2(10f, 7f)));
    }

    [Test]
    public void ClampCentre_KeepsTheViewInsideTheBounds()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(-20f, 40f), Padded, ZoomedHalf);
        Assert.That(centre.x, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(centre.y, Is.EqualTo(14.625f).Within(1e-5f));
    }

    [Test]
    public void ClampCentre_CentresAViewLargerThanTheBounds()
    {
        Vector2 centre = CameraFocusMath.ClampCentre(new Vector2(20f, 1f), Padded, new Vector2(30f, 20f));
        Assert.That(centre, Is.EqualTo(new Vector2(12f, 6f)));
    }

    [Test]
    public void MaxSize_FitsTheTighterAxis()
    {
        // Height 24 gives 12; width 36 at aspect 16:9 gives 10.125.
        Assert.That(CameraFocusMath.MaxSize(Padded, 16f / 9f, 1f), Is.EqualTo(10.125f).Within(1e-4f));
    }

    [Test]
    public void MaxSize_NeverDropsBelowTheMinimum() =>
        Assert.That(CameraFocusMath.MaxSize(new Rect(0f, 0f, 24f, 12f), 16f / 9f, 8f), Is.EqualTo(8f));

    [Test]
    public void ZoomCentre_KeepsTheAnchorPointFixedOnScreen()
    {
        Vector2 centre = new(12f, 6f);
        Vector2 anchor = new(18f, 8f);
        Vector2 zoomed = CameraFocusMath.ZoomCentre(centre, anchor, 6f, 3f);
        // The anchor sits at the same fraction of the view before and after.
        Assert.That((anchor.x - centre.x) / 6f, Is.EqualTo((anchor.x - zoomed.x) / 3f).Within(1e-5f));
        Assert.That((anchor.y - centre.y) / 6f, Is.EqualTo((anchor.y - zoomed.y) / 3f).Within(1e-5f));
    }
}
