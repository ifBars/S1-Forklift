using MelonLoader;
using S1API.Lifecycle;
using S1Forklift.Runtime;
using UnityEngine;

[assembly: MelonInfo(
    typeof(S1Forklift.Core),
    S1Forklift.Constants.ModName,
    S1Forklift.Constants.ModVersion,
    S1Forklift.Constants.ModAuthor)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace S1Forklift;

public sealed class Core : MelonMod
{
    private float _nextRegistrationAttempt;
    private bool _registrationComplete;
    private ForkliftSmokeProbe? _smokeProbe;

    public override void OnInitializeMelon()
    {
        ForkliftPreferences.Initialize();
        GameLifecycle.OnPreLoad += PalletItemRuntime.OnPreLoad;
        GameLifecycle.OnLoadComplete += PalletItemRuntime.OnLoadComplete;
        GameLifecycle.OnPreSceneChange += PalletItemRuntime.OnPreSceneChange;
        _smokeProbe = ForkliftSmokeProbe.TryCreate();
        LoggerInstance.Msg(
            $"{Constants.ModName} {Constants.ModVersion} initialized. " +
            $"Lift: {ForkliftPreferences.RaiseLiftKey.Value}/" +
            $"{ForkliftPreferences.LowerLiftKey.Value}.");
    }

    public override void OnUpdate()
    {
        if (!_registrationComplete && Time.unscaledTime >= _nextRegistrationAttempt)
        {
            _nextRegistrationAttempt = Time.unscaledTime + 1f;
            _registrationComplete =
                ForkliftPrefabFactory.TryEnsureRegistered(LoggerInstance);
        }

        ForkliftRuntime.Update();
        PalletItemRuntime.Update();
        PalletCarryRuntime.Update();
        ForkliftDriverAvatarRuntime.UpdateLocalPlayer();
        _smokeProbe?.Update();
    }

    public override void OnLateUpdate()
    {
        PalletCarryRuntime.LateUpdate();
        ForkliftDriverAvatarRuntime.LateUpdate();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        _nextRegistrationAttempt = 0f;
        _registrationComplete = false;
        ForkliftRuntime.Prune();
        PalletCarryRuntime.Prune();
        LoadingDockPalletRuntime.Clear();
    }

    public override void OnApplicationQuit()
    {
        ForkliftControlHintsRuntime.Clear();
        GameLifecycle.OnPreLoad -= PalletItemRuntime.OnPreLoad;
        GameLifecycle.OnLoadComplete -= PalletItemRuntime.OnLoadComplete;
        GameLifecycle.OnPreSceneChange -= PalletItemRuntime.OnPreSceneChange;
        PalletCarryRuntime.Clear();
        LoadingDockPalletRuntime.Clear();
        PalletItemRuntime.Clear();
        ForkliftRuntime.Clear();
        _smokeProbe = null;
    }

}
