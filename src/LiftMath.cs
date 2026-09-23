using System;

namespace S1Forklift;

public static class LiftMath
{
    public static float Advance(
        float currentHeight,
        float direction,
        float deltaTime,
        float speed,
        float minimumHeight,
        float maximumHeight)
    {
        if (maximumHeight < minimumHeight)
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));

        var clampedCurrent = Math.Clamp(currentHeight, minimumHeight, maximumHeight);
        if (deltaTime <= 0f || speed <= 0f || direction == 0f)
            return clampedCurrent;

        var target = direction > 0f ? maximumHeight : minimumHeight;
        var distance = speed * deltaTime;
        if (target > clampedCurrent)
            return Math.Min(clampedCurrent + distance, target);

        return Math.Max(clampedCurrent - distance, target);
    }
}
