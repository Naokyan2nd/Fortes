using System;
using UnityEngine;

/// <summary>
/// Starter / Better 判定（Home キャラ・Stage Equipment などで共通利用）。
/// </summary>
public static class OutfitItemVisualHelper
{
    public static bool IsBetterVariant(ItemData item)
    {
        return ItemIdOrNameContains(item, "better");
    }

    public static string GetStageEquipmentChildName(ItemType type, ItemData item)
    {
        string prefix = type switch
        {
            ItemType.Top => "Top",
            ItemType.Bottom => "Bottom",
            ItemType.CD => "CD",
            _ => null
        };

        if (prefix == null)
        {
            return null;
        }

        return prefix + (IsBetterVariant(item) ? "Better" : "Starter");
    }

    public static string BuildHomeCharacterVariantName(bool topWhite, bool bottomWhite)
    {
        return $"Top{(topWhite ? "White" : "Black")}Bottom{(bottomWhite ? "White" : "Black")}";
    }

    public static string GetHomeCharacterVariantNameFromLoadout()
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return BuildHomeCharacterVariantName(topWhite: false, bottomWhite: false);
        }

        bool topWhite = IsEquippedPieceWhite(ItemType.Top);
        bool bottomWhite = IsEquippedPieceWhite(ItemType.Bottom);
        return BuildHomeCharacterVariantName(topWhite, bottomWhite);
    }

    /// <summary>
    /// MainScene 用（Assets/InGame/Sprites/Top_*_Bottom_* プレハブ名）。
    /// </summary>
    public static string BuildInGameCharacterVariantName(bool topBetter, bool bottomBetter)
    {
        string topTier = topBetter ? "Better" : "Starter";
        string bottomTier = bottomBetter ? "Better" : "Starter";
        return $"Top_{topTier}_Bottom_{bottomTier}";
    }

    public static string GetInGameCharacterVariantNameFromLoadout()
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return BuildInGameCharacterVariantName(topBetter: false, bottomBetter: false);
        }

        bool topBetter = IsBetterVariant(OutfitLoadoutManager.Instance.GetSelected(ItemType.Top));
        bool bottomBetter = IsBetterVariant(OutfitLoadoutManager.Instance.GetSelected(ItemType.Bottom));
        return BuildInGameCharacterVariantName(topBetter, bottomBetter);
    }

    public static void ApplyInGameCharacterVariant(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return;
        }

        string activeVariantName = GetInGameCharacterVariantNameFromLoadout();

        for (int i = 0; i < characterRoot.childCount; i++)
        {
            Transform child = characterRoot.GetChild(i);
            if (child.name == "InGame_Bone_compressed")
            {
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }

                continue;
            }

            if (!TryParseInGameCharacterVariantKey(child.name, out _))
            {
                continue;
            }

            child.gameObject.SetActive(string.Equals(child.name, activeVariantName, StringComparison.Ordinal));
        }
    }

    public static Transform FindInGameCharacterVariant(Transform characterRoot, bool includeInactive = true)
    {
        if (characterRoot == null)
        {
            return null;
        }

        string activeVariantName = GetInGameCharacterVariantNameFromLoadout();
        return FindInGameCharacterVariantByName(characterRoot, activeVariantName, includeInactive);
    }

    public static Transform FindInGameCharacterVariantByName(
        Transform characterRoot,
        string variantName,
        bool includeInactive = true)
    {
        if (characterRoot == null || string.IsNullOrEmpty(variantName))
        {
            return null;
        }

        for (int i = 0; i < characterRoot.childCount; i++)
        {
            Transform child = characterRoot.GetChild(i);
            if (!string.Equals(child.name, variantName, StringComparison.Ordinal))
            {
                continue;
            }

            if (includeInactive || child.gameObject.activeInHierarchy)
            {
                return child;
            }
        }

        return null;
    }

    public static bool TryParseInGameCharacterVariantKey(string objectName, out string variantKey)
    {
        variantKey = objectName;
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        return objectName.StartsWith("Top_", StringComparison.Ordinal)
            && objectName.Contains("_Bottom_", StringComparison.Ordinal);
    }

    public static bool IsEquippedPieceWhite(ItemType type)
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return false;
        }

        return IsBetterVariant(OutfitLoadoutManager.Instance.GetSelected(type));
    }

    public static void ApplyHomeCharacterVariant(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return;
        }

        string activeVariantName = GetHomeCharacterVariantNameFromLoadout();

        for (int i = 0; i < characterRoot.childCount; i++)
        {
            Transform child = characterRoot.GetChild(i);
            child.gameObject.SetActive(string.Equals(child.name, activeVariantName, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Equipment / Album など、TopStarter・CDBetter 形式の子オブジェクトを装備に合わせて切り替える。
    /// </summary>
    public static void ApplyEquipmentSlotVariant(Transform equipmentRoot, ItemType type)
    {
        if (equipmentRoot == null || OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ItemData selected = OutfitLoadoutManager.Instance.GetSelected(type);
        string activeChildName = GetStageEquipmentChildName(type, selected);
        if (string.IsNullOrEmpty(activeChildName))
        {
            return;
        }

        string prefix = type switch
        {
            ItemType.Top => "Top",
            ItemType.Bottom => "Bottom",
            ItemType.CD => "CD",
            _ => null
        };

        if (prefix == null)
        {
            return;
        }

        for (int i = 0; i < equipmentRoot.childCount; i++)
        {
            Transform child = equipmentRoot.GetChild(i);
            string childName = child.name;
            if (!childName.StartsWith(prefix, StringComparison.Ordinal)
                || (!childName.EndsWith("Starter", StringComparison.Ordinal)
                    && !childName.EndsWith("Better", StringComparison.Ordinal)))
            {
                continue;
            }

            child.gameObject.SetActive(string.Equals(childName, activeChildName, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// OutfitScene &gt; CurrentEquipment（StarterTop / BetterCD 形式）を装備に合わせて切り替える。
    /// </summary>
    public static void ApplyOutfitCurrentEquipmentVariant(Transform equipmentRoot)
    {
        if (equipmentRoot == null || OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ApplyOutfitCurrentEquipmentSlot(equipmentRoot, ItemType.Top);
        ApplyOutfitCurrentEquipmentSlot(equipmentRoot, ItemType.Bottom);
        ApplyOutfitCurrentEquipmentSlot(equipmentRoot, ItemType.CD);
    }

    /// <summary>
    /// OutfitScene &gt; ClotheStatus（StarterTopStatus / BetterCDStatus 形式）を装備に合わせて切り替える。
    /// 各ステータスは同一位置に重なるため、同時に1種類のみ表示する。
    /// </summary>
    public static void ApplyClotheStatusForItemType(Transform clotheStatusRoot, ItemType type)
    {
        if (clotheStatusRoot == null || OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ApplyClotheStatusForItemType(
            clotheStatusRoot,
            type,
            OutfitLoadoutManager.Instance.GetSelected(type));
    }

    public static void ApplyClotheStatusForItemType(
        Transform clotheStatusRoot,
        ItemType type,
        ItemData item)
    {
        if (clotheStatusRoot == null)
        {
            return;
        }

        HideAllClotheStatusVariants(clotheStatusRoot);

        string activeStatusName = GetClotheStatusChildName(type, item);
        if (string.IsNullOrEmpty(activeStatusName))
        {
            return;
        }

        SetClotheStatusChildActive(clotheStatusRoot, activeStatusName, true);
    }

    public static void HideAllClotheStatusVariants(Transform clotheStatusRoot)
    {
        if (clotheStatusRoot == null)
        {
            return;
        }

        for (int i = 0; i < clotheStatusRoot.childCount; i++)
        {
            Transform child = clotheStatusRoot.GetChild(i);
            if (IsClotheStatusVariantName(child.name))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    public static string GetOutfitCurrentEquipmentChildName(ItemType type, ItemData item)
    {
        string suffix = type switch
        {
            ItemType.Top => "Top",
            ItemType.Bottom => "Bottom",
            ItemType.CD => "CD",
            _ => null,
        };

        if (suffix == null)
        {
            return null;
        }

        return (IsBetterVariant(item) ? "Better" : "Starter") + suffix;
    }

    static void ApplyOutfitCurrentEquipmentSlot(Transform equipmentRoot, ItemType type)
    {
        string activeChildName = GetOutfitCurrentEquipmentChildName(
            type,
            OutfitLoadoutManager.Instance.GetSelected(type));

        if (string.IsNullOrEmpty(activeChildName))
        {
            return;
        }

        for (int i = 0; i < equipmentRoot.childCount; i++)
        {
            Transform child = equipmentRoot.GetChild(i);
            if (!IsOutfitCurrentEquipmentVariantName(child.name))
            {
                continue;
            }

            bool isActiveSlot = child.name.EndsWith(
                type switch
                {
                    ItemType.Top => "Top",
                    ItemType.Bottom => "Bottom",
                    ItemType.CD => "CD",
                    _ => string.Empty,
                },
                StringComparison.Ordinal);

            if (!isActiveSlot)
            {
                continue;
            }

            child.gameObject.SetActive(string.Equals(child.name, activeChildName, StringComparison.Ordinal));
        }
    }

    public static string GetClotheStatusChildName(ItemType type, ItemData item)
    {
        string variantName = GetOutfitCurrentEquipmentChildName(type, item);
        return string.IsNullOrEmpty(variantName) ? null : variantName + "Status";
    }

    static string GetClotheSlotSuffix(ItemType type)
    {
        return type switch
        {
            ItemType.Top => "Top",
            ItemType.Bottom => "Bottom",
            ItemType.CD => "CD",
            _ => null,
        };
    }

    static bool IsClotheStatusVariantName(string childName)
    {
        if (string.IsNullOrEmpty(childName))
        {
            return false;
        }

        if (!childName.StartsWith("Starter", StringComparison.Ordinal)
            && !childName.StartsWith("Better", StringComparison.Ordinal))
        {
            return false;
        }

        return childName.EndsWith("TopStatus", StringComparison.Ordinal)
            || childName.EndsWith("BottomStatus", StringComparison.Ordinal)
            || childName.EndsWith("CDStatus", StringComparison.Ordinal);
    }

    static void SetClotheStatusChildActive(Transform clotheStatusRoot, string statusChildName, bool active)
    {
        for (int i = 0; i < clotheStatusRoot.childCount; i++)
        {
            Transform child = clotheStatusRoot.GetChild(i);
            if (string.Equals(child.name, statusChildName, StringComparison.Ordinal))
            {
                child.gameObject.SetActive(active);
                return;
            }
        }
    }

    static bool IsOutfitCurrentEquipmentVariantName(string childName)
    {
        return childName is "StarterTop" or "BetterTop"
            or "StarterBottom" or "BetterBottom"
            or "StarterCD" or "BetterCD";
    }

    /// <summary>
    /// OutfitScene &gt; Album の StarterAlbum / BetterAlbum を装備 CD に合わせて切り替える。
    /// </summary>
    public static void ApplyOutfitAlbumVariant(Transform albumRoot)
    {
        if (albumRoot == null)
        {
            return;
        }

        ItemData cdItem = OutfitLoadoutManager.Instance != null
            ? OutfitLoadoutManager.Instance.GetSelected(ItemType.CD)
            : null;
        ApplyOutfitAlbumVariant(albumRoot, cdItem);
    }

    public static void ApplyOutfitAlbumVariant(Transform albumRoot, ItemData cdItem)
    {
        if (albumRoot == null)
        {
            return;
        }

        string activeChildName = GetOutfitAlbumChildName(cdItem);
        bool foundVariant = false;

        Transform[] descendants = albumRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == null || child == albumRoot)
            {
                continue;
            }

            string childName = child.name;
            if (childName != "StarterAlbum" && childName != "BetterAlbum")
            {
                continue;
            }

            foundVariant = true;
            child.gameObject.SetActive(string.Equals(childName, activeChildName, StringComparison.Ordinal));
        }

        if (!foundVariant)
        {
            for (int i = 0; i < albumRoot.childCount; i++)
            {
                Transform child = albumRoot.GetChild(i);
                child.gameObject.SetActive(string.Equals(child.name, activeChildName, StringComparison.Ordinal));
            }
        }
    }

    public static string GetOutfitAlbumChildName(ItemData cdItem)
    {
        if (cdItem == null)
        {
            return "StarterAlbum";
        }

        StarterInventoryConfig config = Resources.Load<StarterInventoryConfig>("StarterInventoryConfig");
        if (config != null)
        {
            if (IsSameInventoryItem(cdItem, config.betterCD))
            {
                return "BetterAlbum";
            }

            if (IsSameInventoryItem(cdItem, config.starterCD))
            {
                return "StarterAlbum";
            }
        }

        return IsBetterVariant(cdItem) ? "BetterAlbum" : "StarterAlbum";
    }

    public static bool IsSameInventoryItem(ItemData a, ItemData b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (ReferenceEquals(a, b))
        {
            return true;
        }

        string idA = OutfitLoadoutManager.GetStableItemId(a);
        string idB = OutfitLoadoutManager.GetStableItemId(b);
        return !string.IsNullOrEmpty(idA)
            && string.Equals(idA, idB, StringComparison.Ordinal);
    }

    /// <summary>
    /// Home &gt; Album の StarterCD / BetterCD を装備 CD に合わせて切り替える。
    /// </summary>
    public static void ApplyHomeAlbumVariant(Transform albumRoot)
    {
        if (albumRoot == null)
        {
            return;
        }

        bool useBetter = OutfitLoadoutManager.Instance != null
            && IsBetterVariant(OutfitLoadoutManager.Instance.GetSelected(ItemType.CD));

        string activeChildName = useBetter ? "BetterCD" : "StarterCD";

        for (int i = 0; i < albumRoot.childCount; i++)
        {
            Transform child = albumRoot.GetChild(i);
            string childName = child.name;
            if (childName != "StarterCD" && childName != "BetterCD")
            {
                continue;
            }

            child.gameObject.SetActive(string.Equals(childName, activeChildName, StringComparison.Ordinal));
        }
    }

    static bool ItemIdOrNameContains(ItemData item, string token)
    {
        if (item == null)
        {
            return false;
        }

        string id = OutfitLoadoutManager.GetStableItemId(item);
        if (ContainsIgnoreCase(id, token))
        {
            return true;
        }

        if (ContainsIgnoreCase(item.name, token))
        {
            return true;
        }

        return !string.IsNullOrEmpty(item.itemName) && ContainsIgnoreCase(item.itemName, token);
    }

    static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text)
            && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
