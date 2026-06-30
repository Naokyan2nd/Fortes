using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Result 画面のアイテム報酬スロット1件。
/// </summary>
public sealed class ResultItemSlotView : MonoBehaviour
{
    [SerializeField]
    private Image _iconImage;

    [SerializeField]
    private TMP_Text _labelText;

    /// <summary>
    /// 表示内容を設定する。
    /// </summary>
    public void Setup(Sprite icon, string displayName, int count)
    {
        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = icon != null;
        }

        if (_labelText != null)
        {
            string name = string.IsNullOrEmpty(displayName) ? "Item" : displayName;
            _labelText.text = count > 1 ? $"{name} x{count}" : name;
        }
    }
}
