using UnityEngine;

// Turning stick input into steps, for the menu focus and the match's tile cursor.
public static class PadMath
{
    const float StickDeadZone = 0.5f;
    // A widget off to the side costs this much more per pixel than one straight ahead.
    const float SidewaysCost = 2f;

    // One step along the axis the stick leans on most, or none inside the dead zone.
    public static Vector2Int ToStep(Vector2 stick)
    {
        if (stick.sqrMagnitude < StickDeadZone * StickDeadZone) return Vector2Int.zero;
        if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y)) return new Vector2Int(stick.x > 0f ? 1 : -1, 0);
        return new Vector2Int(0, stick.y > 0f ? 1 : -1);
    }

    // How far a widget is from the focused one when stepping in a direction: lower is a better
    // next stop, and infinity means it isn't that way at all.
    public static float StepCost(Vector2 from, Vector2 to, Vector2Int step)
    {
        Vector2 offset = to - from;
        float ahead = Vector2.Dot(offset, step);
        if (ahead <= 1f) return float.PositiveInfinity;
        float sideways = Mathf.Abs(offset.x * step.y - offset.y * step.x);
        return ahead + sideways * SidewaysCost;
    }
}

// Auto-repeat for a held direction: one step at once, a wait, then steady steps until released or
// changed. Takes unscaled time, because menus run while the game is paused.
public class StepRepeat
{
    readonly float firstDelay;
    readonly float interval;
    Vector2Int held;
    float nextStep;

    public StepRepeat(float firstDelay, float interval)
    {
        this.firstDelay = firstDelay;
        this.interval = interval;
    }

    // The step to take this frame, or zero.
    public Vector2Int Update(Vector2Int direction, float now)
    {
        if (direction == Vector2Int.zero)
        {
            held = direction;
            return direction;
        }
        if (direction != held)
        {
            held = direction;
            nextStep = now + firstDelay;
            return direction;
        }
        if (now < nextStep) return Vector2Int.zero;
        nextStep = now + interval;
        return direction;
    }
}
