using MelonLoader;
using UnityEngine;

namespace S1Forklift;

internal static class ForkliftPreferences
{
    internal static MelonPreferences_Entry<KeyCode> RaiseLiftKey { get; private set; } = null!;
    internal static MelonPreferences_Entry<KeyCode> LowerLiftKey { get; private set; } = null!;
    internal static MelonPreferences_Entry<float> MaximumLiftHeight { get; private set; } = null!;
    internal static MelonPreferences_Entry<float> LiftSpeed { get; private set; } = null!;

    internal static void Initialize()
    {
        var category = MelonPreferences.CreateCategory("Forklift and Pallets");
        RaiseLiftKey = category.CreateEntry("RaiseLiftKey", KeyCode.PageUp, "Raise forklift");
        LowerLiftKey = category.CreateEntry("LowerLiftKey", KeyCode.PageDown, "Lower forklift");
        MaximumLiftHeight = category.CreateEntry("MaximumLiftHeight", 2.5f, "Maximum local Y position of the fork carriage");
        LiftSpeed = category.CreateEntry("LiftSpeed", 1.25f, "Lift movement speed in metres per second");
    }
}
