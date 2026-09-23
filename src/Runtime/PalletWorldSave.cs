using System;
using System.Collections.Generic;
using S1API.Internal.Abstraction;
using S1API.Saveables;

namespace S1Forklift.Runtime;

public sealed class PalletWorldSave : Saveable
{
    [SaveableField("pallet_world_poses")]
    private List<PalletWorldPose> _poses = new();

    internal static PalletWorldSave? Instance { get; private set; }

    public PalletWorldSave()
    {
        Instance = this;
    }

    internal List<PalletWorldPose> Poses => _poses;

    protected override void OnLoaded()
    {
        Instance = this;
        PalletCarryRuntime.SetSavedWorldPoses(_poses);
    }
}

[Serializable]
public sealed class PalletWorldPose
{
    public string Guid = string.Empty;
    public float X;
    public float Y;
    public float Z;
    public float RotationY;
}
