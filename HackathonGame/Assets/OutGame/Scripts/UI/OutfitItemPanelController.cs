using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top / Bottom / Weapon / CD の装備選択パネル。
/// Panel・ScrollView・Content はシーン上の GameObject を Inspector で指定する。
/// </summary>
public class OutfitItemPanelController : MonoBehaviour
{
    [SerializeField] private ItemType filterType;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button closeButton;
    [SerializeField] private ItemSlotView slotPrefab;

    private readonly List<ItemSlotView> slotViews = new List<ItemSlotView>();

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void OnEnable()
    {
        RefreshGrid();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshGrid;
    }

    private void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshGrid;
    }

    public void RefreshGrid()
    {
        if (contentRoot == null || slotPrefab == null)
        {
            Debug.LogWarning($"OutfitItemPanelController ({name}): contentRoot または slotPrefab が未設定です。");
            return;
        }

        ClearSlots();

        var items = InventoryManager.Instance.GetItemsByType(filterType);
        if (items.Count == 0) return;

        string selectedId = OutfitLoadoutManager.Instance.GetSelectedId(filterType);
        items.Sort((a, b) => CompareForDisplay(a, b, selectedId));

        foreach (var item in items)
        {
            bool isSelected = OutfitLoadoutManager.GetStableItemId(item) == selectedId;
            var slot = Instantiate(slotPrefab, contentRoot);
            slot.Bind(item, isSelected, OnSlotClicked);
            slotViews.Add(slot);
        }

        if (contentRoot is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private static int CompareForDisplay(ItemData a, ItemData b, string selectedId)
    {
        int Score(ItemData item)
        {
            if (OutfitLoadoutManager.GetStableItemId(item) == selectedId) return 0;
            return 1;
        }

        return Score(a).CompareTo(Score(b));
    }

    private void OnSlotClicked(ItemData item)
    {
        OutfitLoadoutManager.Instance.SetSelected(filterType, item);
        RefreshGrid();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearSlots()
    {
        foreach (var slot in slotViews)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        slotViews.Clear();
    }
}
