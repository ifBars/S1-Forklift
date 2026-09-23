using System;
using HarmonyLib;
using MelonLoader;
using S1Forklift.Runtime;

#if IL2CPP
using S1ControlledDialogueHandler = Il2CppScheduleOne.Dialogue.ControlledDialogueHandler;
using S1DialogueChoiceList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Dialogue.DialogueChoiceData>;
#else
using S1ControlledDialogueHandler = ScheduleOne.Dialogue.ControlledDialogueHandler;
using S1DialogueChoiceList = System.Collections.Generic.List<ScheduleOne.Dialogue.DialogueChoiceData>;
#endif

namespace S1Forklift.Patches;

[HarmonyPatch(typeof(S1ControlledDialogueHandler), "ModifyChoiceList")]
internal static class VehicleSalesmanChoicePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        S1ControlledDialogueHandler __instance,
        S1DialogueChoiceList existingChoices)
    {
        try
        {
            ForkliftDealershipRuntime.TryAddChoice(__instance, existingChoices);
        }
        catch (Exception ex)
        {
            MelonLogger.Error(
                $"[S1Forklift] Could not add the forklift to Hyland Auto: {ex}");
        }
    }
}
