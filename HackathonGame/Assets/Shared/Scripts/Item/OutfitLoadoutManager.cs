using UnityEngine;

/// <summary>
/// 装備スロット（Top / Bottom / Weapon / CD）の選択状態を保持・永続化する。
/// </summary>
public class OutfitLoadoutManager : MonoBehaviour
{
    public static OutfitLoadoutManager Instance { get; private set; }

    private const string PrefKeyPrefix = "outfit_selected_";
    private const string PrefKeyFirstLaunchApplied = "OutfitLoadout_FirstLaunchApplied_v1";

    public System.Action<ItemType> OnLoadoutChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("Runtime_OutfitLoadoutManager");
        Instance = go.AddComponent<OutfitLoadoutManager>();
        DontDestroyOnLoad(go);
    }

    /// <summary>InventoryManager が初期所持品を付与した後に呼ぶ（順序保証）。</summary>
    public static void EnsureReadyAfterInventoryGranted()
    {
        if (Instance == null)
        {
            var go = new GameObject("Runtime_OutfitLoadoutManager");
            Instance = go.AddComponent<OutfitLoadoutManager>();
            DontDestroyOnLoad(go);
        }

        Instance.ApplyLoadoutAfterInventoryReady();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void ApplyLoadoutAfterInventoryReady()
    {
        if (InventoryManager.Instance == null)
        {
            return;
        }

        if (!HasAppliedFirstLaunchStarterLoadout())
        {
            ApplyStarterLoadout(markFirstLaunchApplied: true);
            return;
        }

        EnsureDefaultsForAllTypes();
    }

    public ItemData GetSelected(ItemType type)
    {
        EnsureDefaultForType(type);

        string savedId = PlayerPrefs.GetString(GetPrefKey(type), string.Empty);
        if (!string.IsNullOrEmpty(savedId))
        {
            var saved = InventoryManager.Instance.FindById(savedId);
            if (saved != null && saved.itemType == type && InventoryManager.Instance.Owns(saved))
            {
                return saved;
            }
        }

        return GetStarterForType(type) ?? GetFirstOwnedOfType(type);
    }

    public void SetSelected(ItemType type, ItemData item)
    {
        if (item == null || item.itemType != type)
        {
            return;
        }

        if (!InventoryManager.Instance.Owns(item))
        {
            return;
        }

        string id = GetStableItemId(item);
        PlayerPrefs.SetString(GetPrefKey(type), id);
        PlayerPrefs.Save();

        OnLoadoutChanged?.Invoke(type);
    }

    public string GetSelectedId(ItemType type)
    {
        var selected = GetSelected(type);
        return selected != null ? GetStableItemId(selected) : string.Empty;
    }

    public static void ResetFirstLaunchForDebug()
    {
        PlayerPrefs.DeleteKey(PrefKeyFirstLaunchApplied);
        ClearSavedSelections();
        PlayerPrefs.Save();
    }

    static bool HasAppliedFirstLaunchStarterLoadout()
    {
        return PlayerPrefs.GetInt(PrefKeyFirstLaunchApplied, 0) != 0;
    }

    void ApplyStarterLoadout(bool markFirstLaunchApplied)
    {
        ClearSavedSelections();

        TryEquipStarter(ItemType.Top);
        TryEquipStarter(ItemType.Bottom);
        TryEquipStarter(ItemType.Weapon);
        TryEquipStarter(ItemType.CD);

        if (markFirstLaunchApplied)
        {
            PlayerPrefs.SetInt(PrefKeyFirstLaunchApplied, 1);
            PlayerPrefs.Save();
        }
    }

    void TryEquipStarter(ItemType type)
    {
        ItemData starter = GetStarterForType(type);
        if (starter != null && InventoryManager.Instance.Owns(starter))
        {
            SetSelected(type, starter);
        }
    }

    static void ClearSavedSelections()
    {
        PlayerPrefs.DeleteKey(GetPrefKey(ItemType.Top));
        PlayerPrefs.DeleteKey(GetPrefKey(ItemType.Bottom));
        PlayerPrefs.DeleteKey(GetPrefKey(ItemType.Weapon));
        PlayerPrefs.DeleteKey(GetPrefKey(ItemType.CD));
    }

    private void EnsureDefaultsForAllTypes()
    {
        EnsureDefaultForType(ItemType.Top);
        EnsureDefaultForType(ItemType.Bottom);
        EnsureDefaultForType(ItemType.Weapon);
        EnsureDefaultForType(ItemType.CD);
    }

    private void EnsureDefaultForType(ItemType type)
    {
        if (InventoryManager.Instance == null)
        {
            return;
        }

        string key = GetPrefKey(type);
        if (PlayerPrefs.HasKey(key))
        {
            string savedId = PlayerPrefs.GetString(key, string.Empty);
            var saved = InventoryManager.Instance.FindById(savedId);
            if (saved != null && saved.itemType == type && InventoryManager.Instance.Owns(saved))
            {
                return;
            }
        }

        var fallback = GetStarterForType(type) ?? GetFirstOwnedOfType(type);
        if (fallback != null)
        {
            SetSelected(type, fallback);
        }
    }

    private static ItemData GetStarterForType(ItemType type)
    {
        var config = Resources.Load<StarterInventoryConfig>("StarterInventoryConfig");
        if (config == null)
        {
            return null;
        }

        return type switch
        {
            ItemType.Top => config.starterTop,
            ItemType.Bottom => config.starterBottom,
            ItemType.Weapon => config.starterWeapon,
            ItemType.CD => config.starterCD,
            _ => null
        };
    }

    private static ItemData GetFirstOwnedOfType(ItemType type)
    {
        var items = InventoryManager.Instance.GetItemsByType(type);
        return items.Count > 0 ? items[0] : null;
    }

    private static string GetPrefKey(ItemType type) => PrefKeyPrefix + type;

    public static string GetStableItemId(ItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(item.itemId))
        {
            return item.itemId;
        }

        return item.name;
    }
}
