using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

#if IL2CPP
using ActionReferenceList = Il2CppSystem.Collections.Generic.List<UnityEngine.InputSystem.InputActionReference>;
using DescriptorList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.UI.Input.InputPromptsDescriptorData>;
using S1InputPromptPosition = Il2CppScheduleOne.UI.Input.EInputPromptPosition;
using S1BindingLabelType = Il2CppScheduleOne.UI.Input.InputPromptsBindingData.ESpriteSettingType;
using S1InputPromptsData = Il2CppScheduleOne.UI.Input.InputPromptsData;
using S1InputPromptsDescriptorData = Il2CppScheduleOne.UI.Input.InputPromptsDescriptorData;
using S1InputPromptsManager = Il2CppScheduleOne.UI.Input.InputPromptsManager;
using S1LandVehicle = Il2CppScheduleOne.Vehicles.LandVehicle;
#else
using ActionReferenceList = System.Collections.Generic.List<UnityEngine.InputSystem.InputActionReference>;
using DescriptorList = System.Collections.Generic.List<ScheduleOne.UI.Input.InputPromptsDescriptorData>;
using S1InputPromptPosition = ScheduleOne.UI.Input.EInputPromptPosition;
using S1BindingLabelType = ScheduleOne.UI.Input.InputPromptsBindingData.ESpriteSettingType;
using S1InputPromptsData = ScheduleOne.UI.Input.InputPromptsData;
using S1InputPromptsDescriptorData = ScheduleOne.UI.Input.InputPromptsDescriptorData;
using S1InputPromptsManager = ScheduleOne.UI.Input.InputPromptsManager;
using S1LandVehicle = ScheduleOne.Vehicles.LandVehicle;
#endif

namespace S1Forklift.Runtime;

internal static class ForkliftControlHintsRuntime
{
    internal const string ModuleId = "s1forklift_lift_controls";

    private static readonly List<InputAction> Actions = new();
    private static readonly List<Object> OwnedObjects = new();
    private static S1InputPromptsData? _module;
    private static InputActionAsset? _inputAsset;
    private static InputActionMap? _actionMap;
    private static InputActionReference? _raiseReference;
    private static InputActionReference? _lowerReference;
    private static S1InputPromptsDescriptorData? _raiseDescriptor;
    private static S1InputPromptsDescriptorData? _lowerDescriptor;
    private static bool _carryingPallet;

    internal static void Enter(S1LandVehicle vehicle)
    {
        if (!ForkliftDriverAvatarRuntime.IsForklift(vehicle))
            return;

        var manager = S1InputPromptsManager.Instance;
        if (manager == null)
            return;

        EnsureModule();
        if (_module == null)
            return;

        ApplyPalletLabels(false);
        ConfigureKeyCap(manager, _raiseReference, "PgUp");
        ConfigureKeyCap(manager, _lowerReference, "PgDn");
        if (manager.HasActivePrompt(ModuleId))
            manager.UnloadModule(ModuleId);

        manager.LoadModule(
            _module,
            S1InputPromptPosition.BottomLeftInGame);
    }

    internal static void Exit()
    {
        var manager = S1InputPromptsManager.Instance;
        if (manager != null && manager.HasActivePrompt(ModuleId))
            manager.UnloadModule(ModuleId);
    }

    internal static bool IsVisible =>
        S1InputPromptsManager.Instance != null &&
        S1InputPromptsManager.Instance.HasActivePrompt(ModuleId);

    internal static void SetPalletState(bool carrying)
    {
        if (_carryingPallet == carrying)
            return;

        EnsureModule();
        ApplyPalletLabels(carrying);
        var manager = S1InputPromptsManager.Instance;
        if (manager == null || _module == null || !manager.HasActivePrompt(ModuleId))
            return;

        manager.UnloadModule(ModuleId);
        manager.LoadModule(_module, S1InputPromptPosition.BottomLeftInGame);
        ConfigureKeyCap(manager, _raiseReference, "PgUp");
        ConfigureKeyCap(manager, _lowerReference, "PgDn");
    }

    internal static void Clear()
    {
        Exit();

        for (var index = 0; index < Actions.Count; index++)
            Actions[index].Dispose();
        Actions.Clear();

        for (var index = 0; index < OwnedObjects.Count; index++)
        {
            if (OwnedObjects[index] != null)
                Object.Destroy(OwnedObjects[index]);
        }
        OwnedObjects.Clear();
        _module = null;
        _inputAsset = null;
        _actionMap = null;
        _raiseReference = null;
        _lowerReference = null;
        _raiseDescriptor = null;
        _lowerDescriptor = null;
        _carryingPallet = false;
    }

    private static void EnsureModule()
    {
        if (_module != null)
            return;

        _module = CreateOwned<S1InputPromptsData>("S1Forklift Lift Controls");
        _inputAsset = CreateOwned<InputActionAsset>("S1Forklift Controls");
        _actionMap = _inputAsset.AddActionMap("Forklift");
        _module.Id = ModuleId;
        _module.Position = S1InputPromptPosition.BottomLeftInGame;
        _module.EnablePulseAnimation = false;
        _module.Descriptors = new DescriptorList();
        _raiseDescriptor = CreateDescriptor(
            "Pick up / Raise forks",
            "S1Forklift Raise Lift",
            "<Keyboard>/pageUp",
            out _raiseReference);
        _module.Descriptors.Add(_raiseDescriptor);
        _lowerDescriptor = CreateDescriptor(
            "Lower forks",
            "S1Forklift Lower Lift",
            "<Keyboard>/pageDown",
            out _lowerReference);
        _module.Descriptors.Add(_lowerDescriptor);
    }

    private static void ApplyPalletLabels(bool carrying)
    {
        _carryingPallet = carrying;
        if (_raiseDescriptor != null)
            _raiseDescriptor.DisplayName = carrying
                ? "Raise pallet"
                : "Pick up / Raise forks";
        if (_lowerDescriptor != null)
            _lowerDescriptor.DisplayName = carrying
                ? "Lower / Set down pallet"
                : "Lower forks";
    }

    private static S1InputPromptsDescriptorData CreateDescriptor(
        string displayName,
        string actionName,
        string binding,
        out InputActionReference reference)
    {
        var descriptor = CreateOwned<S1InputPromptsDescriptorData>(actionName);
        descriptor.DisplayName = displayName;
        descriptor.DisplayColor = Color.white;
        descriptor.Actions = new ActionReferenceList();

        var action = _actionMap!.AddAction(
            actionName,
            InputActionType.Button,
            binding);
        Actions.Add(action);
        reference = InputActionReference.Create(action);
        reference.name = actionName;
        reference.hideFlags = HideFlags.HideAndDontSave;
        OwnedObjects.Add(reference);
        descriptor.Actions.Add(reference);
        return descriptor;
    }

    private static void ConfigureKeyCap(
        S1InputPromptsManager manager,
        InputActionReference? reference,
        string label)
    {
        if (reference == null)
            return;

        var bindingData = manager.GetBindingDataFromActionReference(reference);
        if (bindingData == null)
            return;

        bindingData.SpriteSize = new Vector2(48f, 32f);
        bindingData.SpriteLabelSettingType = S1BindingLabelType.Custom;
        bindingData.SpriteLabel = label;
    }

    private static T CreateOwned<T>(string name)
        where T : ScriptableObject
    {
        var instance = ScriptableObject.CreateInstance<T>();
        instance.name = name;
        instance.hideFlags = HideFlags.HideAndDontSave;
        OwnedObjects.Add(instance);
        return instance;
    }
}
