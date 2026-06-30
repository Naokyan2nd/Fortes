using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QTE 中のタップ導線 UI（見た目のみ。判定・入力には関与しない）。
/// アニメーションは Animator で制御する。
/// </summary>
public sealed class QteTapButtonGuideView : MonoBehaviour
{
    private void Awake()
    {
        DisableRaycasts();
    }

    /// <summary>QTE プレイ中に表示する。</summary>
    public void Show()
    {
        DisableRaycasts();
        gameObject.SetActive(true);
    }

    /// <summary>QTE 終了時に非表示へ戻す。</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void DisableRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(includeInactive: true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }
}
