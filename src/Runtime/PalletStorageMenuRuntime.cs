using System;
using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1ItemSlotUi = Il2CppScheduleOne.UI.ItemSlotUI;
using S1PlaceableStorageEntity = Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageEntity = Il2CppScheduleOne.Storage.StorageEntity;
using S1StorageMenu = Il2CppScheduleOne.UI.StorageMenu;
#else
using S1ItemSlotUi = ScheduleOne.UI.ItemSlotUI;
using S1PlaceableStorageEntity = ScheduleOne.ObjectScripts.PlaceableStorageEntity;
using S1StorageEntity = ScheduleOne.Storage.StorageEntity;
using S1StorageMenu = ScheduleOne.UI.StorageMenu;
#endif

namespace S1Forklift.Runtime;

internal static class PalletStorageMenuRuntime
{
    private static bool _layoutCaptured;
    private static Vector2 _nativeCellSize;
    private static Vector2 _nativeSpacing;
    private static Vector2 _nativeTitlePosition;
    private static Vector2 _nativeSubtitlePosition;
    private static Vector2 _nativeSlotContainerPosition;
    private static Vector2 _nativeCloseButtonPosition;

    internal static void Prepare(S1StorageMenu menu, S1StorageEntity storage)
    {
        CaptureNativeLayout(menu);
        RestoreNativeLayout(menu);

        if (!IsPalletStorage(storage))
            return;

        storage.DisplayRowCount = Constants.PalletDisplayRowCount;
        EnsureSlotUiCapacity(menu, Constants.PalletSlotCount);
        menu.SlotGridLayout.cellSize = _nativeCellSize * Constants.PalletStorageUiScale;
        menu.SlotGridLayout.spacing = _nativeSpacing * Constants.PalletStorageUiScale;
        ApplyVerticalOffset(menu, Constants.PalletStorageUiVerticalOffset);
    }

    internal static bool TryVerify(
        S1StorageMenu menu,
        S1StorageEntity storage,
        out string detail)
    {
        Prepare(menu, storage);
        var uiCount = menu.SlotsUIs?.Length ?? 0;
        detail =
            $"StorageSlots={storage.ItemSlots.Count}|" +
            $"SlotUIs={uiCount}|" +
            $"DisplayRows={storage.DisplayRowCount}|" +
            $"CellSize={menu.SlotGridLayout.cellSize.x:F1}x" +
            $"{menu.SlotGridLayout.cellSize.y:F1}|" +
            $"LayoutScale={GetCurrentLayoutScale(menu):F2}|" +
            $"VerticalOffset={GetCurrentVerticalOffset(menu):F1}";
        return storage.ItemSlots.Count == Constants.PalletSlotCount &&
               uiCount >= Constants.PalletSlotCount &&
               storage.DisplayRowCount == Constants.PalletDisplayRowCount &&
               Math.Abs(
                   GetCurrentLayoutScale(menu) -
                   Constants.PalletStorageUiScale) < 0.01f &&
               Math.Abs(
                   GetCurrentVerticalOffset(menu) -
                   Constants.PalletStorageUiVerticalOffset) < 0.01f;
    }

    private static void CaptureNativeLayout(S1StorageMenu menu)
    {
        if (_layoutCaptured)
            return;
        if (menu.SlotGridLayout == null)
            throw new InvalidOperationException(
                "The native storage menu has no slot grid layout.");

        _nativeCellSize = menu.SlotGridLayout.cellSize;
        _nativeSpacing = menu.SlotGridLayout.spacing;
        _nativeTitlePosition = menu.TitleLabel.rectTransform.anchoredPosition;
        _nativeSubtitlePosition = menu.SubtitleLabel.rectTransform.anchoredPosition;
        _nativeSlotContainerPosition = menu.SlotContainer.anchoredPosition;
        _nativeCloseButtonPosition = menu.CloseButtonContainer.anchoredPosition;
        _layoutCaptured = true;
    }

    private static void RestoreNativeLayout(S1StorageMenu menu)
    {
        menu.SlotGridLayout.cellSize = _nativeCellSize;
        menu.SlotGridLayout.spacing = _nativeSpacing;
        menu.TitleLabel.rectTransform.anchoredPosition = _nativeTitlePosition;
        menu.SubtitleLabel.rectTransform.anchoredPosition = _nativeSubtitlePosition;
        menu.SlotContainer.anchoredPosition = _nativeSlotContainerPosition;
        menu.CloseButtonContainer.anchoredPosition = _nativeCloseButtonPosition;
    }

    private static void ApplyVerticalOffset(S1StorageMenu menu, float offset)
    {
        var translation = Vector2.up * offset;
        menu.TitleLabel.rectTransform.anchoredPosition = _nativeTitlePosition + translation;
        menu.SubtitleLabel.rectTransform.anchoredPosition = _nativeSubtitlePosition + translation;
        menu.SlotContainer.anchoredPosition = _nativeSlotContainerPosition + translation;
        menu.CloseButtonContainer.anchoredPosition = _nativeCloseButtonPosition + translation;
    }

    private static float GetCurrentLayoutScale(S1StorageMenu menu) =>
        _nativeCellSize.y <= 0f
            ? 0f
            : menu.SlotGridLayout.cellSize.y / _nativeCellSize.y;

    private static float GetCurrentVerticalOffset(S1StorageMenu menu) =>
        menu.SlotContainer.anchoredPosition.y - _nativeSlotContainerPosition.y;

    private static bool IsPalletStorage(S1StorageEntity? storage)
    {
        if (storage == null)
            return false;

        var placeable = storage.GetComponentInParent<S1PlaceableStorageEntity>();
        return placeable != null &&
               ((placeable.ItemInstance != null &&
                 string.Equals(
                     placeable.ItemInstance.ID,
                     Constants.PalletItemId,
                     StringComparison.OrdinalIgnoreCase)) ||
                placeable.name.StartsWith(
                    Constants.PalletPrefabName,
                    StringComparison.Ordinal));
    }

    private static void EnsureSlotUiCapacity(S1StorageMenu menu, int targetCount)
    {
        var current = menu.SlotsUIs;
        var currentCount = current?.Length ?? 0;
        if (currentCount >= targetCount)
            return;
        if (current == null || current.Length == 0 || menu.SlotContainer == null)
        {
            throw new InvalidOperationException(
                "The native storage menu has no slot UI template.");
        }
        currentCount = current.Length;

#if IL2CPP
        var expanded = new Il2CppReferenceArray<S1ItemSlotUi>(targetCount);
#else
        var expanded = new S1ItemSlotUi[targetCount];
#endif
        for (var index = 0; index < currentCount; index++)
            expanded[index] = current[index];

        var template = current[currentCount - 1]
                       ?? throw new InvalidOperationException(
                           "The native storage menu slot UI template is null.");
        for (var index = currentCount; index < targetCount; index++)
        {
            var slotObject = UnityEngine.Object.Instantiate(
                template.gameObject,
                menu.SlotContainer,
                false);
            slotObject.name = $"PalletStorageSlot_{index + 1}";
            var slotUi = slotObject.GetComponent<S1ItemSlotUi>()
                         ?? throw new InvalidOperationException(
                             $"Cloned storage slot {index + 1} has no ItemSlotUI.");
            slotUi.ClearSlot();
            slotObject.SetActive(false);
            expanded[index] = slotUi;
        }

        menu.SlotsUIs = expanded;
    }
}
