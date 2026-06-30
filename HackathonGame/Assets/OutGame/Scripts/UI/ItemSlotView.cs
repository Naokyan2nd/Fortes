using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装備選択グリッドの1マス（Prefab 用。背景は常にグレー、選択は Outline のみ）。
/// </summary>
[RequireComponent(typeof(Button))]
public class ItemSlotView : MonoBehaviour
{
    private static readonly Color GrayBackground = new Color(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Outline selectionOutline;
    [SerializeField] private Button button;

    private ItemData boundItem;
    private Action<ItemData> onClick;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (selectionOutline == null) selectionOutline = GetComponent<Outline>();

        button.onClick.AddListener(HandleClick);
    }

    public void Bind(ItemData item, bool isSelected, Action<ItemData> clickHandler)
    {
        boundItem = item;
        onClick = clickHandler;

        if (backgroundImage != null)
            backgroundImage.color = GrayBackground;

        if (item.itemIcon != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = item.itemIcon;
                iconImage.color = Color.white;
                iconImage.enabled = true;
            }
            if (labelText != null)
                labelText.enabled = false;
        }
        else
        {
            if (iconImage != null)
                iconImage.enabled = false;
            if (labelText != null)
            {
                labelText.text = string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
                labelText.enabled = true;
            }
        }

        if (selectionOutline != null)
            selectionOutline.enabled = isSelected;
    }

    private void HandleClick()
    {
        if (boundItem != null)
            onClick?.Invoke(boundItem);
    }
}
