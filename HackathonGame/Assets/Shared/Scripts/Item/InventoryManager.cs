using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // シングルトンインスタンスの所有
    public static InventoryManager Instance { get; private set; }

    // プレイヤーの所持アイテムリスト
    private List<ItemData> playerItems = new List<ItemData>();

    // データ変更時にUIへ通知するイベント
    public System.Action OnInventoryChanged;

    /// <summary>
    /// ゲーム起動時にUnityが自動的にこのメソッドを実行します。
    /// ヒエラルキー上にオブジェクトを配置する必要はありません。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // バックグラウンドで動作するゲームオブジェクトを自動生成
        GameObject managerObject = new GameObject("Runtime_InventoryManager");
        
        // 生成したオブジェクトにこのスクリプトをアタッチ
        Instance = managerObject.AddComponent<InventoryManager>();
        
        // シーン遷移（画面切り替え）が発生してもオブジェクトが削除されないように設定
        DontDestroyOnLoad(managerObject);

        Instance.GrantStarterItems();
        OutfitLoadoutManager.EnsureReadyAfterInventoryGranted();
    }

    private void GrantStarterItems()
    {
        var config = Resources.Load<StarterInventoryConfig>("StarterInventoryConfig");
        if (config == null)
        {
            Debug.LogWarning("StarterInventoryConfig not found in Resources. Skipping starter items.");
            return;
        }

        AddItem(config.starterTop);
        AddItem(config.starterBottom);
        AddItem(config.starterWeapon);
        AddItem(config.starterCD);
        AddItem(config.betterTop);
        // BetterBottom / BetterCD are obtained via Craft, not granted at game start.
    }

    // アイテム獲得時の処理
    public void AddItem(ItemData item)
    {
        if (item != null)
        {
            playerItems.Add(item);
            
            // UI側に更新を通知
            OnInventoryChanged?.Invoke();
        }
    }

    // 現在の所持アイテムリストを返す
    public List<ItemData> GetPlayerItems()
    {
        return playerItems;
    }

    public bool Owns(ItemData item)
    {
        return item != null && playerItems.Contains(item);
    }

    public List<ItemData> GetItemsByType(ItemType type)
    {
        var result = new List<ItemData>();
        foreach (var item in playerItems)
        {
            if (item != null && item.itemType == type)
                result.Add(item);
        }
        return result;
    }

    public ItemData FindById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        foreach (var item in playerItems)
        {
            if (item == null) continue;
            if (item.itemId == itemId || item.name == itemId)
                return item;
        }
        return null;
    }
}