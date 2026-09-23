using System;

namespace S1Forklift;

internal static class DrivetrainMath
{
    internal static float CalculateMotorCurveTorque(
        float speedKmh,
        float maximumDrawbarPullNewtons,
        float maximumDrivePowerWatts,
        float driveWheelRadiusMeters,
        float differentialGearing,
        float governorStartSpeedKmh,
        float topSpeedKmh)
    {
        if (maximumDrawbarPullNewtons <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximumDrawbarPullNewtons));
        if (maximumDrivePowerWatts <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximumDrivePowerWatts));
        if (driveWheelRadiusMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(driveWheelRadiusMeters));
        if (differentialGearing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(differentialGearing));

        var governor = CalculateSpeedLimiter(
            speedKmh,
            governorStartSpeedKmh,
            topSpeedKmh);
        if (governor <= 0f)
            return 0f;

        var speedMetersPerSecond = Math.Max(0f, speedKmh) / 3.6f;
        var powerLimitedPull = speedMetersPerSecond <= 0.01f
            ? maximumDrawbarPullNewtons
            : maximumDrivePowerWatts / speedMetersPerSecond;
        var drawbarPull = Math.Min(maximumDrawbarPullNewtons, powerLimitedPull);
        return drawbarPull * driveWheelRadiusMeters /
               differentialGearing * governor;
    }

    internal static float CalculateSpeedLimiter(
        float speedKmh,
        float fadeStartSpeedKmh,
        float cutoffSpeedKmh)
    {
        if (fadeStartSpeedKmh < 0f || cutoffSpeedKmh <= fadeStartSpeedKmh)
            throw new ArgumentOutOfRangeException(nameof(cutoffSpeedKmh));

        if (speedKmh <= fadeStartSpeedKmh)
            return 1f;
        if (speedKmh >= cutoffSpeedKmh)
            return 0f;

        return 1f -
               (speedKmh - fadeStartSpeedKmh) /
               (cutoffSpeedKmh - fadeStartSpeedKmh);
    }

    internal static float ScaleTorque(float torque, float multiplier)
    {
        if (multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        return torque * multiplier;
    }
}
