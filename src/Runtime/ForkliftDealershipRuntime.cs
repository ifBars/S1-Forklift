using System;
using System.Reflection;
using HarmonyLib;

#if IL2CPP
using Il2CppInterop.Runtime;
using S1ControlledDialogueHandler = Il2CppScheduleOne.Dialogue.ControlledDialogueHandler;
using S1DealershipListing = Il2CppScheduleOne.NPCs.CharacterClasses.Jeremy.DealershipListing;
using S1DialogueChoiceData = Il2CppScheduleOne.Dialogue.DialogueChoiceData;
using S1DialogueChoiceList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Dialogue.DialogueChoiceData>;
using S1DialogueHandler = Il2CppScheduleOne.Dialogue.DialogueHandler;
using S1VehicleSalesmanHandler = Il2CppScheduleOne.Dialogue.DialogueHandler_VehicleSalesman;
#else
using S1ControlledDialogueHandler = ScheduleOne.Dialogue.ControlledDialogueHandler;
using S1DealershipListing = ScheduleOne.NPCs.CharacterClasses.Jeremy.DealershipListing;
using S1DialogueChoiceData = ScheduleOne.Dialogue.DialogueChoiceData;
using S1DialogueChoiceList = System.Collections.Generic.List<ScheduleOne.Dialogue.DialogueChoiceData>;
using S1DialogueHandler = ScheduleOne.Dialogue.DialogueHandler;
using S1VehicleSalesmanHandler = ScheduleOne.Dialogue.DialogueHandler_VehicleSalesman;
#endif

namespace S1Forklift.Runtime;

internal static class ForkliftDealershipRuntime
{
    private static readonly MethodInfo CreateTempLinkMethod =
        AccessTools.Method(
            typeof(S1DialogueHandler),
            "CreateTempLink",
            new[] { typeof(string), typeof(string), typeof(string) })
        ?? throw new MissingMethodException(
            typeof(S1DialogueHandler).FullName,
            "CreateTempLink");

    internal static bool TryAddChoice(
        S1ControlledDialogueHandler handler,
        S1DialogueChoiceList choices)
    {
        var salesmanHandler = AsVehicleSalesman(handler);
        if (salesmanHandler?.Salesman == null ||
            choices == null ||
            ForkliftPrefabFactory.GetPrefab() == null ||
            ContainsChoice(choices, Constants.DealershipChoiceLabel))
        {
            return false;
        }

        var pageAnchor = FindChoice(
            choices,
            Constants.DealershipPageAnchorChoiceLabel);
        if (pageAnchor == null)
            return false;

        var activeDialogue = S1DialogueHandler.ActiveDialogue;
        var activeNode = S1DialogueHandler.ActiveDialogueNode;
        var anchorLink = activeDialogue?.GetLink(pageAnchor.Guid);
        if (activeNode == null || anchorLink == null)
            return false;

        EnsureListing(salesmanHandler);

        var choice = new S1DialogueChoiceData
        {
            Guid = Constants.DealershipChoiceGuid,
            ChoiceLabel = Constants.DealershipChoiceLabel,
            ChoiceText = Constants.DealershipChoiceText,
            ShowWorldspaceDialogue = true,
        };

        CreateTempLinkMethod.Invoke(
            handler,
            new object[]
            {
                activeNode.Guid,
                Constants.DealershipChoiceGuid,
                anchorLink.TargetNodeGuid,
            });
        InsertBeforeNavigationChoice(choices, choice);
        return true;
    }

    private static S1VehicleSalesmanHandler? AsVehicleSalesman(
        S1ControlledDialogueHandler handler)
    {
#if IL2CPP
        return handler.TryCast<S1VehicleSalesmanHandler>();
#else
        return handler as S1VehicleSalesmanHandler;
#endif
    }

    private static void EnsureListing(S1VehicleSalesmanHandler handler)
    {
        var listings = handler.Salesman.Listings;
        for (var index = 0; index < listings.Count; index++)
        {
            if (string.Equals(
                    listings[index]?.vehicleCode,
                    Constants.VehicleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        listings.Add(new S1DealershipListing
        {
            vehicleCode = Constants.VehicleCode,
        });
    }

    private static bool ContainsChoice(
        S1DialogueChoiceList choices,
        string choiceLabel) =>
        FindChoice(choices, choiceLabel) != null;

    private static void InsertBeforeNavigationChoice(
        S1DialogueChoiceList choices,
        S1DialogueChoiceData choice)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            if (string.IsNullOrEmpty(choices[index]?.ChoiceLabel))
            {
                choices.Insert(index, choice);
                return;
            }
        }

        choices.Add(choice);
    }

    private static S1DialogueChoiceData? FindChoice(
        S1DialogueChoiceList choices,
        string choiceLabel)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            if (choice != null &&
                string.Equals(
                    choice.ChoiceLabel,
                    choiceLabel,
                    StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }

        return null;
    }
}
